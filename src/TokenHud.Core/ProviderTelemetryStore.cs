using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenHud.Core;

public sealed class ProviderTelemetryStore
{
    public const int SchemaVersion = 1;
    public const int MaximumInputBytes = 16 * 1024;
    private const int MaximumReadings = 100;
    private const long MaximumTokens = 1_000_000_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<string> SourceIds { get; } = Array.AsReadOnly(
        ["claude-code", "antigravity", "cursor", "openai-api", "gemini-api"]);

    private readonly string _directory;

    public ProviderTelemetryStore(string directory)
    {
        _directory = directory;
    }

    public async Task<ProviderImportResult> ImportAsync(
        Stream input,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var reading = await ReadAsync(input, now, persisted: false, cancellationToken).ConfigureAwait(false);
        if (reading is null)
        {
            return ProviderImportResult.Invalid;
        }

        try
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, reading.SessionKey + ".json");
            // Keep the comparison and replace in one critical section across ingest processes.
            using var writeGate = await AcquireWriteGateAsync(cancellationToken).ConfigureAwait(false);
            var previous = await ReadFileAsync(path, now, cancellationToken).ConfigureAwait(false);
            if (previous is not null && previous.ObservedAt >= reading.ObservedAt)
            {
                return ProviderImportResult.Imported;
            }

            var temporary = Path.Combine(_directory, $".{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(output, StoredReading.From(reading), JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The next reading remains independent of this temporary file.
                }
            }

            return ProviderImportResult.Imported;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ProviderImportResult.Unavailable;
        }
    }

    public async Task<IReadOnlyList<ProviderReading>> LoadAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var recent = new List<ProviderReading>();
        if (!Directory.Exists(_directory))
        {
            return recent;
        }

        try
        {
            var candidates = new DirectoryInfo(_directory).EnumerateFiles("*.json")
                .Where(file => IsSessionKey(Path.GetFileNameWithoutExtension(file.Name)))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenBy(file => file.Name, StringComparer.Ordinal)
                .Take(MaximumReadings);
            foreach (var file in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var reading = await ReadFileAsync(file.FullName, now, cancellationToken).ConfigureAwait(false);
                if (reading is not null && file.Name == reading.SessionKey + ".json")
                {
                    recent.Add(reading);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Return usable readings collected before an inaccessible directory or metadata change.
        }

        return recent.OrderByDescending(reading => reading.ObservedAt).ToArray();
    }

    private async Task<FileStream> AcquireWriteGateAsync(CancellationToken cancellationToken)
    {
        var lockPath = Path.Combine(_directory, ".write.lock");
        const int maximumRetries = 4;
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < maximumRetries)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<ProviderReading?> ReadFileAsync(
        string path,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 4096, useAsync: true);
            return await ReadAsync(input, now, persisted: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<ProviderReading?> ReadAsync(
        Stream input,
        DateTimeOffset now,
        bool persisted,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumInputBytes + 1];
        try
        {
            var bytesRead = await input.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0 || bytesRead > MaximumInputBytes)
            {
                return null;
            }

            var data = buffer.AsMemory(0, bytesRead);
            if (data.Span.StartsWith("\uFEFF"u8))
            {
                data = data[3..];
            }

            using var document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 12 });
            return Parse(document.RootElement, now, persisted);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static ProviderReading? Parse(JsonElement root, DateTimeOffset now, bool persisted)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            GetLong(root, "version") != SchemaVersion ||
            ReadString(root, "sourceId") is not { } source ||
            !SourceIds.Contains(source, StringComparer.Ordinal) ||
            ReadDate(root, "observedAt") is not { } observedAt ||
            observedAt < DateTimeOffset.UnixEpoch ||
            observedAt - now > TimeSpan.FromMinutes(1))
        {
            return null;
        }

        var identifier = ReadString(root, persisted ? "sessionKey" : "sessionId");
        if (identifier is null || (persisted ? !IsSessionKey(identifier) : !IsSessionId(identifier)))
        {
            return null;
        }

        var current = ReadContext(root, "currentContext");
        var historical = ReadContext(root, "historicalContext");
        var exchange = ReadExchange(root);
        var quotas = ReadQuotas(root, observedAt);
        if (current is null && historical is null && exchange is null && quotas.Count == 0)
        {
            return null;
        }

        var key = persisted ? identifier : Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(source + "\0" + identifier)));
        return new ProviderReading(source, key, observedAt, current, historical, exchange, quotas);
    }

    private static bool IsSessionId(string value) =>
        value.Length is > 0 and <= 512 && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    private static bool IsSessionKey(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static ProviderContext? ReadContext(JsonElement root, string name)
    {
        if (ReadObject(root, name) is not { } context)
        {
            return null;
        }

        var tokens = GetLong(context, "tokens");
        var capacity = GetLong(context, "capacityTokens");
        var percent = ReadDecimal(context, "usedPercent", maximum: 100);
        if (capacity == 0 || (tokens is null && capacity is null && percent is null))
        {
            throw new FormatException();
        }

        return new ProviderContext(tokens, capacity, percent);
    }

    private static ProviderExchange? ReadExchange(JsonElement root)
    {
        if (ReadObject(root, "latestExchange") is not { } exchange)
        {
            return null;
        }

        var input = GetLong(exchange, "inputTokens");
        var output = GetLong(exchange, "outputTokens");
        var cached = GetLong(exchange, "cachedInputTokens");
        var reasoning = GetLong(exchange, "reasoningOutputTokens");
        return input is null && output is null && cached is null && reasoning is null
            ? null
            : new ProviderExchange(input, output, cached, reasoning);
    }

    private static IReadOnlyList<ProviderQuota> ReadQuotas(JsonElement root, DateTimeOffset observedAt)
    {
        if (ReadProperty(root, "accountQuotas", JsonValueKind.Array) is not { } quotas)
        {
            return [];
        }

        const int maximumQuotaBuckets = 5;
        if (quotas.GetArrayLength() > maximumQuotaBuckets)
        {
            throw new FormatException();
        }

        var readings = new List<ProviderQuota>();
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quota in quotas.EnumerateArray())
        {
            if (quota.ValueKind != JsonValueKind.Object ||
                ReadString(quota, "kind") is not { } kind ||
                kind is not ("account" or "five-hour" or "daily" or "weekly" or "monthly") ||
                !kinds.Add(kind))
            {
                throw new FormatException();
            }

            readings.Add(ReadQuota(quota, kind, observedAt));
        }

        return readings.AsReadOnly();
    }

    private static ProviderQuota ReadQuota(JsonElement quota, string kind, DateTimeOffset observedAt)
    {
        var percent = ReadDecimal(quota, "remainingPercent", 100);
        var amount = ReadDecimal(quota, "remainingAmount", MaximumTokens);
        var unit = ReadString(quota, "unit");
        var reset = ReadDate(quota, "resetsAt");
        if ((percent is null) == (amount is null) ||
            (percent is not null && unit is not null) ||
            (amount is not null && unit is not ("tokens" or "requests" or "credits" or "usd")) ||
            reset <= observedAt)
        {
            throw new FormatException();
        }

        return new ProviderQuota(kind, percent, amount, unit, reset);
    }

    private static long? GetLong(JsonElement root, string name) =>
        ReadProperty(root, name, JsonValueKind.Number) is { } value
            ? value.TryGetInt64(out var number) && number is >= 0 and <= MaximumTokens
                ? number
                : throw new FormatException()
            : null;

    private static decimal? ReadDecimal(JsonElement root, string name, decimal maximum) =>
        ReadProperty(root, name, JsonValueKind.Number) is { } value
            ? value.TryGetDecimal(out var number) && number >= 0 && number <= maximum
                ? number
                : throw new FormatException()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement root, string name)
    {
        if (ReadProperty(root, name, JsonValueKind.String) is not { } value)
        {
            return null;
        }

        var text = value.GetString()!;
        var hasOffset = text.EndsWith('Z') || (text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':');
        return hasOffset && value.TryGetDateTimeOffset(out var date) ? date : throw new FormatException();
    }

    private static string? ReadString(JsonElement root, string name) =>
        ReadProperty(root, name, JsonValueKind.String)?.GetString();

    private static JsonElement? ReadObject(JsonElement root, string name) =>
        ReadProperty(root, name, JsonValueKind.Object);

    private static JsonElement? ReadProperty(JsonElement root, string name, JsonValueKind kind)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == kind ? value : throw new FormatException();
    }

    private sealed record StoredReading(
        int Version,
        string SourceId,
        string SessionKey,
        DateTimeOffset ObservedAt,
        ProviderContext? CurrentContext,
        ProviderContext? HistoricalContext,
        ProviderExchange? LatestExchange,
        IReadOnlyList<ProviderQuota> AccountQuotas)
    {
        public static StoredReading From(ProviderReading reading) => new(
            SchemaVersion, reading.SourceId, reading.SessionKey, reading.ObservedAt, reading.CurrentContext,
            reading.HistoricalContext, reading.LatestExchange, reading.AccountQuotas);
    }
}
