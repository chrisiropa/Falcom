using System.Text;

namespace Falcom;

public sealed class FalcomFileSink
{
   private const long MaxLogfileEntries = 200000;
   private readonly object _sync = new();
   private readonly string _logfilePath;
   private long _logfileEntryCounter;
   private bool _firstError = true;

   public FalcomFileSink(string logfilePath)
   {
      _logfilePath = logfilePath;
      EnsureDirectoryAndFile();
      InitEntryCounter();
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
               RotateIfNeeded();

               using var streamWriter = new StreamWriter(_logfilePath, true, Encoding.UTF8);
               streamWriter.WriteLine(line);

               written = true;
               _logfileEntryCounter++;

               if ((_logfileEntryCounter % 100) == 0)
               {
                  DeleteOldLogFiles();
               }
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

   private void InitEntryCounter()
   {
      if (!File.Exists(_logfilePath))
      {
         _logfileEntryCounter = 0;
         return;
      }

      using var reader = new StreamReader(_logfilePath);
      _logfileEntryCounter = 0;

      while (!reader.EndOfStream)
      {
         reader.ReadLine();
         _logfileEntryCounter++;
      }
   }

   private void RotateIfNeeded()
   {
      if (_logfileEntryCounter <= MaxLogfileEntries)
      {
         return;
      }

      try
      {
         if (!File.Exists(_logfilePath))
         {
            _logfileEntryCounter = 0;
            return;
         }

         var extension = Path.GetExtension(_logfilePath);
         var filenameWithoutExtension = Path.GetFileNameWithoutExtension(_logfilePath);
         var directory = Path.GetDirectoryName(_logfilePath) ?? string.Empty;
         var historyFile = Path.Combine(directory, $"{filenameWithoutExtension} {DateTime.Now:ddMMyyyyHHmmssfff}{extension}");

         File.Move(_logfilePath, historyFile, true);
         _logfileEntryCounter = 0;
      }
      catch (Exception exception)
      {
         Console.Error.WriteLine($"FalcomFileSink rotation failed: {exception.Message}");
         _logfileEntryCounter = 0;
      }
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
