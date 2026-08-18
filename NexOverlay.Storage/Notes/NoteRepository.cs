using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NexOverlay.Core.Notes;
using NexOverlay.Storage.Paths;

namespace NexOverlay.Storage.Notes;

public sealed class NoteRepository
{
    private readonly string _connectionString;

    public NoteRepository(AppDataPathService paths)
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
            CREATE TABLE IF NOT EXISTS notes
            (
                id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_notes_updated_at
            ON notes(updated_at DESC);
            """;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<NoteItem>> GetAllAsync()
    {
        var result =
            new List<NoteItem>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT id, title, content
            FROM notes
            ORDER BY updated_at DESC;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                new NoteItem(
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
            "SELECT COUNT(*) FROM notes;";

        var result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task UpsertAsync(NoteItem item)
    {
        var now =
            DateTimeOffset.UtcNow.ToString("O");

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO notes
            (
                id,
                title,
                content,
                created_at,
                updated_at
            )
            VALUES
            (
                $id,
                $title,
                $content,
                $createdAt,
                $updatedAt
            )
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                content = excluded.content,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue(
            "$id",
            item.Id.ToString());

        command.Parameters.AddWithValue(
            "$title",
            item.Title);

        command.Parameters.AddWithValue(
            "$content",
            item.Content);

        command.Parameters.AddWithValue(
            "$createdAt",
            now);

        command.Parameters.AddWithValue(
            "$updatedAt",
            now);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM notes
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id.ToString());

        await command.ExecuteNonQueryAsync();
    }
}