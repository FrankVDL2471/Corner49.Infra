using Corner49.Logs.Export;
using Corner49.Logs.Storage;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Corner49.Logs.Export.Database;

/// <summary>
/// One resource group's SQLite database ({data dir}/{resource group}.db): every exported
/// LogEvent in "events", plus the export bookkeeping that used to live in the per-day
/// manifest.json files - one "blobs" row per exported blob (ETag, counts, status) and one
/// "day_checks" row per day and "{container}/{service}" listed in Azure.
/// </summary>
public sealed class LogDatabase : IDisposable {
	public const string DateFormat = "yyyy-MM-dd";

	private const int SchemaVersion = 2;

	private const string Schema = """
        CREATE TABLE IF NOT EXISTS blobs (
            id INTEGER PRIMARY KEY,
            container TEXT NOT NULL,
            blob_name TEXT NOT NULL,
            service TEXT NOT NULL,
            date TEXT NOT NULL,
            hour INTEGER NOT NULL,
            etag TEXT,
            blob_size INTEGER,
            last_modified TEXT,
            exported_at TEXT NOT NULL,
            event_count INTEGER NOT NULL,
            unparsed_count INTEGER NOT NULL,
            status TEXT NOT NULL,
            error_message TEXT,
            UNIQUE (container, blob_name));

        CREATE INDEX IF NOT EXISTS ix_blobs_date ON blobs (date);

        CREATE TABLE IF NOT EXISTS day_checks (
            date TEXT NOT NULL,
            check_key TEXT NOT NULL,
            checked_at TEXT NOT NULL,
            PRIMARY KEY (date, check_key));

        CREATE TABLE IF NOT EXISTS events (
            id INTEGER PRIMARY KEY,
            blob_id INTEGER NOT NULL REFERENCES blobs (id) ON DELETE CASCADE,
            service TEXT NOT NULL,
            timestamp TEXT NOT NULL,
            level TEXT,
            category TEXT,
            message TEXT,
            span_id TEXT,
            trace_id TEXT,
            request_id TEXT,
            action_name TEXT,
            event_type TEXT,
            http_method TEXT,
            http_path TEXT,
            status_code INTEGER,
            duration_ms INTEGER,
            source_blob TEXT,
            source_line_number INTEGER NOT NULL,
            imported_at TEXT NOT NULL,
            properties TEXT,
            route TEXT,
            user_id TEXT,
            exception TEXT);

        CREATE INDEX IF NOT EXISTS ix_events_blob_id ON events (blob_id);
        CREATE INDEX IF NOT EXISTS ix_events_timestamp ON events (timestamp);
        CREATE INDEX IF NOT EXISTS ix_events_service_timestamp ON events (service, timestamp);
        CREATE INDEX IF NOT EXISTS ix_events_level_timestamp ON events (level, timestamp);
        CREATE INDEX IF NOT EXISTS ix_events_trace_id ON events (trace_id);
        """;

	/// <summary>
	/// Indexes on columns added after v1: created after <see cref="MigrateEvents"/> so the columns exist.
	/// Also drops the indexes on the app-specific id columns of older databases; those columns are
	/// left in place (nullable, no longer written).
	/// </summary>
	private const string IndexesV2 = """
        DROP INDEX IF EXISTS ix_events_restaurant_id;
        DROP INDEX IF EXISTS ix_events_booking_id;
        CREATE INDEX IF NOT EXISTS ix_events_category_timestamp ON events (category, timestamp);
        CREATE INDEX IF NOT EXISTS ix_events_route ON events (route);
        """;

	/// <summary>Inserts or replaces a blob's bookkeeping row, returning its id.</summary>
	internal const string UpsertBlobSql = """
        INSERT INTO blobs (container, blob_name, service, date, hour, etag, blob_size, last_modified,
                           exported_at, event_count, unparsed_count, status, error_message)
        VALUES ($container, $blobName, $service, $date, $hour, $etag, $blobSize, $lastModified,
                $exportedAt, $eventCount, $unparsedCount, $status, $errorMessage)
        ON CONFLICT (container, blob_name) DO UPDATE SET
            service = excluded.service,
            date = excluded.date,
            hour = excluded.hour,
            etag = excluded.etag,
            blob_size = excluded.blob_size,
            last_modified = excluded.last_modified,
            exported_at = excluded.exported_at,
            event_count = excluded.event_count,
            unparsed_count = excluded.unparsed_count,
            status = excluded.status,
            error_message = excluded.error_message
        RETURNING id;
        """;

	private readonly SqliteConnection _connection;

	public LogDatabase(string path) {
		Path = path;

		// No pooling: a pooled connection would keep the file open after Dispose.
		_connection = new SqliteConnection(new SqliteConnectionStringBuilder {
			DataSource = path,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Pooling = false,
		}.ToString());
		_connection.Open();

		Execute("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON;");
		Execute(Schema);
		MigrateEvents();
		Execute(IndexesV2 + $"PRAGMA user_version = {SchemaVersion};");
	}

	public string Path { get; }

	public BlobRecord? GetBlob(string container, string blobName) {
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT status, etag FROM blobs WHERE container = $container AND blob_name = $blobName;";
		command.Parameters.AddWithValue("$container", container);
		command.Parameters.AddWithValue("$blobName", blobName);

		using SqliteDataReader reader = command.ExecuteReader();
		return reader.Read()
				? new BlobRecord(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1))
				: null;
	}

	/// <summary>
	/// Starts replacing a blob's events: everything written through the returned import is
	/// committed together by <see cref="BlobImport.Complete"/>, or rolled back if it's disposed first.
	/// </summary>
	public BlobImport BeginBlobImport(string container, LogFileInfo file, BlobPathSegments segments) =>
			new(_connection, container, file, segments);

	/// <summary>
	/// Marks a blob as failed so the next run retries it. Events from an earlier successful
	/// export of the blob are kept until a retry succeeds.
	/// </summary>
	public void RecordFailure(string container, LogFileInfo file, BlobPathSegments segments, string errorMessage) {
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = UpsertBlobSql;
		AddBlobParameters(command, container, file, segments, 0, 0, BlobStatus.Failed, errorMessage);
		command.ExecuteScalar();
	}

	/// <summary>What planning needs: the days with blobs, every day check, and the days' failed "{container}/{service}"s.</summary>
	public DatabaseIndex LoadIndex() {
		List<DateOnly> dates = [];
		List<DayCheck> checks = [];
		HashSet<(DateOnly Date, string CheckKey)> failedKeys = [];

		using (SqliteCommand command = _connection.CreateCommand()) {
			command.CommandText = "SELECT DISTINCT date FROM blobs ORDER BY date;";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read()) {
				dates.Add(ParseDate(reader.GetString(0)));
			}
		}

		using (SqliteCommand command = _connection.CreateCommand()) {
			command.CommandText = "SELECT date, check_key, checked_at FROM day_checks;";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read()) {
				checks.Add(new DayCheck(ParseDate(reader.GetString(0)), reader.GetString(1), ParseTimestamp(reader.GetString(2))));
			}
		}

		using (SqliteCommand command = _connection.CreateCommand()) {
			command.CommandText = "SELECT DISTINCT date, container || '/' || service FROM blobs WHERE status = $failed;";
			command.Parameters.AddWithValue("$failed", BlobStatus.Failed);
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read()) {
				failedKeys.Add((ParseDate(reader.GetString(0)), reader.GetString(1)));
			}
		}

		return new DatabaseIndex(dates, checks, failedKeys);
	}

	/// <summary>
	/// Records that <paramref name="checkKey"/> was listed for this day - only for a day that
	/// has at least one blob, so a day without any data leaves no trace.
	/// </summary>
	public void MarkChecked(DateOnly date, string checkKey, DateTimeOffset listedAt) {
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
            INSERT INTO day_checks (date, check_key, checked_at)
            SELECT $date, $checkKey, $checkedAt
            WHERE EXISTS (SELECT 1 FROM blobs WHERE date = $date)
            ON CONFLICT (date, check_key) DO UPDATE SET checked_at = excluded.checked_at;
            """;
		command.Parameters.AddWithValue("$date", FormatDate(date));
		command.Parameters.AddWithValue("$checkKey", checkKey);
		command.Parameters.AddWithValue("$checkedAt", FormatTimestamp(listedAt));
		command.ExecuteNonQuery();
	}

	public void Dispose() => _connection.Dispose();

	internal static void AddBlobParameters(
			SqliteCommand command, string container, LogFileInfo file, BlobPathSegments segments,
			long eventCount, long unparsedCount, string status, string? errorMessage) {
		command.Parameters.AddWithValue("$container", container);
		command.Parameters.AddWithValue("$blobName", file.Name);
		command.Parameters.AddWithValue("$service", segments.Service);
		command.Parameters.AddWithValue("$date", FormatDate(segments.Date));
		command.Parameters.AddWithValue("$hour", segments.Hour);
		command.Parameters.AddWithValue("$etag", (object?)file.ETag ?? DBNull.Value);
		command.Parameters.AddWithValue("$blobSize", file.Size);
		command.Parameters.AddWithValue("$lastModified", file.LastModified is { } lastModified ? FormatTimestamp(lastModified) : DBNull.Value);
		command.Parameters.AddWithValue("$exportedAt", FormatTimestamp(DateTimeOffset.UtcNow));
		command.Parameters.AddWithValue("$eventCount", eventCount);
		command.Parameters.AddWithValue("$unparsedCount", unparsedCount);
		command.Parameters.AddWithValue("$status", status);
		command.Parameters.AddWithValue("$errorMessage", (object?)errorMessage ?? DBNull.Value);
	}

	internal static string FormatDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

	internal static string FormatTimestamp(DateTimeOffset timestamp) =>
			timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

	private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);

	private static DateTimeOffset ParseTimestamp(string value) =>
			DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

	/// <summary>
	/// v1 -> v2: adds the route, user_id and exception columns to an existing events table. A new
	/// database already gets them from <see cref="Schema"/>, so each column is only added when missing.
	/// </summary>
	private void MigrateEvents() {
		HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
		using (SqliteCommand command = _connection.CreateCommand()) {
			command.CommandText = "PRAGMA table_info(events);";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read()) {
				columns.Add(reader.GetString(1));
			}
		}

		foreach (string column in (string[])["route", "user_id", "exception"]) {
			if (!columns.Contains(column)) {
				Execute($"ALTER TABLE events ADD COLUMN {column} TEXT;");
			}
		}
	}

	private void Execute(string sql) {
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}
}

public static class BlobStatus {
	/// <summary>Only ever seen inside a blob's uncommitted import transaction.</summary>
	public const string Importing = "Importing";
	public const string Success = "Success";
	public const string Failed = "Failed";
}

public sealed record BlobRecord(string Status, string? ETag);

public sealed record DayCheck(DateOnly Date, string CheckKey, DateTimeOffset CheckedAt);

public sealed record DatabaseIndex(
		IReadOnlyList<DateOnly> Dates,
		IReadOnlyList<DayCheck> Checks,
		IReadOnlySet<(DateOnly Date, string CheckKey)> FailedKeys);
