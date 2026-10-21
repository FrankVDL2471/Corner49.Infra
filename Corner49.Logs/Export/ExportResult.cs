namespace Corner49.Logs.Export;

public sealed record ExportResult(
		int FilesFound,
		int FilesImported,
		int FilesSkipped,
		int FilesFailed,
		long EventsImported,
		long LinesParsed,
		long LinesUnparsed,
		int DaysAlreadyComplete);
