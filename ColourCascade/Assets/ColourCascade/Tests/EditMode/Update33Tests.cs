using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.3: specials left on the board when the Weekly Rush timer runs out go off before the result; Endless runs
    /// meet special tiles early (rockets at stage 2) and later start on board shapes; old config assets are upgraded;
    /// the shape library takes new families and config templates; shaped boards get a panel that follows their cells;
    /// players past the introductory campaign get advanced Daily and Weekly challenges (shapes, combined objectives,
    /// Weekly objective bonuses), while everyone else keeps exactly the original ones.
    /// </summary>
    public class Update33Tests
    {
        // ================================================================== Weekly Rush: specials at the end

        static PuzzleSession RushWith(params string[] rows)
        {
            var setup = TestUtil.Setup(GameMode.WeeklyRush, 0, 1000, TileColours.Max, rows, null);
            setup.Level.Specials = SpecialTier.Prisms;
            var s = new PuzzleSession(setup);
            s.Start();
            s.StartTimer();
            return s;
        }

        [Test]
        public void WhenTheWeeklyTimerRunsOutTheSpecialsOnTheBoardGoOffOnceBeforeTheResult()
        {
            var s = RushWith("01230", "12301", "2*012", "30123", "0-230");
            int before = s.Score;
            Assert.IsTrue(s.Tick(1000), "the time is up");
            Assert.IsTrue(s.FinalePending, "the specials go off first");
            Assert.AreEqual(SessionState.Playing, s.State, "the run is not over until they have");
            Assert.IsFalse(s.AcceptsMoves, "no more taps once the time is up");
            Assert.IsFalse(s.Tick(1), "the clock does not fire again");

            var o = new MoveOutcome();
            Assert.IsTrue(s.RunFinale(o));
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(EndReason.TimeUp, s.EndReason);
            Assert.IsTrue(o.Ended);
            Assert.AreEqual(0, s.Board.CountType(CellType.Special), "every special went off");
            Assert.GreaterOrEqual(o.SpecialsFired.Count, 1);
            Assert.Greater(s.Score, before, "and scored");
            Assert.AreEqual(s.Score - before, s.Breakdown.Finale, "counted once, as specials at the end");
            Assert.AreEqual(s.Score, s.Breakdown.Total);

            int after = s.Score;
            Assert.IsFalse(s.RunFinale(new MoveOutcome()), "never twice");
            Assert.IsFalse(s.Tick(5));
            Assert.AreEqual(after, s.Score);
        }

        [Test]
        public void WithoutSpecialsTheWeeklyRunEndsTheMomentTheTimeIsUp()
        {
            var s = RushWith("01230", "12301", "23012");
            Assert.IsTrue(s.Tick(1000));
            Assert.IsFalse(s.FinalePending);
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(EndReason.TimeUp, s.EndReason);
        }

        [Test]
        public void FinishingAWeeklyRunWithItsSpecialsStillWaitingSetsThemOffAndRecordsTheFullScore()
        {
            var ctx = TestUtil.Context();
            var s = RushWith("01230", "12301", "2@012", "30123");
            ctx.BeginRun(s);
            s.Tick(1000);
            Assert.IsTrue(s.FinalePending);
            int before = s.Score;
            var r = ctx.FinishRun(s);
            Assert.AreEqual(SessionState.Finished, s.State, "the result screen never shows a run still waiting for its specials");
            Assert.Greater(s.Score, before);
            Assert.AreEqual(s.Score, r.FinalScore);
            Assert.AreEqual(s.Score, ctx.ThisWeekRecord.BestScore, "the week's best includes them");
            Assert.AreEqual(s.Score, r.Breakdown.Total);
        }

        [Test]
        public void TheWeeklyFinaleWorksOnAdvancedBoardsToo()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 400;
            for (int w = 0; w < 6; w++)
            {
                string week = ChallengeCalendar.WeekKey(TestUtil.Today.AddDays(7 * w));
                var setup = ctx.CreateWeeklyRun(week);
                var s = new PuzzleSession(setup);
                s.Start();
                s.StartTimer();
                Bots.PlayOut(s, BotKind.Mixed, new Rng((ulong)(w + 3)), 30);
                bool specials = s.Board.CountType(CellType.Special) > 0;
                s.Tick(1000);
                Assert.AreEqual(specials, s.FinalePending, week);
                if (s.FinalePending) s.RunFinale(new MoveOutcome());
                Assert.AreEqual(SessionState.Finished, s.State, week);
                Assert.AreEqual(0, s.Board.CountType(CellType.Special), week);
                Assert.AreEqual(s.Score, s.Breakdown.Total, week + ": every point in exactly one line");
            }
        }

        // ================================================================== Endless: specials and shapes

        [Test]
        public void EndlessRunsMeetRocketsAtStageTwoAndBombsAtStageSeven()
        {
            var j = new EndlessJourneyConfig();
            Assert.AreEqual(SpecialTier.None, EndlessJourney.SpecialsAt(1, j));
            Assert.AreEqual(SpecialTier.Rockets, EndlessJourney.SpecialsAt(2, j));
            Assert.AreEqual(MechanicSet.Rockets, EndlessJourney.Introduced(2, j));
            Assert.AreEqual(SpecialTier.Bombs, EndlessJourney.SpecialsAt(7, j));
            Assert.AreEqual(SpecialTier.Prisms, EndlessJourney.SpecialsAt(11, j));
            // The old order still works for anyone who configures it.
            var old = EndlessTuning.JourneyBefore33();
            Assert.AreEqual(SpecialTier.None, EndlessJourney.SpecialsAt(5, old));
            Assert.AreEqual(MechanicSet.Rockets | MechanicSet.Bombs, EndlessJourney.Introduced(6, old));
        }

        [Test]
        public void EndlessPlayersMakeAndFireSpecialsInAlmostEveryRun()
        {
            var ctx = TestUtil.Context();
            int runs = 12, withSpecials = 0, placedAtStageTwo = 0;
            for (int i = 0; i < runs; i++)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = (ulong)(40 + i);
                var s = new PuzzleSession(setup);
                s.Start();
                var rng = new Rng((ulong)(i + 9));
                var finder = new GroupFinder();
                var groups = new List<GroupInfo>();
                var o = new MoveOutcome();
                int made = 0, fired = 0;
                while (s.AcceptsMoves && s.MovesMade < 150)
                {
                    if (!Bots.Choose(s.Board, BotKind.Mixed, rng, finder, groups, out var pick)) break;
                    int stage = s.Stage;
                    if (!s.TryPlay(pick, o, PlannerBot.PrismColour(s))) break;
                    if (o.HasCreated)
                    {
                        made++;
                        Assert.GreaterOrEqual(stage, 2, "no specials during the warm-up");
                    }
                    fired += o.SpecialsFired.Count;
                    if (o.StageUp && o.NewStage == 2 && o.Added.Any(a => a.Special != SpecialKind.None)) placedAtStageTwo++;
                    if (!o.RecoveryFailed) TestUtil.AssertBoardInvariants(s.Board, setup.Level.Layout);
                }
                if (made > 0 && fired > 0) withSpecials++;
            }
            TestContext.WriteLine("runs making and firing specials: " + withSpecials + "/" + runs + ", rocket placed on reaching stage 2: " + placedAtStageTwo);
            Assert.GreaterOrEqual(withSpecials, runs - 1, "specials are part of nearly every run (before 3.3 they arrived at stage 6, which most runs never reached)");
            Assert.Greater(placedAtStageTwo, runs / 2, "stage 2 starts with a rocket on the board");
        }

        [Test]
        public void EndlessRunsStartOnShapesOnceTheBestRunHasGoneFarEnough()
        {
            var e = new EndlessConfig();
            var split = new[] { BoardFamily.TwinTowers, BoardFamily.Bunks, BoardFamily.Asymmetric, BoardFamily.StoneWall };
            var advanced = new[] { BoardFamily.Passage, BoardFamily.TShape, BoardFamily.Cross, BoardFamily.Diamond };
            for (ulong seed = 1; seed <= 120; seed++)
            {
                Assert.AreEqual(BoardFamily.Rectangle, GameContext.EndlessBoard(e, 1, seed).Family, "new players: the plain board");
                Assert.AreEqual(BoardFamily.Rectangle, GameContext.EndlessBoard(e, e.shapedRunsFromBestStage - 1, seed).Family);
                var early = GameContext.EndlessBoard(e, e.shapedRunsFromBestStage, seed).Family;
                Assert.IsFalse(split.Contains(early) || advanced.Contains(early), "split boards wait for a better best stage");
                var mid = GameContext.EndlessBoard(e, e.splitRunsFromBestStage, seed).Family;
                Assert.IsFalse(advanced.Contains(mid));
                foreach (int best in new[] { e.shapedRunsFromBestStage, e.splitRunsFromBestStage, e.advancedRunsFromBestStage, 30 })
                {
                    var shape = GameContext.EndlessBoard(e, best, seed);
                    Assert.IsTrue(BoardShapes.IsFair(shape.Layout, out var error), shape.Family + ": " + error);
                    Assert.AreEqual(shape.Layout.ToRows(), GameContext.EndlessBoard(e, best, seed).Layout.ToRows(), "the same seed, the same board");
                }
            }
            var late = Enumerable.Range(1, 200).Select(k => GameContext.EndlessBoard(e, 20, (ulong)k).Family).ToList();
            int plain = late.Count(f => f == BoardFamily.Rectangle);
            TestContext.WriteLine("best stage 20: " + string.Join(", ", late.GroupBy(f => f).Select(g => g.Key + " " + g.Count())));
            Assert.That(plain, Is.InRange(40, 110), "about a third of runs stay plain");
            Assert.GreaterOrEqual(late.Distinct().Count(), 10, "many shapes");
            Assert.IsTrue(late.Any(f => advanced.Contains(f)) && late.Any(f => split.Contains(f)));
        }

        [Test]
        public void ShapedEndlessRunsPlayCleanlyThroughTheirStages()
        {
            var cfg = GameConfigData.CreateDefault();
            var ctx = TestUtil.Context();
            ctx.Data.Endless.BestStage = 20;
            int shaped = 0;
            for (int i = 0; i < 10; i++)
            {
                var setup = ctx.CreateEndlessRun();
                var layout = setup.Level.Layout;
                if (BoardSilhouette.IsShaped(layout)) shaped++;
                var s = new PuzzleSession(setup);
                s.Start();
                var rng = new Rng((ulong)(i + 77));
                var o = new MoveOutcome();
                var finder = new GroupFinder();
                var groups = new List<GroupInfo>();
                while (s.AcceptsMoves && s.MovesMade < 120)
                {
                    if (!Bots.Choose(s.Board, BotKind.Mixed, rng, finder, groups, out var pick)) break;
                    if (!s.TryPlay(pick, o, PlannerBot.PrismColour(s))) break;
                    if (!o.RecoveryFailed) TestUtil.AssertBoardInvariants(s.Board, layout);
                }
                if (s.FinalePending) s.RunFinale(o);
                Assert.AreEqual(s.Score, s.Breakdown.Total);
            }
            Assert.Greater(shaped, 2, "a player with a good best stage meets shaped runs");
        }

        // ================================================================== Old config assets

        [Test]
        public void AnOldEndlessConfigAssetIsBroughtUpToDateButCustomValuesStay()
        {
            var old = new EndlessConfig
            {
                stageScores = (int[])EndlessTuning.StageScoresBefore21.Clone(),
                stageScoreStepAfter = EndlessTuning.StageStepBefore21,
                stageColours = (int[])EndlessTuning.StageColoursBefore21.Clone(),
                journey = EndlessTuning.JourneyBefore33(),
            };
            old.journey.rocketsAt = 2; // a saved asset has no rocketsAt: it reads as the new default
            old.journey.breatherBonusMoves = 7;
            var changed = EndlessTuning.Upgrade(old);
            CollectionAssert.AreEquivalent(new[] { "endless.stageScores", "endless.stageScoreStepAfter", "endless.stageColours", "endless.journey" }, changed);
            var d = new EndlessConfig();
            CollectionAssert.AreEqual(d.stageScores, old.stageScores);
            CollectionAssert.AreEqual(d.stageColours, old.stageColours);
            Assert.AreEqual(2, old.journey.rocketsAt);
            Assert.AreEqual(7, old.journey.specialsAt, "bombs at stage 7");
            Assert.AreEqual(7, old.journey.breatherBonusMoves, "a value the designer changed is kept");
            Assert.IsEmpty(EndlessTuning.Upgrade(old), "upgrading twice changes nothing");

            var current = new EndlessConfig();
            Assert.IsEmpty(EndlessTuning.Upgrade(current), "an up-to-date asset is left alone");
            var custom = new EndlessConfig { stageScores = new[] { 0, 999, 5000 } };
            custom.journey.chainsAt = 9;
            Assert.IsEmpty(EndlessTuning.Upgrade(custom), "custom tuning is never overwritten");
            Assert.AreEqual(9, custom.journey.chainsAt);
        }

        // ================================================================== Shape library and panel

        [Test]
        public void TheNewFamiliesAreInTheLibraryAndShapesCanBeAddedInTheConfig()
        {
            foreach (var f in new[] { BoardFamily.Corners, BoardFamily.TShape, BoardFamily.Cross, BoardFamily.Diamond })
            {
                Assert.GreaterOrEqual(BoardShapes.VariantCount(f), 2, f + " has variants");
                for (int v = 0; v < BoardShapes.VariantCount(f); v++)
                {
                    var l = BoardShapes.Build(f, v, 0, new Rng(1)).Layout;
                    Assert.IsTrue(BoardShapes.IsFair(l, out var error), f + " " + v + ": " + error);
                    Assert.IsTrue(BoardSilhouette.IsShaped(l), f + " has gaps");
                }
            }
            var extra = new BoardShapeTemplate { family = BoardFamily.Cross, rows = new[] { "xx....xx", "xx....xx", "........", "........", "........", "........", "xx....xx", "xx....xx" } };
            var deadEnd = new BoardShapeTemplate { family = BoardFamily.Cross, rows = new[] { "x.xxxxxx", "........", "........", "........", "........", "........" } };
            var lib = new BoardShapeLibrary(new[] { extra, deadEnd });
            Assert.AreEqual(BoardShapes.VariantCount(BoardFamily.Cross) + 1, lib.VariantCount(BoardFamily.Cross), "the fair template joins the family; the dead end is refused");
            int last = lib.VariantCount(BoardFamily.Cross) - 1;
            CollectionAssert.AreEqual(extra.rows, lib.Build(BoardFamily.Cross, last, 0, new Rng(1)).Layout.ToRows());

            // A config template reaches the campaign and the challenges through the progression's library.
            var cfg = GameConfigData.CreateDefault();
            cfg.Campaign.shapes.extraShapes = new[] { extra };
            var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            Assert.AreEqual(lib.VariantCount(BoardFamily.Cross), catalog.Progression.Library.VariantCount(BoardFamily.Cross));
        }

        [Test]
        public void AShapedBoardsPanelCoversItsCellsAndLeavesItsGapsOpen()
        {
            foreach (BoardFamily f in Enum.GetValues(typeof(BoardFamily)))
                for (int v = 0; v < BoardShapes.VariantCount(f); v++)
                {
                    var l = BoardShapes.Build(f, v, 2, new Rng((ulong)(v + 1))).Layout;
                    var s = BoardSilhouette.ForLayout(l, 40, 4, 8, 3, 10);
                    for (int r = 0; r < l.Height; r++)
                        for (int c = 0; c < l.Width; c++)
                        {
                            int alpha = s.FillAt(c + 0.5f, r + 0.5f);
                            if (l.TypeAt(c, r) == CellType.Void) Assert.AreEqual(0, alpha, f + " " + v + ": the gap at " + c + "," + r + " shows the background");
                            else Assert.AreEqual(255, alpha, f + " " + v + ": the cell at " + c + "," + r + " is on the panel");
                        }
                    // The panel reaches a little past the outer cells, with rounded outer corners.
                    Assert.AreEqual(BoardSilhouette.IsShaped(l), l.ToRows().Any(row => row.Contains('x')));
                }
            var plain = BoardSilhouette.ForLayout(LevelLayout.Open(8, 9), 40, 4, 8, 3, 10);
            Assert.AreEqual(255, plain.FillAt(-0.05f, 4.5f), "the panel reaches past the edge");
            Assert.AreEqual(0, plain.FillAt(-0.2f, -0.2f), "and its corners are rounded");
            int below = (plain.Margin - 8) * plain.Width + plain.Width / 2;
            Assert.AreEqual(0, plain.Fill[below]);
            Assert.That((int)plain.Shadow[below], Is.InRange(1, 254), "with a soft shadow fading out around it");
        }

        [Test]
        public void EveryNewBoardComesBackTwiceSoonAfterItsIntroduction()
        {
            var cfg = GameConfigData.CreateDefault();
            var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            var p = catalog.Progression;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int k = 0; k < 20; k++)
                for (int n = 121; n <= 600; n++) p.IsShapeReprise(n, out _);
            Assert.Less(sw.ElapsedMilliseconds, 2000, "a table lookup: worked out recursively on every call, it took seconds a level from 311 on");
            foreach (BoardFamily f in Enum.GetValues(typeof(BoardFamily)))
            {
                if (f == BoardFamily.Rectangle || f == BoardFamily.Obstructed) continue;
                int start = cfg.Campaign.shapes.FromLevel(f);
                Assert.AreEqual(BoardShapes.IntroNote(f), catalog.Get(start).Note, f + ": its introduction keeps its board");
                var returns = Enumerable.Range(start + 1, 20).Where(n => p.IsShapeReprise(n, out var g) && g == f).ToList();
                Assert.AreEqual(2, returns.Count, f + " comes back twice");
                Assert.That(returns[0], Is.InRange(start + 4, start + 7), f.ToString());
                Assert.That(returns[1], Is.InRange(start + 13, start + 16), f.ToString());
                foreach (int n in returns)
                {
                    Assert.AreNotEqual(6, n % 10, "never on a breather");
                    Assert.IsNull(CampaignDesign.ShowcaseFor(n, cfg.Campaign.mechanics), "never on an obstacle showcase");
                    StringAssert.Contains(BoardShapes.NameOf(f), catalog.Get(n).Note, "level " + n + " is played on " + f);
                }
            }
        }

        // ================================================================== Power-ups on the new shapes

        [Test]
        public void PowerUpsRespectTheCellsOfCrossAndDiamondBoards()
        {
            // A cross: the blast at the corner of an arm clears only real cells.
            var cross = Play.Level(new[] { "x012x", "01201", "12012", "x201x" }, null);
            var o = Play.Use(cross, Play.At(PowerUpId.Blast, 1, 3));
            Assert.AreEqual(5, o.Cleared.Count, "3 x 3 around the arm's corner holds five real cells");
            Assert.AreEqual(CellType.Void, cross.Board[0, 3].Type);
            Assert.AreEqual(CellType.Void, cross.Board[0, 0].Type);
            Assert.AreNotEqual(PowerUpFailure.None, Play.Refused(cross, Play.At(PowerUpId.TileBreaker, 0, 3)), "a gap can't be broken");
            Assert.AreNotEqual(PowerUpFailure.None, Play.Refused(cross, Play.At(PowerUpId.Blast, 4, 0)), "nor blasted");
            Assert.AreNotEqual(PowerUpFailure.None, Play.Refused(cross, new PowerUpRequest { Id = PowerUpId.ColourShift, A = new GridPos(4, 3), Colour = 1 }), "nor recoloured");
            Assert.AreNotEqual(PowerUpFailure.None, Play.Refused(cross, Play.Swap(0, 2, 0, 3)), "nothing is swapped into a gap");

            // Swap never reaches across a gap between two sections.
            var towers = Play.Level(new[] { "01x01", "10x10", "01x01" }, null);
            Assert.AreNotEqual(PowerUpFailure.None, Play.Refused(towers, Play.Swap(1, 1, 3, 1)), "the cells either side of a gap are not neighbours");
            int colour = towers.Board[1, 1].Colour;
            int count = TestUtil.CountColour(towers.Board, colour);
            o = Play.Use(towers, Play.At(PowerUpId.ColourBurst, 1, 1));
            Assert.AreEqual(count, o.Cleared.Count, "Colour Burst clears its colour in both sections");
            Assert.IsTrue(o.Cleared.Any(c => c.Pos.Col < 2) && o.Cleared.Any(c => c.Pos.Col > 2));
            TestUtil.AssertBoardInvariants(towers.Board, towers.Level.Layout);
            TestUtil.AssertBoardInvariants(cross.Board, cross.Level.Layout);
        }

        // ================================================================== Advanced Daily and Weekly

        static ChallengeFactory Factory(out GameConfigData cfg)
        {
            cfg = GameConfigData.CreateDefault();
            return new ChallengeFactory(cfg.Daily, cfg.Rush, cfg.Scoring, new CampaignCatalog(cfg.Campaign, cfg.Scoring).Progression);
        }

        static string Signature(LevelSpec s) =>
            string.Join("/", s.Layout.ToRows()) + "|" + LevelCodec.Encode(s) + "|" + s.Moves + "|" + s.Target + "|" + s.Star2 + "|" + s.Star3 + "|" + s.Note;

        [Test]
        public void PlayersBeforeTheAdvancedChallengesKeepExactlyTheOriginalOnes()
        {
            var f = Factory(out var cfg);
            var original = new ChallengeFactory(cfg.Daily, cfg.Rush, cfg.Scoring);
            foreach (int level in new[] { 1, 45, 120 })
            {
                var unlocked = Mechanics.FromCampaign(level, cfg.Campaign.mechanics);
                for (int d = 0; d < 4; d++)
                {
                    string day = ChallengeCalendar.DayKey(TestUtil.Today.AddDays(d));
                    Assert.AreEqual(Signature(original.Daily(day, unlocked)), Signature(f.Daily(day, unlocked, level)), "level " + level + ", " + day);
                }
                string week = ChallengeCalendar.WeekKey(TestUtil.Today);
                Assert.AreEqual(Signature(original.Weekly(week, unlocked)), Signature(f.Weekly(week, unlocked, level)), "level " + level);
            }
            // Turned off in the config, everyone keeps the originals.
            cfg.Daily.advancedFromLevel = 0;
            cfg.Rush.advancedFromLevel = 0;
            var off = new ChallengeFactory(cfg.Daily, cfg.Rush, cfg.Scoring, new CampaignCatalog(cfg.Campaign, cfg.Scoring).Progression);
            Assert.AreEqual(Signature(original.Daily("2026-10-25", MechanicSet.All)), Signature(off.Daily("2026-10-25", MechanicSet.All, 400)));
        }

        [Test]
        public void AdvancedDailyChallengesUseShapesThePlayerHasMetAndCombineObjectives()
        {
            var f = Factory(out var cfg);
            var schedule = cfg.Campaign.shapes;
            foreach (int level in new[] { 150, 260, 400 })
            {
                int advanced = 0, shaped = 0;
                var mixes = new HashSet<string>();
                for (int d = 0; d < 12; d++)
                {
                    string day = ChallengeCalendar.DayKey(TestUtil.Today.AddDays(d));
                    var spec = f.Daily(day, MechanicSet.All, level);
                    Assert.AreEqual("daily:" + day, spec.Id, "records and rewards stay keyed by the date");
                    Assert.IsTrue(spec.FixedSeed, "the same puzzle and refills for everyone");
                    Assert.IsTrue(BoardShapes.IsFair(spec.Layout, out var error), day + ": " + error);
                    Assert.Less(spec.Target, spec.Star2);
                    Assert.Less(spec.Star2, spec.Star3);
                    if (spec.Goals == null || spec.Goals.Length < 3) continue; // a day that fell back to the original daily
                    advanced++;
                    Assert.AreEqual(GoalKind.Score, spec.Goals[0].kind, "the target comes first");
                    Assert.AreEqual(spec.Target, spec.Goals[0].amount);
                    Assert.That(spec.Moves, Is.InRange(cfg.Daily.moves, cfg.Daily.advancedMaxMoves));
                    mixes.Add(string.Join("+", spec.Goals.Skip(1).Select(g => g.kind)));
                    if (BoardSilhouette.IsShaped(spec.Layout) || spec.Layout.StoneCount > 0 || !string.IsNullOrEmpty(spec.BoardName)) shaped++;
                    // Only shapes the player has already played.
                    foreach (BoardFamily fam in Enum.GetValues(typeof(BoardFamily)))
                        if (!string.IsNullOrEmpty(spec.BoardName) && spec.BoardName == BoardShapes.NameOf(fam))
                            Assert.Less(schedule.FromLevel(fam), level, day + ": " + fam + " before the player has met it");
                }
                TestContext.WriteLine("level " + level + ": advanced " + advanced + "/12, shaped " + shaped + ", objective pairs " + string.Join(", ", mixes));
                Assert.GreaterOrEqual(advanced, 10, "nearly every day is an advanced puzzle");
                Assert.GreaterOrEqual(mixes.Count, 5, "with varied combinations");
                Assert.GreaterOrEqual(shaped, 8, "mostly on shaped boards (gaps, stones, tall or wide)");
            }
        }

        [Test]
        public void AdvancedChallengesAreTheSameForEveryoneAtTheSameStage()
        {
            var a = Factory(out _);
            var b = Factory(out _);
            foreach (string day in new[] { "2026-10-25", "2026-11-03" })
            {
                Assert.AreEqual(Signature(a.Daily(day, MechanicSet.All, 300)), Signature(b.Daily(day, MechanicSet.All, 300)));
                Assert.AreEqual(Signature(a.Daily(day, MechanicSet.All, 300)), Signature(b.Daily(day, MechanicSet.All, 305)), "players who have met the same shapes share the puzzle");
            }
            Assert.AreEqual(Signature(a.Weekly("2026-W44", MechanicSet.All, 300)), Signature(b.Weekly("2026-W44", MechanicSet.All, 300)));
            // Built in the background and asked for at the same time: one puzzle, built once.
            var c = Factory(out _);
            var specs = new LevelSpec[4];
            System.Threading.Tasks.Parallel.For(0, 4, i => specs[i] = c.Daily("2026-12-01", MechanicSet.All, 300));
            Assert.IsTrue(specs.All(s => ReferenceEquals(s, specs[0])));
        }

        [Test]
        public void AdvancedWeeklyBoardsAreSplitOrIrregularWithObjectivesWorthBonusPoints()
        {
            var f = Factory(out var cfg);
            int shaped = 0, withObjectives = 0;
            for (int w = 0; w < 10; w++)
            {
                string week = ChallengeCalendar.WeekKey(TestUtil.Today.AddDays(7 * w));
                var spec = f.Weekly(week, MechanicSet.All, 400);
                Assert.AreEqual("weekly:" + week, spec.Id, "records and medals stay keyed by the week");
                Assert.AreEqual(cfg.Rush.seconds, spec.TimeLimitSeconds);
                Assert.IsTrue(BoardShapes.IsFair(spec.Layout, out var error), week + ": " + error);
                Assert.Less(spec.Target, spec.Star2);
                Assert.Less(spec.Star2, spec.Star3);
                if (BoardSilhouette.IsShaped(spec.Layout) || spec.Layout.StoneCount > 0) shaped++;
                if (spec.Goals != null && spec.Goals.Length > 0)
                {
                    withObjectives++;
                    Assert.LessOrEqual(spec.Goals.Length, 3, "the goals panel shows three");
                    Assert.AreEqual(cfg.Rush.objectiveBonus, spec.ObjectiveBonus);
                    Assert.IsFalse(spec.Goals.Any(g => g.kind == GoalKind.Score), "medals are the score targets");
                }
            }
            Assert.AreEqual(10, shaped, "every advanced week is on a split, irregular or stone board");
            Assert.GreaterOrEqual(withObjectives, 9);
        }

        [Test]
        public void EachWeeklyObjectivePaysItsBonusOnceAndMedalsStayScoreBased()
        {
            var setup = TestUtil.Setup(GameMode.WeeklyRush, 0, 1000, TileColours.Max, new[] { "1100", "2233", "0123" }, null,
                GoalSpec.Collect(1, 2), GoalSpec.Collect(2, 6));
            setup.Level.ObjectiveBonus = 1000;
            var s = new PuzzleSession(setup);
            s.Start();
            s.StartTimer();
            var o = Play.Tap(s, 0, 2);
            Assert.AreEqual(1000, o.ObjectiveBonus, "the first objective is complete");
            Assert.AreEqual(1, s.ObjectivesDone);
            Assert.AreEqual(1000, s.Breakdown.Objectives);
            Assert.AreEqual(s.Score, s.Breakdown.Total);
            int score = s.Score;
            // More of the same colour: the objective stays done and pays nothing more.
            var more = new MoveOutcome();
            for (int k = 0; k < 6 && s.AcceptsMoves; k++)
            {
                if (!Bots.Choose(s.Board, BotKind.Greedy, new Rng((ulong)k), new GroupFinder(), new List<GroupInfo>(), out var pick)) break;
                s.TryPlay(pick, more, -1);
                Assert.LessOrEqual(s.Breakdown.Objectives, 2000, "each objective pays once");
            }
            Assert.AreEqual(s.Score, s.Breakdown.Total);
            Assert.GreaterOrEqual(s.Score, score);
            s.Tick(1000);
            if (s.FinalePending) s.RunFinale(new MoveOutcome());
            Assert.AreEqual(SessionState.Finished, s.State, "objectives never end the run early");
            Assert.AreEqual(StarRating.Compute(s.Score, 1000, 2000, 3000), s.Stars, "medals come from the score, finished objectives or not");
        }

        [Test]
        public void TheGameUsesThePlayersCampaignLevelForTheirChallenges()
        {
            var ctx = TestUtil.Context();
            var plainDaily = ctx.DailyFor(ctx.TodayKey);
            var plainWeekly = ctx.WeeklyFor(ctx.ThisWeekKey);
            ctx.Data.HighestUnlocked = 320;
            var daily = ctx.CreateDailyRun().Level;
            var weekly = ctx.CreateWeeklyRun().Level;
            Assert.AreEqual(plainDaily.Id, daily.Id, "the day's record and rewards stay the same");
            Assert.AreEqual(plainWeekly.Id, weekly.Id);
            Assert.AreNotEqual(Signature(plainDaily), Signature(daily), "an advanced puzzle for an advanced player");
            Assert.AreNotEqual(Signature(plainWeekly), Signature(weekly));
            Assert.AreSame(daily, ctx.DailyFor(ctx.TodayKey), "the info popup and the run show the same puzzle");

            // Playing it records the day's best and pays the day's reward once, as before.
            var s = new PuzzleSession(ctx.CreateDailyRun());
            s.Start();
            ctx.BeginRun(s);
            Bots.PlayOut(s, BotKind.Planner, null);
            var r = ctx.FinishRun(s);
            Assert.AreEqual(ctx.TodayKey, r.PeriodKey);
            Assert.AreEqual(s.Score, ctx.TodayRecord.BestScore);
            Assert.AreEqual(1, ctx.TodayRecord.Plays);
            ctx.PrewarmChallenges();
        }
    }
}
