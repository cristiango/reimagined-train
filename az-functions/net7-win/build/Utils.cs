using System.Xml.Linq;
using System.Xml.XPath;

namespace build
{
    internal static class Utils
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

        /// <summary>
        /// Gets the plugin version related to the references Pulumi provider. This function
        /// crudely just parses the supplied csproj xml and looks for a single package reference
        /// for "Pulumi.{Plugin}". Probably not the most bullet proof way but it works when it
        /// works. Note: will only work with fixed version specification (i.e. "4.2.11") and not
        /// for version ranges or wildcards (i.e. "4.2.*").
        /// </summary>
        /// <param name="csprojPath">Path to the csproj file that has </param>
        /// <param name="plugin">The Pulumi plugin.</param>
        /// <returns></returns>
        public static string GetPulumiPluginVersion(string csprojPath, string plugin) =>
            XDocument
                .Load(csprojPath)
                .XPathSelectElement($"/Project/ItemGroup/PackageReference[@Include=\"Pulumi.{plugin}\"]")?
                .Attribute("Version")?
                .Value!;
    }
}
