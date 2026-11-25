using System.Globalization;
using System.Text.Json;

namespace TokenHud.Core;

public static class CursorTelemetryImport
{
    public const int MaximumUsageInputBytes = 1024 * 1024;
    private const long MaximumTokens = 1_000_000_000_000;
    private const int MaximumUsageReadings = 20;

    public static async Task<ProviderImportResult> ImportPreCompactAsync(
        Stream input,
        ProviderTelemetryStore store,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await ReadAsync(input, ProviderTelemetryStore.MaximumInputBytes, cancellationToken)
                .ConfigureAwait(false);
            if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root ||
                (ReadString(root, "hook_event_name") is { } eventName && eventName != "preCompact") ||
                (ReadString(root, "conversation_id") ?? ReadString(root, "session_id")) is not { } id ||
                !IsValidId(id))
            {
                return ProviderImportResult.Invalid;
            }

            var context = new ProviderContext(
                ReadTokens(root, "context_tokens"),
                ReadTokens(root, "context_window_size"),
                ReadPercent(root, "context_usage_percent"));
            return await StoreAsync(store, "pre-compact:" + id, now, now, context, null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            return ProviderImportResult.Invalid;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ProviderImportResult.Unavailable;
        }
    }

    public static async Task<ProviderImportResult> ImportUsageAsync(
        Stream input,
        ProviderTelemetryStore store,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await ReadAsync(input, MaximumUsageInputBytes, cancellationToken).ConfigureAwait(false);
            if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root ||
                !root.TryGetProperty("usageEvents", out var events) || events.ValueKind != JsonValueKind.Array)
            {
                return ProviderImportResult.Invalid;
            }

            var latest = new Dictionary<string, UsageReading>(StringComparer.Ordinal);
            foreach (var row in events.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ParseUsage(row, now) is { } reading &&
                    (!latest.TryGetValue(reading.Id, out var previous) || reading.ObservedAt > previous.ObservedAt))
                {
                    latest[reading.Id] = reading;
                }
            }

            var result = ProviderImportResult.Invalid;
            foreach (var reading in latest.Values.OrderByDescending(reading => reading.ObservedAt)
                .ThenBy(reading => reading.Id, StringComparer.Ordinal).Take(MaximumUsageReadings))
            {
                result = await StoreAsync(store, reading.Id, reading.ObservedAt, now, null, reading.Exchange,
                    cancellationToken).ConfigureAwait(false);
                if (result != ProviderImportResult.Imported)
                {
                    return result;
                }
            }
            return result;
        }
        catch (JsonException)
        {
            return ProviderImportResult.Invalid;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ProviderImportResult.Unavailable;
        }
    }

    private static UsageReading? ParseUsage(JsonElement row, DateTimeOffset now)
    {
        try
        {
            if (row.ValueKind != JsonValueKind.Object ||
                (ReadString(row, "product") is { } product && product != "cursor") ||
                !row.TryGetProperty("tokenUsage", out var usage) || usage.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("timestamp", out var timestamp) || ReadMilliseconds(timestamp) is not { } milliseconds ||
                milliseconds < 0 || milliseconds > now.ToUnixTimeMilliseconds() + 60_000)
            {
                return null;
            }

            var input = ReadTokens(usage, "inputTokens");
            var output = ReadTokens(usage, "outputTokens");
            var cached = ReadTokens(usage, "cacheReadTokens");
            if (input is null && output is null && cached is null)
            {
                return null;
            }

            var conversation = ReadString(row, "conversationId");
            if (conversation is not null && !IsValidId(conversation))
            {
                return null;
            }

            var id = conversation is not null ? "usage-conversation:" + conversation :
                FormattableString.Invariant($"usage-request:{milliseconds}:{input}:{output}:{cached}");
            return new UsageReading(id, DateTimeOffset.FromUnixTimeMilliseconds(milliseconds),
                new ProviderExchange(input, output, cached, null));
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<ProviderImportResult> StoreAsync(
        ProviderTelemetryStore store,
        string id,
        DateTimeOffset observed,
        DateTimeOffset now,
        ProviderContext? context,
        ProviderExchange? exchange,
        CancellationToken cancellationToken)
    {
        using var projected = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = ProviderTelemetryStore.SchemaVersion,
            sourceId = "cursor",
            sessionId = id,
            observedAt = observed,
            historicalContext = context is null ? null : new { tokens = context.Tokens,
                capacityTokens = context.CapacityTokens, usedPercent = context.UsedPercent },
            latestExchange = exchange is null ? null : new { inputTokens = exchange.InputTokens,
                outputTokens = exchange.OutputTokens, cachedInputTokens = exchange.CachedInputTokens },
        }));
        return await store.ImportAsync(projected, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonDocument?> ReadAsync(Stream input, int limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[limit + 1];
        var count = await input.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken).ConfigureAwait(false);
        if (count == 0 || count > limit)
        {
            return null;
        }

        var data = buffer.AsMemory(0, count);
        if (data.Span.StartsWith("\uFEFF"u8))
        {
            data = data[3..];
        }
        return JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 12 });
    }

    private static bool IsValidId(string value) =>
        value.Length is > 0 and <= 480 && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    private static string? ReadString(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null ? null :
        field.ValueKind == JsonValueKind.String ? field.GetString() : throw new FormatException();

    private static long? ReadTokens(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null ? null :
        field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out var value) &&
        value is >= 0 and <= MaximumTokens ? value : throw new FormatException();

    private static decimal? ReadPercent(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null ? null :
        field.ValueKind == JsonValueKind.Number && field.TryGetDecimal(out var value) &&
        value is >= 0 and <= 100 ? value : throw new FormatException();

    private static long? ReadMilliseconds(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetInt64(out var milliseconds) => milliseconds,
        JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture,
            out var milliseconds) => milliseconds,
        _ => null,
    };

    private sealed record UsageReading(string Id, DateTimeOffset ObservedAt, ProviderExchange Exchange);
}
