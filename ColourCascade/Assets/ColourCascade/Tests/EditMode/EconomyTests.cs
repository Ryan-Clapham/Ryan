using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    public class WalletTests
    {
        [Test]
        public void CreditAndDebitAreValidatedAndLogged()
        {
            var ctx = TestUtil.Context();
            var w = ctx.Wallet;
            Assert.AreEqual(0, w.Balance);
            Assert.IsFalse(w.Credit(0, "x"));
            Assert.IsFalse(w.Credit(-5, "x"));
            Assert.IsTrue(w.Credit(100, "test.credit"));
            Assert.IsFalse(w.TryDebit(101, "too much"));
            Assert.IsFalse(w.TryDebit(-1, "negative"));
            Assert.AreEqual(100, w.Balance);
            Assert.IsTrue(w.TryDebit(40, "test.debit"));
            Assert.AreEqual(60, w.Balance);
            Assert.AreEqual(2, w.Ledger.Count);
            Assert.AreEqual(-40, w.Ledger[1].Delta);
            Assert.AreEqual(60, w.Ledger[1].Balance);
            Assert.AreEqual("test.debit", w.Ledger[1].Reason);
        }

        [Test]
        public void ChangedEventReportsBeforeAndAfter()
        {
            var ctx = TestUtil.Context();
            int before = -1, after = -1;
            ctx.Wallet.Changed += (b, a) => { before = b; after = a; };
            ctx.Wallet.Credit(25, "x");
            Assert.AreEqual(0, before);
            Assert.AreEqual(25, after);
        }

        [Test]
        public void LedgerIsCapped()
        {
            var ctx = TestUtil.Context();
            for (int i = 0; i < 500; i++) ctx.Wallet.Credit(1, "x");
            Assert.AreEqual(ctx.Config.Economy.ledgerCapacity, ctx.Wallet.Ledger.Count);
            Assert.AreEqual(500, ctx.Wallet.Balance);
        }

        [Test]
        public void LedgerReasonsReadAsPlainEnglish()
        {
            Assert.AreEqual("Level 4 cleared", RewardKeys.Describe(RewardKeys.CampaignFirstClear(4)));
            Assert.AreEqual("Level 4, star 2", RewardKeys.Describe(RewardKeys.CampaignStar(4, 2)));
            Assert.AreEqual("Level 10 milestone", RewardKeys.Describe(RewardKeys.CampaignMilestone(10)));
            Assert.AreEqual("Daily 2026-10-25 complete", RewardKeys.Describe(RewardKeys.DailyComplete("2026-10-25")));
            Assert.AreEqual("Daily 2026-10-25, 3 stars", RewardKeys.Describe(RewardKeys.DailyThreeStars("2026-10-25")));
            Assert.AreEqual("Weekly Rush 2026-W43, tier 1", RewardKeys.Describe(RewardKeys.WeeklyTier("2026-W43", 0)));
            Assert.AreEqual("Endless, 25,000 points", RewardKeys.Describe(RewardKeys.EndlessMilestone(25000)));
            Assert.AreEqual("Developer coins", RewardKeys.Describe("debug.grant"));
            Assert.AreEqual("unrecognised.key", RewardKeys.Describe("unrecognised.key"));

            // Purchases, through the real shop.
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Wallet.Credit(1000, "debug.grant");
            Assert.IsTrue(ctx.Shop.TryBuy(PowerUpId.ColourShift).Allowed);
            Assert.AreEqual("Bought Colour Shift", RewardKeys.Describe(ctx.Wallet.Ledger[ctx.Wallet.Ledger.Count - 1].Reason));
        }
    }

    public class ShopTests
    {
        static GameContext Unlocked(int coins)
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            if (coins > 0) ctx.Wallet.Credit(coins, "test");
            return ctx;
        }

        [Test]
        public void PurchaseDeductsCoinsAndAddsStock()
        {
            var ctx = Unlocked(300);
            var r = ctx.Shop.TryBuy(PowerUpId.Blast);
            Assert.IsTrue(r.Allowed);
            Assert.AreEqual(200, ctx.Wallet.Balance);
            Assert.AreEqual(1, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.AreEqual("shop.Blast.x1", ctx.Wallet.Ledger.Last().Reason);
        }

        [Test]
        public void InsufficientFundsBlocksPurchaseWithoutChangingAnything()
        {
            var ctx = Unlocked(60);
            var r = ctx.Shop.TryBuy(PowerUpId.Blast);
            Assert.IsFalse(r.Allowed);
            Assert.AreEqual(PurchaseBlock.InsufficientFunds, r.Block);
            Assert.AreEqual(40, r.Shortfall);
            StringAssert.Contains("40 more coins", r.Message);
            Assert.AreEqual(60, ctx.Wallet.Balance);
            Assert.AreEqual(0, ctx.Inventory.Owned(PowerUpId.Blast));
        }

        [Test]
        public void LockedItemsCannotBeBought()
        {
            var ctx = TestUtil.Context();
            ctx.Wallet.Credit(1000, "test");
            var r = ctx.Shop.TryBuy(PowerUpId.ColourBurst);
            Assert.AreEqual(PurchaseBlock.Locked, r.Block);
            Assert.AreEqual(1000, ctx.Wallet.Balance);
        }

        [Test]
        public void PurchasesAreBlockedDuringChallengeRunsOnly()
        {
            var ctx = Unlocked(1000);
            foreach (var mode in new[] { GameMode.Daily, GameMode.WeeklyRush })
            {
                ctx.Activity.BeginRun(mode);
                Assert.AreEqual(PurchaseBlock.ChallengeInProgress, ctx.Shop.TryBuy(PowerUpId.Blast).Block);
                ctx.Activity.EndRun();
            }
            ctx.Activity.BeginRun(GameMode.Campaign);
            Assert.IsTrue(ctx.Shop.TryBuy(PowerUpId.Blast).Allowed);
            ctx.Activity.BeginRun(GameMode.Endless);
            Assert.IsTrue(ctx.Shop.TryBuy(PowerUpId.Blast).Allowed);
            ctx.Activity.EndRun();
            Assert.IsTrue(ctx.Shop.TryBuy(PowerUpId.Blast).Allowed);
            Assert.AreEqual(1000 - 300, ctx.Wallet.Balance);
        }

        [Test]
        public void PricesComeFromConfiguration()
        {
            var cfg = GameConfigData.CreateDefault();
            cfg.PowerUps.First(p => p.id == PowerUpId.Swap).price = 5;
            var ctx = new GameContext(cfg, new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            ctx.Load();
            ctx.Data.HighestUnlocked = 100;
            ctx.Wallet.Credit(10, "t");
            Assert.IsTrue(ctx.Shop.TryBuy(PowerUpId.Swap, 2).Allowed);
            Assert.AreEqual(0, ctx.Wallet.Balance);
        }
    }

    public class RewardTests
    {
        static RunResult WinCampaign(GameContext ctx, int level, int extraBonusMoves = 0)
        {
            var setup = ctx.CreateCampaignRun(level);
            var spec = setup.Level;
            // Force a quick win on a copy of the level with a tiny target.
            var quick = new LevelSpec
            {
                Id = spec.Id, Mode = GameMode.Campaign, Number = spec.Number, Layout = LevelLayout.Parse(new[] { "000", "111", "222" }),
                Colours = 3, Moves = 3 + extraBonusMoves, Target = 10, Star2 = 20, Star3 = 30, LeftoverBonusPerMove = 10,
                IsMilestone = spec.IsMilestone, IsRegionFinale = spec.IsRegionFinale,
            };
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = quick, BoardSeed = 1 });
            s.Start();
            ctx.BeginRun(s);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            Assert.AreEqual(SessionState.Won, s.State);
            return ctx.FinishRun(s);
        }

        [Test]
        public void FirstLevelRewardIsLowerThanTheOld30Coins()
        {
            var eco = new EconomyConfig();
            Assert.Less(eco.FirstClearReward(1), 30);
            Assert.AreEqual(10, eco.FirstClearReward(1));
            Assert.AreEqual(12, eco.FirstClearReward(6));
            Assert.AreEqual(30, eco.FirstClearReward(200));
        }

        [Test]
        public void FirstClearIsPaidOnceAndReplaysPayNothingExtra()
        {
            var ctx = TestUtil.Context();
            var first = WinCampaign(ctx, 1);
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual(10 + 3 * 2, first.Rewards.TotalCoins, "first clear + three new stars");
            int balance = ctx.Wallet.Balance;
            var replay = WinCampaign(ctx, 1);
            Assert.IsFalse(replay.FirstClear);
            Assert.AreEqual(0, replay.Rewards.TotalCoins);
            Assert.AreEqual(balance, ctx.Wallet.Balance);
        }

        [Test]
        public void NewStarsOnReplayPayOnlyForTheNewStars()
        {
            var ctx = TestUtil.Context();
            var spec = ctx.Catalog.Get(1);
            var r1 = ctx.Rewards.ForCampaignWin(spec, 1);
            Assert.AreEqual(10 + 2, r1.TotalCoins);
            var r2 = ctx.Rewards.ForCampaignWin(spec, 3);
            Assert.AreEqual(2 * 2, r2.TotalCoins);
            var r3 = ctx.Rewards.ForCampaignWin(spec, 3);
            Assert.AreEqual(0, r3.TotalCoins);
        }

        [Test]
        public void MilestonesAndRegionFinalesPayOnce()
        {
            var ctx = TestUtil.Context();
            var ten = ctx.Catalog.Get(10);
            var twenty = ctx.Catalog.Get(20);
            Assert.IsTrue(ten.IsMilestone);
            Assert.IsTrue(twenty.IsRegionFinale);
            var a = ctx.Rewards.ForCampaignWin(ten, 1);
            Assert.IsTrue(a.Lines.Any(l => l.Kind == RewardKind.Milestone && l.Coins == 50));
            var b = ctx.Rewards.ForCampaignWin(twenty, 1);
            Assert.IsTrue(b.Lines.Any(l => l.Kind == RewardKind.RegionFinale && l.Coins == 100));
            Assert.AreEqual(0, ctx.Rewards.ForCampaignWin(ten, 1).TotalCoins);
        }

        [Test]
        public void FinishRunIsIdempotent()
        {
            var ctx = TestUtil.Context();
            var s = TestUtil.Session(GameMode.Campaign, 3, 10, 3, "000", "111", "222");
            ctx.BeginRun(s);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            var a = ctx.FinishRun(s);
            int balance = ctx.Wallet.Balance;
            int games = ctx.Data.Stats.GamesPlayed;
            var b = ctx.FinishRun(s);
            Assert.AreSame(a, b);
            Assert.AreEqual(balance, ctx.Wallet.Balance);
            Assert.AreEqual(games, ctx.Data.Stats.GamesPlayed);
        }

        [Test]
        public void DailyRewardOncePerDayAndNotForEarlierDates()
        {
            var ctx = TestUtil.Context();
            ctx.NoteDaySeen("2026-10-25");
            Assert.AreEqual(30, ctx.Rewards.ForDaily("2026-10-25", true, 1).TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-25", true, 1).TotalCoins, "replaying the same daily pays nothing");
            Assert.AreEqual(10, ctx.Rewards.ForDaily("2026-10-25", true, 3).TotalCoins, "three stars later pays the bonus once");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-25", true, 3).TotalCoins);
            ctx.NoteDaySeen("2026-10-26");
            Assert.AreEqual(30, ctx.Rewards.ForDaily("2026-10-26", true, 1).TotalCoins, "a new day pays again");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-24", true, 1).TotalCoins, "winding the clock back pays nothing");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-27", false, 0).TotalCoins, "incomplete runs pay nothing");
        }

        [Test]
        public void WeeklyTiersPayOncePerWeek()
        {
            var ctx = TestUtil.Context();
            var r = ctx.Rewards.ForWeekly("2026-W43", 6000);
            Assert.AreEqual(20 + 30, r.TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForWeekly("2026-W43", 6000).TotalCoins);
            Assert.AreEqual(50, ctx.Rewards.ForWeekly("2026-W43", 9000).TotalCoins, "only the newly reached Gold tier pays");
            Assert.AreEqual(100, ctx.Rewards.ForWeekly("2026-W44", 9000).TotalCoins, "next week starts fresh");
        }

        [Test]
        public void EndlessMilestonesAreOneTimeOnly()
        {
            var ctx = TestUtil.Context();
            Assert.AreEqual(20 + 30, ctx.Rewards.ForEndless(7000).TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForEndless(7000).TotalCoins);
            Assert.AreEqual(50, ctx.Rewards.ForEndless(12000).TotalCoins);
        }

        [Test]
        public void EndlessRunsNeverPayCampaignRewards()
        {
            var ctx = TestUtil.Context();
            var s = new PuzzleSession(ctx.CreateEndlessRun());
            s.Start();
            ctx.BeginRun(s);
            Bots.PlayOut(s, BotKind.Greedy, new Rng(1));
            var r = ctx.FinishRun(s);
            Assert.IsFalse(r.Rewards.Lines.Any(l => l.Kind == RewardKind.FirstClear || l.Kind == RewardKind.NewStar || l.Kind == RewardKind.Milestone));
            Assert.AreEqual(1, ctx.Campaign.HighestUnlocked, "endless never unlocks campaign levels");
        }

        [Test]
        public void PowerUpTrialsAreGrantedExactlyOnce()
        {
            var ctx = TestUtil.Context();
            var def = ctx.GrantIntroTrialsFor(10);
            Assert.IsNotNull(def);
            Assert.AreEqual(PowerUpId.Blast, def.id);
            Assert.AreEqual(2, ctx.Inventory.Trials(PowerUpId.Blast));
            Assert.IsNull(ctx.GrantIntroTrialsFor(10));
            Assert.AreEqual(2, ctx.Inventory.Trials(PowerUpId.Blast));
            Assert.IsNull(ctx.GrantIntroTrialsFor(11));
        }

        [Test]
        public void EarlyEconomyAffordsTheFirstPowerUpByItsIntroduction()
        {
            // Clearing levels 1-10 with average stars should comfortably afford at least one Blast.
            var ctx = TestUtil.Context();
            int total = 0;
            for (int level = 1; level <= 10; level++) total += ctx.Rewards.ForCampaignWin(ctx.Catalog.Get(level), 2).TotalCoins;
            Assert.GreaterOrEqual(total, ctx.PowerUps.Get(PowerUpId.Blast).price);
            Assert.Less(total, 400, "not so generous that coins stop mattering");
        }
    }

    public class ProgressionTests
    {
        [Test]
        public void BoardSizesFollowTheAgreedProgression()
        {
            var catalog = new CampaignCatalog(GameConfigData.CreateDefault().Campaign, new ScoringRules());
            for (int n = 1; n <= 3; n++) Assert.AreEqual((6, 6), (catalog.Get(n).Width, catalog.Get(n).Height), "level " + n);
            for (int n = 4; n <= 6; n++) Assert.AreEqual((7, 7), (catalog.Get(n).Width, catalog.Get(n).Height), "level " + n);
            foreach (int n in new[] { 7, 8, 15, 60, 150 }) Assert.AreEqual((8, 9), (catalog.Get(n).Width, catalog.Get(n).Height), "level " + n + " is 8 columns × 9 rows");
        }

        [Test]
        public void GeneratedLevelsAreValidAndDeterministic()
        {
            var cfg = GameConfigData.CreateDefault();
            var a = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            var b = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            for (int n = 1; n <= 120; n++)
            {
                var x = a.Get(n);
                var y = b.Get(n);
                CollectionAssert.AreEqual(x.Layout.ToRows(), y.Layout.ToRows(), "level " + n + " layout must be stable");
                Assert.AreEqual(x.Target, y.Target, "level " + n + " target must be stable");
                Assert.IsTrue(x.Layout.Validate(out string error), "level " + n + ": " + error);
                Assert.Greater(x.Moves, 0);
                Assert.Greater(x.Target, 0);
                Assert.Greater(x.Star2, x.Target);
                Assert.Greater(x.Star3, x.Star2);
                Assert.Greater(x.LeftoverBonusPerMove, 0);
                Assert.IsTrue(x.Colours >= 3 && x.Colours <= 6);
            }
        }

        [Test]
        public void StonesAreIntroducedAtLevelEight()
        {
            var catalog = new CampaignCatalog(GameConfigData.CreateDefault().Campaign, new ScoringRules());
            for (int n = 1; n < 8; n++) Assert.AreEqual(0, catalog.Get(n).StoneCount, "no stones before level 8");
            Assert.Greater(catalog.Get(8).StoneCount, 0);
            Assert.AreEqual(TutorialKeys.Stones, catalog.Get(8).TutorialKey);
        }

        [Test]
        public void FirstLevelUsesTheTeachingBoard()
        {
            var catalog = new CampaignCatalog(GameConfigData.CreateDefault().Campaign, new ScoringRules());
            var l1 = catalog.Get(1);
            Assert.IsTrue(l1.Layout.HasFixedColours);
            Assert.AreEqual(3, l1.Colours);
            Assert.AreEqual(TutorialKeys.TapGroup, l1.TutorialKey);
        }

        [Test]
        public void EarlyLevelsAreFriendly()
        {
            var cfg = GameConfigData.CreateDefault();
            var catalog = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            for (int n = 1; n <= 5; n++)
            {
                var spec = catalog.Get(n);
                int wins = 0;
                for (int i = 0; i < 30; i++)
                {
                    var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, BoardSeed = (ulong)(i * 31 + n) });
                    s.Start();
                    Bots.PlayOut(s, BotKind.Casual, new Rng((ulong)i));
                    if (s.State == SessionState.Won) wins++;
                }
                Assert.GreaterOrEqual(wins, 24, "level " + n + " should be beatable by a relaxed player at least 80% of the time");
            }
        }

        [Test]
        public void WinsUnlockTheNextLevelAndReplaysNeverLoseProgress()
        {
            var ctx = TestUtil.Context();
            var p = ctx.Campaign;
            Assert.AreEqual(1, p.HighestUnlocked);
            Assert.IsFalse(p.IsUnlocked(2));
            var w = p.RecordWin(1, 5000, 3);
            Assert.IsTrue(w.FirstClear);
            Assert.IsTrue(w.UnlockedNext);
            Assert.AreEqual(2, p.HighestUnlocked);
            var replay = p.RecordWin(1, 1000, 1);
            Assert.IsFalse(replay.FirstClear);
            Assert.AreEqual(3, p.StarsFor(1), "stars are never reduced");
            Assert.AreEqual(5000, p.BestScore(1), "best score is never reduced");
            Assert.AreEqual(2, p.HighestUnlocked);
            Assert.AreEqual(3, p.TotalStars);
        }

        [Test]
        public void CampaignRunForLockedLevelIsRefused()
        {
            var ctx = TestUtil.Context();
            Assert.Throws<InvalidOperationException>(() => ctx.CreateCampaignRun(5));
        }

        [Test]
        public void RegionsCycleThroughThemes()
        {
            Assert.AreEqual("Sunrise Meadow", Regions.ForLevel(1, 20).Name);
            Assert.AreEqual("Sunrise Meadow", Regions.ForLevel(20, 20).Name);
            Assert.AreEqual("Coral Lagoon", Regions.ForLevel(21, 20).Name);
            Assert.AreEqual("Sunrise Meadow II", Regions.ForLevel(121, 20).Name);
            Assert.AreEqual(21, Regions.ForLevel(25, 20).FirstLevel);
        }

        [Test]
        public void LayoutLibraryProducesValidLayouts()
        {
            var rng = new Rng(9);
            foreach (LayoutPattern p in Enum.GetValues(typeof(LayoutPattern)))
                for (int tier = 0; tier <= 3; tier++)
                    foreach (var size in new[] { (8, 9), (7, 7), (6, 6) })
                    {
                        var l = LayoutLibrary.Build(p, size.Item1, size.Item2, tier, rng);
                        Assert.IsTrue(l.Validate(out string error), p + " tier " + tier + " " + size + ": " + error);
                    }
        }
    }
}
