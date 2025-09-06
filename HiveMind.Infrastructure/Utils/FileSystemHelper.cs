namespace HiveMind.Infrastructure.Utils
{
  /// <summary>
  /// Helper utilities for file system operations
  /// </summary>
  public static class FileSystemHelper
  {
    /// <summary>
    /// Gets the default HiveMind application data directory
    /// </summary>
    public static string GetDefaultDataDirectory()
    {
      string appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
      return Path.Combine(appDataPath, "HiveMind");
    }

    /// <summary>
    /// Gets the default snapshots directory
    /// </summary>
    public static string GetDefaultSnapshotsDirectory() =>
      Path.Combine(GetDefaultDataDirectory(), "Snapshots");

    /// <summary>
    /// Gets the default exports directory
    /// </summary>
    public static string GetDefaultExportsDirectory() =>
      Path.Combine(GetDefaultDataDirectory(), "Exports");

    /// <summary>
    /// Ensures a directory exists, creating it if necessary
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
      if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Gets a safe filename from a given string
    /// </summary>
    public static string GetSafeFileName(string fileName)
    {
      char[] invalidChars = Path.GetInvalidFileNameChars();
      string safeName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

      // Limit length and ensure it's not empty
      if (string.IsNullOrWhiteSpace(safeName)) safeName = "unnamed";
      else if (safeName.Length > 100) safeName = safeName[..100];

      return safeName;
    }

    /// <summary>
    /// Gets available disk space for a given path
    /// </summary>
    public static long GetAvailableDiskSpace(string path)
    {
      try
      {
        DriveInfo drive = new(Path.GetPathRoot(path) ?? "C:");
        return drive.AvailableFreeSpace;
      }
      catch
      {
        return -1;
      }
    }

    /// <summary>
    /// Cleans up old files in a directory, keeping only the newest N files
    /// </summary>
    public static void CleanupOldFiles(string directory, string searchPattern, int keepCount)
    {
      try
      {
        if (!Directory.Exists(directory)) return;

        List<FileInfo> files = [.. Directory.GetFiles(directory, searchPattern)
          .Select(f => new FileInfo(f))
          .OrderByDescending(fi => fi.LastWriteTime)];

        IEnumerable<FileInfo> filesToDelete = files.Skip(keepCount);
        foreach (FileInfo file in filesToDelete)
        {
          try
          {
            file.Delete();
          }
          catch
          {
            // Ignore errors when deleting individual files
          }
        }
      }
      catch
      {
        // Ignore errors in cleanup
      }
    }
  }
}
