using System.Security.AccessControl;

namespace PeopleCodeTypeInfo.Contracts;

/// <summary>
/// [DEPRECATED] File system-based implementation of IProgramSourceProvider that reads PeopleCode
/// source files from a local directory structure exported from PeopleSoft.
/// This class is deprecated along with IProgramSourceProvider.
/// </summary>
/// <remarks>
/// DEPRECATION NOTICE:
/// This class implements the deprecated IProgramSourceProvider interface.
/// For new code, use the ITypeMetadataResolver pattern instead:
/// 1. Use this class to read source files (keep the file reading logic)
/// 2. Parse source with PeopleCodeParser
/// 3. Use TypeMetadataBuilder.ExtractMetadata() to get TypeMetadata
/// 4. Store metadata in an ITypeMetadataResolver implementation
///
/// Supports two types of program resolution:
/// 1. Application Classes/Interfaces: PKG:Class, PKG:SUBPKG:Class, PKG:SUBPKG1:SUBPKG2:Class
///    (Root package + up to 2 subpackages)
///    Located at: {BasePath}\Application Packages\{package}\{subpackage?}\{subpackage2?}\{class}.pcode
/// 2. Record Field PeopleCode: RECORD.FIELD.Event
///    Located at: {BasePath}\Records\{record}\{field}\{event}.pcode
/// </remarks>
public class LocalDirectorySourceProvider
{
    private readonly string _basePath;

    /// <summary>
    /// Creates a new LocalDirectorySourceProvider with the specified base directory.
    /// </summary>
    /// <param name="basePath">
    /// The root directory containing the exported PeopleCode files.
    /// Should contain "Application Packages" and/or "Records" subdirectories.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when basePath is null or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when basePath does not exist.</exception>
    public LocalDirectorySourceProvider(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentNullException(nameof(basePath));

        if (!Directory.Exists(basePath))
            throw new DirectoryNotFoundException($"Base path does not exist: {basePath}");

        _basePath = basePath;
    }

    /// <summary>
    /// Attempts to retrieve the source code for a PeopleCode program from the file system.
    /// </summary>
    /// <param name="qualifiedName">
    /// The qualified name of the program:
    /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
    /// - Record Field: "RECORD.FIELD.Event"
    /// </param>
    /// <returns>
    /// A tuple indicating whether the source was found and the source code if available.
    /// </returns>
    public async Task<(bool found, string? source)> GetSourceAsync(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            return (false, null);

        // Determine if this is an app class/interface or record field peoplecode
        if (qualifiedName.Contains(':'))
        {
            return await GetAppClassSourceAsync(qualifiedName);
        }
        else if (qualifiedName.Contains('.'))
        {
            return await GetRecordFieldSourceAsync(qualifiedName);
        }

        return (false, null);
    }

    /// <summary>
    /// Retrieves source for an Application Class or Interface.
    /// Format: PKG:Class, PKG:SUBPKG:Class, or PKG:SUBPKG1:SUBPKG2:Class
    /// Path: {BasePath}\Application Packages\{package}\{subpackage?}\{subpackage2?}\{class}.pcode
    /// </summary>
    private async Task<(bool found, string? source)> GetAppClassSourceAsync(string qualifiedName)
    {
        // Split on colon to get package path and class name
        var parts = qualifiedName.Split(':');
        if (parts.Length < 2)
            return (false, null);

        // Last part is the class name, everything else is package path
        var className = parts[^1];
        var packageParts = parts[..^1];

        // Build the file path: Application Packages\pkg1\pkg2\...\ClassName.pcode
        var pathParts = new List<string> { _basePath, "Application Packages" };
        pathParts.AddRange(packageParts);
        pathParts.Add($"{className}.pcode");

        var filePath = Path.Combine(pathParts.ToArray());

        return await ReadSourceFileAsync(filePath);
    }

    /// <summary>
    /// Retrieves source for Record Field PeopleCode.
    /// Format: RECORD.FIELD.Event
    /// Path: {BasePath}\Records\{record}\{field}\{event}.pcode
    /// </summary>
    private async Task<(bool found, string? source)> GetRecordFieldSourceAsync(string qualifiedName)
    {
        // Split on period to get record, field, and event
        var parts = qualifiedName.Split('.');
        if (parts.Length != 3)
            return (false, null);

        var record = parts[0];
        var field = parts[1];
        var eventName = parts[2];

        // Build the file path: Records\RECORD\FIELD\Event.pcode
        var filePath = Path.Combine(_basePath, "Records", record, field, $"{eventName}.pcode");

        return await ReadSourceFileAsync(filePath);
    }

    /// <summary>
    /// Reads the source code from a file if it exists.
    /// </summary>
    private static async Task<(bool found, string? source)> ReadSourceFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, null);

            var source = await File.ReadAllTextAsync(filePath);
            return (true, source);
        }
        catch (Exception)
        {
            // File access errors, encoding issues, etc.
            return (false, null);
        }
    }

    public List<string> GetClassesForPackage(string packageName)
    {
        // Split on colon to get package path parts
        var packageParts = packageName.Split(':');

        // Build the file path: Application Packages\pkg1\pkg2\...\ClassName.pcode
        var pathParts = new List<string> { _basePath, "Application Packages" };
        pathParts.AddRange(packageParts);
        

        string.Join(Path.PathSeparator, pathParts);
        List<string> classes = new List<string>();
        /* get all files that end in .pcode in this directory (do not recurse) */
        foreach(var fileName in Directory.EnumerateFiles(Path.Combine(pathParts.ToArray()), "*.pcode", SearchOption.TopDirectoryOnly)){
            classes.Add(Path.GetFileNameWithoutExtension(fileName));
        }
        return classes;
    }

}
