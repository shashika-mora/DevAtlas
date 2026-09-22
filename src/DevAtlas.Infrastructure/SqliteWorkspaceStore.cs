using DevAtlas.Application;
using DevAtlas.Domain;
using Microsoft.Data.Sqlite;

namespace DevAtlas.Infrastructure;

public sealed class SqliteWorkspaceStore : IWorkspaceRootStore, IProjectStore, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteWorkspaceStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        Initialize();
    }

    public async Task<IReadOnlyList<WorkspaceRoot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roots = new List<WorkspaceRoot>();
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, path, is_ignored FROM workspace_roots ORDER BY path";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            roots.Add(new WorkspaceRoot(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetInt32(2) != 0));
        return roots;
    }

    public async Task AddAsync(WorkspaceRoot root, CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO workspace_roots (id, path, is_ignored) VALUES ($id, $path, $ignored)";
        command.Parameters.AddWithValue("$id", root.Id.ToString());
        command.Parameters.AddWithValue("$path", root.Path);
        command.Parameters.AddWithValue("$ignored", root.IsIgnored ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM workspace_roots WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, path, project_type, language, current_branch,
                   changed_file_count, last_worked_at, is_favorite
            FROM projects ORDER BY name
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            DateTimeOffset? lastWorked = reader.IsDBNull(7)
                ? null
                : DateTimeOffset.Parse(reader.GetString(7));
            projects.Add(new Project(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6),
                lastWorked, reader.GetInt32(8) != 0));
        }
        return projects;
    }

    public async Task SaveAsync(Project project, CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO projects
              (id, name, path, project_type, language, current_branch, changed_file_count, last_worked_at, is_favorite)
            VALUES ($id, $name, $path, $type, $language, $branch, $changed, $worked, $favorite)
            """;
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$path", project.Path);
        command.Parameters.AddWithValue("$type", project.ProjectType);
        command.Parameters.AddWithValue("$language", project.Language);
        command.Parameters.AddWithValue("$branch", (object?)project.CurrentBranch ?? DBNull.Value);
        command.Parameters.AddWithValue("$changed", project.ChangedFileCount);
        command.Parameters.AddWithValue("$worked", (object?)project.LastWorkedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$favorite", project.IsFavorite ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void Initialize()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS workspace_roots (
              id TEXT PRIMARY KEY, path TEXT NOT NULL UNIQUE, is_ignored INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS projects (
              id TEXT PRIMARY KEY, name TEXT NOT NULL, path TEXT NOT NULL UNIQUE,
              project_type TEXT NOT NULL, language TEXT NOT NULL, current_branch TEXT,
              changed_file_count INTEGER NOT NULL DEFAULT 0, last_worked_at TEXT, is_favorite INTEGER NOT NULL DEFAULT 0
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
