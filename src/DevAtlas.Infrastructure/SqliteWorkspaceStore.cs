using System.Globalization;
using DevAtlas.Application;
using DevAtlas.Domain;
using Microsoft.Data.Sqlite;

namespace DevAtlas.Infrastructure;

// Connections are owned by individual background operations, never by the UI thread.
public sealed class SqliteWorkspaceStore(string databasePath) : IWorkspaceRootStore, IProjectStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _databasePath = Path.GetFullPath(databasePath);
    private bool _initialized;

    private async Task<T> RunAsync<T>(Func<SqliteConnection, T> operation, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = _databasePath,
                    Pooling = false,
                    DefaultTimeout = 5
                }.ToString());
                connection.Open();
                if (!_initialized) { Initialize(connection); _initialized = true; }
                token.ThrowIfCancellationRequested();
                return operation(connection);
            }, token);
        }
        finally { _gate.Release(); }
    }

    public Task<IReadOnlyList<WorkspaceRoot>> GetAllAsync(CancellationToken cancellationToken = default) => RunAsync<IReadOnlyList<WorkspaceRoot>>(connection =>
    {
        var roots = new List<WorkspaceRoot>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, path, is_ignored FROM workspace_roots ORDER BY path";
        using var reader = command.ExecuteReader();
        while (reader.Read()) roots.Add(new WorkspaceRoot(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetInt32(2) != 0));
        return roots;
    }, cancellationToken);

    public Task AddAsync(WorkspaceRoot root, CancellationToken cancellationToken = default) => RunAsync(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO workspace_roots (id, path, is_ignored) VALUES ($id, $path, $ignored) ON CONFLICT(path) DO UPDATE SET is_ignored = excluded.is_ignored";
        command.Parameters.AddWithValue("$id", root.Id.ToString());
        command.Parameters.AddWithValue("$path", ProjectPaths.Normalize(root.Path));
        command.Parameters.AddWithValue("$ignored", root.IsIgnored ? 1 : 0);
        return command.ExecuteNonQuery();
    }, cancellationToken);

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) => RunAsync(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workspace_roots WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery();
    }, cancellationToken);

    public Task SetIgnoredAsync(Guid id, bool ignored, CancellationToken cancellationToken = default) => RunAsync(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE workspace_roots SET is_ignored = $ignored WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$ignored", ignored ? 1 : 0);
        return command.ExecuteNonQuery();
    }, cancellationToken);

    public Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default, bool includeUnavailable = false) =>
        RunAsync<IReadOnlyList<Project>>(connection => ReadProjects(connection, includeUnavailable), cancellationToken);

    private static List<Project> ReadProjects(SqliteConnection connection, bool includeUnavailable, SqliteTransaction? transaction = null)
    {
        var projects = new List<Project>();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, name, path, project_type, language, current_branch, changed_file_count,
                   last_worked_at, is_favorite, last_scanned_at, git_error, is_available
            FROM projects WHERE $all = 1 OR is_available = 1 ORDER BY is_favorite DESC, name COLLATE NOCASE, path
            """;
        command.Parameters.AddWithValue("$all", includeUnavailable ? 1 : 0);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            projects.Add(new Project(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6),
                reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture), reader.GetInt32(8) != 0,
                reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
                reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetInt32(11) != 0));
        return projects;
    }

    public Task SaveAsync(Project project, CancellationToken cancellationToken = default) => RunAsync(connection =>
    {
        Save(connection, project, null, preserveUserData: false);
        return 0;
    }, cancellationToken);

    public Task ApplyScanAsync(IReadOnlyList<Project> projects, IReadOnlyList<string> completedRoots, CancellationToken cancellationToken = default) => RunAsync(connection =>
    {
        using var transaction = connection.BeginTransaction();
        var existing = ReadProjects(connection, includeUnavailable: true, transaction);
        var found = projects.Select(p => ProjectPaths.Normalize(p.Path)).ToHashSet(ProjectPaths.Comparer);
        var activeRoots = new List<string>();
        using (var roots = connection.CreateCommand())
        {
            roots.Transaction = transaction;
            roots.CommandText = "SELECT path FROM workspace_roots WHERE is_ignored = 0";
            using var reader = roots.ExecuteReader();
            while (reader.Read()) activeRoots.Add(reader.GetString(0));
        }
        foreach (var old in existing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var belongsToActiveRoot = activeRoots.Any(root => ProjectPaths.Contains(root, old.Path));
            if (!found.Contains(old.Path) && (!belongsToActiveRoot || completedRoots.Any(root => ProjectPaths.Contains(root, old.Path))))
            {
                using var hide = connection.CreateCommand();
                hide.Transaction = transaction;
                hide.CommandText = "UPDATE projects SET is_available = 0 WHERE id = $id";
                hide.Parameters.AddWithValue("$id", old.Id.ToString());
                hide.ExecuteNonQuery();
            }
        }
        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Save(connection, project with { IsAvailable = true }, transaction, preserveUserData: true);
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return 0;
    }, cancellationToken);

    private static void Save(SqliteConnection connection, Project project, SqliteTransaction? transaction, bool preserveUserData)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO projects (id, name, path, project_type, language, current_branch, changed_file_count,
                                  last_worked_at, is_favorite, last_scanned_at, git_error, is_available)
            VALUES ($id, $name, $path, $type, $language, $branch, $changed, $worked, $favorite, $scanned, $error, $available)
            ON CONFLICT DO UPDATE SET path = excluded.path, name = excluded.name, project_type = excluded.project_type,
                language = excluded.language, current_branch = excluded.current_branch, changed_file_count = excluded.changed_file_count,
                last_scanned_at = excluded.last_scanned_at, git_error = excluded.git_error, is_available = excluded.is_available
            """ + (preserveUserData ? "" : ", is_favorite = excluded.is_favorite, last_worked_at = excluded.last_worked_at");
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$path", ProjectPaths.Normalize(project.Path));
        command.Parameters.AddWithValue("$type", project.ProjectType);
        command.Parameters.AddWithValue("$language", project.Language);
        command.Parameters.AddWithValue("$branch", (object?)project.CurrentBranch ?? DBNull.Value);
        command.Parameters.AddWithValue("$changed", project.ChangedFileCount);
        command.Parameters.AddWithValue("$worked", (object?)project.LastWorkedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$favorite", project.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$scanned", (object?)project.LastScannedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)project.GitError ?? DBNull.Value);
        command.Parameters.AddWithValue("$available", project.IsAvailable ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private static void Initialize(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL";
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version > 1) throw new InvalidOperationException("This database was created by a newer DevAtlas version. Install that version to open it.");
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS workspace_roots (id TEXT PRIMARY KEY, path TEXT NOT NULL UNIQUE, is_ignored INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS projects (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, path TEXT NOT NULL UNIQUE, project_type TEXT NOT NULL,
                language TEXT NOT NULL, current_branch TEXT, changed_file_count INTEGER NOT NULL DEFAULT 0,
                last_worked_at TEXT, is_favorite INTEGER NOT NULL DEFAULT 0);
            """;
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA table_info(projects)";
        var columns = new HashSet<string>();
        using (var reader = command.ExecuteReader()) while (reader.Read()) columns.Add(reader.GetString(1));
        foreach (var (name, definition) in new[] { ("last_scanned_at", "TEXT"), ("git_error", "TEXT"), ("is_available", "INTEGER NOT NULL DEFAULT 1") })
        {
            if (columns.Contains(name)) continue;
            command.CommandText = $"ALTER TABLE projects ADD COLUMN {name} {definition}";
            command.ExecuteNonQuery();
        }
        if (version == 0)
        {
            // Legacy paths could contain trailing separators or aliases. Merge metadata before
            // canonicalizing paths so existing primary keys and favorites survive the upgrade.
            foreach (var group in ReadProjects(connection, includeUnavailable: true, transaction)
                .GroupBy(project => ProjectPaths.Normalize(project.Path), ProjectPaths.Comparer))
            {
                var retained = group.FirstOrDefault(project => project.Path == group.Key) ?? group.First();
                foreach (var duplicate in group.Where(project => project.Id != retained.Id))
                {
                    command.CommandText = "DELETE FROM projects WHERE id = $id";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$id", duplicate.Id.ToString());
                    command.ExecuteNonQuery();
                }
                var worked = group.Where(project => project.LastWorkedAt != null).Select(project => project.LastWorkedAt).Max();
                Save(connection, retained with { Path = group.Key, IsFavorite = group.Any(project => project.IsFavorite), LastWorkedAt = worked }, transaction, preserveUserData: false);
            }
        }
        command.Parameters.Clear();
        command.CommandText = "PRAGMA user_version = 1";
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
