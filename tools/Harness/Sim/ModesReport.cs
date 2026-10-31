using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;

/// <summary>
/// Tile mechanics outside the campaign: Daily Challenge success rates by unlock level, Weekly Rush medal rates and
/// medal-score scaling, and Endless runs with and without the Endless unlocks.
/// Usage: dotnet run --project Sim -c Release -- modes
/// </summary>
static class ModesReport
{
    public static void Run()
    {
        var cfg = GameConfigData.CreateDefault();
        var f = new ChallengeFactory(cfg.Daily, cfg.Rush, cfg.Scoring);
        var sched = cfg.Campaign.mechanics;
        var unlockLevels = new (string name, MechanicSet set)[]
        {
            ("nothing unlocked", MechanicSet.None),
            ("level 45 (ice, colour goals, rockets, bombs)", Mechanics.FromCampaign(45, sched)),
            ("level 80 (+ prisms, chains, treasure)", Mechanics.FromCampaign(80, sched)),
            ("everything unlocked", MechanicSet.All),
        };
        var day0 = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        Console.WriteLine("DAILY CHALLENGE: 60 days, 6 games per bot per day (success = target and every goal by the last move)");
        foreach (var (name, set) in unlockLevels)
        {
            int days = 0, withGoals = 0;
            double casual = 0, planner = 0;
            var used = new Dictionary<string, int>();
            for (int d = 0; d < 60; d++)
            {
                string key = ChallengeCalendar.DayKey(day0.AddDays(d));
                var spec = f.Daily(key, set);
                days++;
                if (spec.Goals != null) withGoals++;
                string mix = Describe(spec.Layout);
                used[mix] = used.TryGetValue(mix, out var c) ? c + 1 : 1;
                casual += Rate(spec, cfg, BotKind.Casual, 6);
                planner += Rate(spec, cfg, BotKind.Planner, 6);
            }
            Console.WriteLine($"  {name,-46} Casual {casual / days,5:0}%  Planner {planner / days,5:0}%  days with goals {withGoals,2}/{days}  boards: {string.Join(", ", used.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value))}");
        }

        Console.WriteLine();
        Console.WriteLine("WEEKLY RUSH: 16 weeks, everything unlocked, Mixed bot playing 45 moves (about 60 seconds)");
        int bronze = 0, silver = 0, gold = 0, runs = 0;
        double ratio = 0;
        var themes = new Dictionary<string, int>();
        for (int w = 0; w < 16; w++)
        {
            string week = ChallengeCalendar.WeekKey(day0.AddDays(7 * w));
            var plain = f.Weekly(week);
            var spec = f.Weekly(week, MechanicSet.All);
            ChallengeMechanics.WeeklyFor(week, MechanicSet.All, out var theme);
            string t = ChallengeMechanics.ThemeName(theme);
            themes[t] = themes.TryGetValue(t, out var c) ? c + 1 : 1;
            ratio += spec.Target / (double)Math.Max(1, plain.Target);
            for (int i = 0; i < 8; i++)
            {
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.WeeklyRush, Level = spec, Scoring = cfg.Scoring, BoardSeed = spec.Seed });
                s.Start();
                s.StartTimer();
                int score = Bots.PlayOut(s, BotKind.Mixed, new Rng((ulong)(w * 100 + i + 7)), ChallengeFactory.RushProbeMoves);
                runs++;
                if (score >= spec.Target) bronze++;
                if (score >= spec.Star2) silver++;
                if (score >= spec.Star3) gold++;
            }
        }
        Console.WriteLine($"  medal scores x{ratio / 16:0.00} of the plain board on average; Mixed bot: bronze {100.0 * bronze / runs:0}%  silver {100.0 * silver / runs:0}%  gold {100.0 * gold / runs:0}%");
        Console.WriteLine("  themes: " + string.Join(", ", themes.Select(kv => kv.Key + " " + kv.Value)));
        var baseline = new int[3];
        int baseRuns = 0;
        for (int w = 0; w < 16; w++)
        {
            var plain = f.Weekly(ChallengeCalendar.WeekKey(day0.AddDays(7 * w)));
            for (int i = 0; i < 8; i++)
            {
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.WeeklyRush, Level = plain, Scoring = cfg.Scoring, BoardSeed = plain.Seed });
                s.Start();
                s.StartTimer();
                int score = Bots.PlayOut(s, BotKind.Mixed, new Rng((ulong)(w * 100 + i + 7)), ChallengeFactory.RushProbeMoves);
                baseRuns++;
                if (score >= plain.Target) baseline[0]++;
                if (score >= plain.Star2) baseline[1]++;
                if (score >= plain.Star3) baseline[2]++;
            }
        }
        Console.WriteLine($"  same weeks without mechanics:  bronze {100.0 * baseline[0] / baseRuns:0}%  silver {100.0 * baseline[1] / baseRuns:0}%  gold {100.0 * baseline[2] / baseRuns:0}%");

        Console.WriteLine();
        Console.WriteLine("ENDLESS: 20 runs per Endless level, Planner bot");
        var e = cfg.Endless;
        foreach (int level in new[] { 1, 5, 10, 20, 30, 40 })
        {
            var pool = Mechanics.FromEndless(level, e.mechanics);
            var scores = new List<int>();
            var stages = new List<int>();
            var moves = new List<int>();
            int added = 0;
            for (int i = 0; i < 20; i++)
            {
                var spec = new LevelSpec
                {
                    Id = "endless", Mode = GameMode.Endless, Layout = LevelLayout.Open(e.columns, e.rows), Colours = e.ColoursForStage(1),
                    AssistPermille = e.assistPermille, Moves = e.startMoves, Pool = pool, Specials = Mechanics.Tier(pool),
                };
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Endless, Level = spec, Scoring = cfg.Scoring, Endless = e, BoardSeed = (ulong)(i + 1) });
                s.Start();
                var bot = new PlannerBot();
                var o = new MoveOutcome();
                while (s.AcceptsMoves && s.MovesMade < 2000)
                {
                    if (!bot.Choose(s, out var p, out var c)) break;
                    s.TryPlay(p, o, c);
                    added += o.Added.Count;
                }
                scores.Add(s.Score);
                stages.Add(s.Stage);
                moves.Add(s.MovesMade);
            }
            scores.Sort();
            Console.WriteLine($"  level {level,2} ({(pool == MechanicSet.None ? "original tiles" : Mechanics.Names(pool))}): median score {scores[10],6}, median stage {stages.OrderBy(x => x).ElementAt(10)}, max stage {stages.Max()}, median moves {moves.OrderBy(x => x).ElementAt(10)}, items added per run {added / 20.0:0.0}");
        }
    }

    static string Describe(LevelLayout l)
    {
        var parts = new List<string>();
        if (l.ObstacleCount(ObstacleKind.Ice) > 0) parts.Add("ice");
        if (l.ObstacleCount(ObstacleKind.Chain) > 0) parts.Add("chains");
        if (l.TreasureCount > 0) parts.Add("treasure");
        if (l.ObstacleCount(ObstacleKind.Moss) > 0) parts.Add("moss");
        return parts.Count == 0 ? "plain" : string.Join("+", parts);
    }

    static double Rate(LevelSpec spec, GameConfigData cfg, BotKind kind, int runs)
    {
        int ok = 0;
        for (int i = 0; i < runs; i++)
        {
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Daily, Level = spec, Scoring = cfg.Scoring, BoardSeed = spec.Seed });
            s.Start();
            Bots.PlayOut(s, kind, new Rng((ulong)(i * 31 + 5)));
            if (s.Succeeded) ok++;
        }
        return 100.0 * ok / runs;
    }
}
