using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.1: when an Endless run ends, every special tile still on the board goes off before the final score is
    /// taken (the same end-of-level finale as the campaign, docs/MECHANICS.md §4.4), and the run then really ends.
    /// </summary>
    public class EndlessFinaleTests
    {
        static PuzzleSession Endless(string[] rowsTopFirst, int moves = 1, string[] obstacles = null)
        {
            var setup = TestUtil.Setup(GameMode.Endless, moves, 100000, TileColours.Max, rowsTopFirst, obstacles);
            setup.Level.Specials = SpecialTier.Prisms;
            var s = new PuzzleSession(setup);
            s.Start();
            return s;
        }

        [Test]
        public void ATreasureCollectedByTheFinaleDoesNotKeepTheRunGoing()
        {
            // The last move leaves a column rocket under a treasure: the finale clears the column, the treasure falls to
            // the bottom and is collected. Before 3.1 that paid Endless bonus moves, so the run carried on with the finale
            // already spent, and specials made later never went off.
            var s = Endless(new[] { "33$12", "40|01", "12345" });
            var o = Play.Tap(s, 0, 2);
            Assert.IsTrue(o.FinalePending);
            Assert.IsTrue(s.RunFinale(o));
            Assert.Greater(o.Treasures.Count, 0, "the finale collected the treasure");
            Assert.AreEqual(SessionState.Finished, s.State, "the run is over");
            Assert.AreEqual(0, o.BonusMoves, "no bonus moves once the run is ending");
            Assert.IsFalse(s.AcceptsMoves);
        }

        /// <summary>Plays the last move (a pair at the top left), leaving the finale to run.</summary>
        static PuzzleSession LastMove(string[] rows, out int scoreBefore, out int specialsBefore)
        {
            var s = Endless(rows);
            specialsBefore = s.Board.CountType(CellType.Special);
            var o = Play.Tap(s, 0, 2);
            scoreBefore = s.Score;
            Assert.AreEqual(0, s.MovesLeft);
            return s;
        }

        /// <summary>The finale ran: every special went off once, the run is over and the score adds up.</summary>
        static MoveOutcome RunAndCheck(PuzzleSession s, int scoreBefore, int specialsBefore)
        {
            Assert.IsTrue(s.FinalePending, "out of moves with specials left: the finale waits to run");
            Assert.IsFalse(s.IsOver, "no result yet");
            Assert.IsFalse(s.AcceptsMoves, "and no more moves");
            var o = new MoveOutcome();
            Assert.IsTrue(s.RunFinale(o));
            Assert.AreEqual(OutcomeKind.Finale, o.Kind);
            Assert.AreEqual(SessionState.Finished, s.State, "the run is over");
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
            Assert.AreEqual(0, s.Board.CountType(CellType.Special), "nothing is left on the board");
            var fired = o.SpecialsFired.Select(f => f.TileId).ToList();
            Assert.AreEqual(fired.Count, fired.Distinct().Count(), "each special goes off once");
            Assert.GreaterOrEqual(fired.Count + o.ComboMembers.Count, specialsBefore, "every special left went off");
            Assert.Greater(s.Breakdown.Finale, 0, "and scored");
            Assert.AreEqual(scoreBefore + s.Breakdown.Finale, s.Score, "the finale's points are added once");
            Assert.AreEqual(s.Breakdown.Total, s.FinalScore, "the breakdown is the whole score");
            Assert.AreEqual(s.Breakdown.Finale, s.FinaleBreakdown.Total);
            Assert.IsFalse(s.RunFinale(new MoveOutcome()), "it can't run twice");
            Assert.AreEqual(scoreBefore + s.Breakdown.Finale, s.Score);
            return o;
        }

        [Test]
        public void NoSpecialsLeftEndsTheRunAtOnce()
        {
            var s = Endless(new[] { "33212", "40101", "12345" });
            var o = Play.Tap(s, 0, 2);
            Assert.IsFalse(o.FinalePending);
            Assert.IsTrue(o.Ended);
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(0, s.Breakdown.Finale);
            Assert.IsFalse(s.RunFinale(new MoveOutcome()));
        }

        [Test]
        public void ARocketLeftOnTheBoardGoesOff()
        {
            var s = LastMove(new[] { "33212", "-0102", "12345" }, out int before, out int specials);
            var o = RunAndCheck(s, before, specials);
            Assert.AreEqual(SpecialKind.RocketRow, o.SpecialsFired[0].Kind);
        }

        [Test]
        public void SeveralRocketsAllGoOff()
        {
            var s = LastMove(new[] { "33212", "-0102", "1234|" }, out int before, out int specials);
            Assert.AreEqual(2, specials);
            var o = RunAndCheck(s, before, specials);
            Assert.AreEqual(2, o.SpecialsFired.Count, "two separate rockets, two blasts");
        }

        [Test]
        public void ABombLeftOnTheBoardGoesOff()
        {
            var s = LastMove(new[] { "33212", "*0102", "12345" }, out int before, out int specials);
            var o = RunAndCheck(s, before, specials);
            Assert.AreEqual(SpecialKind.Bomb, o.SpecialsFired[0].Kind);
        }

        [Test]
        public void APrismLeftOnTheBoardGoesOff()
        {
            var s = LastMove(new[] { "33212", "@0102", "12345" }, out int before, out int specials);
            var o = RunAndCheck(s, before, specials);
            Assert.AreEqual(SpecialKind.Prism, o.SpecialsFired[0].Kind);
            Assert.GreaterOrEqual(o.SpecialsFired[0].Colour, 0, "it takes the most common colour");
        }

        [Test]
        public void DifferentSpecialsAllGoOff()
        {
            var s = LastMove(new[] { "33212", "-0102", "12@4|" }, out int before, out int specials);
            Assert.AreEqual(3, specials);
            var o = RunAndCheck(s, before, specials);
            CollectionAssert.IsSupersetOf(o.SpecialsFired.Select(f => f.Kind).ToList(), new[] { SpecialKind.RocketRow, SpecialKind.Prism, SpecialKind.RocketColumn });
        }

        [Test]
        public void ConnectedSpecialsGoOffTogetherAsACombination()
        {
            var s = LastMove(new[] { "33212", "-*102", "12345" }, out int before, out int specials);
            var o = RunAndCheck(s, before, specials);
            Assert.AreEqual(1, o.SpecialsFired.Count, "one combination");
            Assert.AreEqual(2, o.SpecialsFired[0].ComboSize);
            Assert.Greater(s.FinaleBreakdown.ComboBonus, 0);
        }

        [Test]
        public void ASpecialSetsOffAnotherInTurn()
        {
            // The row rocket's line reaches the bomb at the far end: the bomb goes off as a chain reaction, once.
            var s = LastMove(new[] { "33212", "-010*", "12345" }, out int before, out int specials);
            var o = RunAndCheck(s, before, specials);
            Assert.AreEqual(2, o.SpecialsFired.Count);
            Assert.AreEqual(SpecialKind.RocketRow, o.SpecialsFired[0].Kind);
            Assert.AreEqual(SpecialKind.Bomb, o.SpecialsFired[1].Kind);
        }

        // ------------------------------------------------------------------ The result uses the final score

        /// <summary>The score after the last move and after the finale, from an identical twin of the run.</summary>
        static void Scores(string[] rows, out int before, out int after)
        {
            var twin = LastMove(rows, out before, out _);
            twin.RunFinale(new MoveOutcome());
            after = twin.Score;
            Assert.Greater(after, before);
        }

        static readonly string[] Board = { "33212", "-*102", "12@45" };

        [Test]
        public void TheFinaleCanMakeANewPersonalBest()
        {
            Scores(Board, out int before, out int after);
            var ctx = TestUtil.Context();
            ctx.Data.Endless.BestScore = before + 1;
            var s = LastMove(Board, out _, out _);
            ctx.BeginRun(s);
            // The result is recorded only after the specials have gone off (the game screen animates them first; FinishRun
            // runs them itself if they are still waiting).
            var r = ctx.FinishRun(s);
            Assert.AreEqual(after, r.FinalScore);
            Assert.IsTrue(r.NewBest, "beaten only thanks to the finale");
            Assert.AreEqual(after, ctx.Data.Endless.BestScore);
            Assert.AreSame(r, ctx.FinishRun(s), "recorded once");
        }

        [Test]
        public void AFinaleBelowTheBestLeavesItAlone()
        {
            Scores(Board, out _, out int after);
            var ctx = TestUtil.Context();
            ctx.Data.Endless.BestScore = after + 500;
            var s = LastMove(Board, out _, out _);
            s.RunFinale(new MoveOutcome());
            var r = ctx.FinishRun(s);
            Assert.IsFalse(r.NewBest);
            Assert.AreEqual(after + 500, ctx.Data.Endless.BestScore);
            Assert.AreEqual(after + 500, r.PreviousBest);
        }

        [Test]
        public void TheFinaleCountsTowardsEndlessMilestonesAndAchievements()
        {
            Scores(Board, out int before, out int after);
            var config = GameConfigData.CreateDefault();
            config.Endless.milestoneScores = new[] { before + 1 };
            config.Endless.milestoneRewards = new[] { 7 };
            config.Achievements = new[]
            {
                new AchievementDefinition { id = "endless_test", title = "T", description = "d", category = AchievementCategory.Endless,
                    stat = AchievementStat.EndlessBestScore, target = before + 1, reward = new AchievementReward { coins = 5 } },
            };
            var ctx = new GameContext(config, new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            ctx.Load();
            int coins = ctx.Wallet.Balance;
            var s = LastMove(Board, out _, out _);
            var o = new MoveOutcome();
            s.RunFinale(o);
            var r = ctx.FinishRun(s);
            Assert.AreEqual(after, ctx.Data.Endless.BestScore);
            Assert.IsTrue(ctx.Rewards.IsClaimed(RewardKeys.EndlessMilestone(before + 1)), "the milestone was crossed by the finale");
            Assert.AreEqual(coins + 7, ctx.Wallet.Balance);
            CollectionAssert.AreEqual(new[] { "endless_test" }, r.NewAchievements.Select(d => d.id).ToList());
            ctx.FinishRun(s);
            Assert.AreEqual(coins + 7, ctx.Wallet.Balance, "and paid once");
        }

        [Test]
        public void OwnedPowerUpsAreNeverUsedByTheFinale()
        {
            var ctx = TestUtil.Context();
            ctx.Inventory.AddOwned(PowerUpId.Blast, 2);
            ctx.Inventory.AddOwned(PowerUpId.ColourBurst, 1);
            var s = LastMove(Board, out int before, out int specials);
            ctx.BeginRun(s);
            RunAndCheck(s, before, specials);
            ctx.FinishRun(s);
            Assert.AreEqual(0, s.PowerUpsUsed);
            Assert.AreEqual(2, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.AreEqual(1, ctx.Inventory.Owned(PowerUpId.ColourBurst));
        }

        [Test]
        public void LeavingWhileTheFinaleWaitsStillRecordsTheWholeScore()
        {
            // The moves are gone and the specials are about to go off when the player leaves: the run is finished, not
            // abandoned, and its score includes them (the game screen's CloseRun does exactly this).
            Scores(Board, out _, out int after);
            var ctx = TestUtil.Context();
            var s = LastMove(Board, out _, out _);
            ctx.BeginRun(s);
            Assert.IsTrue(s.FinalePending);
            s.RunFinale(new MoveOutcome());
            Assert.IsTrue(s.IsOver);
            ctx.FinishRun(s);
            Assert.AreEqual(after, ctx.Data.Endless.BestScore);
        }

        [Test]
        public void RealEndlessRunsNeverEndWithSpecialsLeft()
        {
            // Bots play whole Endless runs with every mechanic unlocked, running the finale whenever it waits, as the game
            // screen does. Every finale ends its run and no run ends with a special still on the board.
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 400;
            ctx.Data.Endless.Level = 40;
            var finder = new GroupFinder();
            var groups = new System.Collections.Generic.List<GroupInfo>();
            int finales = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = (ulong)seed * 7919;
                var s = new PuzzleSession(setup);
                s.Start();
                var rng = new Rng((ulong)seed);
                var o = new MoveOutcome();
                for (int step = 0; step < 3000 && !s.IsOver; step++)
                {
                    if (s.FinalePending)
                    {
                        int before = s.Score;
                        Assert.IsTrue(s.RunFinale(o));
                        finales++;
                        Assert.IsTrue(s.IsOver, "seed " + seed + ": the finale ends the run");
                        Assert.GreaterOrEqual(s.Score, before);
                        break;
                    }
                    GridPos p;
                    if (!Bots.Choose(s.Board, BotKind.Mixed, rng, finder, groups, out p)) break;
                    s.TryPlay(p, o);
                }
                Assert.IsTrue(s.IsOver, "seed " + seed);
                Assert.AreEqual(0, s.Board.CountType(CellType.Special), "seed " + seed + ": no special left unfired");
                Assert.AreEqual(s.Breakdown.Total, s.FinalScore, "seed " + seed);
            }
            Assert.Greater(finales, 0, "some runs ended with specials to set off");
        }
    }
}
