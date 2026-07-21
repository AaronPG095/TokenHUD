using System.Text.Json;

namespace TokenHud.Core;

public sealed class RolloutReader
{
    private const int DefaultTailBytes = 4 * 1024 * 1024;

    public async Task<IReadOnlyList<UsageSnapshot>> ReadRecentUsageAsync(
        string path,
        int maxEvents = 64,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        var events = new Queue<UsageSnapshot>(maxEvents);
        UsageSnapshot? previous = null;
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var start = Math.Max(0, stream.Length - DefaultTailBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: true);

        if (start > 0)
        {
            _ = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!line.Contains("\"type\":\"token_count\"", StringComparison.Ordinal))
            {
                continue;
            }

            var snapshot = TryParseUsage(line);
            if (snapshot is null)
            {
                continue;
            }

            if (snapshot.SessionTotal.TotalTokens == 0 && snapshot.Context.TotalTokens == 0 && previous is not null)
            {
                snapshot = snapshot with
                {
                    SessionTotal = previous.SessionTotal,
                    Context = previous.Context,
                    ModelContextWindow = previous.ModelContextWindow,
                };
            }

            if (events.Count == maxEvents)
            {
                _ = events.Dequeue();
            }

            events.Enqueue(snapshot);
            previous = snapshot;
        }

        return events.ToArray();
    }

    public UsageSnapshot? TryParseUsage(string jsonLine)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;
            if (!TryGetString(root, "type", out var eventType) || eventType != "event_msg")
            {
                return null;
            }

            if (!root.TryGetProperty("payload", out var payload) ||
                !TryGetString(payload, "type", out var payloadType) ||
                payloadType != "token_count")
            {
                return null;
            }

            var timestamp = TryGetTimestamp(root, "timestamp") ?? DateTimeOffset.UtcNow;
            var total = TokenBreakdown.Empty;
            var context = TokenBreakdown.Empty;
            long? window = null;

            if (payload.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
            {
                if (info.TryGetProperty("total_token_usage", out var totalElement))
                {
                    total = ParseBreakdown(totalElement);
                }

                if (info.TryGetProperty("last_token_usage", out var contextElement))
                {
                    context = ParseBreakdown(contextElement);
                }

                if (info.TryGetProperty("model_context_window", out var windowElement) && windowElement.TryGetInt64(out var parsedWindow))
                {
                    window = parsedWindow;
                }
            }

            var quota = payload.TryGetProperty("rate_limits", out var limits) && limits.ValueKind == JsonValueKind.Object
                ? ParseQuota(limits)
                : null;

            if (total.TotalTokens == 0 && context.TotalTokens == 0 && quota is null)
            {
                return null;
            }

            return new UsageSnapshot(timestamp, total, context, window, quota);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TokenBreakdown ParseBreakdown(JsonElement element) => new(
        GetInt64(element, "input_tokens"),
        GetInt64(element, "cached_input_tokens"),
        GetInt64(element, "cache_write_input_tokens"),
        GetInt64(element, "output_tokens"),
        GetInt64(element, "reasoning_output_tokens"),
        GetInt64(element, "total_tokens"));

    private static AccountQuota ParseQuota(JsonElement limits)
    {
        var primary = limits.TryGetProperty("primary", out var primaryElement)
            ? ParseWindow(primaryElement)
            : null;
        var secondary = limits.TryGetProperty("secondary", out var secondaryElement)
            ? ParseWindow(secondaryElement)
            : null;

        return new AccountQuota(
            primary,
            secondary,
            GetString(limits, "plan_type"),
            GetBoolean(limits, "spend_control_reached"),
            GetString(limits, "rate_limit_reached_type"));
    }

    private static RateLimitWindow? ParseWindow(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !TryGetDouble(element, "used_percent", out var used))
        {
            return null;
        }

        int? minutes = null;
        if (element.TryGetProperty("window_minutes", out var minutesElement) && minutesElement.TryGetInt32(out var parsedMinutes))
        {
            minutes = parsedMinutes;
        }

        DateTimeOffset? reset = null;
        if (element.TryGetProperty("resets_at", out var resetElement))
        {
            if (resetElement.ValueKind == JsonValueKind.Number && resetElement.TryGetInt64(out var unix))
            {
                reset = unix > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                    : DateTimeOffset.FromUnixTimeSeconds(unix);
            }
            else if (resetElement.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(resetElement.GetString(), out var parsed))
            {
                reset = parsed;
            }
        }

        return new RateLimitWindow(used, minutes, reset);
    }

    private static long GetInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var parsed) ? parsed : 0;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = GetString(element, name);
        return value is not null;
    }

    private static bool TryGetDouble(JsonElement element, string name, out double value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.TryGetDouble(out value);
    }

    private static DateTimeOffset? TryGetTimestamp(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}
