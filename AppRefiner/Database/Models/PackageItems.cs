using System.Collections.Generic;

namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents a collection of items (subpackages and classes) within a package
    /// </summary>
    public class PackageItems
    {
        /// <summary>
        /// Gets the root package name
        /// </summary>
        public string RootPackage { get; }

        /// <summary>
        /// Gets the full package path
        /// </summary>
        public string PackagePath { get; }

        /// <summary>
        /// Gets the list of subpackages in this package
        /// </summary>
        public List<string> Subpackages { get; }

        /// <summary>
        /// Gets the list of classes in this package
        /// </summary>
        public List<string> Classes { get; }

        /// <summary>
        /// Gets the current package level (0=root, 1=subpackage, 2=subsubpackage)
        /// </summary>
        public int PackageLevel { get; }

        /// <summary>
        /// Creates a new package items collection
        /// </summary>
        /// <param name="packagePath">The full package path</param>
        /// <param name="subpackages">List of subpackages</param>
        /// <param name="classes">List of classes</param>
        public PackageItems(string packagePath, List<string> subpackages, List<string> classes)
        {
            PackagePath = packagePath ?? string.Empty;
            Subpackages = subpackages ?? new List<string>();
            Classes = classes ?? new List<string>();

            // Extract root package from path
            string[] parts = PackagePath.Split(':');
            RootPackage = parts.Length > 0 ? parts[0] : string.Empty;

            // Determine package level
            PackageLevel = parts.Length - 1;
            if (PackageLevel < 0) PackageLevel = 0;
        }
    }
} 