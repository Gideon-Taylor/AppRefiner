namespace PeopleCodeTypeInfo.Contracts;

/// <summary>
/// Provides access to PeopleCode program source code.
/// Implemented by the host application (AppRefiner) to fetch source from database/files.
/// </summary>
public interface IProgramSourceProvider
{
    /// <summary>
    /// Attempts to retrieve the source code for a PeopleCode program.
    /// </summary>
    /// <param name="qualifiedName">
    /// The qualified name of the program (e.g., "MY_PKG:MyClass" or "MyClass").
    /// Can be an app class, interface, or function library.
    /// </param>
    /// <returns>
    /// A tuple indicating whether the source was found and the source code if available.
    /// </returns>
    Task<(bool found, string? source)> GetSourceAsync(string qualifiedName);
}

/// <summary>
/// Null implementation of IProgramSourceProvider that never finds any source.
/// Useful for testing or when external program resolution is not available.
/// </summary>
public class NullProgramSourceProvider : IProgramSourceProvider
{
    /// <summary>
    /// Singleton instance
    /// </summary>
    public static readonly NullProgramSourceProvider Instance = new();

    private NullProgramSourceProvider() { }

    /// <summary>
    /// Always returns (false, null) indicating no source was found.
    /// </summary>
    public Task<(bool found, string? source)> GetSourceAsync(string qualifiedName)
    {
        return Task.FromResult((false, (string?)null));
    }
}
