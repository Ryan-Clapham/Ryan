using System.Collections.Generic;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    public class SessionTests
    {
        static readonly string[] Stripes = { "000", "111", "222" };

        [Test]
        public void ValidGroupScoresAndUsesAMove()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 10000, 3, Stripes);
            var o = new MoveOutcome();
            Assert.IsTrue(s.TryPlay(new GridPos(0, 2), o));
            Assert.AreEqual(90, o.ScoreGained);
            Assert.AreEqual(90, s.Score);
            Assert.AreEqual(4, s.MovesLeft);
            Assert.AreEqual(3, o.Cleared.Count);
            Assert.AreEqual(3, o.GroupSize);
        }

        [Test]
        public void SingleTileIsRejectedWithoutCostingAMove()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 10000, 3, "012", "001", "220");
            var o = new MoveOutcome();
            Assert.IsFalse(s.TryPlay(new GridPos(1, 2), o));
            Assert.AreEqual(5, s.MovesLeft);
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(OutcomeKind.None, o.Kind);
        }

        [Test]
        public void CampaignEndsAsWonWhenTargetReachedWithLeftoverBonus()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 90, 3, Stripes);
            var o = new MoveOutcome();
            s.TryPlay(new GridPos(0, 2), o);
            Assert.AreEqual(SessionState.Won, s.State);
            Assert.AreEqual(EndReason.TargetReached, s.EndReason);
            Assert.IsTrue(o.Ended);
            Assert.AreEqual(4 * 100, s.LeftoverBonus);
            Assert.AreEqual(90 + 400, s.FinalScore);
            Assert.AreEqual(3, s.Stars);
            Assert.IsTrue(s.Succeeded);
            Assert.IsFalse(s.TryPlay(new GridPos(0, 0), o), "no moves after the level ends");
        }

        [Test]
        public void CampaignFailsWhenMovesRunOut()
        {
            var s = TestUtil.Session(GameMode.Campaign, 1, 100000, 3, Stripes);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            Assert.AreEqual(SessionState.Failed, s.State);
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
            Assert.AreEqual(0, s.Stars);
            Assert.IsFalse(s.Succeeded);
        }

        [Test]
        public void AddingMovesRevivesAnOutOfMovesLevel()
        {
            var s = TestUtil.Session(GameMode.Campaign, 1, 100000, 3, Stripes);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            Assert.IsTrue(s.AddMoves(3));
            Assert.AreEqual(SessionState.Playing, s.State);
            Assert.AreEqual(3, s.MovesLeft);
            Assert.IsTrue(s.AcceptsMoves);
        }

        [Test]
        public void DailyPlaysAllMovesAndSucceedsOnTarget()
        {
            var s = TestUtil.Session(GameMode.Daily, 2, 50, 3, Stripes);
            var o = new MoveOutcome();
            s.TryPlay(new GridPos(0, 2), o);
            Assert.AreEqual(SessionState.Playing, s.State, "Daily does not end early on reaching the target");
            GridPos any = FindGroup(s.Board);
            s.TryPlay(any, o);
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.IsTrue(s.Succeeded);
            Assert.IsFalse(s.IsTimed);
        }

        [Test]
        public void EndlessAwardsBonusMovesForBigGroups()
        {
            var s = TestUtil.Session(GameMode.Endless, 10, 0, 3, "000", "000", "111");
            var o = new MoveOutcome();
            s.TryPlay(new GridPos(0, 2), o);
            Assert.AreEqual(6, o.GroupSize);
            Assert.AreEqual(1, o.BonusMoves);
            Assert.AreEqual(10, s.MovesLeft);
        }

        [Test]
        public void EndlessEndsWhenMovesRunOut()
        {
            var s = TestUtil.Session(GameMode.Endless, 1, 0, 3, Stripes);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
        }

        [Test]
        public void WeeklyRushTimerStartsOnlyWhenToldAndStopsPlayAtZero()
        {
            var s = TestUtil.Session(GameMode.WeeklyRush, 0, 1000, 3, Stripes);
            Assert.IsTrue(s.IsTimed);
            Assert.AreEqual(60, (int)s.TimeLeft);
            Assert.IsFalse(s.AcceptsMoves, "no play during the 3-2-1 countdown");
            Assert.IsFalse(s.Tick(10), "timer does not run before StartTimer");
            Assert.AreEqual(60, (int)s.TimeLeft);
            s.StartTimer();
            Assert.IsTrue(s.TryPlay(new GridPos(0, 2), new MoveOutcome()));
            Assert.IsFalse(s.Tick(59.5));
            Assert.IsTrue(s.Tick(1.0));
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(EndReason.TimeUp, s.EndReason);
            Assert.AreEqual(0, s.TimeLeft);
            Assert.IsFalse(s.TryPlay(FindGroup(s.Board), new MoveOutcome()), "no play after time is up");
        }

        [Test]
        public void PausingStopsTheTimerAndInput()
        {
            var s = TestUtil.Session(GameMode.WeeklyRush, 0, 1000, 3, Stripes);
            s.StartTimer();
            s.SetPaused(true);
            Assert.IsFalse(s.Tick(100));
            Assert.AreEqual(60, (int)s.TimeLeft);
            Assert.IsFalse(s.AcceptsMoves);
            s.SetPaused(false);
            Assert.IsTrue(s.AcceptsMoves);
        }

        [Test]
        public void OnlyWeeklyRushHasATimer()
        {
            foreach (var mode in new[] { GameMode.Campaign, GameMode.Endless, GameMode.Daily })
            {
                var s = TestUtil.Session(mode, 5, 100, 3, Stripes);
                Assert.IsFalse(s.IsTimed, mode + " must not be timed");
                Assert.IsFalse(s.Tick(1000));
                Assert.AreEqual(SessionState.Playing, s.State);
            }
            Assert.IsTrue(ActionPolicy.ModeIsTimed(GameMode.WeeklyRush));
            Assert.IsFalse(ActionPolicy.ModeIsTimed(GameMode.Campaign));
            Assert.IsFalse(ActionPolicy.ModeIsTimed(GameMode.Endless));
            Assert.IsFalse(ActionPolicy.ModeIsTimed(GameMode.Daily));
        }

        [Test]
        public void DeadBoardIsRecoveredAutomaticallyAfterAMove()
        {
            // Clearing the left pair leaves a board where refills could be dead; the session must always end playable.
            for (int seed = 0; seed < 200; seed++)
            {
                var spec = new LevelSpec { Layout = LevelLayout.Parse(new[] { "0012", "1201", "2010" }), Colours = 6, Moves = 50, Target = 1000000 };
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, BoardSeed = (ulong)seed });
                s.Start();
                var o = new MoveOutcome();
                for (int i = 0; i < 20 && s.AcceptsMoves; i++)
                {
                    Assert.IsTrue(s.TryPlay(FindGroup(s.Board), o));
                    Assert.IsTrue(GroupFinder.HasLegalMove(s.Board) || s.State == SessionState.Stuck);
                }
            }
        }

        [Test]
        public void BoardInvariantsHoldThroughLongPlay()
        {
            var catalog = new CampaignCatalog(GameConfigData.CreateDefault().Campaign, new ScoringRules());
            foreach (int level in new[] { 1, 8, 25, 40, 77 })
            {
                var spec = catalog.Get(level);
                for (int seed = 0; seed < 5; seed++)
                {
                    var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, BoardSeed = (ulong)(seed + 1) });
                    s.Start();
                    var rng = new Rng((ulong)seed);
                    var finder = new GroupFinder();
                    var groups = new List<GroupInfo>();
                    var o = new MoveOutcome();
                    int stones = s.Board.CountType(CellType.Stone);
                    while (s.AcceptsMoves)
                    {
                        Assert.IsTrue(Bots.Choose(s.Board, BotKind.Casual, rng, finder, groups, out var pick));
                        Assert.IsTrue(s.TryPlay(pick, o));
                        TestUtil.AssertBoardInvariants(s.Board, spec.Layout);
                        Assert.AreEqual(stones, s.Board.CountType(CellType.Stone), "matching never removes stones");
                    }
                    Assert.IsTrue(s.IsOver);
                }
            }
        }

        [Test]
        public void OutcomeDescribesEveryTileThatMoved()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "012", "000", "121");
            var o = new MoveOutcome();
            Assert.IsTrue(s.TryPlay(new GridPos(0, 1), o));
            Assert.AreEqual(4, o.Cleared.Count);
            // Every cleared cell is refilled either by a falling tile or a spawn.
            Assert.AreEqual(o.Cleared.Count, o.Spawns.Count);
            foreach (var f in o.Falls) Assert.AreEqual(s.Board[f.To].Id, f.TileId);
            foreach (var sp in o.Spawns) Assert.AreEqual(s.Board[sp.At].Id, sp.TileId);
        }

        static GridPos FindGroup(Board b)
        {
            var groups = new List<GroupInfo>();
            new GroupFinder().FindAllGroups(b, groups);
            Assert.IsTrue(groups.Count > 0, "board has no legal move");
            return groups[0].Anchor;
        }
    }

    public class PowerUpTests
    {
        static PowerUpOutcome Apply(PuzzleSession s, PowerUpRequest r)
        {
            var o = new MoveOutcome();
            bool ok = s.TryApplyPowerUp(r, o, out var failure);
            return new PowerUpOutcome { Ok = ok, Failure = failure, Outcome = o };
        }

        struct PowerUpOutcome
        {
            public bool Ok;
            public PowerUpFailure Failure;
            public MoveOutcome Outcome;
        }

        [Test]
        public void BlastClearsThreeByThreeAndScores()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "000", "111", "222");
            var r = Apply(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(1, 1)));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(9, r.Outcome.Cleared.Count);
            Assert.AreEqual(9 * 20, s.Score);
            Assert.AreEqual(5, s.MovesLeft, "power-ups do not cost moves");
            TestUtil.AssertBoardInvariants(s.Board, s.Level.Layout);
        }

        [Test]
        public void BlastIsClippedAtTheBoardEdge()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "000", "111", "222");
            var r = Apply(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(0, 0)));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(4, r.Outcome.Cleared.Count);
        }

        [Test]
        public void BlastLeavesStonesIntact()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "0#1", "2#2", "0#1");
            var r = Apply(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(1, 1)));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(6, r.Outcome.Cleared.Count);
            for (int row = 0; row < 3; row++) Assert.AreEqual(CellType.Stone, s.Board[1, row].Type);
        }

        [Test]
        public void BlastNeedsSomethingToClear()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "00###");
            var r = Apply(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(4, 0)));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(PowerUpFailure.NothingToClear, r.Failure);
        }

        [Test]
        public void SwapExchangesNeighbours()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "001", "120", "201");
            int a = s.Board[2, 2].Id, b = s.Board[2, 1].Id;
            var r = Apply(s, PowerUpRequest.Pair(PowerUpId.Swap, new GridPos(2, 2), new GridPos(2, 1)));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(b, s.Board[2, 2].Id);
            Assert.AreEqual(a, s.Board[2, 1].Id);
            Assert.AreEqual(0, s.Score);
        }

        [Test]
        public void SwapRejectsInvalidPairs()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "001", "12#", "201");
            Assert.AreEqual(PowerUpFailure.NotAdjacent, Apply(s, PowerUpRequest.Pair(PowerUpId.Swap, new GridPos(0, 0), new GridPos(2, 2))).Failure);
            Assert.AreEqual(PowerUpFailure.SameColour, Apply(s, PowerUpRequest.Pair(PowerUpId.Swap, new GridPos(0, 2), new GridPos(1, 2))).Failure);
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Apply(s, PowerUpRequest.Pair(PowerUpId.Swap, new GridPos(2, 2), new GridPos(2, 1))).Failure);
        }

        [Test]
        public void ColourShiftRepaintsOneTile()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "001", "120", "201");
            int id = s.Board[2, 2].Id;
            Assert.IsTrue(Apply(s, PowerUpRequest.CellColour(PowerUpId.ColourShift, new GridPos(2, 2), 0)).Ok);
            Assert.AreEqual(0, s.Board[2, 2].Colour);
            Assert.AreEqual(id, s.Board[2, 2].Id);
            Assert.AreEqual(PowerUpFailure.SameColour, Apply(s, PowerUpRequest.CellColour(PowerUpId.ColourShift, new GridPos(2, 2), 0)).Failure);
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Apply(s, PowerUpRequest.CellColour(PowerUpId.ColourShift, new GridPos(2, 2), 5)).Failure);
        }

        [Test]
        public void TileBreakerCanBreakAStone()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "00#", "111", "222");
            var r = Apply(s, PowerUpRequest.AtCell(PowerUpId.TileBreaker, new GridPos(2, 2)));
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.Outcome.Cleared[0].WasStone);
            Assert.AreEqual(CellType.Colour, s.Board[2, 2].Type, "the freed cell refills");
            Assert.AreEqual(0, s.Score, "breaking stones scores nothing");
        }

        [Test]
        public void TileBreakerCannotBreakVoids()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "00x", "111", "222");
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Apply(s, PowerUpRequest.AtCell(PowerUpId.TileBreaker, new GridPos(2, 2))).Failure);
        }

        [Test]
        public void ColourBurstRemovesEveryTileOfThatColour()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "001", "120", "201");
            var r = Apply(s, PowerUpRequest.AtCell(PowerUpId.ColourBurst, new GridPos(0, 2)));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(4, r.Outcome.Cleared.Count);
            Assert.AreEqual(80, s.Score);
        }

        [Test]
        public void ExtraMovesAddsThreeInCampaignOnly()
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "000", "111", "222");
            Assert.IsTrue(Apply(s, PowerUpRequest.Instant(PowerUpId.ExtraMoves)).Ok);
            Assert.AreEqual(8, s.MovesLeft);
            var endless = TestUtil.Session(GameMode.Endless, 5, 0, 3, "000", "111", "222");
            Assert.IsFalse(Apply(endless, PowerUpRequest.Instant(PowerUpId.ExtraMoves)).Ok);
        }

        [Test]
        public void ChallengeModesRejectEveryPowerUpAtEngineLevel()
        {
            foreach (var mode in new[] { GameMode.Daily, GameMode.WeeklyRush })
            {
                var s = TestUtil.Session(mode, 5, 100, 3, "000", "111", "222");
                s.StartTimer();
                foreach (PowerUpId id in System.Enum.GetValues(typeof(PowerUpId)))
                {
                    var r = Apply(s, new PowerUpRequest { Id = id, A = new GridPos(1, 1), B = new GridPos(1, 2), Colour = 1 });
                    Assert.IsFalse(r.Ok, id + " must be blocked in " + mode);
                    Assert.AreEqual(PowerUpFailure.NotAllowedInMode, r.Failure);
                }
            }
        }

        [Test]
        public void PowerUpServiceConsumesOnlyAfterASuccessfulEffect()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Inventory.AddOwned(PowerUpId.Blast, 1);
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "00####", "11####");
            var o = new MoveOutcome();
            Assert.IsFalse(ctx.PowerUpUse.TryUse(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(5, 1)), o, out var block, out var failure));
            Assert.AreEqual(PowerUpFailure.NothingToClear, failure);
            Assert.AreEqual(1, ctx.Inventory.Owned(PowerUpId.Blast), "a rejected target costs nothing");
            Assert.IsTrue(ctx.PowerUpUse.TryUse(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(0, 0)), o, out block, out failure));
            Assert.AreEqual(0, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.AreEqual(UseBlock.NoneOwned, ctx.PowerUpUse.CanSelect(s, PowerUpId.Blast));
        }

        [Test]
        public void TrialUsesAreSpentBeforePurchasedOnes()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Inventory.AddOwned(PowerUpId.Blast, 1);
            ctx.Inventory.AddTrials(PowerUpId.Blast, 1);
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "000", "111", "222");
            var o = new MoveOutcome();
            Assert.IsTrue(ctx.PowerUpUse.TryUse(s, PowerUpRequest.AtCell(PowerUpId.Blast, new GridPos(1, 1)), o, out _, out _));
            Assert.IsTrue(o.UsedTrial);
            Assert.AreEqual(0, ctx.Inventory.Trials(PowerUpId.Blast));
            Assert.AreEqual(1, ctx.Inventory.Owned(PowerUpId.Blast));
        }

        [Test]
        public void LockedAndModeRestrictedPowerUpsCannotBeSelected()
        {
            var ctx = TestUtil.Context();
            ctx.Inventory.AddOwned(PowerUpId.Blast, 1);
            var s = TestUtil.Session(GameMode.Campaign, 5, 100000, 3, "000", "111", "222");
            Assert.AreEqual(UseBlock.Locked, ctx.PowerUpUse.CanSelect(s, PowerUpId.Blast), "Blast unlocks at level 10");
            ctx.Data.HighestUnlocked = 100;
            ctx.Inventory.AddOwned(PowerUpId.ExtraMoves, 1);
            var endless = TestUtil.Session(GameMode.Endless, 5, 0, 3, "000", "111", "222");
            Assert.AreEqual(UseBlock.NotAllowedInMode, ctx.PowerUpUse.CanSelect(endless, PowerUpId.ExtraMoves));
            Assert.AreEqual(UseBlock.None, ctx.PowerUpUse.CanSelect(endless, PowerUpId.Blast));
            var daily = TestUtil.Session(GameMode.Daily, 5, 100, 3, "000", "111", "222");
            Assert.AreEqual(UseBlock.NotAllowedInMode, ctx.PowerUpUse.CanSelect(daily, PowerUpId.Blast));
            Assert.IsFalse(ctx.PowerUpUse.IsVisibleIn(PowerUpId.Blast, GameMode.Daily));
            Assert.IsFalse(ctx.PowerUpUse.IsVisibleIn(PowerUpId.Blast, GameMode.WeeklyRush));
        }

        [Test]
        public void ExtraMovesCanReviveAFailedLevelThroughTheService()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Inventory.AddOwned(PowerUpId.ExtraMoves, 1);
            var s = TestUtil.Session(GameMode.Campaign, 1, 100000, 3, "000", "111", "222");
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            Assert.AreEqual(SessionState.Failed, s.State);
            var o = new MoveOutcome();
            Assert.IsTrue(ctx.PowerUpUse.TryUse(s, PowerUpRequest.Instant(PowerUpId.ExtraMoves), o, out _, out _));
            Assert.AreEqual(SessionState.Playing, s.State);
            Assert.AreEqual(3, s.MovesLeft);
            Assert.AreEqual(3, o.MovesAdded);
        }

        [Test]
        public void EveryDefinitionHasAnEffectAndSensibleData()
        {
            var defs = PowerUpCatalog.CreateDefaults();
            Assert.AreEqual(6, defs.Length);
            foreach (var d in defs)
            {
                Assert.IsNotNull(PowerUpEffects.Get(d.id), d.id + " has no effect");
                Assert.Greater(d.price, 0);
                Assert.IsNotEmpty(d.displayName);
                Assert.IsNotEmpty(d.description);
                Assert.IsFalse(d.allowedModes.Has(GameMode.Daily));
                Assert.IsFalse(d.allowedModes.Has(GameMode.WeeklyRush));
            }
            var catalog = new PowerUpCatalog(defs);
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 40, 50, 60 }, new[] { catalog.All[0].unlockLevel, catalog.All[1].unlockLevel, catalog.All[2].unlockLevel, catalog.All[3].unlockLevel, catalog.All[4].unlockLevel, catalog.All[5].unlockLevel });
            Assert.AreEqual(100, catalog.Get(PowerUpId.Blast).price);
            Assert.AreEqual(75, catalog.Get(PowerUpId.Swap).price);
            Assert.AreEqual(150, catalog.Get(PowerUpId.ColourShift).price);
            Assert.AreEqual(125, catalog.Get(PowerUpId.TileBreaker).price);
            Assert.AreEqual(200, catalog.Get(PowerUpId.ExtraMoves).price);
            Assert.AreEqual(250, catalog.Get(PowerUpId.ColourBurst).price);
        }
    }
}
