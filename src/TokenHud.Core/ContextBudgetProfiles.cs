namespace TokenHud.Core;

public enum ContextBudgetSource
{
    Installation,
    Codex,
    Chat,
}

public sealed record ResolvedContextBudget(
    long ConfiguredTokens,
    long EffectiveTokens,
    ContextBudgetSource Source,
    long? ModelContextLimit)
{
    public static ResolvedContextBudget Default { get; } =
        new(150_000, 150_000, ContextBudgetSource.Installation, null);

    public bool IsCapped => EffectiveTokens < ConfiguredTokens;
}

public sealed record ContextBudgetProfiles(
    long? CodexBudget = null,
    IReadOnlyDictionary<string, long>? ChatBudgets = null)
{
    public ResolvedContextBudget Resolve(long installationBudget, string chatId, long? reportedModelLimit)
    {
        var configured = installationBudget > 0 ? installationBudget : ResolvedContextBudget.Default.ConfiguredTokens;
        var source = ContextBudgetSource.Installation;
        if (CodexBudget is > 0 and var codexBudget)
        {
            configured = codexBudget;
            source = ContextBudgetSource.Codex;
        }

        if (ChatBudgets is not null && ChatBudgets.TryGetValue(chatId, out var chatBudget) && chatBudget > 0)
        {
            configured = chatBudget;
            source = ContextBudgetSource.Chat;
        }

        // At least two tokens are required for a positive budget strictly below the limit.
        var modelLimit = reportedModelLimit is >= 2 ? reportedModelLimit : null;
        var effective = modelLimit is { } limit
            ? Math.Min(configured, Math.Max(1, (long)(limit * 0.95m)))
            : configured;

        return new ResolvedContextBudget(configured, effective, source, modelLimit);
    }
}
