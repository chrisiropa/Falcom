using System.Text;

namespace Falcom;

public sealed class FalcomFileSink : IDisposable
{
   private const long MaxLogfileBytes = 20L * 1024L * 1024L;
   private const int KeepDays = 120;
   private readonly object _sync = new();
   private readonly string _logfilePath;
   private StreamWriter? _writer;
   private bool _firstError = true;
   private bool _disposed;

   public FalcomFileSink(string logfilePath)
   {
      _logfilePath = NormalizeLogfilePath(logfilePath);
      EnsureDirectoryAndFile();
      StartNewLogfile();
      OpenWriter();
   }

   public void Write(string line)
   {
      lock (_sync)
      {
         if (_disposed)
         {
            return;
         }

         try
         {
            RotateIfNeeded(line);
            _writer ??= CreateWriter();
            _writer.WriteLine(line);
            DeleteOldLogFiles();
         }
         catch (Exception exception)
         {
            if (_firstError)
            {
               _firstError = false;
               Console.Error.WriteLine($"FalcomFileSink write failed: {exception.Message}");
            }
         }
      }
   }

   public void Dispose()
   {
      lock (_sync)
      {
         if (_disposed)
         {
            return;
         }

         _disposed = true;
         CloseWriter();
      }
   }

   private static string NormalizeLogfilePath(string logfilePath)
   {
      string trimmedPath = string.IsNullOrWhiteSpace(logfilePath)
         ? Path.Combine(AppContext.BaseDirectory, "FALCOM.log")
         : logfilePath.Trim();

      if (Directory.Exists(trimmedPath)
          || string.IsNullOrWhiteSpace(Path.GetExtension(trimmedPath)))
      {
         string directoryName = Path.GetFileName(trimmedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
         if (string.IsNullOrWhiteSpace(directoryName))
         {
            directoryName = "FALCOM";
         }

         return Path.Combine(trimmedPath, $"{directoryName}.log");
      }

      return trimmedPath;
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
      CloseWriter();

      if (!File.Exists(_logfilePath))
      {
         OpenWriter();
         return;
      }

      var extension = Path.GetExtension(_logfilePath);
      var filenameWithoutExtension = Path.GetFileNameWithoutExtension(_logfilePath);
      var directory = Path.GetDirectoryName(_logfilePath) ?? string.Empty;
      var historyFile = Path.Combine(
         directory,
         $"{filenameWithoutExtension} {DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}");

      File.Move(_logfilePath, historyFile, true);
      OpenWriter();
   }

   private void OpenWriter()
   {
      _writer = CreateWriter();
   }

   private StreamWriter CreateWriter()
   {
      var stream = new FileStream(
         _logfilePath,
         FileMode.Append,
         FileAccess.Write,
         FileShare.ReadWrite);

      return new StreamWriter(stream, Encoding.UTF8)
      {
         AutoFlush = true
      };
   }

   private void CloseWriter()
   {
      try
      {
         _writer?.Flush();
         _writer?.Dispose();
      }
      finally
      {
         _writer = null;
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
      var oldest = new DirectoryInfo(directoryPath)
         .GetFiles()
         .Where(file => file.Name.Contains(filePrefix, StringComparison.OrdinalIgnoreCase))
         .MinBy(file => file.LastWriteTimeUtc);

      if (oldest is null)
      {
         return;
      }

      var age = DateTime.UtcNow - oldest.LastWriteTimeUtc;
      if (age.TotalDays <= KeepDays)
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
