using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;

/// <summary>
/// Coin income for a typical player, through the real reward code (GameContext.FinishRun). Each simulated day the
/// player clears a few new campaign levels (retrying failures), plays the Daily Challenge, plays Endless every few
/// days and the Weekly Rush once a week. Nothing is spent, so the balance is total income.
/// </summary>
static class EconomySim
{
    public static void Report(GameConfigData cfg, int days, int levelsPerDay)
    {
        cfg.Economy.ledgerCapacity = 1000000; // the sim reads income by source from the full ledger
        var clock = new FixedClock(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc)); // a Monday
        var ctx = new GameContext(cfg, new MemorySaveStorage(), clock);
        ctx.Load();
        var rng = new Rng(2026);
        int level = 1;
        var levelReachedOnDay = new Dictionary<int, int>();
        var campaignCoinsAtLevel = new Dictionary<int, int>();

        Console.WriteLine($"Player: {levelsPerDay} new levels a day, the Daily every day, Endless every 3rd day, Weekly Rush on Mondays (Mixed bot)");
        Console.WriteLine("Day  Level  Balance | campaign  daily  weekly  endless | coins/day");
        int lastBalance = 0;
        for (int day = 1; day <= days; day++)
        {
            for (int cleared = 0; cleared < levelsPerDay; cleared++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    var s = Play(ctx.CreateCampaignRun(level), rng);
                    ctx.FinishRun(s);
                    if (s.State == SessionState.Won) break;
                }
                campaignCoinsAtLevel[level] = Source(ctx, "c.");
                level++;
            }
            ctx.FinishRun(Play(ctx.CreateDailyRun(), rng));
            if (day % 3 == 1) ctx.FinishRun(Play(ctx.CreateEndlessRun(), rng));
            if (clock.Now.DayOfWeek == DayOfWeek.Monday)
            {
                var rush = new PuzzleSession(ctx.CreateWeeklyRun());
                rush.Start();
                rush.StartTimer();
                Bots.PlayOut(rush, BotKind.Mixed, rng, 45); // about one move every 1.3 s
                rush.Tick(1000);
                ctx.FinishRun(rush);
            }
            int balance = ctx.Wallet.Balance;
            if (day <= 7 || day % 5 == 0)
                Console.WriteLine($"{day,3}  {level - 1,5}  {balance,7} | {Source(ctx, "c."),8} {Source(ctx, "d."),6} {Source(ctx, "w."),7} {Source(ctx, "e."),8} | {balance - lastBalance,5}");
            lastBalance = balance;
            levelReachedOnDay[day] = level - 1;
            clock.Now = clock.Now.AddDays(1);
        }

        int totalDays = days;
        double perDay = ctx.Wallet.Balance / (double)totalDays;
        Console.WriteLine();
        Console.WriteLine($"Average income {perDay:0} coins/day. Campaign coins by level 10/20/30/60: " +
            string.Join(" / ", new[] { 10, 20, 30, 60 }.Where(campaignCoinsAtLevel.ContainsKey).Select(l => campaignCoinsAtLevel[l])));
        Console.WriteLine("Power-up          price  unlock   days of income per purchase");
        foreach (var p in cfg.PowerUps.OrderBy(p => p.unlockLevel))
            Console.WriteLine($"{p.displayName,-16} {p.price,6} {p.unlockLevel,7}   {p.price / perDay,6:0.0}");
    }

    static PuzzleSession Play(SessionSetup setup, Rng rng)
    {
        var s = new PuzzleSession(setup);
        s.Start();
        Bots.PlayOut(s, BotKind.Mixed, rng);
        return s;
    }

    static int Source(GameContext ctx, string prefix)
    {
        int total = 0;
        foreach (var e in ctx.Wallet.Ledger)
            if (e.Delta > 0 && e.Reason.StartsWith(prefix, StringComparison.Ordinal)) total += e.Delta;
        return total;
    }
}
