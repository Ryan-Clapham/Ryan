using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ColourCascade.Core;

// Balance simulation: automated players run the real game rules and report how hard each mode is.
// Usage: dotnet run --project Sim -- [balance|art <outDir>|all <outDir>]
static class Program
{
    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "balance";
        if (mode == "balance" || mode == "all") Balance(args.Length > 2 ? int.Parse(args[2]) : 60);
        if (mode == "art" || mode == "all") ArtPreview.Export(args.Length > 1 ? args[1] : "art-preview");
        return 0;
    }

    static void Balance(int maxLevel)
    {
        var cfg = GameConfigData.CreateDefault();
        var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
        var sw = Stopwatch.StartNew();
        Console.WriteLine("CAMPAIGN (pass% = share of 40 attempts that reach the target; stars shown for greedy wins)");
        Console.WriteLine("Lvl  Size  Col Mov Stn  Target  Star2  Star3 Bonus | Greedy%  Mixed%  Casual% | G-stars(1/2/3)  avgLeft");
        for (int n = 1; n <= maxLevel; n++)
        {
            var t0 = sw.ElapsedMilliseconds;
            var spec = catalog.Get(n);
            var calibMs = sw.ElapsedMilliseconds - t0;
            var g = Attempts(spec, cfg, BotKind.Greedy, 40, n);
            var m = Attempts(spec, cfg, BotKind.Mixed, 40, n);
            var c = Attempts(spec, cfg, BotKind.Casual, 40, n);
            Console.WriteLine($"{n,3}  {spec.Width}x{spec.Height}  {spec.Colours,2} {spec.Moves,3} {spec.StoneCount,3} {spec.Target,7} {spec.Star2,6} {spec.Star3,6} {spec.LeftoverBonusPerMove,5} | {g.PassPct,6:0}% {m.PassPct,6:0}% {c.PassPct,7:0}% | {g.S1,3}/{g.S2,3}/{g.S3,3}  {g.AvgLeft,6:0.0}   ({calibMs}ms) {spec.Note}");
        }

        Console.WriteLine();
        Console.WriteLine("ENDLESS (30 runs each)");
        foreach (var kind in new[] { BotKind.Greedy, BotKind.Mixed, BotKind.Casual })
        {
            var ctx = new GameContext(cfg, new MemorySaveStorage(), new SystemClock());
            var scores = new List<int>(); var moves = new List<int>(); var stages = new List<int>();
            for (int i = 0; i < 30; i++)
            {
                var s = new PuzzleSession(ctx.CreateEndlessRun());
                s.Start();
                Bots.PlayOut(s, kind, new Rng((ulong)(i + 7)), 2000);
                scores.Add(s.Score); moves.Add(s.MovesMade); stages.Add(s.Stage);
            }
            scores.Sort(); moves.Sort();
            Console.WriteLine($"{kind,-7} score median {scores[15],6} (min {scores[0]}, max {scores[29]})  moves median {moves[15]} (min {moves[0]}, max {moves[29]})  max stage {stages.Max()}");
        }

        Console.WriteLine();
        Console.WriteLine("WEEKLY RUSH (score after N moves; a human makes roughly 1 move per 1.0-1.5 s)");
        var rushCtx = new GameContext(cfg, new MemorySaveStorage(), new SystemClock());
        foreach (int movesN in new[] { 30, 40, 50, 60 })
        {
            var line = $"{movesN} moves:";
            foreach (var kind in new[] { BotKind.Greedy, BotKind.Mixed, BotKind.Casual })
            {
                var scores = new List<int>();
                for (int w = 0; w < 12; w++)
                {
                    var setup = rushCtx.CreateWeeklyRun("2026-W" + (10 + w));
                    var s = new PuzzleSession(setup);
                    s.Start();
                    s.StartTimer();
                    Bots.PlayOut(s, kind, new Rng((ulong)(w + 3)), movesN);
                    scores.Add(s.Score);
                }
                scores.Sort();
                line += $"  {kind} median {scores[6]}";
            }
            Console.WriteLine(line + $"   (tiers {string.Join("/", cfg.Rush.tierScores)})");
        }

        Console.WriteLine();
        Console.WriteLine("DAILY (7 consecutive days)");
        var dailyCtx = new GameContext(cfg, new MemorySaveStorage(), new SystemClock());
        var day0 = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        for (int d = 0; d < 7; d++)
        {
            string key = ChallengeCalendar.DayKey(day0.AddDays(d));
            var spec = dailyCtx.Challenges.Daily(key);
            var res = new List<string>();
            foreach (var kind in new[] { BotKind.Greedy, BotKind.Mixed, BotKind.Casual })
            {
                int stars = 0, done = 0;
                for (int i = 0; i < 20; i++)
                {
                    var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Daily, Level = spec, Scoring = cfg.Scoring, BoardSeed = spec.Seed });
                    s.Start();
                    Bots.PlayOut(s, kind, new Rng((ulong)(i * 31 + 5)));
                    if (s.Succeeded) done++;
                    stars += s.Stars;
                }
                res.Add($"{kind} done {done * 5}% avgStars {stars / 20.0:0.0}");
            }
            Console.WriteLine($"{key} {spec.Width}x{spec.Height} c{spec.Colours} stones {spec.StoneCount} target {spec.Target}/{spec.Star2}/{spec.Star3}  " + string.Join("  ", res));
        }
        Console.WriteLine($"\nTotal time {sw.ElapsedMilliseconds} ms");
    }

    struct AttemptStats
    {
        public double PassPct;
        public int S1, S2, S3;
        public double AvgLeft;
    }

    static AttemptStats Attempts(LevelSpec spec, GameConfigData cfg, BotKind kind, int count, int salt)
    {
        int pass = 0, s1 = 0, s2 = 0, s3 = 0;
        double left = 0;
        for (int i = 0; i < count; i++)
        {
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = cfg.Scoring, BoardSeed = StableHash.Combine((ulong)salt * 7919, (ulong)i + 99) });
            s.Start();
            Bots.PlayOut(s, kind, new Rng((ulong)(i * 13 + salt)));
            if (s.State == SessionState.Won)
            {
                pass++;
                left += s.MovesLeft;
                if (s.Stars == 1) s1++; else if (s.Stars == 2) s2++; else if (s.Stars == 3) s3++;
            }
        }
        return new AttemptStats { PassPct = 100.0 * pass / count, S1 = s1, S2 = s2, S3 = s3, AvgLeft = pass > 0 ? left / pass : 0 };
    }
}
