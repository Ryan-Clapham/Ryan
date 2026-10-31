using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>Random play (bots plus random power-ups) on real campaign levels with every mechanic, checking the rules after each action.</summary>
    public class AllMechanicsLongPlayTests
    {
        [Test]
        public void RandomPlayOnCampaignLevelsKeepsEveryRule()
        {
            var cfg = GameConfigData.CreateDefault();
            var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            var rules = cfg.Scoring.specials;
            int actions = 0, fired = 0, combos = 0, created = 0, treasures = 0, mossGrew = 0, iceFreed = 0, chainsFreed = 0, won = 0, games = 0;
            for (int n = 21; n <= 200; n += 4)
            {
                var spec = catalog.Get(n);
                for (ulong seed = 1; seed <= 2; seed++)
                {
                    games++;
                    var rng = new Rng(StableHash.Combine((ulong)n, seed));
                    var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = cfg.Scoring, BoardSeed = seed * 977 + (ulong)n });
                    s.Start();
                    int startTreasure = Play.CountPieces(s.Board, CellType.Treasure);
                    int collected = 0, freedIce = 0, freedChains = 0, sumGained = 0;
                    bool grewLast = false;
                    var finder = new GroupFinder();
                    var groups = new List<GroupInfo>();
                    var o = new MoveOutcome();
                    for (int step = 0; step < 120 && s.AcceptsMoves; step++)
                    {
                        // Ice and chains hold their cells while they last.
                        var held = new Dictionary<int, GridPos>();
                        for (int i = 0; i < s.Board.CellCount; i++)
                        {
                            var t = s.Board.GetByIndex(i);
                            if (TileRules.IsGravityBarrier(t) && TileRules.HasObstacle(t)) held[t.Id] = s.Board.PosOf(i);
                        }
                        bool acted;
                        if (rng.Next(6) == 0) acted = RandomPowerUp(s, rng, o);
                        else
                        {
                            Assert.IsTrue(Bots.Choose(s.Board, BotKind.Casual, rng, finder, groups, out var pick), "level " + n + ": a playing board always has a move");
                            int colour = s.NeedsColourChoice(pick) ? rng.Next(s.ColourCount) : -1;
                            acted = s.TryPlay(pick, o, colour);
                            Assert.IsTrue(acted, "level " + n);
                        }
                        if (!acted) continue;
                        actions++;
                        sumGained += o.ScoreGained;
                        Assert.AreEqual(sumGained, s.Score, "level " + n + ": score is the sum of what each action gained");

                        Assert.AreEqual(o.SpecialsFired.Count, o.SpecialsFired.Select(f => f.TileId).Distinct().Count(), "a special fired twice");
                        fired += o.SpecialsFired.Count;
                        combos += o.SpecialsFired.Count(f => f.ComboWith != SpecialKind.None);
                        if (o.HasCreated)
                        {
                            created++;
                            Assert.AreEqual(OutcomeKind.Group, o.Kind);
                            Assert.GreaterOrEqual(o.GroupSize, rules.rocketGroupSize);
                            Assert.AreNotEqual(SpecialTier.None, spec.Specials);
                        }
                        Assert.AreEqual(o.ObstacleHits.Count, o.ObstacleHits.Select(h => h.TileId).Distinct().Count(), "an obstacle was hit twice in one action");
                        freedIce += o.ObstacleHits.Count(h => h.Kind == ObstacleKind.Ice && h.Freed);
                        freedChains += o.ObstacleHits.Count(h => h.Kind == ObstacleKind.Chain && h.Freed);
                        collected += o.Treasures.Count;

                        Assert.IsTrue(o.MossGrowth.Count <= 1);
                        bool grew = o.MossGrowth.Count > 0;
                        Assert.IsFalse(grew && grewLast, "moss grew on two moves in a row");
                        if (o.Kind != OutcomeKind.PowerUp) grewLast = grew;
                        if (grew) mossGrew++;
                        if (spec.MossCap > 0) Assert.LessOrEqual(s.Board.CountObstacle(ObstacleKind.Moss), System.Math.Max(spec.MossCap, spec.Layout.ObstacleCount(ObstacleKind.Moss)));

                        for (int i = 0; i < s.Board.CellCount; i++)
                        {
                            var t = s.Board.GetByIndex(i);
                            if (!TileRules.HasObstacle(t)) continue;
                            Assert.IsTrue(t.Strength >= 1 && t.Strength <= Obstacles.Get(t.Obstacle).MaxStrength);
                            if (held.TryGetValue(t.Id, out var was) && TileRules.IsGravityBarrier(t)) Assert.AreEqual(was, s.Board.PosOf(i), "level " + n + ": ice or a chain moved");
                        }
                        Assert.AreEqual(startTreasure, collected + Play.CountPieces(s.Board, CellType.Treasure), "treasure appeared or vanished");
                        if (!o.RecoveryFailed) TestUtil.AssertBoardInvariants(s.Board, spec.Layout);

                        for (int g = 0; g < s.Goals.Count; g++)
                        {
                            var goal = s.Goals.Goal(g);
                            if (goal.kind == GoalKind.FreeIce) Assert.AreEqual(freedIce, s.Goals.Progress(g));
                            if (goal.kind == GoalKind.BreakChains) Assert.AreEqual(freedChains, s.Goals.Progress(g));
                            if (goal.kind == GoalKind.CollectTreasure) Assert.AreEqual(collected, s.Goals.Progress(g));
                            if (goal.kind == GoalKind.ClearMoss) Assert.AreEqual(s.Board.CountObstacle(ObstacleKind.Moss), s.Goals.Remaining(g));
                        }
                        if (s.Goals.AllComplete) Assert.AreEqual(SessionState.Won, s.State, "complete goals win at once");
                    }
                    treasures += collected;
                    iceFreed += freedIce;
                    chainsFreed += freedChains;
                    if (s.State == SessionState.Won) won++;
                }
            }
            TestContext.WriteLine($"{games} games, {actions} actions, won {won}; specials made {created}, fired {fired} ({combos} combos); treasure {treasures}; ice {iceFreed}, chains {chainsFreed}; moss grew {mossGrew}");
            Assert.Greater(actions, 2000);
            Assert.Greater(created, 50);
            Assert.Greater(fired, 50);
            Assert.Greater(combos, 0);
            Assert.Greater(treasures, 5);
            Assert.Greater(mossGrew, 5);
            Assert.Greater(iceFreed, 50);
            Assert.Greater(chainsFreed, 20);
        }

        static bool RandomPowerUp(PuzzleSession s, Rng rng, MoveOutcome o)
        {
            var b = s.Board;
            var a = new GridPos(rng.Next(b.Width), rng.Next(b.Height));
            PowerUpRequest req;
            switch (rng.Next(6))
            {
                case 0: req = Play.At(PowerUpId.Blast, a.Col, a.Row); break;
                case 1: req = Play.At(PowerUpId.TileBreaker, a.Col, a.Row); break;
                case 2: req = Play.At(PowerUpId.ColourBurst, a.Col, a.Row); break;
                case 3: req = Play.Shift(a.Col, a.Row, rng.Next(s.ColourCount)); break;
                case 4: req = new PowerUpRequest { Id = PowerUpId.ExtraMoves, Amount = 1 }; break;
                default:
                    int d = rng.Next(4);
                    req = Play.Swap(a.Col, a.Row, a.Col + Board.DCol[d], a.Row + Board.DRow[d]);
                    break;
            }
            return s.TryApplyPowerUp(req, o, out _);
        }
    }
}
