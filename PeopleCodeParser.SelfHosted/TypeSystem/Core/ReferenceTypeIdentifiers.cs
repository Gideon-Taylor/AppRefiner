namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Valid reference type identifiers for PeopleCode references like HTML.FOO, SQL.BAR, etc.
/// </summary>
public enum ReferenceTypeIdentifier
{
    /// <summary>
    /// Bar name references (BARNAME.*)
    /// </summary>
    BARNAME,

    /// <summary>
    /// Business process references (BUSPROCESS.*)
    /// </summary>
    BUSPROCESS,

    /// <summary>
    /// Component references (COMPONENT.*)
    /// </summary>
    COMPONENT,

    /// <summary>
    /// Component interface references (COMPINTFC.*)
    /// </summary>
    COMPINTFC,

    /// <summary>
    /// Field references (FIELD.*)
    /// </summary>
    FIELD,

    /// <summary>
    /// HTML element references (HTML.*)
    /// </summary>
    HTML,

    /// <summary>
    /// Image references (IMAGE.*)
    /// </summary>
    IMAGE,

    /// <summary>
    /// Interlink references (INTERLINK.*)
    /// </summary>
    INTERLINK,

    /// <summary>
    /// Menu name references (MENUNAME.*)
    /// </summary>
    MENUNAME,

    /// <summary>
    /// Operation references (OPERATION.*)
    /// </summary>
    OPERATION,

    /// <summary>
    /// Page references (PAGE.*)
    /// </summary>
    PAGE,

    /// <summary>
    /// Panel references (PANEL.*)
    /// </summary>
    PANEL,

    /// <summary>
    /// Portal references (PORTAL.*)
    /// </summary>
    PORTAL,

    /// <summary>
    /// Record references (RECORD.*)
    /// </summary>
    RECORD,

    /// <summary>
    /// Record name references (RECORDNAME.*)
    /// </summary>
    RECORDNAME,

    /// <summary>
    /// Rowset references (ROWSET.*)
    /// </summary>
    ROWSET,

    /// <summary>
    /// Scroll references (SCROLL.*)
    /// </summary>
    SCROLL,

    /// <summary>
    /// Search references (SEARCH.*)
    /// </summary>
    SEARCH,

    /// <summary>
    /// SQL references (SQL.*)
    /// </summary>
    SQL,

    /// <summary>
    /// Stylesheet references (STYLESHEET.*)
    /// </summary>
    STYLESHEET,

    /// <summary>
    /// URL references (URL.*)
    /// </summary>
    URL,

    /// <summary>
    /// URL ID references (URLID.*)
    /// </summary>
    URLID
}

/// <summary>
/// Utilities for working with reference type identifiers
/// </summary>
public static class ReferenceTypeIdentifierUtils
{
    /// <summary>
    /// All valid reference type identifier strings
    /// </summary>
    public static readonly HashSet<string> ValidIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "BARNAME",
        "BUSPROCESS",
        "COMPONENT",
        "COMPINTFC",
        "FIELD",
        "HTML",
        "IMAGE",
        "INTERLINK",
        "MENUNAME",
        "OPERATION",
        "PAGE",
        "PANEL",
        "PORTAL",
        "RECORD",
        "RECORDNAME",
        "ROWSET",
        "SCROLL",
        "SEARCH",
        "SQL",
        "STYLESHEET",
        "URL",
        "URLID"
    };

    /// <summary>
    /// Checks if a string is a valid reference type identifier
    /// </summary>
    /// <param name="identifier">The identifier to check</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidReferenceIdentifier(string identifier)
    {
        return !string.IsNullOrEmpty(identifier) && ValidIdentifiers.Contains(identifier);
    }

    /// <summary>
    /// Tries to parse a string as a reference type identifier enum value
    /// </summary>
    /// <param name="identifier">The identifier string</param>
    /// <param name="result">The parsed enum value if successful</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParseIdentifier(string identifier, out ReferenceTypeIdentifier result)
    {
        result = default;

        if (string.IsNullOrEmpty(identifier))
            return false;

        return Enum.TryParse<ReferenceTypeIdentifier>(identifier, true, out result);
    }

    /// <summary>
    /// Gets all valid reference type identifiers as strings
    /// </summary>
    /// <returns>Collection of all valid identifier strings</returns>
    public static IEnumerable<string> GetAllValidIdentifiers()
    {
        return ValidIdentifiers;
    }

    /// <summary>
    /// Gets the description for a reference type identifier
    /// </summary>
    /// <param name="identifier">The identifier</param>
    /// <returns>A human-readable description</returns>
    public static string GetDescription(ReferenceTypeIdentifier identifier)
    {
        return identifier switch
        {
            ReferenceTypeIdentifier.BARNAME => "Bar name references",
            ReferenceTypeIdentifier.BUSPROCESS => "Business process references",
            ReferenceTypeIdentifier.COMPONENT => "Component references",
            ReferenceTypeIdentifier.COMPINTFC => "Component interface references",
            ReferenceTypeIdentifier.FIELD => "Field references",
            ReferenceTypeIdentifier.HTML => "HTML element references",
            ReferenceTypeIdentifier.IMAGE => "Image references",
            ReferenceTypeIdentifier.INTERLINK => "Interlink references",
            ReferenceTypeIdentifier.MENUNAME => "Menu name references",
            ReferenceTypeIdentifier.OPERATION => "Operation references",
            ReferenceTypeIdentifier.PAGE => "Page references",
            ReferenceTypeIdentifier.PANEL => "Panel references",
            ReferenceTypeIdentifier.PORTAL => "Portal references",
            ReferenceTypeIdentifier.RECORD => "Record references",
            ReferenceTypeIdentifier.RECORDNAME => "Record name references",
            ReferenceTypeIdentifier.ROWSET => "Rowset references",
            ReferenceTypeIdentifier.SCROLL => "Scroll references",
            ReferenceTypeIdentifier.SEARCH => "Search references",
            ReferenceTypeIdentifier.SQL => "SQL references",
            ReferenceTypeIdentifier.STYLESHEET => "Stylesheet references",
            ReferenceTypeIdentifier.URL => "URL references",
            ReferenceTypeIdentifier.URLID => "URL ID references",
            _ => "Unknown reference type"
        };
    }
}