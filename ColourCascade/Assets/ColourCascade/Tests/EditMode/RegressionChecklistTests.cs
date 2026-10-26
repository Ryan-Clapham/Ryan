using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// One test per item of the "Required regression checks" list in the design brief (section 21) that can
    /// be verified from game logic. UI-only items (layout, button wiring) are covered by the manual
    /// checklist in docs/TESTING.md.
    /// </summary>
    public class RegressionChecklistTests
    {
        [Test]
        public void Check01_SymbolsSettingPersistsAndIsAnActualSetting()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            SettingKind? changed = null;
            ctx.Settings.Changed += k => changed = k;
            Assert.IsFalse(ctx.Settings.Symbols, "off by default");
            ctx.Settings.Symbols = true;
            Assert.AreEqual(SettingKind.Symbols, changed, "toggling raises a change the board listens to");
            ctx.Save();
            Assert.IsTrue(TestUtil.Context(null, store).Settings.Symbols, "persists across restarts");
        }

        [Test]
        public void Check05_PurchasesDeductCoins()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Wallet.Credit(250, "t");
            Assert.IsTrue(ctx.Shop.TryBuy(PowerUpId.ColourBurst).Allowed);
            Assert.AreEqual(0, ctx.Wallet.Balance);
        }

        [Test]
        public void Check06_InsufficientFundsPreventPurchase()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Wallet.Credit(199, "t");
            Assert.AreEqual(PurchaseBlock.InsufficientFunds, ctx.Shop.TryBuy(PowerUpId.ExtraMoves).Block);
            Assert.AreEqual(199, ctx.Wallet.Balance);
            Assert.AreEqual(0, ctx.Inventory.Owned(PowerUpId.ExtraMoves));
        }

        [Test]
        public void Check07_ChallengeModesCannotBypassPowerUpRestrictions()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Wallet.Credit(10000, "t");
            foreach (var def in ctx.PowerUps.All) ctx.Inventory.AddOwned(def.id, 5);
            foreach (var setup in new[] { ctx.CreateDailyRun(), ctx.CreateWeeklyRun() })
            {
                var s = new PuzzleSession(setup);
                s.Start();
                s.StartTimer();
                ctx.BeginRun(s);
                foreach (var def in ctx.PowerUps.All)
                {
                    Assert.AreEqual(UseBlock.NotAllowedInMode, ctx.PowerUpUse.CanSelect(s, def.id));
                    var o = new MoveOutcome();
                    Assert.IsFalse(ctx.PowerUpUse.TryUse(s, new PowerUpRequest { Id = def.id, A = new GridPos(1, 1), B = new GridPos(1, 2) }, o, out _, out _));
                    Assert.AreEqual(5, ctx.Inventory.Owned(def.id));
                    Assert.AreEqual(PurchaseBlock.ChallengeInProgress, ctx.Shop.TryBuy(def.id).Block);
                }
                ctx.AbandonRun(s);
            }
            Assert.AreEqual(10000, ctx.Wallet.Balance);
        }

        [Test]
        public void Check08_ReplayingACompletedLevelDoesNotRepeatTheFirstClearReward()
        {
            var ctx = TestUtil.Context();
            var spec = ctx.Catalog.Get(1);
            int first = ctx.Rewards.ForCampaignWin(spec, 1).TotalCoins;
            Assert.Greater(first, 0);
            for (int i = 0; i < 5; i++) Assert.AreEqual(0, ctx.Rewards.ForCampaignWin(spec, 1).TotalCoins);
        }

        [Test]
        public void Check09_FirstLevelRewardIsBelowThirtyCoins()
        {
            var ctx = TestUtil.Context();
            Assert.Less(ctx.Config.Economy.FirstClearReward(1), 30);
        }

        [Test]
        public void Check11_MapStateMatchesSavedUnlocksAndStars()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            ctx.Campaign.RecordWin(1, 3000, 3);
            ctx.Campaign.RecordWin(2, 2000, 1);
            ctx.Save();
            var again = TestUtil.Context(null, store);
            Assert.AreEqual(3, again.Campaign.HighestUnlocked);
            Assert.AreEqual(3, again.Campaign.StarsFor(1));
            Assert.AreEqual(1, again.Campaign.StarsFor(2));
            Assert.IsTrue(again.Campaign.IsUnlocked(3));
            Assert.IsFalse(again.Campaign.IsUnlocked(4));
        }

        [Test]
        public void Check14_EndlessGeneratesAFreshBoardForANewRun()
        {
            var ctx = TestUtil.Context();
            var a = new PuzzleSession(ctx.CreateEndlessRun());
            var b = new PuzzleSession(ctx.CreateEndlessRun());
            a.Start();
            b.Start();
            Assert.AreNotEqual(a.Board.ToDebugString(), b.Board.ToDebugString());
        }

        [Test]
        public void Check15_DailyAndWeeklyDefinitionsAreStableForTheirPeriod()
        {
            var morning = TestUtil.Context(new System.DateTime(2026, 10, 25, 0, 5, 0, System.DateTimeKind.Utc));
            var night = TestUtil.Context(new System.DateTime(2026, 10, 25, 23, 55, 0, System.DateTimeKind.Utc));
            var d1 = new PuzzleSession(morning.CreateDailyRun());
            var d2 = new PuzzleSession(night.CreateDailyRun());
            d1.Start();
            d2.Start();
            Assert.AreEqual(d1.Board.ToDebugString(), d2.Board.ToDebugString());
            var w1 = new PuzzleSession(morning.CreateWeeklyRun());
            var w2 = new PuzzleSession(TestUtil.Context(new System.DateTime(2026, 10, 20, 9, 0, 0, System.DateTimeKind.Utc)).CreateWeeklyRun());
            w1.Start();
            w2.Start();
            Assert.AreEqual(w1.Board.ToDebugString(), w2.Board.ToDebugString());
        }

        [Test]
        public void Check16_And17_OnlyWeeklyRushHasASixtySecondCountdown()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 10;
            Assert.AreEqual(0, ctx.CreateCampaignRun(1).Level.TimeLimitSeconds);
            Assert.AreEqual(0, ctx.CreateEndlessRun().Level.TimeLimitSeconds);
            Assert.AreEqual(0, ctx.CreateDailyRun().Level.TimeLimitSeconds);
            Assert.AreEqual(60, ctx.CreateWeeklyRun().Level.TimeLimitSeconds);
        }

        [Test]
        public void Check18_DeadBoardRecoveryWorks()
        {
            var b = Board.FromRows("0123", "1230", "2301", "3012");
            Assert.IsFalse(GroupFinder.HasLegalMove(b));
            Assert.AreEqual(RecoveryResult.Shuffled, BoardRecovery.EnsurePlayable(b, new Rng(1), null));
            Assert.IsTrue(GroupFinder.HasLegalMove(b));
        }

        [Test]
        public void Check19_ScoreMovesCoinsAndProgressStayConsistentAcrossARun()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            var setup = ctx.CreateCampaignRun(1);
            var s = new PuzzleSession(setup);
            s.Start();
            ctx.BeginRun(s);
            int expectedScore = 0;
            var o = new MoveOutcome();
            var finder = new GroupFinder();
            var groups = new List<GroupInfo>();
            var rng = new Rng(3);
            while (s.AcceptsMoves)
            {
                int movesBefore = s.MovesLeft;
                Bots.Choose(s.Board, BotKind.Greedy, rng, finder, groups, out var pick);
                Assert.IsTrue(s.TryPlay(pick, o));
                expectedScore += ctx.Config.Scoring.GroupPoints(o.GroupSize);
                Assert.AreEqual(expectedScore, s.Score);
                Assert.AreEqual(movesBefore - 1, s.MovesLeft);
            }
            var result = ctx.FinishRun(s);
            var reloaded = TestUtil.Context(null, store);
            Assert.AreEqual(ctx.Wallet.Balance, reloaded.Wallet.Balance);
            Assert.AreEqual(ctx.Campaign.HighestUnlocked, reloaded.Campaign.HighestUnlocked);
            if (result.Success)
            {
                Assert.AreEqual(result.FinalScore, reloaded.Campaign.BestScore(1));
                Assert.AreEqual(result.Rewards.TotalCoins, reloaded.Wallet.Balance);
            }
        }

        [Test]
        public void Check20_PausingResumingAndRestartingDoNotCorruptState()
        {
            var ctx = TestUtil.Context();
            var s = new PuzzleSession(ctx.CreateWeeklyRun());
            s.Start();
            ctx.BeginRun(s);
            s.StartTimer();
            s.Tick(10);
            s.SetPaused(true);
            s.Tick(30);
            Assert.AreEqual(50, (int)System.Math.Round(s.TimeLeft));
            s.SetPaused(false);
            ctx.AbandonRun(s);
            Assert.IsFalse(ctx.Activity.InRun, "quitting ends the run so the shop reopens");
            var restarted = new PuzzleSession(ctx.CreateWeeklyRun());
            restarted.Start();
            Assert.AreEqual(60, (int)restarted.TimeLeft);
            Assert.AreEqual(0, restarted.Score);
        }
    }
}
