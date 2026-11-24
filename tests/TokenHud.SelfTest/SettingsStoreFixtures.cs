using TokenHud.Core;

internal static class SettingsStoreFixtures
{
    public static async Task VerifyInterruptedAndOverlappingSavesAsync()
    {
        var directory = Directory.CreateTempSubdirectory("TokenHUD-settings-");
        try
        {
            var paths = new AppDataPaths(directory.FullName);
            var store = new SettingsStore(paths);
            var oldTemporary = paths.Settings + ".tmp";

            // An interrupted legacy writer can leave this file behind with an open handle.
            await using (var legacyWriter = new FileStream(oldTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var first = store.SaveAsync(new HudSettings { ContextBudget = 120_000 });
                var second = store.SaveAsync(new HudSettings { ContextBudget = 130_000 });
                await Task.WhenAll(first, second);
            }

            var saved = await store.LoadAsync();
            Check(saved.ContextBudget == 130_000, "Overlapping saves must leave the most recent settings readable.");
            Check(Directory.EnumerateFiles(paths.Root, "settings.json.*.tmp").Count() == 0, "A completed save left a temporary file.");

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                await store.SaveAsync(new HudSettings { ContextBudget = 140_000 }, cancelled.Token);
                throw new InvalidOperationException("A cancelled save completed.");
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
            {
            }

            Check((await store.LoadAsync()).ContextBudget == 130_000, "A cancelled save changed the last committed settings.");
            await File.WriteAllTextAsync(paths.Settings, "{\"AlertStates\":null}");
            Check((await store.LoadAsync()).AlertStates.Count == 0, "Older settings with a null alert dictionary must load safely.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
