using System.Text;

namespace Falcom;

public sealed class FalcomFileSink
{
   private const long MaxLogfileBytes = 20L * 1024L * 1024L;
   private readonly object _sync = new();
   private readonly string _logfilePath;
   private bool _firstError = true;

   public FalcomFileSink(string logfilePath)
   {
      _logfilePath = logfilePath;
      EnsureDirectoryAndFile();
      StartNewLogfile();
   }

   public void Write(string line)
   {
      lock (_sync)
      {
         var written = false;
         var tryCounter = 0;

         while (!written && tryCounter < 10)
         {
            try
            {
               RotateIfNeeded(line);

               using var streamWriter = new StreamWriter(_logfilePath, true, Encoding.UTF8);
               streamWriter.WriteLine(line);

               written = true;
               DeleteOldLogFiles();
            }
            catch (Exception exception)
            {
               if (_firstError)
               {
                  _firstError = false;
                  Console.Error.WriteLine($"FalcomFileSink write failed: {exception.Message}");
               }

               tryCounter++;
               Thread.Sleep(0);
            }
         }
      }
   }

   private void EnsureDirectoryAndFile()
   {
      try
      {
         var directory = Path.GetDirectoryName(_logfilePath);
         if (!string.IsNullOrWhiteSpace(directory))
         {
            Directory.CreateDirectory(directory);
         }

         using var fileStream = new FileStream(_logfilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
      }
      catch (Exception exception)
      {
         Console.Error.WriteLine($"FalcomFileSink initialization failed: {exception.Message}");
      }
   }

   private void StartNewLogfile()
   {
      if (!File.Exists(_logfilePath))
      {
         return;
      }

      try
      {
         FileInfo fileInfo = new(_logfilePath);

         if (fileInfo.Length <= 0)
         {
            return;
         }

         RotateCurrentFile();
      }
      catch (Exception exception)
      {
         Console.Error.WriteLine($"FalcomFileSink startup rotation failed: {exception.Message}");
      }
   }

   private void RotateIfNeeded(string nextLine)
   {
      if (!File.Exists(_logfilePath))
      {
         return;
      }

      try
      {
         FileInfo fileInfo = new(_logfilePath);
         long nextWriteBytes =
            Encoding.UTF8.GetByteCount(nextLine)
            + Encoding.UTF8.GetByteCount(Environment.NewLine);

         if (fileInfo.Length + nextWriteBytes <= MaxLogfileBytes)
         {
            return;
         }

         RotateCurrentFile();
      }
      catch (Exception exception)
      {
         Console.Error.WriteLine($"FalcomFileSink rotation failed: {exception.Message}");
      }
   }

   private void RotateCurrentFile()
   {
      if (!File.Exists(_logfilePath))
      {
         return;
      }

      var extension = Path.GetExtension(_logfilePath);
      var filenameWithoutExtension = Path.GetFileNameWithoutExtension(_logfilePath);
      var directory = Path.GetDirectoryName(_logfilePath) ?? string.Empty;
      var historyFile = Path.Combine(
         directory,
         $"{filenameWithoutExtension} {DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}");

      File.Move(_logfilePath, historyFile, true);

      using var fileStream = new FileStream(
         _logfilePath,
         FileMode.Create,
         FileAccess.Write,
         FileShare.ReadWrite);
   }

   private void DeleteOldLogFiles()
   {
      var directoryPath = Path.GetDirectoryName(_logfilePath);
      if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
      {
         return;
      }

      var filePrefix = Path.GetFileNameWithoutExtension(_logfilePath);
      var children = new DirectoryInfo(directoryPath)
         .GetFiles()
         .Where(file => file.Name.Contains(filePrefix, StringComparison.OrdinalIgnoreCase))
         .ToList();

      if (children.Count == 0)
      {
         return;
      }

      var oldest = children.MinBy(file => file.LastWriteTimeUtc);
      if (oldest is null)
      {
         return;
      }

      var timeSpan = DateTime.UtcNow - oldest.LastWriteTimeUtc;
      const int keepDays = 120;

      if (timeSpan.TotalDays <= keepDays)
      {
         return;
      }

      if (string.Equals(oldest.FullName, _logfilePath, StringComparison.OrdinalIgnoreCase))
      {
         return;
      }

      try
      {
         File.Delete(oldest.FullName);
      }
      catch (Exception exception)
      {
         Console.Error.WriteLine($"FalcomFileSink cleanup failed: {exception.Message}");
      }
   }
}
