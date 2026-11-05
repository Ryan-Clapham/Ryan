using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ColourCascade.Core;

/// <summary>
/// What the campaign's generated levels contain, range by range (update 3.2): board sizes and shapes, goals, obstacles,
/// moves and how varied the boards are. No games are played. With "legacy" the progression is switched off, which gives
/// the campaign as it was before update 3.2.
/// Usage: dotnet run --project Sim -c Release -- campaign-shape [from] [to] [step] [legacy]
/// </summary>
static class CampaignShapeReport
{
    public static void Run(int from, int to, int step, bool legacy, TextWriter log)
    {
        var cfg = GameConfigData.CreateDefault();
        if (legacy) cfg.Campaign.progressionFromLevel = 0;
        var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
        Parallel.For(from, to + 1, n => catalog.Get(n));
        log.WriteLine("CAMPAIGN SHAPE " + from + "-" + to + (legacy ? " (progression switched off: the generator before update 3.2)" : ""));
        log.WriteLine("Levels     Boards (top sizes)            Shaped  Score-only  Goals  Kinds  3 kinds  Obstacles  Ice  Chains  Moss  Treasure (1 only)  Moves  44+ moves  Geometries");
        for (int a = from; a <= to; a += step)
        {
            int b = Math.Min(to, a + step - 1);
            var specs = Enumerable.Range(a, b - a + 1).Select(n => catalog.Get(n)).ToList();
            int count = specs.Count;
            string sizes = string.Join(" ", specs.GroupBy(s => s.Layout.Width + "x" + s.Layout.Height).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key + ":" + g.Count()));
            int shaped = specs.Count(s => Enumerable.Range(0, s.Layout.Height).Any(r => Enumerable.Range(0, s.Layout.Width).Any(c => s.Layout.TypeAt(c, r) == CellType.Void)) || BoardShapes.Sections(s.Layout).Count > 1);
            int scoreOnly = specs.Count(s => s.Goals == null || s.Goals.Length == 0);
            double goals = specs.Average(s => s.EffectiveGoals().Count());
            double kinds = specs.Average(Kinds);
            int three = specs.Count(s => Kinds(s) >= 3);
            double obstacles = specs.Average(s => Obstacles(s));
            string Avg(Func<LevelSpec, int> f) { var xs = specs.Select(f).Where(v => v > 0).ToList(); return xs.Count == 0 ? "-" : xs.Average().ToString("0.0"); }
            var treasure = specs.Where(s => s.Layout.TreasureCount > 0).ToList();
            string single = treasure.Count == 0 ? "-" : (100 * treasure.Count(s => s.Layout.TreasureCount == 1) / treasure.Count) + "%";
            double moves = specs.Average(s => s.Moves);
            int many = specs.Count(s => s.Moves >= 44);
            int geometries = specs.Select(s => s.Layout.Width + "x" + s.Layout.Height + ":" + string.Join("/", s.Layout.ToRows().Select(r => new string(r.Select(ch => ch == 'x' || ch == '#' ? ch : '.').ToArray())))).Distinct().Count();
            log.WriteLine($"{a,4}-{b,-5} {sizes,-29} {Pct(shaped, count),6} {Pct(scoreOnly, count),10} {goals,6:0.00} {kinds,6:0.00} {three,8} {obstacles,10:0.0} {Avg(s => s.Layout.ObstacleCount(ObstacleKind.Ice)),4} {Avg(s => s.Layout.ObstacleCount(ObstacleKind.Chain)),7} {Avg(s => s.Layout.ObstacleCount(ObstacleKind.Moss)),5} {Avg(s => s.Layout.TreasureCount),5} ({single,4}) {moves,11:0.0} {Pct(many, count),10} {geometries,11}");
        }
        if (legacy) return;
        log.WriteLine();
        log.WriteLine("Board families by range (from the level notes):");
        var families = Enum.GetValues(typeof(BoardFamily)).Cast<BoardFamily>().Where(f => BoardShapes.NameOf(f) != "").ToList();
        for (int a = from; a <= to; a += step)
        {
            int b = Math.Min(to, a + step - 1);
            var counts = families.Select(f => (f, n: Enumerable.Range(a, b - a + 1).Count(n => catalog.Get(n).Note.Contains(BoardShapes.NameOf(f)) || catalog.Get(n).Note == BoardShapes.IntroNote(f)))).Where(x => x.n > 0);
            log.WriteLine($"{a,4}-{b,-5} " + string.Join(", ", counts.Select(x => BoardShapes.NameOf(x.f) + " " + x.n)));
        }
    }

    static int Kinds(LevelSpec s)
    {
        var l = s.Layout;
        return (l.ObstacleCount(ObstacleKind.Ice) > 0 ? 1 : 0) + (l.ObstacleCount(ObstacleKind.Chain) > 0 ? 1 : 0) + (l.ObstacleCount(ObstacleKind.Moss) > 0 ? 1 : 0) +
               (l.TreasureCount > 0 ? 1 : 0) + (s.EffectiveGoals().Any(g => g.kind == GoalKind.CollectColour) ? 1 : 0);
    }

    static int Obstacles(LevelSpec s) => s.Layout.ObstacleCount(ObstacleKind.Ice) + s.Layout.ObstacleCount(ObstacleKind.Chain) + s.Layout.ObstacleCount(ObstacleKind.Moss) + s.Layout.TreasureCount;

    static string Pct(int x, int n) => (100 * x / Math.Max(1, n)) + "%";
}
