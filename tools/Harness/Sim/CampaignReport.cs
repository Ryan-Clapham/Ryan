using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ColourCascade.Core;

/// <summary>
/// Plays every campaign level in a range with the Casual and Planner bots and reports how hard each one is, and which
/// look too easy, too hard or unfair. Usage: dotnet run --project Sim -c Release -- campaign [from] [to] [runs] [csvPath]
/// </summary>
static class CampaignReport
{
    public sealed class Row
    {
        public int Level;
        public string Band = "";
        public string Note = "";
        public string Goals = "";
        public int Moves, Colours, Stones;
        public double CasualWin, PlannerWin, CasualMoves, PlannerMoves, Planner3Star, Casual3Star, Shuffles, Stuck;
        public long GenMs;
        public string Flag = "";
    }

    public static List<Row> Run(int from, int to, int runs, TextWriter log)
    {
        var cfg = GameConfigData.CreateDefault();
        var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
        var rows = new List<Row>();
        var sw = new Stopwatch();
        log.WriteLine($"CAMPAIGN {from}-{to}: {runs} Casual and {runs} Planner games per level (fresh boards, the level's own moves)");
        log.WriteLine("Lvl  Band      Moves Col  Casual%  Planner%  C-moves P-moves  P-3*%  C-3*% Shuf  Gen(ms)  Goals / note   Flags");
        for (int n = from; n <= to; n++)
        {
            sw.Restart();
            var spec = catalog.Get(n);
            long gen = sw.ElapsedMilliseconds;
            var row = new Row
            {
                Level = n, Band = CampaignDesign.BandFor(n).Name, Note = spec.Note, Moves = spec.Moves, Colours = spec.Colours,
                Stones = spec.StoneCount, GenMs = gen, Goals = Describe(spec),
            };
            Play(spec, cfg, BotKind.Casual, runs, n, out row.CasualWin, out row.CasualMoves, out row.Casual3Star, out row.Shuffles, out row.Stuck);
            Play(spec, cfg, BotKind.Planner, runs, n + 50000, out row.PlannerWin, out row.PlannerMoves, out row.Planner3Star, out _, out _);
            row.Flag = Judge(n, row);
            rows.Add(row);
            log.WriteLine($"{n,3}  {row.Band,-8} {row.Moves,5} {row.Colours,3} {row.CasualWin,7:0}% {row.PlannerWin,8:0}% {row.CasualMoves,8:0.0} {row.PlannerMoves,7:0.0} {row.Planner3Star,6:0}% {row.Casual3Star,5:0}% {row.Shuffles,4:0.0} {gen,8}  {row.Goals}{(string.IsNullOrEmpty(row.Note) ? "" : " · " + row.Note)}  {row.Flag}");
        }
        Summarise(rows, log);
        return rows;
    }

    static string Describe(LevelSpec spec)
    {
        var parts = new List<string>();
        foreach (var g in spec.EffectiveGoals())
        {
            switch (g.kind)
            {
                case GoalKind.Score: parts.Add("score " + g.amount); break;
                case GoalKind.CollectColour: parts.Add(TileColours.Names[g.colour].ToLowerInvariant() + " x" + g.amount); break;
                case GoalKind.FreeIce: parts.Add("ice x" + g.amount + (spec.Layout.ObstacleCount(ObstacleKind.Ice) > 0 && HasThick(spec.Layout) ? " (thick)" : "")); break;
                case GoalKind.BreakChains: parts.Add("chains x" + g.amount); break;
                case GoalKind.CollectTreasure: parts.Add("treasure x" + g.amount); break;
                case GoalKind.ClearMoss: parts.Add("moss x" + Math.Max(g.amount, spec.Layout.ObstacleCount(ObstacleKind.Moss))); break;
            }
        }
        if (spec.Specials != SpecialTier.None) parts.Add("[" + spec.Specials + "]");
        return string.Join(", ", parts);
    }

    static bool HasThick(LevelLayout l)
    {
        for (int r = 0; r < l.Height; r++)
            for (int c = 0; c < l.Width; c++)
                if (l.ObstacleAt(c, r) == ObstacleKind.Ice && l.StrengthAt(c, r) >= 2) return true;
        return false;
    }

    static void Play(LevelSpec spec, GameConfigData cfg, BotKind kind, int runs, int salt, out double winPct, out double avgMoves, out double threeStarPct, out double shuffles, out double stuckPct)
    {
        int wins = 0, three = 0, stuck = 0;
        long moves = 0, shuf = 0;
        for (int i = 0; i < runs; i++)
        {
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = cfg.Scoring, BoardSeed = StableHash.Combine((ulong)salt * 7919, (ulong)i + 7) });
            s.Start();
            Bots.PlayOut(s, kind, new Rng((ulong)(i * 31 + salt)));
            shuf += s.Shuffles;
            if (s.State == SessionState.Stuck) stuck++;
            if (s.State != SessionState.Won) continue;
            wins++;
            moves += s.MovesMade;
            if (s.Stars >= 3) three++;
        }
        winPct = 100.0 * wins / runs;
        avgMoves = wins > 0 ? (double)moves / wins : 0;
        threeStarPct = 100.0 * three / runs;
        shuffles = (double)shuf / runs;
        stuckPct = 100.0 * stuck / runs;
    }

    /// <summary>Win-rate expectations per band for the Casual bot (a relaxed player), with the planner as a fairness check.</summary>
    static string Judge(int n, Row r)
    {
        var flags = new List<string>();
        int lo, hi;
        switch (r.Band)
        {
            case "Learn": lo = 75; hi = 101; break;
            case "Develop": lo = 55; hi = 98; break;
            case "Combine": lo = 40; hi = 95; break;
            case "Advanced": lo = 30; hi = 90; break;
            default: lo = 20; hi = 85; break;
        }
        bool intro = r.Note.StartsWith("New:", StringComparison.Ordinal);
        if (intro) lo = Math.Max(lo, 70);
        if (r.CasualWin < lo) flags.Add("TOO HARD");
        if (r.CasualWin > hi && !intro && n % 10 != 6) flags.Add("too easy");
        if (r.PlannerWin < 80) flags.Add("UNFAIR? planner " + r.PlannerWin.ToString("0") + "%");
        if (r.Stuck > 0) flags.Add("stuck " + r.Stuck.ToString("0") + "%");
        return string.Join(", ", flags);
    }

    static void Summarise(List<Row> rows, TextWriter log)
    {
        log.WriteLine();
        log.WriteLine("BY BAND (average Casual / Planner win rate, flagged levels)");
        foreach (var g in rows.GroupBy(r => r.Band))
        {
            var list = g.ToList();
            log.WriteLine($"{g.Key,-9} levels {list.First().Level}-{list.Last().Level}: Casual {list.Average(r => r.CasualWin),5:0}%  Planner {list.Average(r => r.PlannerWin),5:0}%  " +
                          $"too hard {list.Count(r => r.Flag.Contains("TOO HARD")),3}  too easy {list.Count(r => r.Flag.Contains("too easy")),3}  unfair {list.Count(r => r.Flag.Contains("UNFAIR")),3}  " +
                          $"avg moves {list.Average(r => r.Moves):0.0}  gen {list.Average(r => r.GenMs):0} ms (max {list.Max(r => r.GenMs)})");
        }
    }

    public static void WriteCsv(List<Row> rows, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("level,band,moves,colours,stones,casual_win,planner_win,casual_moves,planner_moves,planner_3star,casual_3star,shuffles,stuck,gen_ms,goals,note,flags");
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", r.Level, r.Band, r.Moves, r.Colours, r.Stones, r.CasualWin.ToString("0"), r.PlannerWin.ToString("0"), r.CasualMoves.ToString("0.0"),
                r.PlannerMoves.ToString("0.0"), r.Planner3Star.ToString("0"), r.Casual3Star.ToString("0"), r.Shuffles.ToString("0.0"), r.Stuck.ToString("0"), r.GenMs,
                "\"" + r.Goals + "\"", "\"" + r.Note + "\"", "\"" + r.Flag + "\""));
        File.WriteAllText(path, sb.ToString());
    }
}
