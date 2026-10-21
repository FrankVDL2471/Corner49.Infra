namespace Corner49.Logs.Domain;

public class LogEvent {
	public DateTimeOffset Timestamp { get; set; }

	public string? Level { get; set; }

	public string? Category { get; set; }

	public string? Message { get; set; }


	public string? SpanId { get; set; }

	public string? TraceId { get; set; }

	public string? RequestId { get; set; }

	public string? ActionName { get; set; }


	//public string? Service { get; set; }

	//public string? Environment { get; set; }


	public string? EventType { get; set; }


	public string? HttpMethod { get; set; }

	public string? HttpPath { get; set; }

	public int? StatusCode { get; set; }


	/// <summary>Route template of the request (e.g. api/orders/{id}), from the request scope or state.</summary>
	public string? Route { get; set; }

	/// <summary>Authenticated user id, from the request scope.</summary>
	public string? UserId { get; set; }

	/// <summary>Full exception text (type, message, stack trace) from the console formatter's Exception field.</summary>
	public string? Exception { get; set; }



	public long? DurationMs { get; set; }

	public string? SourceBlob { get; set; }

	public long SourceLineNumber { get; set; }

	//public string? RawMessage { get; set; }

	public DateTimeOffset ImportedAt { get; set; }

	public Dictionary<string, string> Properties { get; set; } = [];
}
