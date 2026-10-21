using Corner49.Logs.Domain;
using Corner49.Logs.Storage;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Corner49.Logs.Export.Database;

/// <summary>
/// Replaces one blob's events inside a single transaction: the blob's row is upserted, its old
/// events deleted, and new events streamed in one prepared INSERT at a time. <see cref="Complete"/>
/// commits; disposing without completing (an exception mid-blob) rolls back, leaving whatever
/// a previous run exported for the blob untouched.
/// </summary>
public sealed class BlobImport : IDisposable {
	private readonly SqliteConnection _connection;
	private readonly SqliteTransaction _transaction;
	private readonly SqliteCommand _insertEvent;
	private readonly string _container;
	private readonly LogFileInfo _file;
	private readonly BlobPathSegments _segments;
	private readonly long _blobId;
	private bool _completed;

	internal BlobImport(SqliteConnection connection, string container, LogFileInfo file, BlobPathSegments segments) {
		_connection = connection;
		_container = container;
		_file = file;
		_segments = segments;
		_transaction = connection.BeginTransaction();

		try {
			_blobId = UpsertBlob(0, 0, BlobStatus.Importing);

			using (SqliteCommand delete = CreateCommand("DELETE FROM events WHERE blob_id = $blobId;")) {
				delete.Parameters.AddWithValue("$blobId", _blobId);
				delete.ExecuteNonQuery();
			}

			_insertEvent = CreateCommand("""
                INSERT INTO events (blob_id, service, timestamp, level, category, message, span_id, trace_id,
                                    request_id, action_name, event_type,
                                    http_method, http_path, status_code, duration_ms, source_blob,
                                    source_line_number, imported_at, properties, route, user_id, exception)
                VALUES ($blobId, $service, $timestamp, $level, $category, $message, $spanId, $traceId,
                        $requestId, $actionName, $eventType,
                        $httpMethod, $httpPath, $statusCode, $durationMs, $sourceBlob,
                        $sourceLineNumber, $importedAt, $properties, $route, $userId, $exception);
                """);

			foreach (string name in (string[])[
					"$blobId", "$service", "$timestamp", "$level", "$category", "$message", "$spanId", "$traceId",
								"$requestId", "$actionName", "$eventType",
								"$httpMethod", "$httpPath", "$statusCode", "$durationMs", "$sourceBlob",
								"$sourceLineNumber", "$importedAt", "$properties", "$route", "$userId", "$exception"]) {
				_insertEvent.Parameters.Add(new SqliteParameter { ParameterName = name });
			}

			_insertEvent.Parameters["$blobId"].Value = _blobId;
			_insertEvent.Parameters["$service"].Value = segments.Service;
			_insertEvent.Prepare();
		} catch {
			_transaction.Dispose();
			throw;
		}
	}

	public void Add(LogEvent logEvent) {
		SqliteParameterCollection p = _insertEvent.Parameters;
		p["$timestamp"].Value = LogDatabase.FormatTimestamp(logEvent.Timestamp);
		p["$level"].Value = DbValue(logEvent.Level);
		p["$category"].Value = DbValue(logEvent.Category);
		p["$message"].Value = DbValue(logEvent.Message);
		p["$spanId"].Value = DbValue(logEvent.SpanId);
		p["$traceId"].Value = DbValue(logEvent.TraceId);
		p["$requestId"].Value = DbValue(logEvent.RequestId);
		p["$actionName"].Value = DbValue(logEvent.ActionName);
		p["$eventType"].Value = DbValue(logEvent.EventType);
		p["$httpMethod"].Value = DbValue(logEvent.HttpMethod);
		p["$httpPath"].Value = DbValue(logEvent.HttpPath);
		p["$statusCode"].Value = DbValue(logEvent.StatusCode);
		p["$durationMs"].Value = DbValue(logEvent.DurationMs);
		p["$sourceBlob"].Value = DbValue(logEvent.SourceBlob);
		p["$sourceLineNumber"].Value = logEvent.SourceLineNumber;
		p["$importedAt"].Value = LogDatabase.FormatTimestamp(logEvent.ImportedAt);
		p["$properties"].Value = logEvent.Properties.Count > 0 ? JsonSerializer.Serialize(logEvent.Properties) : DBNull.Value;
		p["$route"].Value = DbValue(logEvent.Route);
		p["$userId"].Value = DbValue(logEvent.UserId);
		p["$exception"].Value = DbValue(logEvent.Exception);

		_insertEvent.ExecuteNonQuery();
	}

	public void Complete(long eventCount, long unparsedCount) {
		UpsertBlob(eventCount, unparsedCount, BlobStatus.Success);
		_transaction.Commit();
		_completed = true;
	}

	public void Dispose() {
		_insertEvent?.Dispose();

		if (!_completed) {
			_transaction.Rollback();
		}

		_transaction.Dispose();
	}

	private long UpsertBlob(long eventCount, long unparsedCount, string status) {
		using SqliteCommand command = CreateCommand(LogDatabase.UpsertBlobSql);
		LogDatabase.AddBlobParameters(command, _container, _file, _segments, eventCount, unparsedCount, status, null);
		return (long)command.ExecuteScalar()!;
	}

	private SqliteCommand CreateCommand(string sql) {
		SqliteCommand command = _connection.CreateCommand();
		command.Transaction = _transaction;
		command.CommandText = sql;
		return command;
	}

	private static object DbValue<T>(T? value) => value is null ? DBNull.Value : value;
}
