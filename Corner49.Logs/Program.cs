using Corner49.Logs.CLI;
using System.CommandLine;

RootCommand root = RootCommandFactory.Build();

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) => {
	e.Cancel = true;
	cts.Cancel();
};

ParseResult parseResult = root.Parse(args);

try {
	return await parseResult.InvokeAsync(new InvocationConfiguration(), cts.Token);
} catch (OperationCanceledException) {
	Console.Error.WriteLine("Export cancelled by user.");
	return 130;
}
