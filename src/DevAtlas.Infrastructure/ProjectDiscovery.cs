using DevAtlas.Application;
using DevAtlas.Domain;

namespace DevAtlas.Infrastructure;

public sealed class FileSystemProjectDiscovery : IProjectDiscovery
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", ".vs", ".idea", ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache",
        "node_modules", "bin", "obj", "target", "dist", "build", ".next", ".nuxt", ".gradle", "vendor", "coverage"
    };
    private static readonly (string File, string Type, string Language)[] Signals =
    [
        ("package.json", "Node.js", "JavaScript/TypeScript"), ("pom.xml", "Maven", "Java"),
        ("build.gradle.kts", "Gradle", "Kotlin"), ("build.gradle", "Gradle", "Java"),
        ("Cargo.toml", "Rust", "Rust"), ("pyproject.toml", "Python", "Python"),
        ("requirements.txt", "Python", "Python"), ("go.mod", "Go", "Go"), ("composer.json", "PHP", "PHP"),
        ("Dockerfile", "Docker", "Container"), ("compose.yml", "Docker Compose", "Container"),
        ("compose.yaml", "Docker Compose", "Container"), ("docker-compose.yml", "Docker Compose", "Container"),
        ("docker-compose.yaml", "Docker Compose", "Container")
    ];

    public Task<DiscoveryResult> DiscoverAsync(IEnumerable<WorkspaceRoot> roots, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var selected = roots.Where(r => !r.IsIgnored)
            .GroupBy(r => ProjectPaths.Normalize(r.Path), ProjectPaths.Comparer).Select(group => group.First())
            .OrderBy(r => ProjectPaths.Normalize(r.Path).Length).ToArray();
        selected = selected.Where(root => !selected.Any(parent => parent != root && ProjectPaths.Contains(parent.Path, root.Path))).ToArray();
        return Task.Run(() => Discover(selected, progress, cancellationToken), cancellationToken);
    }

    private static DiscoveryResult Discover(WorkspaceRoot[] roots, IProgress<string>? progress, CancellationToken token)
    {
        var projects = new Dictionary<string, Project>(ProjectPaths.Comparer);
        var completed = new HashSet<string>(ProjectPaths.Comparer);
        var warnings = new List<string>();
        var visited = 0;
        foreach (var root in roots)
        {
            token.ThrowIfCancellationRequested();
            var rootPath = ProjectPaths.Normalize(root.Path);
            var pending = new Stack<string>();
            pending.Push(rootPath);
            var complete = true;
            while (pending.TryPop(out var current))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    // Recheck at visitation: directories may change after being queued.
                    var info = new DirectoryInfo(current);
                    if (!info.Exists) throw new DirectoryNotFoundException();
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        if (ProjectPaths.Comparer.Equals(rootPath, current))
                        {
                            complete = false;
                            warnings.Add($"Linked workspace root {rootPath} was skipped. Choose its physical folder instead.");
                        }
                        continue;
                    }
                    if (++visited % 100 == 0) progress?.Report($"Examined {visited:N0} folders…");
                    var files = Directory.GetFiles(current).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var signal = Signals.FirstOrDefault(s => files.Contains(s.File));
                    var dotnet = files.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase));
                    var solution = files.Any(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));
                    if (signal == default && (dotnet != null || solution))
                        signal = (dotnet ?? "solution", ".NET", dotnet?.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) == true ? "F#" : solution && dotnet == null ? ".NET" : "C#");
                    if (signal == default && (Directory.Exists(Path.Combine(current, ".git")) || files.Contains(".git")))
                        signal = (".git", "Git repository", "Unknown");
                    if (signal != default)
                        projects.TryAdd(current, new Project(ProjectPaths.Identity(current), info.Name, current, signal.Type, signal.Language));
                    // Materialize inside the error boundary, including failures in MoveNext.
                    foreach (var child in Directory.GetDirectories(current))
                    {
                        token.ThrowIfCancellationRequested();
                        if (!IgnoredDirectories.Contains(Path.GetFileName(child))) pending.Push(ProjectPaths.Normalize(child));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    complete = false;
                    warnings.Add($"A folder in {rootPath} was unavailable; existing projects in this root were retained.");
                }
            }
            if (complete) completed.Add(rootPath);
        }
        return new DiscoveryResult(projects.Values.OrderBy(p => p.Path, ProjectPaths.Comparer).ToArray(), completed.ToArray(), warnings.Distinct().ToArray());
    }
}
