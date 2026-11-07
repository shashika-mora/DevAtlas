using System.Security.Cryptography;
using System.Text;

namespace DevAtlas.Application;

public static class ProjectPaths
{
    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool Contains(string root, string path)
    {
        root = Normalize(root);
        path = Normalize(path);
        return Comparer.Equals(root, path) || path.StartsWith(
            Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static Guid Identity(string path)
    {
        var normalized = Normalize(path);
        if (OperatingSystem.IsWindows()) normalized = normalized.ToUpperInvariant();
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)).AsSpan(0, 16));
    }
}
