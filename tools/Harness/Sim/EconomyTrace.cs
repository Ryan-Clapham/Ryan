using System;
using System.IO;
using System.Linq;
using ColourCascade.Core;

/// <summary>
/// The long-run economy simulation (update 3.1): the real game code plays the campaign, the Daily, the Weekly Rush and
/// Endless day by day and claims achievements each evening, and every coin credit and every power-up an achievement
/// grants is written out with its day and reason. Nothing is spent: tools/economy_model.py adds spending, adverts and
/// the checkpoint report on top. Boards are seeded from the level, attempt and day, so the same profile plays exactly
/// the same games under every economy version, and only the rewards differ.
/// Lines: lvl,NAME,LEVEL,DAY,ATTEMPTS,WON,STARS / credit,NAME,DAY,REASON,COINS / grant,NAME,DAY,ACHIEVEMENT,POWERUP,COUNT /
/// endless,NAME,DAY,SCORE / day,NAME,DAY,LEVEL,BALANCE / end,NAME,BALANCE,CLAIMED.
/// </summary>
static class EconomyTrace
{
    public static void Run(TextWriter o, string name, int levelsPerDay, int dailyOf7, int endlessPerWeek, BotKind bot, int maxLevel, int version)
    {
        var cfg = GameConfigData.CreateDefault();
        if (version < EconomyTuning.Current) EconomyTuning.ApplyVersion(cfg, version);
        cfg.Economy.ledgerCapacity = 10000000;
        var clock = new FixedClock(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc));
        var ctx = new GameContext(cfg, new MemorySaveStorage(), clock);
        ctx.Load();
        var rng = new Rng(77 + (ulong)levelsPerDay);
        var ids = ctx.PowerUps.All.Select(p => p.id).ToList();
        int level = 1, printed = 0;
        for (int day = 1; level <= maxLevel; day++)
        {
            for (int n = 0; n < levelsPerDay && level <= maxLevel; n++)
            {
                bool won = false;
                int attempts = 0;
                for (; attempts < 12 && !won; attempts++)
                {
                    var setup = ctx.CreateCampaignRun(level);
                    setup.BoardSeed = StableHash.Combine((ulong)level * 131 + (ulong)attempts, 0xC0FFEEUL + (ulong)levelsPerDay);
                    var s = new PuzzleSession(setup);
                    s.Start();
                    ctx.BeginRun(s);
                    Bots.PlayOut(s, bot, rng);
                    if (!s.IsOver && s.CanFinish) s.Finish(new MoveOutcome());
                    ctx.FinishRun(s);
                    won = s.State == SessionState.Won;
                }
                // A level the bot can't win in twelve tries is skipped, as a player would get help or move on.
                if (!won) ctx.Data.HighestUnlocked = Math.Max(ctx.Data.HighestUnlocked, level + 1);
                o.WriteLine($"lvl,{name},{level},{day},{attempts},{(won ? 1 : 0)},{ctx.Data.GetOrCreateLevel(level).Stars}");
                level++;
            }
            if ((day - 1) % 7 < dailyOf7)
            {
                var s = new PuzzleSession(ctx.CreateDailyRun());
                s.Start();
                ctx.BeginRun(s);
                Bots.PlayOut(s, bot, rng);
                ctx.FinishRun(s);
            }
            if ((day - 1) % 7 < endlessPerWeek)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = StableHash.Combine((ulong)day, 0xE11D1E55UL + (ulong)levelsPerDay);
                var s = new PuzzleSession(setup);
                s.Start();
                ctx.BeginRun(s);
                Bots.PlayOut(s, bot, rng, 3000);
                if (s.FinalePending) s.RunFinale(new MoveOutcome());
                if (s.IsOver) ctx.FinishRun(s);
                else ctx.AbandonRun(s);
                o.WriteLine($"endless,{name},{day},{s.Score}");
            }
            if (clock.Now.DayOfWeek == DayOfWeek.Monday)
            {
                var r = new PuzzleSession(ctx.CreateWeeklyRun());
                r.Start();
                ctx.BeginRun(r);
                r.StartTimer();
                Bots.PlayOut(r, bot == BotKind.Planner ? BotKind.Mixed : bot, rng, 45);
                r.Tick(1000);
                ctx.FinishRun(r);
            }
            foreach (var d in ctx.Achievements.All)
            {
                if (ctx.Achievements.State(d) != AchievementState.Unlocked) continue;
                var before = ids.ToDictionary(i => i, i => ctx.Inventory.Owned(i));
                ctx.Achievements.Claim(d.id);
                foreach (var i in ids)
                {
                    int got = ctx.Inventory.Owned(i) - before[i];
                    if (got > 0) o.WriteLine($"grant,{name},{day},{d.id},{i},{got}");
                }
            }
            var ledger = ctx.Wallet.Ledger;
            for (; printed < ledger.Count; printed++)
                if (ledger[printed].Delta > 0) o.WriteLine($"credit,{name},{day},{ledger[printed].Reason},{ledger[printed].Delta}");
            o.WriteLine($"day,{name},{day},{level - 1},{ctx.Wallet.Balance}");
            clock.Now = clock.Now.AddDays(1);
        }
        o.WriteLine($"end,{name},{ctx.Wallet.Balance},{ctx.Achievements.All.Count(a => ctx.Achievements.State(a) == AchievementState.Claimed)}");
    }
}
