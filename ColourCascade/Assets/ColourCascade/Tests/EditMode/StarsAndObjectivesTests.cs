using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Passing a level, then playing on for stars (docs/MECHANICS.md §4.3); objectives about special tiles; and the
    /// playtest reports this was built for: level 47 won in one tap with one star, level 67 ending at its target,
    /// level 78 failed with a high score and a treasure left.
    /// </summary>
    public class StarsAndObjectivesTests
    {
        static CampaignCatalog Catalog()
        {
            var cfg = GameConfigData.CreateDefault();
            return new CampaignCatalog(cfg.Campaign, cfg.Scoring);
        }

        static bool PlayAnyGroup(PuzzleSession s)
        {
            var groups = new List<GroupInfo>();
            if (new GroupFinder().FindAllGroups(s.Board, groups) == 0) return false;
            return s.TryPlay(groups[0].Anchor, new MoveOutcome());
        }

        // ------------------------------------------------------------------ Level 67: the target passes, play goes on

        [Test]
        public void ReachingTheTargetPassesTheLevelAndStarsRiseWithEveryMoveLeft()
        {
            var s = TestUtil.Session(GameMode.Campaign, 8, 40, 3, "001122", "220011", "112200", "001122");
            int lastLive = 0;
            while (s.AcceptsMoves)
            {
                Assert.IsTrue(PlayAnyGroup(s));
                if (s.Score >= s.Level.Target) Assert.IsTrue(s.Passed, "the target is the only goal");
                if (s.MovesLeft > 0) Assert.AreEqual(SessionState.Playing, s.State, "moves are left, so the level is not over");
                Assert.GreaterOrEqual(s.LiveStars, lastLive, "stars never go down during play");
                if (s.Passed && !s.IsOver) Assert.AreEqual(System.Math.Max(1, StarRating.Compute(s.Score, s.Level.Target, s.Level.Star2, s.Level.Star3)), s.LiveStars);
                lastLive = s.LiveStars;
            }
            Assert.AreEqual(SessionState.Won, s.State);
            Assert.AreEqual(0, s.MovesLeft);
            Assert.AreEqual(s.Score, s.FinalScore, "the final score is the score");
            Assert.AreEqual(System.Math.Max(1, StarRating.Compute(s.FinalScore, s.Level.Target, s.Level.Star2, s.Level.Star3)), s.Stars);
            Assert.AreEqual(s.Stars, s.LiveStars, "the HUD's stars are the stars awarded");
        }

        [Test]
        public void Level67KeepsGoingAfterItsGoalsAreMet()
        {
            var cat = Catalog();
            var spec = cat.Get(67);
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = seed });
                s.Start();
                var planner = new PlannerBot();
                var o = new MoveOutcome();
                bool sawPass = false;
                while (s.AcceptsMoves && planner.Choose(s, out var pick, out var colour))
                {
                    Assert.IsTrue(s.TryPlay(pick, o, colour));
                    if (o.JustPassed)
                    {
                        sawPass = true;
                        if (s.MovesLeft > 0)
                        {
                            Assert.AreEqual(SessionState.Playing, s.State, "seed " + seed + ": passing must not end the level");
                            Assert.IsFalse(o.Ended);
                            Assert.IsTrue(s.CanFinish);
                        }
                    }
                }
                if (s.FinalePending) s.RunFinale(o);
                if (!sawPass) continue;
                Assert.AreEqual(SessionState.Won, s.State);
                Assert.AreEqual(0, s.MovesLeft, "the planner, like a player chasing stars, used every move");
                Assert.Greater(s.MovesMade, s.PassedAtMove);
            }
        }

        // ------------------------------------------------------------------ Level 78: score never passes on its own

        [Test]
        public void AHighScoreWithATreasureLeftFailsAndContinuingKeepsEverything()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 60;
            ctx.Data.Coins = 100;
            // Six 0s to clear for points; the treasure sits on three tiles that match nothing.
            var s = TestUtil.Session(GameMode.Campaign, 1, 100, TileColours.Max, new[] { "$000", "1000", "2311", "3222" }, null, GoalSpec.CollectTreasure(1));
            ctx.BeginRun(s);
            Assert.IsTrue(s.TryPlay(new GridPos(1, 3), new MoveOutcome()));
            Assert.GreaterOrEqual(s.Score, s.Level.Target, "the score is past the target");
            Assert.AreEqual(SessionState.Failed, s.State, "a treasure is left: the level fails whatever the score");
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
            Assert.AreEqual(0, s.Stars);
            Assert.IsFalse(s.Passed);
            var failed = ctx.FinishRun(s);
            Assert.IsFalse(failed.Success);
            Assert.IsFalse(failed.Passed);
            Assert.AreEqual(1, s.Goals.Remaining(0), "the failure screen lists exactly this");

            // Out of Moves, +5 moves for 100 coins (3.0): the score and the goal progress carry on.
            int score = s.Score;
            Assert.AreEqual(CoinContinueResult.Done, ctx.Monetisation.Continue.ContinueWithCoins(s));
            ctx.BeginRun(s);
            Assert.AreEqual(SessionState.Playing, s.State);
            Assert.AreEqual(score, s.Score);
            Assert.AreEqual(0, s.Goals.Progress(0));
            Assert.Greater(s.MovesLeft, 0);

            // A power-up digs the treasure out: the level is passed and play goes on.
            var blast = Play.Use(s, Play.At(PowerUpId.Blast, 0, 1));
            Assert.AreEqual(1, blast.Treasures.Count);
            Assert.IsTrue(blast.JustPassed);
            Assert.AreEqual(SessionState.Playing, s.State);
            int left = s.MovesLeft;
            Assert.IsTrue(s.Finish());
            Assert.Greater(left, 0);
            Assert.AreEqual(s.Score, s.FinalScore, "no bonus for the moves left");
            var won = ctx.FinishRun(s);
            Assert.IsTrue(won.Success);
            Assert.IsTrue(won.Passed);
            Assert.AreEqual(EndReason.FinishedEarly, won.EndReason);
            Assert.AreEqual(left, won.MovesLeft);
            Assert.GreaterOrEqual(won.Stars, 1);
            Assert.AreEqual(won.Stars, ctx.Campaign.StarsFor(1));
        }

        // ------------------------------------------------------------------ Level 47: no more one-tap wins

        [Test]
        public void TheComboTutorialIsNoLongerWonInOneTap()
        {
            var cat = Catalog();
            var spec = cat.Get(cat.Schedule.combosFromLevel);
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = 1 });
            s.Start();
            Assert.AreEqual(SpecialKind.RocketRow, s.Board[3, 4].Special);
            var o = new MoveOutcome();
            Assert.IsTrue(s.TryPlay(new GridPos(3, 4), o));
            Assert.AreEqual(SpecialKind.RocketColumn, o.SpecialsFired[0].ComboWith, "the two rockets fire together");
            Assert.IsTrue(s.Goals.IsComplete(0), "fire a combination: done");
            Assert.AreEqual(10, s.Goals.Progress(1), "the cross frees 10 frozen tiles");
            Assert.AreEqual(4, s.Goals.Remaining(1), "4 are out of its reach");
            Assert.IsFalse(s.Passed);
            Assert.AreEqual(SessionState.Playing, s.State);
        }

        [Test]
        public void NoEarlyLevelIsPassedInAMoveOrTwo()
        {
            // The planner (a thoughtful player) needs more than two moves to meet the goals of every level 21-60.
            var cat = Catalog();
            var scoring = new ScoringRules();
            for (int n = 21; n <= 60; n++)
            {
                var spec = cat.Get(n);
                var results = LevelCalibrator.PassedResults(spec, scoring, 6, BotKind.Planner, 900);
                if (results.Count == 0) continue;
                var at = results.Select(r => r.PassedAtMove).OrderBy(x => x).ToList();
                Assert.Greater(at[at.Count / 2], 2, "level " + n);
            }
        }

        [Test]
        public void ThreeStarsAreWithinReachOnEveryEarlyLevel()
        {
            // Stars come from games played to the last move: a thoughtful player's best of a few fresh games gets
            // close to three stars on every level, and reaches it on most.
            var cat = Catalog();
            var scoring = new ScoringRules();
            int reached = 0, levels = 0;
            for (int n = 21; n <= 60; n++)
            {
                var spec = cat.Get(n);
                var results = LevelCalibrator.PassedResults(spec, scoring, 4, BotKind.Planner, 900);
                Assert.Greater(results.Count, 0, "level " + n + " was passed");
                int best = results.Max(r => r.Score);
                Assert.GreaterOrEqual(best * 100, spec.Star3 * 80, "level " + n + ": best " + best + ", three stars " + spec.Star3);
                levels++;
                if (best >= spec.Star3) reached++;
            }
            Assert.GreaterOrEqual(reached * 100, levels * 60, reached + " of " + levels);
        }

        // ------------------------------------------------------------------ Objectives about special tiles

        static string[] Checker(int size, params (int col, int row, char piece)[] pieces)
        {
            var rows = new string[size];
            for (int i = 0; i < size; i++)
            {
                int r = size - 1 - i;
                var chars = new char[size];
                for (int c = 0; c < size; c++) chars[c] = (c + r) % 2 == 0 ? '0' : '1';
                foreach (var p in pieces)
                    if (p.row == r) chars[p.col] = p.piece;
                rows[i] = new string(chars);
            }
            return rows;
        }

        [Test]
        public void MakingSpecialsCountsOnlyTheKindAsked()
        {
            GoalSpec[] Goals() => new[] { GoalSpec.CreateSpecial(SpecialKind.RocketColumn, 1), GoalSpec.CreateSpecial(SpecialKind.Bomb, 1), GoalSpec.CreateSpecial(SpecialKind.None, 1) };
            // A group of 6 makes a rocket.
            var s = Play.Specials(new[] { "000222", "000333", "123451", "234512" }, null, SpecialTier.Prisms, 20, Goals());
            var o = Play.Tap(s, 0, 3);
            Assert.IsTrue(SpecialEffects.IsRocket(o.Created.Kind));
            Assert.AreEqual(1, s.Goals.Progress(0), "any rocket counts for a rocket goal");
            Assert.AreEqual(0, s.Goals.Progress(1));
            Assert.AreEqual(1, s.Goals.Progress(2));
            Assert.IsFalse(s.Passed, "no bomb yet");
            // A group of 10 makes a bomb.
            var t = Play.Specials(new[] { "000002", "000013", "012345", "234512" }, null, SpecialTier.Prisms, 20, Goals());
            var b = Play.Tap(t, 0, 3);
            Assert.AreEqual(SpecialKind.Bomb, b.Created.Kind);
            Assert.AreEqual(0, t.Goals.Progress(0));
            Assert.AreEqual(1, t.Goals.Progress(1));
            Assert.AreEqual(1, t.Goals.Progress(2));
        }

        [Test]
        public void SettingOffSpecialsCountsChainReactionsCombosAndPowerUps()
        {
            // The rocket reaches the bomb, which goes off too.
            var s = Play.Specials(Checker(5, (0, 2, '-'), (3, 2, '*')), null, SpecialTier.Prisms, 20,
                GoalSpec.FireSpecial(SpecialKind.Bomb, 1), GoalSpec.FireSpecial(SpecialKind.None, 5), GoalSpec.FireCombo(1));
            Play.Tap(s, 0, 2);
            Assert.AreEqual(1, s.Goals.Progress(0), "the bomb set off by the rocket counts");
            Assert.AreEqual(2, s.Goals.Progress(1));
            Assert.AreEqual(0, s.Goals.Progress(2));

            // Two specials side by side: a combination, and both count as set off.
            var t = Play.Specials(Checker(5, (1, 2, '-'), (2, 2, '|')), null, SpecialTier.Prisms, 20,
                GoalSpec.FireSpecial(SpecialKind.RocketRow, 2), GoalSpec.FireCombo(1));
            var o = Play.Tap(t, 1, 2);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(2, t.Goals.Progress(0), "both rockets of the combination");
            Assert.AreEqual(1, t.Goals.Progress(1));
            Assert.IsTrue(t.Passed);

            // A power-up that reaches a special sets it off.
            var u = Play.Specials(Checker(5, (2, 2, '*')), null, SpecialTier.Prisms, 20, GoalSpec.FireSpecial(SpecialKind.Bomb, 1));
            var blast = Play.Use(u, Play.At(PowerUpId.Blast, 1, 2));
            Assert.AreEqual(1, blast.SpecialsFired.Count);
            Assert.AreEqual(1, u.Goals.Progress(0));
            Assert.IsTrue(u.Passed);
        }

        [Test]
        public void ObjectivesAreTrackedIndependentlyAndAllAreNeeded()
        {
            // Break the ice AND collect 2 blue: the ice alone does not pass.
            var s = Play.Level(new[] { "0012", "3345" }, new[] { "...i", "...." }, 10, GoalSpec.FreeIce(1), GoalSpec.Collect(TileColours.Blue, 2));
            var o = Play.Use(s, Play.At(PowerUpId.TileBreaker, 3, 1));
            Assert.IsTrue(o.GoalsChanged);
            Assert.IsTrue(s.Goals.IsComplete(0));
            Assert.IsFalse(s.Goals.IsComplete(1));
            Assert.IsFalse(s.Passed);
            Assert.IsFalse(s.CanFinish);
        }

        [Test]
        public void NewObjectivesRoundTripThroughTheLevelFormat()
        {
            var cat = Catalog();
            var spec = cat.Get(cat.Schedule.combosFromLevel);
            spec = new LevelSpec
            {
                Id = spec.Id, Mode = spec.Mode, Number = spec.Number, Layout = spec.Layout, Colours = spec.Colours, Moves = spec.Moves,
                Target = spec.Target, Star2 = spec.Star2, Star3 = spec.Star3, Specials = spec.Specials,
                Goals = new[] { GoalSpec.CreateSpecial(SpecialKind.RocketColumn, 2), GoalSpec.FireSpecial(SpecialKind.Bomb, 1), GoalSpec.FireCombo(1), GoalSpec.CreateSpecial(SpecialKind.None, 3) },
            };
            string json = LevelCodec.Encode(spec);
            var back = LevelCodec.Decode(json);
            Assert.AreEqual(4, back.goals.Length);
            Assert.AreEqual(string.Join("|", spec.Goals.Select(g => g.ToString())), string.Join("|", back.goals.Select(g => g.ToString())));
            Assert.AreEqual(SpecialKind.RocketRow, back.goals[0].special, "rockets are one family");
        }

        // ------------------------------------------------------------------ Star requirements on screen

        [Test]
        public void StarRequirementsAreExactlyHowStarsAreAwarded()
        {
            var cat = Catalog();
            for (int n = 1; n <= 60; n++)
            {
                var spec = cat.Get(n);
                var items = StarChecklist.Requirements(spec);
                Assert.AreEqual(3, items.Count);
                StringAssert.Contains(NumberFormat.Thousands(spec.Star2), items[1].Text, "level " + n);
                StringAssert.Contains(NumberFormat.Thousands(spec.Star3), items[2].Text, "level " + n);
                if (spec.Goals == null) StringAssert.Contains(NumberFormat.Thousands(spec.Target), items[0].Text, "level " + n);
                // Ticked against a result, the items agree with the session's own star rule.
                foreach (int score in new[] { spec.Star2 - 10, spec.Star2, spec.Star3 - 10, spec.Star3 })
                {
                    int stars = System.Math.Max(1, StarRating.Compute(score, spec.Target, spec.Star2, spec.Star3));
                    var got = StarChecklist.For(spec, null, true, score, stars);
                    Assert.AreEqual(stars, got.Count(i => i.Met), "level " + n + " score " + score);
                }
            }
        }

        [Test]
        public void TheResultSaysWhyTheNextStarWasMissed()
        {
            var level = new LevelSpec { Target = 1900, Star2 = 2500, Star3 = 2550 };
            // The playtest report: level 47 won on the first move with 17 to spare, 2,350 in all.
            string why = StarChecklist.WhyNotNext(level, 2350, 1, true, 17);
            StringAssert.Contains("2,500", why);
            StringAssert.Contains("150 short", why);
            StringAssert.Contains("17 moves left", why);
            Assert.IsNull(StarChecklist.WhyNotNext(level, 2600, 3, false, 0));
            StringAssert.Contains("not passed", StarChecklist.WhyNotNext(level, 5000, 0, false, 0));
        }

        // ------------------------------------------------------------------ Saving

        [Test]
        public void StarsEarnedByPlayingOnAreSavedAndKept()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            var s = TestUtil.Session(GameMode.Campaign, 6, 40, 3, "001122", "220011", "112200", "001122");
            ctx.BeginRun(s);
            while (s.AcceptsMoves && PlayAnyGroup(s)) { }
            Assert.AreEqual(SessionState.Won, s.State);
            var r = ctx.FinishRun(s);
            Assert.IsTrue(ctx.Save());
            var reloaded = TestUtil.Context(null, store);
            Assert.AreEqual(r.Stars, reloaded.Campaign.StarsFor(1));
            Assert.AreEqual(s.FinalScore, reloaded.Campaign.BestScore(1));
            Assert.AreEqual(2, reloaded.Campaign.HighestUnlocked);
        }
    }
}
