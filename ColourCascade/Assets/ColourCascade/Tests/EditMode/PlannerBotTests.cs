using System.Collections.Generic;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    public class PlannerBotTests
    {
        [Test]
        public void ItWorksTowardsTheGoalRatherThanTheBiggestGroup()
        {
            string[] rows = { "11112", "00333" };
            var scoreOnly = Play.Level(rows, null, 20);
            Assert.IsTrue(new PlannerBot().Choose(scoreOnly, out var pick));
            Assert.AreEqual(1, scoreOnly.Board[pick].Colour, "with only a score goal it takes the big group");

            var collect = Play.Level(rows, null, 20, GoalSpec.Collect(0, 5));
            Assert.IsTrue(new PlannerBot().Choose(collect, out pick));
            Assert.AreEqual(0, collect.Board[pick].Colour, "it collects the goal colour");
        }

        [Test]
        public void ItBreaksIceAndChainsTheGoalNeeds()
        {
            // The 2s touch the frozen tile; the 1s do not. Same size, so only the goal separates them.
            var ice = Play.Level(new[] { "11223", "45045" }, new[] { ".....", "..i.." }, 20, GoalSpec.FreeIce(1));
            Assert.IsTrue(new PlannerBot().Choose(ice, out var pick));
            Assert.AreEqual(2, ice.Board[pick].Colour);

            // The 3s include a chained tile; the 1s do not.
            var chain = Play.Level(new[] { "11433", "52025" }, new[] { "....1", "....." }, 20, GoalSpec.BreakChains(1));
            Assert.IsTrue(new PlannerBot().Choose(chain, out pick));
            Assert.AreEqual(3, chain.Board[pick].Colour);
        }

        [Test]
        public void ItLooksOneMoveAhead()
        {
            // Clearing the 2s drops the 1s above onto the 1s below (a group of 6); clearing the 3s leads nowhere.
            var s = Play.Level(new[] { "111", "222", "111", "335" }, null, 20);
            Assert.IsTrue(new PlannerBot().Choose(s, out var pick));
            Assert.AreEqual(2, s.Board[pick].Colour);
        }

        [Test]
        public void ItIsDeterministicAndOnlyUsedThroughTheSession()
        {
            var ctx = TestUtil.Context();
            for (int n = 1; n <= 10; n++)
            {
                int a = PlayLevel(ctx, n, BotKind.Planner, 1, out _);
                int b = PlayLevel(ctx, n, BotKind.Planner, 1, out _);
                Assert.AreEqual(a, b, "level " + n);
            }
            Assert.Throws<System.ArgumentException>(() =>
                Bots.Choose(Board.FromRows("00"), BotKind.Planner, new Rng(1), new GroupFinder(), new List<GroupInfo>(), out _));
        }

        [Test]
        public void ItPlaysBetterThanTheCasualBot()
        {
            var ctx = TestUtil.Context();
            int plannerWins = 0, casualWins = 0;
            long plannerScore = 0, casualScore = 0;
            for (int n = 1; n <= 40; n++)
                for (ulong seed = 1; seed <= 3; seed++)
                {
                    plannerScore += PlayLevel(ctx, n, BotKind.Planner, seed, out bool pw);
                    casualScore += PlayLevel(ctx, n, BotKind.Casual, seed, out bool cw);
                    if (pw) plannerWins++;
                    if (cw) casualWins++;
                }
            TestContext.WriteLine("wins: planner " + plannerWins + "/120, casual " + casualWins + "/120");
            Assert.GreaterOrEqual(plannerWins, casualWins);
            Assert.Greater(plannerScore, casualScore);
        }

        [Test]
        public void ItCompletesObstacleGoalsMoreOftenThanTheCasualBot()
        {
            int plannerWins = 0, casualWins = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                if (PlayObstacleLevel(BotKind.Planner, seed)) plannerWins++;
                if (PlayObstacleLevel(BotKind.Casual, seed)) casualWins++;
            }
            TestContext.WriteLine("obstacle level wins: planner " + plannerWins + "/30, casual " + casualWins + "/30");
            Assert.Greater(plannerWins, casualWins);
        }

        static int PlayLevel(GameContext ctx, int level, BotKind kind, ulong seed, out bool won)
        {
            var spec = ctx.Catalog.Get(level);
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = ctx.Config.Scoring, BoardSeed = StableHash.Combine(spec.Seed, seed) });
            s.Start();
            Bots.PlayOut(s, kind, new Rng(seed));
            won = s.State == SessionState.Won;
            return s.FinalScore;
        }

        static bool PlayObstacleLevel(BotKind kind, ulong seed)
        {
            var layout = LevelLayout.Open(7, 7);
            layout.ApplyObstacleRows(new[] { ".......", ".I...I.", ".......", "...I...", ".......", ".i...i.", "......." });
            var spec = new LevelSpec
            {
                Id = "ice", Mode = GameMode.Campaign, Number = 1, Layout = layout, Colours = 5, Moves = 14,
                Target = 1000000, Star2 = 2000000, Star3 = 3000000, LeftoverBonusPerMove = 50,
                Goals = new[] { GoalSpec.FreeIce(5) },
            };
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = seed });
            s.Start();
            Bots.PlayOut(s, kind, new Rng(seed));
            return s.State == SessionState.Won;
        }
    }
}
