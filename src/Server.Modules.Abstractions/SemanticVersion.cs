namespace ResellerSystem.Server.Modules.Abstractions;

/// <summary>
/// Minimal strict MAJOR.MINOR.PATCH parser/comparer. Deliberately not a
/// NuGet dependency (e.g. NuGet.Versioning) — this system only ever needs
/// three-part comparison for module/Core compatibility checks and the
/// future Update Engine, so pulling in a much larger package would be
/// unnecessary weight for what a ~30-line type covers.
/// </summary>
public readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException($"'{value}' is not a valid MAJOR.MINOR.PATCH version string.");
        }
        return result;
    }

    public static bool TryParse(string? value, out SemanticVersion result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split('.');
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;
        if (!int.TryParse(parts[2], out var patch)) return false;

        result = new SemanticVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0) return majorCompare;

        var minorCompare = Minor.CompareTo(other.Minor);
        if (minorCompare != 0) return minorCompare;

        return Patch.CompareTo(other.Patch);
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
