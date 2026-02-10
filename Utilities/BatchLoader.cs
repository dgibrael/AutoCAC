
namespace AutoCAC.Utilities;
public static class BatchLoader
{
    // Cancels/disposes old CTS and returns a new one.
    public static CancellationTokenSource ResetCts(ref CancellationTokenSource cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        return cts;
    }

    // Runs a set of loaders concurrently; cancellation is not treated as an error.
    public static async Task RunAllAsync(
        CancellationToken token,
        params Func<CancellationToken, Task>[] loaders)
    {
        var tasks = new Task[loaders.Length];

        for (int i = 0; i < loaders.Length; i++)
            tasks[i] = Safe(loaders[i], token);

        await Task.WhenAll(tasks);
    }

    private static async Task Safe(Func<CancellationToken, Task> loader, CancellationToken token)
    {
        try
        {
            await loader(token);
        }
        catch (OperationCanceledException)
        {
            // expected during refresh
        }
        catch
        {
            // swallow: panel loaders should set their own error state
        }
    }
}
