using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenHud.Core;

public sealed record VendorStatusLineResult(ProviderImportResult ImportResult, string Text);

public static class VendorStatusLine
{
    public const int MaximumInputBytes = 64 * 1024;
    private const long MaximumTokens = 1_000_000_000_000;
    private static readonly JsonSerializerOptions NormalizedOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<VendorStatusLineResult> ImportAsync(
        string sourceId,
        Stream input,
        ProviderTelemetryStore store,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var label = sourceId switch
        {
            "claude-code" => "Claude Code",
            "antigravity" => "Gemini (Antigravity CLI)",
            _ => null,
        };
        if (label is null)
        {
            return new(ProviderImportResult.Invalid, "Token HUD | Unsupported status line");
        }

        var bytes = new byte[MaximumInputBytes + 1];
        try
        {
            var length = await input.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken).ConfigureAwait(false);
            if (length == 0 || length > MaximumInputBytes)
            {
                return new(ProviderImportResult.Invalid, $"{label} | Status unavailable");
            }

            var raw = bytes.AsMemory(0, length);
            if (raw.Span.StartsWith("\uFEFF"u8))
            {
                raw = raw[3..];
            }

            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 24 });
            var payload = document.RootElement;
            var sessionId = sourceId == "antigravity"
                ? ReadSessionId(payload, "conversation_id") ?? ReadSessionId(payload, "session_id")
                : ReadSessionId(payload, "session_id");
            if (sessionId is null)
            {
                return new(ProviderImportResult.Invalid, $"{label} | Context unavailable");
            }

            var window = Property(payload, "context_window");
            var usage = Property(window, "current_usage");
            var inputTokens = Tokens(usage, "input_tokens");
            var outputTokens = Tokens(usage, "output_tokens");
            var cacheRead = Tokens(usage, "cache_read_input_tokens");
            var cacheCreation = Tokens(usage, "cache_creation_input_tokens");
            var capacity = Tokens(window, "context_window_size") is > 0 and var size ? size : (long?)null;
            var percent = Percentage(window, "used_percentage");

            long? occupied = null;
            IReadOnlyList<ProviderQuota> quotas;
            if (sourceId == "claude-code")
            {
                if (inputTokens == 0 && outputTokens == 0 && cacheRead == 0 && cacheCreation == 0)
                {
                    // Claude Code reports an all-zero usage object during compaction.
                    inputTokens = outputTokens = cacheRead = cacheCreation = null;
                    if (percent == 0)
                    {
                        percent = null;
                    }
                }

                if (inputTokens is { } actualInput && cacheRead is { } actualRead && cacheCreation is { } actualWrite)
                {
                    var combined = actualInput + actualRead + actualWrite;
                    occupied = combined <= MaximumTokens ? combined : null;
                }

                quotas = ClaudeQuotas(Property(payload, "rate_limits"), now);
            }
            else
            {
                // Google's percentage and capacity are independent of the latest request counters.
                quotas = AntigravityQuotas(Property(payload, "quota"), now);
            }

            var current = occupied is null && capacity is null && percent is null
                ? null
                : new ProviderContext(occupied, capacity, percent);
            var latest = inputTokens is null && outputTokens is null && cacheRead is null
                ? null
                : new ProviderExchange(inputTokens, outputTokens, cacheRead, ReasoningOutputTokens: null);
            if (current is null && latest is null && quotas.Count == 0)
            {
                return new(ProviderImportResult.Invalid, $"{label} | Context unavailable");
            }

            var normalized = JsonSerializer.SerializeToUtf8Bytes(new
            {
                version = ProviderTelemetryStore.SchemaVersion,
                sourceId,
                sessionId,
                observedAt = now,
                currentContext = current,
                latestExchange = latest,
                accountQuotas = quotas,
            }, NormalizedOptions);
            using var projected = new MemoryStream(normalized, writable: false);
            var result = await store.ImportAsync(projected, now, cancellationToken).ConfigureAwait(false);
            var summary = percent is { } used
                ? FormattableString.Invariant($"Context {used:0.##}%")
                : occupied is { } measured
                    ? FormattableString.Invariant($"Context {measured:N0} tokens")
                    : "Context unavailable";
            var suffix = result == ProviderImportResult.Imported ? string.Empty : " | HUD feed unavailable";
            return new(result, $"{label} | {summary}{suffix}");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new(ProviderImportResult.Invalid, $"{label} | Status unavailable");
        }
        catch (IOException)
        {
            return new(ProviderImportResult.Unavailable, $"{label} | Status unavailable");
        }
    }

    private static IReadOnlyList<ProviderQuota> ClaudeQuotas(JsonElement windows, DateTimeOffset now)
    {
        var quotas = new List<ProviderQuota>(2);
        AddClaudeQuota(quotas, Property(windows, "five_hour"), "five-hour", now);
        AddClaudeQuota(quotas, Property(windows, "seven_day"), "weekly", now);
        return quotas;
    }

    private static void AddClaudeQuota(List<ProviderQuota> quotas, JsonElement window, string kind, DateTimeOffset now)
    {
        if (Percentage(window, "used_percentage") is not { } used)
        {
            return;
        }

        var resetValue = Property(window, "resets_at");
        DateTimeOffset? reset = null;
        if (resetValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            if (resetValue.ValueKind != JsonValueKind.Number ||
                !resetValue.TryGetInt64(out var seconds) ||
                seconds is <= 0 or > 253_402_300_799)
            {
                return;
            }

            reset = DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (reset <= now)
            {
                return;
            }
        }

        quotas.Add(new ProviderQuota(kind, 100 - used, null, null, reset));
    }

    private static IReadOnlyList<ProviderQuota> AntigravityQuotas(JsonElement windows, DateTimeOffset now)
    {
        var weekly = Property(windows, "gemini-weekly");
        if (Property(weekly, "disabled").ValueKind == JsonValueKind.True ||
            DecimalInRange(weekly, "remaining_fraction", 1) is not { } fraction)
        {
            return [];
        }

        var resetValue = Property(weekly, "reset_time");
        DateTimeOffset? reset = null;
        if (resetValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            var text = resetValue.ValueKind == JsonValueKind.String ? resetValue.GetString() : null;
            if (text is null ||
                !(text.EndsWith('Z') || (text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':')) ||
                !resetValue.TryGetDateTimeOffset(out var timestamp) ||
                timestamp <= now)
            {
                return [];
            }

            reset = timestamp;
        }
        else if (Property(weekly, "reset_in_seconds") is { ValueKind: JsonValueKind.Number } duration &&
            duration.TryGetInt64(out var seconds))
        {
            if (seconds is <= 0 or > 604_800 || now > DateTimeOffset.MaxValue.AddSeconds(-seconds))
            {
                return [];
            }

            reset = now.ToUniversalTime().AddSeconds(seconds);
        }

        return [new ProviderQuota("weekly", fraction * 100, null, null, reset)];
    }

    private static long? Tokens(JsonElement node, string name) =>
        Property(node, name) is { ValueKind: JsonValueKind.Number } value &&
        value.TryGetInt64(out var tokens) && tokens is >= 0 and <= MaximumTokens ? tokens : null;

    private static decimal? Percentage(JsonElement node, string name) => DecimalInRange(node, name, 100);

    private static decimal? DecimalInRange(JsonElement node, string name, decimal maximum) =>
        Property(node, name) is { ValueKind: JsonValueKind.Number } value &&
        value.TryGetDecimal(out var amount) && amount >= 0 && amount <= maximum ? amount : null;

    private static string? ReadSessionId(JsonElement payload, string name) =>
        Property(payload, name) is { ValueKind: JsonValueKind.String } value &&
        value.GetString() is { Length: > 0 and <= 512 } text &&
        !string.IsNullOrWhiteSpace(text) && !text.Any(char.IsControl) ? text : null;

    private static JsonElement Property(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) ? value : default;
}
