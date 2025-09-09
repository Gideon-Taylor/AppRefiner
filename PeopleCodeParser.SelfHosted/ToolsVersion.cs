using System.Globalization;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Represents a PeopleTools version for comparison in compiler directives.
/// Supports version formats: major.minor[.patch] (e.g., "8.54", "8.55.13")
/// </summary>
public class ToolsVersion : IComparable<ToolsVersion>
{
    /// <summary>
    /// Major version number
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Minor version number
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Optional patch version number (null if not specified)
    /// </summary>
    public int? Patch { get; }

    /// <summary>
    /// Create a ToolsVersion from a version string
    /// </summary>
    /// <param name="version">Version string in format "major.minor[.patch]"</param>
    /// <exception cref="ArgumentException">Thrown if version format is invalid</exception>
    public ToolsVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version string cannot be null or empty", nameof(version));

        var parts = version.Trim().Split('.');
        if (parts.Length < 2 || parts.Length > 3)
            throw new ArgumentException($"Invalid version format: '{version}'. Expected format: major.minor[.patch]", nameof(version));

        try
        {
            Major = int.Parse(parts[0], CultureInfo.InvariantCulture);
            Minor = int.Parse(parts[1], CultureInfo.InvariantCulture);
            Patch = parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : null;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"Invalid version format: '{version}'. All version components must be valid integers.", nameof(version), ex);
        }
        catch (OverflowException ex)
        {
            throw new ArgumentException($"Invalid version format: '{version}'. Version components are too large.", nameof(version), ex);
        }

        if (Major < 0 || Minor < 0 || (Patch.HasValue && Patch.Value < 0))
            throw new ArgumentException($"Invalid version format: '{version}'. Version components cannot be negative.", nameof(version));
    }

    /// <summary>
    /// Create a ToolsVersion with explicit values
    /// </summary>
    public ToolsVersion(int major, int minor, int? patch = null)
    {
        if (major < 0) throw new ArgumentException("Major version cannot be negative", nameof(major));
        if (minor < 0) throw new ArgumentException("Minor version cannot be negative", nameof(minor));
        if (patch.HasValue && patch.Value < 0) throw new ArgumentException("Patch version cannot be negative", nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Compare two ToolsVersion instances according to PeopleCode rules:
    /// - If either version lacks patch level, compare only major.minor
    /// - If both have patch level, compare all three components
    /// </summary>
    public int CompareTo(ToolsVersion? other)
    {
        if (other == null) return 1;

        // Compare major version first
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;

        // Compare minor version
        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;

        // PeopleCode rule: If either version lacks patch level, stop comparison here
        if (Patch == null || other.Patch == null)
            return 0; // Versions are considered equal at release level

        // Both have patch levels - compare them
        return Patch.Value.CompareTo(other.Patch.Value);
    }

    /// <summary>
    /// Check if two versions are equal
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is ToolsVersion other && CompareTo(other) == 0;
    }

    /// <summary>
    /// Get hash code for this version
    /// </summary>
    public override int GetHashCode()
    {
        // Only use major and minor for hash code to match comparison logic
        return HashCode.Combine(Major, Minor);
    }

    /// <summary>
    /// Get string representation of this version
    /// </summary>
    public override string ToString()
    {
        return Patch.HasValue ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}";
    }

    /// <summary>
    /// Equality operator
    /// </summary>
    public static bool operator ==(ToolsVersion? left, ToolsVersion? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.CompareTo(right) == 0;
    }

    /// <summary>
    /// Inequality operator
    /// </summary>
    public static bool operator !=(ToolsVersion? left, ToolsVersion? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Less than operator
    /// </summary>
    public static bool operator <(ToolsVersion? left, ToolsVersion? right)
    {
        if (left is null) return right is not null;
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Less than or equal operator
    /// </summary>
    public static bool operator <=(ToolsVersion? left, ToolsVersion? right)
    {
        if (left is null) return true;
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Greater than operator
    /// </summary>
    public static bool operator >(ToolsVersion? left, ToolsVersion? right)
    {
        if (left is null) return false;
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Greater than or equal operator
    /// </summary>
    public static bool operator >=(ToolsVersion? left, ToolsVersion? right)
    {
        if (right is null) return true;
        if (left is null) return false;
        return left.CompareTo(right) >= 0;
    }
}