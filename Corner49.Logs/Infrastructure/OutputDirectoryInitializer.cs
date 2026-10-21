namespace Corner49.Logs.Infrastructure;

/// <summary>
/// Resolves the configured output directory relative to the app's base directory (not the
/// process's current working directory, which varies depending on how the CLI is launched),
/// and makes sure it exists before anything tries to write into it.
/// </summary>
public static class OutputDirectoryInitializer {
	public static string EnsureOutputDirectoryExists(string outputDirectory) {
		string resolvedPath = Path.IsPathRooted(outputDirectory)
				? outputDirectory
				: Path.Combine(AppContext.BaseDirectory, outputDirectory);

		Directory.CreateDirectory(resolvedPath);

		return resolvedPath;
	}
}
