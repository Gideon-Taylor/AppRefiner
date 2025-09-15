using System.Threading.Tasks;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Provides access to serialized PeopleCode programs (application classes, interfaces, etc.)
/// so the type system can resolve external type information on demand.
/// </summary>
public interface IProgramSourceProvider
{
    /// <summary>
    /// Attempts to retrieve the source text for a PeopleCode program by its fully qualified identifier.
    /// The identifier should match the canonical path (e.g., package:classname for application classes).
    /// </summary>
    /// <param name="qualifiedName">Fully qualified program identifier.</param>
    /// <param name="source">If found, the raw PeopleCode source.</param>
    /// <returns>True when source was located; otherwise false.</returns>
    Task<(bool found, string? source)> TryGetProgramSourceAsync(string qualifiedName);
}
