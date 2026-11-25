using System.Text.Json;

namespace TokenHud.Core;

public static class ApiUsageImport
{
    public const int MaximumInputBytes = ProviderTelemetryStore.MaximumInputBytes;
    private static readonly JsonSerializerOptions NormalizedOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ProviderImportResult> ImportAsync(
        string sourceId,
        Stream input,
        ProviderTelemetryStore store,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (sourceId is not ("openai-api" or "gemini-api"))
        {
            return ProviderImportResult.Invalid;
        }

        var bytes = new byte[MaximumInputBytes + 1];
        try
        {
            var length = await input.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken).ConfigureAwait(false);
            if (length == 0 || length > MaximumInputBytes)
            {
                return ProviderImportResult.Invalid;
            }

            var raw = bytes.AsMemory(0, length);
            if (raw.Span.StartsWith("\uFEFF"u8))
            {
                raw = raw[3..];
            }

            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 });
            var wrapper = Object(document.RootElement);
            var sessionId = Property(wrapper, "sessionId");
            if (sessionId.ValueKind != JsonValueKind.String)
            {
                return ProviderImportResult.Invalid;
            }

            var observedAt = ReadObservedAt(wrapper) ?? now;
            var exchange = sourceId == "openai-api"
                ? OpenAiExchange(Object(Property(wrapper, "usage")))
                : GeminiExchange(Object(Property(wrapper, "usageMetadata")));

            // Forward only the wrapper identity, timestamp and explicitly projected request counters.
            var normalized = JsonSerializer.SerializeToUtf8Bytes(new
            {
                version = ProviderTelemetryStore.SchemaVersion,
                sourceId,
                sessionId = sessionId.GetString(),
                observedAt,
                latestExchange = exchange,
            }, NormalizedOptions);
            using var projected = new MemoryStream(normalized, writable: false);
            return await store.ImportAsync(projected, now, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return ProviderImportResult.Invalid;
        }
        catch (IOException)
        {
            return ProviderImportResult.Unavailable;
        }
    }

    private static DateTimeOffset? ReadObservedAt(JsonElement wrapper)
    {
        var value = Property(wrapper, "observedAt");
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var hasOffset = text is not null &&
            (text.EndsWith('Z') || (text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':'));
        return hasOffset && value.TryGetDateTimeOffset(out var parsed) ? parsed : throw new JsonException();
    }

    private static ProviderExchange OpenAiExchange(JsonElement usage) => new(
        Tokens(usage, "input_tokens"),
        Tokens(usage, "output_tokens"),
        Tokens(Object(Property(usage, "input_tokens_details")), "cached_tokens"),
        Tokens(Object(Property(usage, "output_tokens_details")), "reasoning_tokens"));

    private static ProviderExchange GeminiExchange(JsonElement usage) => new(
        Tokens(usage, "promptTokenCount"),
        Tokens(usage, "candidatesTokenCount"),
        Tokens(usage, "cachedContentTokenCount"),
        Tokens(usage, "thoughtsTokenCount"));

    private static long? Tokens(JsonElement node, string name) => Property(node, name) switch
    {
        { ValueKind: JsonValueKind.Undefined or JsonValueKind.Null } => null,
        { ValueKind: JsonValueKind.Number } value when value.TryGetInt64(out var tokens) => tokens,
        _ => throw new JsonException(),
    };

    private static JsonElement Object(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined => element,
        _ => throw new JsonException(),
    };

    private static JsonElement Property(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) ? value : default;
}
