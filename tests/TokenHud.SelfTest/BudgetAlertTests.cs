using TokenHud.Core;

internal static class BudgetAlertTests
{
    public static Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var allEnabled = new BudgetAlertPreferences(Caution: true);
        var noHistory = new BudgetAlertState();

        var caution = BudgetAlerts.Evaluate(noHistory, BudgetZone.Caution, allEnabled, now);
        Expect(caution, BudgetZone.Caution, new(BudgetZone.Caution, now), "Caution transition");
        Expect(BudgetAlerts.Evaluate(caution.State, BudgetZone.Caution, allEnabled, now.AddMinutes(10)),
            null, caution.State, "Unchanged caution");
        Expect(BudgetAlerts.Evaluate(caution.State, BudgetZone.Unavailable, allEnabled, now.AddMinutes(1)),
            null, caution.State, "Unavailable observation");

        var duringCooldown = BudgetAlerts.Evaluate(caution.State, BudgetZone.Critical, allEnabled, now.AddMinutes(4));
        Expect(duringCooldown, null, caution.State, "Escalation inside cooldown");
        var critical = BudgetAlerts.Evaluate(duringCooldown.State, BudgetZone.Critical, allEnabled, now.AddMinutes(5));
        Expect(critical, BudgetZone.Critical, new(BudgetZone.Critical, now.AddMinutes(5)), "Escalation at cooldown boundary");
        Expect(BudgetAlerts.Evaluate(noHistory, BudgetZone.Critical, allEnabled, now.AddMinutes(1)),
            BudgetZone.Critical, new(BudgetZone.Critical, now.AddMinutes(1)), "Independent chat");

        var lowered = BudgetAlerts.Evaluate(critical.State, BudgetZone.Caution, allEnabled, now.AddMinutes(6));
        Expect(lowered, null, new(BudgetZone.Caution, now.AddMinutes(5)), "Recovery below prior alert zone");
        Expect(BudgetAlerts.Evaluate(lowered.State, BudgetZone.Critical, allEnabled, now.AddMinutes(10)),
            BudgetZone.Critical, new(BudgetZone.Critical, now.AddMinutes(10)), "Rearmed alert after recovery");
        Expect(BudgetAlerts.Evaluate(duringCooldown.State, BudgetZone.Caution, allEnabled, now.AddMinutes(5)),
            null, caution.State, "Escalation no longer qualifies");

        var disabled = BudgetAlerts.Evaluate(noHistory, BudgetZone.Caution, new(), now);
        Expect(disabled, null, new(BudgetZone.Caution), "Disabled caution");
        Expect(BudgetAlerts.Evaluate(disabled.State, BudgetZone.Caution, allEnabled, now.AddMinutes(5)),
            null, disabled.State, "Preference enabled while unchanged");

        foreach (var snooze in new[] { TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60) })
        {
            var until = now + snooze;
            var snoozed = BudgetAlerts.Evaluate(critical.State, BudgetZone.OverBudget, allEnabled, until.AddSeconds(-1), until);
            Expect(snoozed, null, new(BudgetZone.OverBudget, now.AddMinutes(5)), "Transition while snoozed");
            Expect(BudgetAlerts.Evaluate(snoozed.State, BudgetZone.OverBudget, allEnabled, until, until),
                null, snoozed.State, "Unchanged zone after snooze");
            var recovered = BudgetAlerts.Evaluate(snoozed.State, BudgetZone.Critical, allEnabled, until, until);
            Expect(BudgetAlerts.Evaluate(recovered.State, BudgetZone.OverBudget, allEnabled, until.AddSeconds(1), until),
                BudgetZone.OverBudget, new(BudgetZone.OverBudget, until.AddSeconds(1)), "New transition after snooze");
            Expect(BudgetAlerts.Evaluate(critical.State, BudgetZone.OverBudget, allEnabled, until, until),
                BudgetZone.OverBudget, new(BudgetZone.OverBudget, until), "First transition at snooze boundary");
        }

        var suppressed = BudgetAlerts.Evaluate(noHistory, BudgetZone.Critical, allEnabled, now, suppressNotifications: true);
        Expect(suppressed, null, new(BudgetZone.Critical), "Suppressed initial observation");
        Expect(BudgetAlerts.Evaluate(suppressed.State, BudgetZone.Critical, allEnabled, now.AddMinutes(1)),
            null, suppressed.State, "Unchanged zone after suppression");
        return Task.CompletedTask;
    }

    private static void Expect(
        BudgetAlertTransition transition, BudgetZone? notification, BudgetAlertState state, string scenario)
    {
        if (transition.NotificationZone != notification || transition.State != state)
        {
            throw new InvalidOperationException($"{scenario} returned {transition}.");
        }
    }
}
