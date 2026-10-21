using Corner49.Logs.Domain;

namespace Corner49.Logs.Parsing;

public interface ILogParser {
	bool CanParse(string line);

	LogEvent? Parse(string line, string sourceBlob, long lineNumber);
}
