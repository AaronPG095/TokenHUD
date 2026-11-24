namespace TokenHud.Core;

public sealed record BudgetAlertState(
    BudgetZone HandledZone = BudgetZone.Healthy,
    DateTimeOffset? LastNotificationAt = null);

public sealed record BudgetAlertPreferences(
    bool Caution = false,
    bool Critical = true,
    bool OverBudget = true)
{
    public bool IsEnabled(BudgetZone zone) => zone switch
    {
        BudgetZone.Caution => Caution,
        BudgetZone.Critical => Critical,
        BudgetZone.OverBudget => OverBudget,
        _ => false,
    };
}

public sealed record BudgetAlertTransition(BudgetAlertState State, BudgetZone? NotificationZone = null);

public static class BudgetAlerts
{
    public static TimeSpan DefaultCooldown { get; } = TimeSpan.FromMinutes(5);

    public static BudgetAlertTransition Evaluate(
        BudgetAlertState state,
        BudgetZone observedZone,
        BudgetAlertPreferences preferences,
        DateTimeOffset now,
        DateTimeOffset? snoozedUntil = null,
        bool suppressNotifications = false,
        TimeSpan? cooldown = null)
    {
        if (observedZone == BudgetZone.Unavailable || observedZone == state.HandledZone)
        {
            return new(state);
        }

        if (observedZone < state.HandledZone || !preferences.IsEnabled(observedZone) ||
            suppressNotifications || snoozedUntil > now)
        {
            return new(state with { HandledZone = observedZone });
        }

        if (state.LastNotificationAt is { } lastNotification && now - lastNotification < (cooldown ?? DefaultCooldown))
        {
            return new(state);
        }

        return new(new BudgetAlertState(observedZone, now), observedZone);
    }
}
