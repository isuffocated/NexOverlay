using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NexOverlay.Core.Files;
using NexOverlay.Storage.Paths;

namespace NexOverlay.Storage.Files;

public sealed class WorkspaceFileRepository
{
    private readonly string _connectionString;

    public WorkspaceFileRepository(
        AppDataPathService paths)
    {
        _connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = paths.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS workspace_files
            (
                id TEXT PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                path TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_workspace_files_created_at
            ON workspace_files(created_at DESC);
            """;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<WorkspaceFileItem>> GetAllAsync()
    {
        var result =
            new List<WorkspaceFileItem>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT id, name, path
            FROM workspace_files
            ORDER BY created_at DESC;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                new WorkspaceFileItem(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2)));
        }

        return result;
    }

    public async Task<int> CountAsync()
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            "SELECT COUNT(*) FROM workspace_files;";

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    public async Task AddAsync(
        WorkspaceFileItem item)
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO workspace_files
            (
                id,
                name,
                path,
                created_at
            )
            VALUES
            (
                $id,
                $name,
                $path,
                $createdAt
            )
            ON CONFLICT(path) DO UPDATE SET
                name = excluded.name;
            """;

        command.Parameters.AddWithValue(
            "$id",
            item.Id.ToString());

        command.Parameters.AddWithValue(
            "$name",
            item.Name);

        command.Parameters.AddWithValue(
            "$path",
            item.Path);

        command.Parameters.AddWithValue(
            "$createdAt",
            DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(
        Guid id)
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            "DELETE FROM workspace_files WHERE id = $id;";

        command.Parameters.AddWithValue(
            "$id",
            id.ToString());

        await command.ExecuteNonQueryAsync();
    }
}