using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>The seven tile mechanics in every mode: the same rules everywhere, mode-specific unlocks and generation.</summary>
    public class CrossModeTests
    {
        public enum Kind { Ice, Chains, Rockets, Bombs, Prisms, Treasure, Moss }

        static readonly GameMode[] Modes = { GameMode.Campaign, GameMode.Daily, GameMode.WeeklyRush, GameMode.Endless };

        static PuzzleSession Session(GameMode mode, string[] rows, string[] obstacles)
        {
            bool rush = mode == GameMode.WeeklyRush;
            var setup = TestUtil.Setup(mode, rush ? 0 : 20, 1000000, TileColours.Max, rows, obstacles);
            setup.Level.Specials = SpecialTier.Prisms;
            var s = new PuzzleSession(setup);
            s.Start();
            if (rush) s.StartTimer();
            return s;
        }

        static string[] Checker(params (int col, int row, char piece)[] pieces)
        {
            var rows = new string[5];
            for (int i = 0; i < 5; i++)
            {
                int r = 4 - i;
                var chars = new char[5];
                for (int c = 0; c < 5; c++) chars[c] = (c + r) % 2 == 0 ? '0' : '1';
                foreach (var p in pieces) if (p.row == r) chars[p.col] = p.piece;
                rows[i] = new string(chars);
            }
            return rows;
        }

        /// <summary>Plays the mechanic's defining action in a mode and returns what happened.</summary>
        static MoveOutcome Exercise(Kind kind, GameMode mode, out PuzzleSession s)
        {
            switch (kind)
            {
                case Kind.Ice:
                    s = Session(mode, new[] { "000", "010", "000" }, new[] { "...", ".I.", "..." });
                    return Play.Tap(s, 0, 0);
                case Kind.Chains:
                    s = Session(mode, new[] { "111", "000" }, new[] { "...", ".2." });
                    return Play.Tap(s, 0, 0);
                case Kind.Rockets:
                    s = Session(mode, Checker((2, 2, '-')), null);
                    return Play.Tap(s, 2, 2);
                case Kind.Bombs:
                    s = Session(mode, Checker((2, 2, '*')), null);
                    return Play.Tap(s, 2, 2);
                case Kind.Prisms:
                    s = Session(mode, Checker((2, 2, '@')), null);
                    return Play.Tap(s, 2, 2, 1);
                case Kind.Treasure:
                    s = Session(mode, new[] { "121", "2$3", "401", "502" }, null);
                    return Play.Tap(s, 1, 0);
                default:
                    s = Session(mode, new[] { "0012", "3445" }, new[] { "...m", "...." });
                    return Play.Tap(s, 0, 1);
            }
        }

        [Test]
        public void EveryMechanicFollowsTheSameRulesInEveryMode([Values] Kind kind)
        {
            MoveOutcome reference = null;
            foreach (var mode in Modes)
            {
                var o = Exercise(kind, mode, out var s);
                string at = kind + " in " + mode;
                switch (kind)
                {
                    case Kind.Ice:
                        Assert.AreEqual(1, o.ObstacleHits.Count, at);
                        Assert.AreEqual(1, s.Board[1, 1].Strength, at);
                        break;
                    case Kind.Chains:
                        Assert.AreEqual(3, o.GroupSize, at);
                        Assert.AreEqual(ObstacleKind.Chain, s.Board[1, 0].Obstacle, at);
                        Assert.AreEqual(1, s.Board[1, 0].Strength, at);
                        break;
                    case Kind.Rockets:
                        Assert.AreEqual(4, o.Cleared.Count, at);
                        Assert.IsTrue(o.Cleared.All(c => c.Pos.Row == 2), at);
                        break;
                    case Kind.Bombs:
                        Assert.AreEqual(24, o.Cleared.Count, at);
                        break;
                    case Kind.Prisms:
                        Assert.IsTrue(o.Cleared.Count > 0 && o.Cleared.All(c => c.Colour == 1), at);
                        break;
                    case Kind.Treasure:
                        Assert.AreEqual(1, o.Treasures.Count, at);
                        break;
                    case Kind.Moss:
                        Assert.AreEqual(1, o.MossGrowth.Count, at);
                        break;
                }
                if (s.HasMoveLimit && mode != GameMode.Endless) Assert.AreEqual(19, s.MovesLeft, at + ": one move used");
                // The same action scores the same everywhere (Endless only adds bonus moves, never points).
                if (reference == null) reference = o;
                else
                {
                    Assert.AreEqual(reference.ScoreGained, o.ScoreGained, at);
                    Assert.AreEqual(reference.Cleared.Count, o.Cleared.Count, at);
                    Assert.AreEqual(reference.ObstacleHits.Count, o.ObstacleHits.Count, at);
                }
                if (kind == Kind.Treasure && mode == GameMode.Endless)
                    Assert.AreEqual(new EndlessConfig().treasureBonusMoves, o.BonusMoves, "treasure brings bonus moves in Endless");
            }
        }

        [Test]
        public void PowerUpsKeepTheirModeRulesWithMechanics()
        {
            foreach (var mode in Modes)
            {
                var s = Session(mode, new[] { "000", "010", "000" }, new[] { "...", ".I.", "..." });
                var o = new MoveOutcome();
                bool ok = s.TryApplyPowerUp(Play.At(PowerUpId.Blast, 1, 1), o, out var failure);
                if (ActionPolicy.ModeAllowsPowerUps(mode))
                {
                    Assert.IsTrue(ok, mode.ToString());
                    Assert.AreEqual(1, o.ObstacleHits.Count, mode.ToString());
                }
                else
                {
                    Assert.IsFalse(ok, mode + ": challenge modes stay power-up free");
                    Assert.AreEqual(PowerUpFailure.NotAllowedInMode, failure);
                }
            }
        }

        [Test]
        public void RecoveryKeepsObstaclesInPlaceInEveryMode()
        {
            foreach (var mode in Modes)
            {
                // No legal move: the session reshuffles free tiles at the start, leaving the ice and the chain alone.
                var s = Session(mode, new[] { "0123", "1230", "2301", "3012" }, new[] { "...i", "....", "..2.", "I..." });
                Assert.AreEqual(ObstacleKind.Ice, s.Board[3, 3].Obstacle, mode.ToString());
                Assert.AreEqual(ObstacleKind.Chain, s.Board[2, 1].Obstacle, mode.ToString());
                Assert.AreEqual(2, s.Board[0, 0].Strength, mode.ToString());
                Assert.IsTrue(GroupFinder.HasLegalMove(s.Board), mode.ToString());
            }
        }

        // ------------------------------------------------------------------ Unlocks

        [Test]
        public void CampaignUnlocksFollowTheIntroductions()
        {
            var s = new MechanicSchedule();
            Assert.AreEqual(MechanicSet.None, Mechanics.FromCampaign(s.iceFromLevel, s), "reaching the ice level is not enough: it has to be completed");
            Assert.AreEqual(MechanicSet.Ice, Mechanics.FromCampaign(s.iceFromLevel + 1, s));
            var all = Mechanics.FromCampaign(s.mossFromLevel + 1, s);
            Assert.AreEqual(MechanicSet.All, all);
            Assert.AreEqual(SpecialTier.Rockets, Mechanics.Tier(Mechanics.FromCampaign(s.rocketsFromLevel + 1, s)));
        }

        [Test]
        public void TheEndlessJourneyBringsOneThingPerStage()
        {
            var j = new EndlessJourneyConfig();
            var expected = new[]
            {
                (1, EndlessStep.WarmUp, MechanicSet.None), (2, EndlessStep.Introduce, MechanicSet.Stones), (3, EndlessStep.Introduce, MechanicSet.Ice),
                (4, EndlessStep.Breather, MechanicSet.None), (5, EndlessStep.Introduce, MechanicSet.Chains),
                (6, EndlessStep.Introduce, MechanicSet.Rockets | MechanicSet.Bombs), (7, EndlessStep.Introduce, MechanicSet.Treasure),
                (8, EndlessStep.Breather, MechanicSet.None), (9, EndlessStep.Introduce, MechanicSet.Moss), (10, EndlessStep.Introduce, MechanicSet.Prisms),
            };
            foreach (var (stage, step, what) in expected)
            {
                var plan = EndlessJourney.For(stage, j);
                Assert.AreEqual(step, plan.Step, "stage " + stage);
                if (step == EndlessStep.Introduce) Assert.AreEqual(what, plan.Mechanics, "stage " + stage);
                Assert.IsFalse(string.IsNullOrEmpty(plan.Title), "stage " + stage + " says what it is");
            }
            Assert.AreEqual(SpecialTier.None, EndlessJourney.SpecialsAt(5, j), "no specials before they are introduced");
            Assert.AreEqual(SpecialTier.Bombs, EndlessJourney.SpecialsAt(6, j));
            Assert.AreEqual(SpecialTier.Prisms, EndlessJourney.SpecialsAt(10, j));
            Assert.Greater(EndlessJourney.For(4, j).BonusMoves, 0, "a breather gives moves");

            // From 11 every stage mixes what came before: more kinds and more of each as stages rise, a breather every 4.
            int lastKinds = 0, lastDensity = 0;
            for (int stage = 11; stage <= 30; stage++)
            {
                var plan = EndlessJourney.For(stage, j);
                if (plan.Step == EndlessStep.Breather) continue;
                Assert.AreEqual(EndlessStep.Mix, plan.Step, "stage " + stage);
                Assert.GreaterOrEqual(plan.Kinds, 2);
                Assert.GreaterOrEqual(plan.Kinds, lastKinds);
                Assert.GreaterOrEqual(plan.DensityPercent, lastDensity);
                Assert.IsTrue((plan.Mechanics & ~(Mechanics.EndlessObstacles)) == MechanicSet.None);
                lastKinds = plan.Kinds;
                lastDensity = plan.DensityPercent;
            }
            Assert.AreEqual(EndlessStep.Breather, EndlessJourney.For(14, j).Step);
            Assert.AreEqual(EndlessStep.Breather, EndlessJourney.For(18, j).Step);
            Assert.AreEqual(4, EndlessJourney.For(23, j).Kinds);
            Assert.AreEqual(5, EndlessJourney.NextIntroduction(3, j, out var next));
            Assert.AreEqual(MechanicSet.Chains, next);
            Assert.AreEqual(0, EndlessJourney.NextIntroduction(10, j, out _), "everything has arrived by stage 10");

            j.iceAt = 2;
            j.stonesAt = 3;
            Assert.AreEqual(MechanicSet.Ice, EndlessJourney.For(2, j).Mechanics, "the order comes from the config");
        }

        // ------------------------------------------------------------------ Daily Challenge

        static ChallengeFactory Factory()
        {
            var cfg = GameConfigData.CreateDefault();
            return new ChallengeFactory(cfg.Daily, cfg.Rush, cfg.Scoring);
        }

        static IEnumerable<string> Days(int count) => Enumerable.Range(0, count).Select(d => TestUtil.Today.AddDays(d).ToString("yyyy-MM-dd"));

        static MechanicSet OnBoard(LevelLayout l)
        {
            var set = MechanicSet.None;
            if (l.ObstacleCount(ObstacleKind.Ice) > 0) set |= MechanicSet.Ice;
            if (l.ObstacleCount(ObstacleKind.Chain) > 0) set |= MechanicSet.Chains;
            if (l.ObstacleCount(ObstacleKind.Moss) > 0) set |= MechanicSet.Moss;
            if (l.TreasureCount > 0) set |= MechanicSet.Treasure;
            return set;
        }

        [Test]
        public void TheDailyOnlyUsesWhatThePlayerHasUnlocked()
        {
            var f = Factory();
            foreach (var day in Days(40))
            {
                var none = f.Daily(day, MechanicSet.None);
                Assert.AreEqual(MechanicSet.None, OnBoard(none.Layout), day);
                Assert.IsNull(none.Goals, day + ": the original daily");
                Assert.AreEqual(SpecialTier.None, none.Specials);

                var iceOnly = f.Daily(day, MechanicSet.Ice);
                Assert.AreEqual(MechanicSet.None, OnBoard(iceOnly.Layout) & ~MechanicSet.Ice, day);
                Assert.AreEqual(SpecialTier.None, iceOnly.Specials, day);

                var all = f.Daily(day, MechanicSet.All);
                var plan = ChallengeMechanics.DailyPlan(day);
                Assert.AreEqual(MechanicSet.None, OnBoard(all.Layout) & ~plan, day + ": only the day's mechanics");
                if (OnBoard(all.Layout).Has(MechanicSet.Chains)) Assert.AreEqual(MechanicSet.Chains, OnBoard(all.Layout), day + ": chains only ever alone");
                Assert.AreEqual(SpecialTier.Prisms, all.Specials);
                if (all.Goals != null)
                {
                    Assert.AreEqual(GoalKind.Score, all.Goals[0].kind, "the target is the first goal");
                    Assert.AreEqual(all.Target, all.Goals[0].amount);
                }
            }
        }

        [Test]
        public void EveryoneWithTheSameUnlocksGetsTheSameDaily()
        {
            var a = Factory();
            var b = Factory();
            foreach (var day in Days(12))
            {
                var x = a.Daily(day, MechanicSet.All);
                var y = b.Daily(day, MechanicSet.All);
                Assert.AreEqual(string.Join("/", x.Layout.ToRows()), string.Join("/", y.Layout.ToRows()), day);
                Assert.AreEqual(string.Join("/", x.Layout.ToObstacleRows()), string.Join("/", y.Layout.ToObstacleRows()), day);
                Assert.AreEqual(x.Seed, y.Seed);
                Assert.AreEqual(x.Moves, y.Moves);
                Assert.AreEqual(x.Target, y.Target);
                Assert.AreEqual(x.Goals?.Length ?? 0, y.Goals?.Length ?? 0);
                // And the board under the mechanics is the day's board for everyone.
                Assert.AreEqual(string.Join("/", a.Daily(day).Layout.ToRows()).Replace('$', '.'), string.Join("/", x.Layout.ToRows()).Replace('$', '.'), day);
                Assert.AreEqual(x.Id, a.Daily(day).Id, "records and rewards stay keyed by the date");
            }
        }

        [Test]
        public void ADailyWithGoalsNeedsThemAsWellAsTheTarget()
        {
            var f = Factory();
            var day = Days(60).First(d => f.Daily(d, MechanicSet.All).Goals != null);
            var spec = f.Daily(day, MechanicSet.All);
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Daily, Level = spec, Scoring = new ScoringRules(), BoardSeed = spec.Seed });
            s.Start();
            Assert.AreEqual(spec.Goals.Length, s.Goals.Count);
            Bots.PlayOut(s, BotKind.Planner, null);
            Assert.IsTrue(s.IsOver, "the daily plays all its moves");
            Assert.AreEqual(s.Goals.AllComplete && s.FinalScore >= spec.Target, s.Succeeded);
            if (!s.GoalsMet) Assert.AreEqual(0, s.Stars, "no stars until every goal is met");
        }

        [Test]
        public void DailyGoalsAreUsuallyReachable()
        {
            var f = Factory();
            int days = 0, done = 0;
            foreach (var day in Days(30))
            {
                var spec = f.Daily(day, MechanicSet.All);
                if (spec.Goals == null) continue;
                days++;
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Daily, Level = spec, Scoring = new ScoringRules(), BoardSeed = spec.Seed });
                s.Start();
                Bots.PlayOut(s, BotKind.Planner, null);
                if (s.Goals.AllComplete || Enumerable.Range(0, s.Goals.Count).Where(i => s.Goals.Goal(i).kind != GoalKind.Score).All(i => s.Goals.IsComplete(i))) done++;
            }
            TestContext.WriteLine("daily obstacle goals finished by the planner: " + done + "/" + days);
            Assert.Greater(days, 10);
            Assert.GreaterOrEqual(done * 10, days * 8, "the planner finishes the obstacle goals on at least 80% of days");
        }

        // ------------------------------------------------------------------ Weekly Rush

        static IEnumerable<string> Weeks(int count) => Enumerable.Range(0, count).Select(w => ChallengeCalendar.WeekKey(TestUtil.Today.AddDays(7 * w)));

        [Test]
        public void WeeklyBoardsAreThemedAndMoreComplexThanDailies()
        {
            var f = Factory();
            int weeklyKinds = 0, dailyKinds = 0, weeks = 0;
            var themes = new HashSet<MechanicSet>();
            foreach (var week in Weeks(20))
            {
                var plain = f.Weekly(week);
                Assert.AreEqual(MechanicSet.None, OnBoard(f.Weekly(week, MechanicSet.None).Layout), "the original Weekly Rush without unlocks");
                Assert.AreEqual(plain.Target, f.Weekly(week, MechanicSet.None).Target);
                var spec = f.Weekly(week, MechanicSet.All);
                var set = ChallengeMechanics.WeeklyFor(week, MechanicSet.All, out var theme);
                themes.Add(theme);
                var board = OnBoard(spec.Layout);
                Assert.AreEqual(MechanicSet.None, board & ~set, week);
                Assert.IsFalse(board.Has(MechanicSet.Treasure) && board.Has(MechanicSet.Chains), week + ": never treasure with chains");
                if (theme == MechanicSet.Rockets) Assert.Greater(spec.Layout.PieceCount('-') + spec.Layout.PieceCount('|'), 0, week);
                if (theme == MechanicSet.Bombs) Assert.Greater(spec.Layout.PieceCount('*'), 0, week);
                if (Mechanics.ObstaclesIn(MechanicSet.All).Contains(theme)) Assert.IsTrue(board.Has(theme), week + ": the theme is on the board");
                Assert.Less(spec.Target, spec.Star2);
                Assert.Less(spec.Star2, spec.Star3);
                Assert.AreEqual(plain.Id, spec.Id);
                weeklyKinds += Mechanics.ObstaclesIn(board).Count;
                weeks++;
            }
            foreach (var day in Days(20)) dailyKinds += Mechanics.ObstaclesIn(OnBoard(f.Daily(day, MechanicSet.All).Layout)).Count;
            Assert.Greater(weeklyKinds, dailyKinds, "weekly boards combine more mechanics");
            Assert.GreaterOrEqual(themes.Count, 4, "themes vary from week to week");
        }

        [Test]
        public void WeeklyMedalsFollowTheBoardThatWasPlayed()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = ctx.Catalog.Schedule.mossFromLevel + 1;
            var setup = ctx.CreateWeeklyRun();
            Assert.AreEqual(ctx.Challenges.Weekly(ctx.ThisWeekKey, MechanicSet.All).Target, setup.Level.Target);
            var tiers = new[] { setup.Level.Target, setup.Level.Star2, setup.Level.Star3 };
            var r = ctx.Rewards.ForWeekly("2099-W01", tiers[1], tiers);
            Assert.AreEqual(2, r.Lines.Count, "bronze and silver at the silver score of this board");
            Assert.AreEqual(0, ctx.Rewards.ForWeekly("2099-W01", tiers[2], tiers).Lines.Count(l => l.Coins > 0 && l.Label.StartsWith("Bronze")), "each medal once per week");
        }

        [Test]
        public void ChallengesForAPlayerWithNothingUnlockedAreTheOriginals()
        {
            var ctx = TestUtil.Context();
            Assert.AreEqual(MechanicSet.None, ctx.CampaignMechanics);
            var d = ctx.CreateDailyRun().Level;
            Assert.AreEqual(MechanicSet.None, OnBoard(d.Layout));
            Assert.IsNull(d.Goals);
            var w = ctx.CreateWeeklyRun().Level;
            Assert.AreEqual(MechanicSet.None, OnBoard(w.Layout));
            Assert.AreEqual(ctx.Config.Rush.tierScores[0], w.Target);
            // Endless no longer waits for unlocks: every run starts plain and its stages bring the mechanics in turn
            // (docs/DESIGN.md §4).
            var e = ctx.CreateEndlessRun().Level;
            Assert.IsFalse(e.Layout.HasObstacles);
            Assert.AreEqual(SpecialTier.None, e.Specials, "no specials until the journey brings them");
        }

        // ------------------------------------------------------------------ Endless

        [Test]
        public void EndlessProgressIsSaved()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            Assert.AreEqual(1, ctx.Data.Endless.Level);
            int best = 0;
            for (int run = 0; run < 3; run++)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = (ulong)(run + 1);
                var s = new PuzzleSession(setup);
                s.Start();
                ctx.BeginRun(s);
                Bots.PlayOut(s, BotKind.Greedy, new Rng((ulong)run), 2000);
                int before = ctx.Data.Endless.Level;
                var r = ctx.FinishRun(s);
                Assert.AreEqual(before + s.Stage - 1, ctx.Data.Endless.Level, "one Endless level per stage reached");
                Assert.AreEqual(best, r.BestStageBefore);
                best = System.Math.Max(best, s.Stage);
                Assert.AreEqual(best, ctx.Data.Endless.BestStage);
            }
            Assert.GreaterOrEqual(best, 3, "a greedy player gets a few stages in");
            Assert.IsTrue(ctx.Save());
            var reloaded = TestUtil.Context(null, store);
            Assert.AreEqual(ctx.Data.Endless.Level, reloaded.Data.Endless.Level, "the Endless level survives a restart");
            Assert.AreEqual(best, reloaded.Data.Endless.BestStage);
        }

        [Test]
        public void EndlessStagesBringTheJourney()
        {
            var ctx = TestUtil.Context();
            var j = ctx.Config.Endless.journey;
            // Long runs, so every seed gets well into the mixing stages.
            ctx.Config.Endless.startMoves = 160;
            int waves = 0, mixes = 0, maxKinds = 0, stones = 0, breathers = 0;
            int bestStage = 0;
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var setup = ctx.CreateEndlessRun();
                setup.BoardSeed = seed;
                var s = new PuzzleSession(setup);
                s.Start();
                Assert.IsFalse(setup.Level.Layout.HasObstacles, "a run starts with the original tiles");
                var bot = new PlannerBot();
                var o = new MoveOutcome();
                for (int step = 0; step < 500 && s.AcceptsMoves; step++)
                {
                    int stageBefore = s.Stage, movesBefore = s.MovesLeft;
                    bot.Choose(s, out var pick, out var colour);
                    Assert.IsTrue(s.TryPlay(pick, o, colour));
                    if (o.HasCreated)
                    {
                        Assert.GreaterOrEqual(s.Stage, j.specialsAt, "specials only once the journey has brought them");
                        if (o.Created.Kind == SpecialKind.Prism) Assert.GreaterOrEqual(s.Stage, j.prismsAt);
                    }
                    if (!o.StageUp)
                    {
                        Assert.AreEqual(0, o.Added.Count, "things only arrive with a new stage");
                        continue;
                    }
                    for (int st = stageBefore + 1; st <= s.Stage; st++)
                    {
                        var plan = EndlessJourney.For(st, j);
                        if (plan.Step == EndlessStep.Breather) breathers++;
                        if (plan.Step == EndlessStep.Mix) mixes++;
                    }
                    if (s.Stage == stageBefore + 1)
                    {
                        var plan = EndlessJourney.For(s.Stage, j);
                        var kinds = new HashSet<string>(o.Added.Select(a => a.IsTreasure ? "treasure" : a.IsStone ? "stone" : a.Special != SpecialKind.None ? "special" : a.Obstacle.ToString()));
                        switch (plan.Step)
                        {
                            case EndlessStep.Breather:
                                Assert.AreEqual(0, o.Added.Count, "a breather adds nothing");
                                Assert.GreaterOrEqual(s.MovesLeft, movesBefore - 1 + plan.BonusMoves, "and gives moves");
                                break;
                            case EndlessStep.Introduce:
                                Assert.LessOrEqual(kinds.Count, 1, "one new thing at a time");
                                if (plan.Mechanics == MechanicSet.Stones) Assert.IsTrue(kinds.Count == 0 || kinds.Contains("stone"));
                                if (plan.Mechanics == MechanicSet.Ice) Assert.IsTrue(kinds.Count == 0 || kinds.Contains("Ice"));
                                if (plan.Mechanics.Has(MechanicSet.Rockets)) Assert.IsTrue(kinds.Count == 0 || kinds.Contains("special"), "the first rocket is placed to try");
                                break;
                            case EndlessStep.Mix:
                                Assert.LessOrEqual(kinds.Count, plan.Kinds);
                                maxKinds = System.Math.Max(maxKinds, kinds.Count);
                                break;
                        }
                    }
                    if (o.Added.Count > 0) waves++;
                    stones += o.Added.Count(a => a.IsStone);
                    TestUtil.AssertBoardInvariants(s.Board, setup.Level.Layout);
                    Assert.LessOrEqual(s.Board.CountType(CellType.Stone), s.Board.Width, "never more stones than columns");
                }
                bestStage = System.Math.Max(bestStage, s.Stage);
            }
            TestContext.WriteLine("best stage " + bestStage + ", waves " + waves + ", mixes " + mixes + ", most kinds in a mix " + maxKinds + ", stones placed " + stones + ", breathers " + breathers);
            Assert.Greater(waves, 10);
            Assert.Greater(stones, 0, "stones arrive in stage 2");
            Assert.Greater(breathers, 0);
            Assert.GreaterOrEqual(bestStage, j.mixFrom, "a thoughtful player reaches the mixing stages");
            Assert.GreaterOrEqual(maxKinds, 2, "later stages combine mechanics");
        }

        [Test]
        public void TheFirstStageOfEndlessIsPlainMatching()
        {
            var ctx = TestUtil.Context();
            var setup = ctx.CreateEndlessRun();
            setup.BoardSeed = 42;
            var s = new PuzzleSession(setup);
            s.Start();
            var o = new MoveOutcome();
            var rng = new Rng(9);
            var finder = new GroupFinder();
            var groups = new List<GroupInfo>();
            while (s.AcceptsMoves && s.Stage == 1)
            {
                Bots.Choose(s.Board, BotKind.Greedy, rng, finder, groups, out var pick);
                s.TryPlay(pick, o);
                if (s.Stage > 1) break;
                Assert.AreEqual(0, o.Added.Count);
                Assert.IsFalse(o.HasCreated, "no specials in the warm-up");
            }
            Assert.AreEqual(SpecialTier.None, setup.Level.Specials);
        }

        [Test]
        public void OldSavesGetAnEndlessLevelFromTheirBestStage()
        {
            var json = "{\"format\":\"colour-cascade-save\",\"version\":1,\"endless\":{\"best\":9000,\"bestStage\":4,\"runs\":7}}";
            Assert.AreEqual(4, SaveCodec.Decode(json).Endless.Level);
            Assert.AreEqual(1, SaveCodec.Decode("{\"format\":\"colour-cascade-save\",\"version\":1}").Endless.Level);
            var d = SaveCodec.Decode(json);
            d.Endless.Level = 23;
            Assert.AreEqual(23, SaveCodec.Decode(SaveCodec.Encode(d)).Endless.Level);
        }

        // ------------------------------------------------------------------ Long play in every mode

        [Test]
        public void RandomPlayKeepsEveryRuleInEveryMode()
        {
            var ctx = TestUtil.Context();
            var f = ctx.Challenges;
            var e = ctx.Config.Endless;
            var games = new List<(string name, SessionSetup setup)>();
            foreach (var day in Days(8)) games.Add(("daily " + day, new SessionSetup { Mode = GameMode.Daily, Level = f.Daily(day, MechanicSet.All), Scoring = ctx.Config.Scoring, BoardSeed = 1 }));
            foreach (var week in Weeks(6)) games.Add(("weekly " + week, new SessionSetup { Mode = GameMode.WeeklyRush, Level = f.Weekly(week, MechanicSet.All), Scoring = ctx.Config.Scoring, BoardSeed = 2 }));
            for (int i = 0; i < 6; i++)
            {
                var spec = new LevelSpec
                {
                    Id = "endless", Mode = GameMode.Endless, Layout = LevelLayout.Open(e.columns, e.rows), Colours = e.ColoursForStage(1),
                    AssistPermille = e.assistPermille, Moves = e.startMoves, Pool = MechanicSet.All & ~MechanicSet.ColourGoals, Specials = SpecialTier.Prisms,
                };
                games.Add(("endless " + i, new SessionSetup { Mode = GameMode.Endless, Level = spec, Scoring = ctx.Config.Scoring, Endless = e, BoardSeed = (ulong)(100 + i) }));
            }
            int actions = 0;
            foreach (var (name, setup) in games)
            {
                var s = new PuzzleSession(setup);
                s.Start();
                if (s.IsTimed) s.StartTimer();
                var rng = new Rng(StableHash.Of(name));
                var finder = new GroupFinder();
                var groups = new List<GroupInfo>();
                var o = new MoveOutcome();
                int treasureStart = Play.CountPieces(s.Board, CellType.Treasure), collected = 0, added = 0, sum = 0;
                for (int step = 0; step < 150 && s.AcceptsMoves; step++)
                {
                    var held = new Dictionary<int, GridPos>();
                    for (int i = 0; i < s.Board.CellCount; i++)
                        if (TileRules.IsGravityBarrier(s.Board.GetByIndex(i)) && TileRules.HasObstacle(s.Board.GetByIndex(i))) held[s.Board.GetByIndex(i).Id] = s.Board.PosOf(i);
                    bool acted;
                    if (ActionPolicy.ModeAllowsPowerUps(s.Mode) && rng.Next(6) == 0)
                        acted = s.TryApplyPowerUp(Play.At(rng.Next(2) == 0 ? PowerUpId.Blast : PowerUpId.TileBreaker, rng.Next(s.Board.Width), rng.Next(s.Board.Height)), o, out _);
                    else
                    {
                        Assert.IsTrue(Bots.Choose(s.Board, BotKind.Casual, rng, finder, groups, out var pick), name);
                        acted = s.TryPlay(pick, o, s.NeedsColourChoice(pick) ? rng.Next(s.ColourCount) : -1);
                        Assert.IsTrue(acted, name);
                    }
                    if (!acted) continue;
                    actions++;
                    sum += o.ScoreGained;
                    Assert.AreEqual(sum, s.Score, name + ": score bookkeeping");
                    Assert.AreEqual(o.ObstacleHits.Count, o.ObstacleHits.Select(h => h.TileId).Distinct().Count(), name);
                    Assert.AreEqual(o.SpecialsFired.Count, o.SpecialsFired.Select(h => h.TileId).Distinct().Count(), name);
                    collected += o.Treasures.Count;
                    added += o.Added.Count(a => a.IsTreasure);
                    Assert.AreEqual(treasureStart + added, collected + Play.CountPieces(s.Board, CellType.Treasure), name + ": treasure conserved");
                    for (int i = 0; i < s.Board.CellCount; i++)
                    {
                        var t = s.Board.GetByIndex(i);
                        if (!TileRules.HasObstacle(t)) continue;
                        Assert.IsTrue(t.Strength >= 1 && t.Strength <= Obstacles.Get(t.Obstacle).MaxStrength, name);
                        if (held.TryGetValue(t.Id, out var was) && TileRules.IsGravityBarrier(t)) Assert.AreEqual(was, s.Board.PosOf(i), name + ": ice or a chain moved");
                    }
                    if (!o.RecoveryFailed) TestUtil.AssertBoardInvariants(s.Board, setup.Level.Layout);
                }
            }
            TestContext.WriteLine(games.Count + " games, " + actions + " actions");
            Assert.Greater(actions, 1200);
        }

        [Test]
        public void TheStarChecklistSaysWhatEachStarNeeds()
        {
            var level = new LevelSpec { Target = 1000, Star2 = 2000, Star3 = 3000, Goals = new[] { GoalSpec.FreeIce(3) } };
            var items = StarChecklist.For(level, null, true, 2300, 2);
            Assert.AreEqual(3, items.Count);
            Assert.IsTrue(items[0].Met && items[1].Met && !items[2].Met);
            StringAssert.Contains("goal", items[0].Text);
            StringAssert.Contains("3,000", items[2].Text);
            Assert.AreEqual("2,300 of 3,000", items[2].Result, "the result against the requirement");
            StringAssert.Contains("700 short", StarChecklist.WhyNotNext(level, 2300, 2, false, 0));
            var scoreLevel = new LevelSpec { Target = 1000, Star2 = 2000, Star3 = 3000 };
            StringAssert.Contains("1,000", StarChecklist.For(scoreLevel, null, true, 1200, 1)[0].Text);
            // Not passed: no star is met, whatever the score.
            Assert.IsFalse(StarChecklist.For(level, null, false, 5000, 0).Any(i => i.Met));
        }
    }
}
