namespace DevAtlas.Domain;

public sealed record WorkspaceRoot(Guid Id, string Path, bool IsIgnored = false);

public sealed record Project(
    Guid Id,
    string Name,
    string Path,
    string ProjectType,
    string Language,
    string? CurrentBranch = null,
    int ChangedFileCount = 0,
    DateTimeOffset? LastWorkedAt = null,
    bool IsFavorite = false);

public sealed record ProjectTechnology(string Name, string Category);

public sealed record GitSnapshot(
    bool IsRepository,
    string? Branch,
    int ChangedFileCount,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> RecentCommits);

public sealed record ProjectActivity(Guid ProjectId, string Kind, DateTimeOffset OccurredAt, string? Details);
