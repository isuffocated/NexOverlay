using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NexOverlay.Core.Clipboard;
using NexOverlay.Storage.Paths;

namespace NexOverlay.Storage.Clipboard;

public sealed class ClipboardRepository
{
    private const int MaxUnpinnedItems = 100;

    private readonly string _connectionString;

    public ClipboardRepository(
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
            CREATE TABLE IF NOT EXISTS clipboard_items
            (
                id TEXT PRIMARY KEY NOT NULL,
                content TEXT NOT NULL UNIQUE,
                pinned INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS
            idx_clipboard_items_updated
            ON clipboard_items(pinned DESC, updated_at DESC);
            """;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetAllAsync()
    {
        var result =
            new List<ClipboardItem>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                content,
                pinned,
                updated_at
            FROM clipboard_items
            ORDER BY
                pinned DESC,
                updated_at DESC;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(
                new ClipboardItem(
                    Guid.Parse(
                        reader.GetString(0)),

                    reader.GetString(1),

                    reader.GetInt64(2) != 0,

                    DateTimeOffset.Parse(
                        reader.GetString(3))));
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
            "SELECT COUNT(*) FROM clipboard_items;";

        return
            Convert.ToInt32(
                await command.ExecuteScalarAsync());
    }

    public async Task CaptureAsync(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        if (content.Length > 100_000)
            return;

        var now =
            DateTimeOffset.UtcNow.ToString("O");

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        await using var transaction =
            (SqliteTransaction)
            await connection.BeginTransactionAsync();

        var upsert =
            connection.CreateCommand();

        upsert.Transaction =
            transaction;

        upsert.CommandText =
            """
            INSERT INTO clipboard_items
            (
                id,
                content,
                pinned,
                created_at,
                updated_at
            )
            VALUES
            (
                $id,
                $content,
                0,
                $now,
                $now
            )
            ON CONFLICT(content)
            DO UPDATE SET
                updated_at = excluded.updated_at;
            """;

        upsert.Parameters.AddWithValue(
            "$id",
            Guid.NewGuid().ToString());

        upsert.Parameters.AddWithValue(
            "$content",
            content);

        upsert.Parameters.AddWithValue(
            "$now",
            now);

        await upsert.ExecuteNonQueryAsync();

        var trim =
            connection.CreateCommand();

        trim.Transaction =
            transaction;

        trim.CommandText =
            """
            DELETE FROM clipboard_items
            WHERE id IN
            (
                SELECT id
                FROM clipboard_items
                WHERE pinned = 0
                ORDER BY updated_at DESC
                LIMIT -1 OFFSET $limit
            );
            """;

        trim.Parameters.AddWithValue(
            "$limit",
            MaxUnpinnedItems);

        await trim.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }

    public async Task SetPinnedAsync(
        Guid id,
        bool pinned)
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE clipboard_items
            SET pinned = $pinned
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id.ToString());

        command.Parameters.AddWithValue(
            "$pinned",
            pinned
                ? 1
                : 0);

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
            """
            DELETE FROM clipboard_items
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            id.ToString());

        await command.ExecuteNonQueryAsync();
    }
}