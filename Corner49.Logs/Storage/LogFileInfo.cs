namespace Corner49.Logs.Storage;

public sealed record LogFileInfo(string Name, long Size, DateTimeOffset? LastModified, string? ETag);
