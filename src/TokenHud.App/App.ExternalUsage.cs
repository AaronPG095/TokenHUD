using System.IO;
using TokenHud.Core;

namespace TokenHud.App;

public partial class App
{
    private static bool TryHandleUsageCommand(string[] arguments, out int exitCode)
    {
        exitCode = 0;
        var readsApi = arguments.Length == 2 && arguments[0].Equals("--ingest-usage", StringComparison.OrdinalIgnoreCase);
        var readsCursorHook = arguments.Length == 1 && arguments[0].Equals("--ingest-cursor-hook", StringComparison.OrdinalIgnoreCase);
        var readsCursorFile = arguments.Length == 2 && arguments[0].Equals("--import-cursor-usage", StringComparison.OrdinalIgnoreCase);
        if (!readsApi && !readsCursorHook && !readsCursorFile)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var store = new ProviderTelemetryStore(new AppDataPaths().ProviderTelemetry);
        ProviderImportResult result;
        try
        {
            if (readsCursorFile)
            {
                using var file = File.OpenRead(arguments[1]);
                result = CursorTelemetryImport.ImportUsageAsync(file, store, now).GetAwaiter().GetResult();
            }
            else
            {
                using var input = Console.OpenStandardInput();
                result = readsCursorHook
                    ? CursorTelemetryImport.ImportPreCompactAsync(input, store, now).GetAwaiter().GetResult()
                    : ApiUsageImport.ImportAsync(arguments[1], input, store, now).GetAwaiter().GetResult();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            result = ProviderImportResult.Unavailable;
        }

        if (!readsCursorHook)
        {
            Console.Out.WriteLine(result switch
            {
                ProviderImportResult.Imported => "Token HUD recorded provider usage.",
                ProviderImportResult.Invalid => "Token HUD did not recognize a supported usage record.",
                _ => "Token HUD could not read or store provider usage.",
            });
        }

        exitCode = result switch
        {
            ProviderImportResult.Imported => 0,
            ProviderImportResult.Invalid => 2,
            _ => 1,
        };
        return true;
    }
}