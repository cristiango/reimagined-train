public static class Utils
{
    /// <summary>
    /// Deletes all files (except .gitignore) and subdirectories from specified path.
    /// </summary>
    /// <param name="path">The path whose files and subdirectories will be deleted</param>
    public static void CleanDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
            
        var filesToDelete = Directory
            .GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".gitignore"));
        foreach (var file in filesToDelete)
        {
            Console.WriteLine($"Deleting file {file}");
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        var directoriesToDelete = Directory.GetDirectories(path);
        foreach (var directory in directoriesToDelete)
        {
            Console.WriteLine($"Deleting directory {directory}");
            Directory.Delete(directory, true);
        }
    }
}