using DevAtlas.Application;
using DevAtlas.Domain;

namespace DevAtlas.Infrastructure;

public sealed class FileSystemProjectDiscovery : IProjectDiscovery
{
    private static readonly string[] IgnoredDirectories = ["node_modules", "bin", "obj", "target", "dist", "build"];
    private static readonly (string File, string Type, string Language)[] Signals =
    [
        (".git", "Git repository", "Unknown"),
        ("package.json", "Node.js", "JavaScript/TypeScript"),
        ("pom.xml", "Maven", "Java"),
        ("build.gradle", "Gradle", "Java"),
        ("build.gradle.kts", "Gradle", "Kotlin"),
        ("Cargo.toml", "Rust", "Rust"),
        ("pyproject.toml", "Python", "Python"),
        ("requirements.txt", "Python", "Python"),
        ("go.mod", "Go", "Go"),
        ("composer.json", "PHP", "PHP"),
        ("Dockerfile", "Docker", "Container"),
        ("compose.yml", "Docker Compose", "Container"),
        ("docker-compose.yml", "Docker Compose", "Container")
    ];

    public async IAsyncEnumerable<Project> DiscoverAsync(
        IEnumerable<WorkspaceRoot> roots,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(r => !r.IsIgnored))
        {
            if (!Directory.Exists(root.Path))
                continue;

            foreach (var directory in EnumerateDirectories(root.Path, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = Path.GetFullPath(directory);
                if (!seen.Add(normalized))
                    continue;

                var signals = Signals.Where(s => File.Exists(Path.Combine(directory, s.File))).ToArray();
                if (signals.Length == 0)
                    continue;

                var primary = signals.FirstOrDefault(s => s.File == ".git");
                if (primary == default)
                    primary = signals[0];
                var name = new DirectoryInfo(directory).Name;
                yield return new Project(Guid.NewGuid(), name, normalized, primary.Type, primary.Language);
                await Task.Yield();
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            yield return current;
            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(current); }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            foreach (var child in children)
            {
                if (!IgnoredDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                    pending.Push(child);
            }
        }
    }
}
