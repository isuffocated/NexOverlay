using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NexOverlay.Core.Snippets;
using NexOverlay.Storage.Paths;

namespace NexOverlay.Storage.Snippets;

public sealed class SnippetRepository
{
    private readonly string _connectionString;

    public SnippetRepository(AppDataPathService paths)
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

        var command = connection.CreateCommand();

        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS snippets
            (
                id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                category TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_snippets_updated_at
            ON snippets(updated_at DESC);
            """;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<SnippetItem>> GetAllAsync()
    {
        var result = new List<SnippetItem>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT id, title, content, category
            FROM snippets
            ORDER BY updated_at DESC;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                new SnippetItem(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
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
            "SELECT COUNT(*) FROM snippets;";

        var result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<SnippetItem>> GetRecentAsync(
        int limit)
    {
        var result =
            new List<SnippetItem>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT id, title, content, category
            FROM snippets
            ORDER BY updated_at DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$limit",
            Math.Max(1, limit));

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                new SnippetItem(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
        }

        return result;
    }

    public async Task UpsertAsync(SnippetItem item)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO snippets
            (
                id,
                title,
                content,
                category,
                created_at,
                updated_at
            )
            VALUES
            (
                $id,
                $title,
                $content,
                $category,
                $createdAt,
                $updatedAt
            )
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                content = excluded.content,
                category = excluded.category,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$id", item.Id.ToString());
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$content", item.Content);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM snippets
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync();
    }
}