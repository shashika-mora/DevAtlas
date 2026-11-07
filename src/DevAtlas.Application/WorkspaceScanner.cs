using System.Collections.Concurrent;
using System.ComponentModel;
using DevAtlas.Domain;

namespace DevAtlas.Application;

public sealed class WorkspaceScanner(IWorkspaceRootStore roots, IProjectStore store, IProjectDiscovery discovery, IGitReader git, IAppLog log) : IWorkspaceScanner
{
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    public async Task<WorkspaceScanResult> ScanAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            var previous = (await store.GetProjectsAsync(cancellationToken, includeUnavailable: true))
                .ToDictionary(p => ProjectPaths.Normalize(p.Path), ProjectPaths.Comparer);
            var discovered = await discovery.DiscoverAsync(await roots.GetAllAsync(cancellationToken), progress, cancellationToken);
            var warnings = new ConcurrentBag<string>(discovered.Warnings);
            var projects = new ConcurrentBag<Project>();
            await Parallel.ForEachAsync(discovered.Projects, new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            }, async (project, token) =>
            {
                previous.TryGetValue(project.Path, out var saved);
                var enriched = project with
                {
                    Id = saved?.Id ?? project.Id,
                    IsFavorite = saved?.IsFavorite ?? false,
                    LastWorkedAt = saved?.LastWorkedAt,
                    LastScannedAt = DateTimeOffset.UtcNow
                };
                progress?.Report($"Inspecting {project.Name}…");
                try
                {
                    var snapshot = await git.ReadAsync(project.Path, token);
                    enriched = enriched with { CurrentBranch = snapshot.Branch, ChangedFileCount = snapshot.ChangedFileCount };
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or UnauthorizedAccessException or TimeoutException)
                {
                    warnings.Add($"Git information unavailable for {project.Name}.");
                    log.Write("Git inspection unavailable", ex);
                    enriched = enriched with
                    {
                        CurrentBranch = saved?.CurrentBranch,
                        ChangedFileCount = saved?.ChangedFileCount ?? 0,
                        GitError = ex is Win32Exception ? "Git is unavailable. Install Git and add it to PATH." : "Git inspection failed. Previous metadata, if any, is retained."
                    };
                }
                projects.Add(enriched);
            });
            cancellationToken.ThrowIfCancellationRequested();
            await store.ApplyScanAsync(projects.ToArray(), discovered.CompletedRoots, cancellationToken);
            // Commit is the cancellation boundary: return the committed view even if a late
            // cancellation arrives, rather than claiming the old state was retained.
            var result = await store.GetProjectsAsync(CancellationToken.None);
            log.Write($"Scan completed: {result.Count} projects; {warnings.Count} warnings.");
            return new WorkspaceScanResult(result, warnings.Order(StringComparer.Ordinal).ToArray());
        }
        finally { _scanLock.Release(); }
    }
}
