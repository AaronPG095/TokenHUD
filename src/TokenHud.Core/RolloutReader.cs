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

            if (!snapshot.HasSessionTotal && !snapshot.HasContext && snapshot.ModelContextWindow is null && previous is not null)
            {
                snapshot = snapshot with
                {
                    SessionTotal = previous.SessionTotal,
                    Context = previous.Context,
                    ModelContextWindow = previous.ModelContextWindow,
                    HasContext = previous.HasContext,
                    HasSessionTotal = previous.HasSessionTotal,
                    ContextObservedAt = previous.ContextObservedAt,
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
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetString(root, "type", out var eventType) || eventType != "event_msg")
            {
                return null;
            }

            if (!root.TryGetProperty("payload", out var payload) ||
                payload.ValueKind != JsonValueKind.Object ||
                !TryGetString(payload, "type", out var payloadType) ||
                payloadType != "token_count")
            {
                return null;
            }

            var timestamp = TryGetTimestamp(root, "timestamp") ?? DateTimeOffset.UtcNow;
            TokenBreakdown? total = null;
            TokenBreakdown? context = null;
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

                if (info.TryGetProperty("model_context_window", out var windowElement) && windowElement.ValueKind == JsonValueKind.Number && windowElement.TryGetInt64(out var parsedWindow))
                {
                    window = parsedWindow;
                }
            }

            var quota = payload.TryGetProperty("rate_limits", out var limits) && limits.ValueKind == JsonValueKind.Object
                ? ParseQuota(limits)
                : null;

            if (total is null && context is null && window is null && quota is null)
            {
                return null;
            }

            return new UsageSnapshot(
                timestamp,
                total ?? TokenBreakdown.Empty,
                context ?? TokenBreakdown.Empty,
                window,
                quota,
                HasContext: context is not null,
                HasSessionTotal: total is not null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TokenBreakdown? ParseBreakdown(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("total_tokens", out var total) ||
            total.ValueKind != JsonValueKind.Number ||
            !total.TryGetInt64(out var totalTokens) ||
            totalTokens < 0)
        {
            return null;
        }

        var fields = TokenFields.Total;
        var input = ReadField("input_tokens", TokenFields.Input);
        var cached = ReadField("cached_input_tokens", TokenFields.CachedInput);
        var write = ReadField("cache_write_input_tokens", TokenFields.CacheWrite);
        var output = ReadField("output_tokens", TokenFields.Output);
        var reasoning = ReadField("reasoning_output_tokens", TokenFields.Reasoning);
        return new TokenBreakdown(input, cached, write, output, reasoning, totalTokens) { ReportedFields = fields };

        long ReadField(string name, TokenFields field)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out var tokens) && tokens >= 0)
            {
                fields |= field;
                return tokens;
            }

            return 0;
        }
    }

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
        if (element.TryGetProperty("window_minutes", out var minutesElement) &&
            minutesElement.ValueKind == JsonValueKind.Number && minutesElement.TryGetInt32(out var parsedMinutes))
        {
            minutes = parsedMinutes;
        }

        DateTimeOffset? reset = null;
        if (element.TryGetProperty("resets_at", out var resetElement))
        {
            reset = ParseReset(resetElement);
            if (reset is null)
            {
                return null;
            }
        }

        return new RateLimitWindow(used, minutes, reset);
    }

    private static DateTimeOffset? ParseReset(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var unix))
        {
            return null;
        }

        try
        {
            return unix > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                : DateTimeOffset.FromUnixTimeSeconds(unix);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

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
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) && double.IsFinite(value);
    }

    private static DateTimeOffset? TryGetTimestamp(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}
