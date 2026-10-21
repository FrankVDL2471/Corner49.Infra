namespace Corner49.Logs.Export;

/// <summary>
/// The data directory (--data-dir or the data_dir setting), already resolved to an absolute path
/// and created on disk (see Infrastructure.OutputDirectoryInitializer). Registered once in
/// CLI.LogsHost and injected wherever a component needs to read/write under it, so nothing
/// re-resolves the setting independently.
/// </summary>
public sealed record ResolvedOutputDirectory(string Path);
