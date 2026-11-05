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
                Colours = 3, Moves = 3 + extraBonusMoves, Target = 10, Star2 = 20, Star3 = 30,
                IsMilestone = spec.IsMilestone, IsRegionFinale = spec.IsRegionFinale,
            };
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = quick, BoardSeed = 1 });
            s.Start();
            ctx.BeginRun(s);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            Assert.IsTrue(s.Passed);
            Assert.IsTrue(s.Finish());
            Assert.AreEqual(SessionState.Won, s.State);
            return ctx.FinishRun(s);
        }

        [Test]
        public void FirstLevelRewardIsLowerThanTheOld30Coins()
        {
            // Update 3.1: 3 coins on levels 1-25, +1 every 25 levels, 8 from level 126 and never more.
            var eco = new EconomyConfig();
            Assert.Less(eco.FirstClearReward(1), 30);
            Assert.AreEqual(3, eco.FirstClearReward(1));
            Assert.AreEqual(3, eco.FirstClearReward(25));
            Assert.AreEqual(4, eco.FirstClearReward(26));
            Assert.AreEqual(5, eco.FirstClearReward(51));
            Assert.AreEqual(6, eco.FirstClearReward(76));
            Assert.AreEqual(7, eco.FirstClearReward(101));
            Assert.AreEqual(7, eco.FirstClearReward(125));
            Assert.AreEqual(8, eco.FirstClearReward(126));
            Assert.AreEqual(8, eco.FirstClearReward(500));
            Assert.AreEqual(8, eco.FirstClearReward(10000));
            int previous = 0;
            for (int level = 1; level <= 600; level++)
            {
                int r = eco.FirstClearReward(level);
                Assert.GreaterOrEqual(r, previous, "never falls");
                Assert.LessOrEqual(r - previous, level == 1 ? 3 : 1, "rises by at most one coin at a time");
                Assert.LessOrEqual(r, 8);
                previous = r;
            }
        }

        [Test]
        public void FirstClearIsPaidOnceAndReplaysPayNothingExtra()
        {
            var ctx = TestUtil.Context();
            var first = WinCampaign(ctx, 1);
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual(3 + 2 + 3 + 5, first.Rewards.TotalCoins, "first clear + the three stars (2, 3 and 5)");
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
            // Update 3.1: the first star pays 2, the second 3, the third 5, each the first time it is earned.
            var r1 = ctx.Rewards.ForCampaignWin(spec, 1);
            Assert.AreEqual(3 + 2, r1.TotalCoins, "first clear + the first star");
            var again = ctx.Rewards.ForCampaignWin(spec, 1);
            Assert.AreEqual(0, again.TotalCoins, "the same star again pays nothing");
            var r2 = ctx.Rewards.ForCampaignWin(spec, 2);
            Assert.AreEqual(3, r2.TotalCoins, "one star to two: only the second star's 3");
            var r3 = ctx.Rewards.ForCampaignWin(spec, 3);
            Assert.AreEqual(5, r3.TotalCoins, "two to three: only the third star's 5");
            Assert.AreEqual(0, ctx.Rewards.ForCampaignWin(spec, 3).TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForCampaignWin(spec, 1).TotalCoins, "a worse result later pays nothing either");
            var other = ctx.Catalog.Get(2);
            Assert.AreEqual(3 + 2 + 3 + 5, ctx.Rewards.ForCampaignWin(other, 3).TotalCoins, "three stars at once: 10 for the stars");
            Assert.AreEqual(1, ctx.Rewards.ForCampaignWin(ctx.Catalog.Get(3), 3).Lines.Count(l => l.Kind == RewardKind.NewStar), "shown as one line");
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
            Assert.IsTrue(a.Lines.Any(l => l.Kind == RewardKind.Milestone && l.Coins == 20));
            var b = ctx.Rewards.ForCampaignWin(twenty, 1);
            Assert.IsTrue(b.Lines.Any(l => l.Kind == RewardKind.RegionFinale && l.Coins == 40));
            Assert.AreEqual(0, ctx.Rewards.ForCampaignWin(ten, 1).TotalCoins);
        }

        [Test]
        public void FinishRunIsIdempotent()
        {
            var ctx = TestUtil.Context();
            var s = TestUtil.Session(GameMode.Campaign, 3, 10, 3, "000", "111", "222");
            ctx.BeginRun(s);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            s.Finish();
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
            Assert.AreEqual(10, ctx.Rewards.ForDaily("2026-10-25", true, 1).TotalCoins, "10 for completing (update 3.1)");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-25", true, 1).TotalCoins, "replaying the same daily pays nothing");
            Assert.AreEqual(5, ctx.Rewards.ForDaily("2026-10-25", true, 3).TotalCoins, "three stars later pays the bonus once");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-25", true, 3).TotalCoins);
            ctx.NoteDaySeen("2026-10-26");
            Assert.AreEqual(15, ctx.Rewards.ForDaily("2026-10-26", true, 3).TotalCoins, "a new day pays again: at most 15 with three stars");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-24", true, 1).TotalCoins, "winding the clock back pays nothing");
            Assert.AreEqual(0, ctx.Rewards.ForDaily("2026-10-27", false, 0).TotalCoins, "incomplete runs pay nothing");
        }

        [Test]
        public void WeeklyTiersPayOncePerWeek()
        {
            var ctx = TestUtil.Context();
            var r = ctx.Rewards.ForWeekly("2026-W43", 6000);
            Assert.AreEqual(10 + 15, r.TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForWeekly("2026-W43", 6000).TotalCoins);
            Assert.AreEqual(25, ctx.Rewards.ForWeekly("2026-W43", 9000).TotalCoins, "only the newly reached Gold tier pays");
            Assert.AreEqual(50, ctx.Rewards.ForWeekly("2026-W44", 9000).TotalCoins, "next week starts fresh");
        }

        [Test]
        public void EndlessMilestonesAreOneTimeOnly()
        {
            var ctx = TestUtil.Context();
            // Update 3.1: 5 / 10 / 15 / 25 / 40 for 2,500 / 5,000 / 10,000 / 20,000 / 40,000, each once ever.
            Assert.AreEqual(5 + 10, ctx.Rewards.ForEndless(7000).TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForEndless(7000).TotalCoins, "a later run with the same score pays nothing");
            Assert.AreEqual(15, ctx.Rewards.ForEndless(12000).TotalCoins, "only the new milestone");
            Assert.AreEqual(25 + 40, ctx.Rewards.ForEndless(45000).TotalCoins);
            Assert.AreEqual(0, ctx.Rewards.ForEndless(60000).TotalCoins, "all five paid: nothing more, ever");
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
        public void TheIntroducedPowerUpIsLentOnEveryAttemptAndNeverKept()
        {
            // Playtest bug: replaying the Colour Burst level gave no free Colour Burst (it was granted once, ever).
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            ctx.Data.HighestUnlocked = 61;
            ctx.Inventory.AddOwned(PowerUpId.ColourBurst, 2);
            int burstLevel = ctx.PowerUps.Get(PowerUpId.ColourBurst).unlockLevel;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var def = ctx.LendIntroPowerUpFor(burstLevel);
                Assert.IsNotNull(def, "attempt " + attempt);
                Assert.AreEqual(PowerUpId.ColourBurst, def.id);
                Assert.AreEqual(1, ctx.PowerUpUse.Lent(PowerUpId.ColourBurst), "lent again on every attempt");
                Assert.AreEqual(3, ctx.PowerUpUse.Available(PowerUpId.ColourBurst));
                var s = TestUtil.Session(GameMode.Campaign, 20, 1000000, 4, "0123", "1230", "2301");
                var o = new MoveOutcome();
                Assert.IsTrue(ctx.PowerUpUse.TryUse(s, new PowerUpRequest { Id = PowerUpId.ColourBurst, A = new GridPos(0, 0) }, o, out var block, out var failure));
                Assert.IsTrue(o.UsedTrial, "the lent use goes first");
                Assert.AreEqual(0, ctx.PowerUpUse.Lent(PowerUpId.ColourBurst));
                Assert.AreEqual(2, ctx.Inventory.Owned(PowerUpId.ColourBurst), "the player's own power-ups are untouched");
                Assert.AreEqual(0, ctx.Inventory.Trials(PowerUpId.ColourBurst), "nothing was added to the inventory");
            }
            // An unused loan never outlives its run: the next level takes it back.
            ctx.LendIntroPowerUpFor(burstLevel);
            Assert.IsNull(ctx.LendIntroPowerUpFor(burstLevel + 1));
            Assert.AreEqual(0, ctx.PowerUpUse.Lent(PowerUpId.ColourBurst));
            Assert.AreEqual(2, ctx.Inventory.Available(PowerUpId.ColourBurst));

            // Closing and reopening the game: nothing about loans is saved, and the level lends again.
            Assert.IsTrue(ctx.Save());
            var reopened = TestUtil.Context(null, store);
            Assert.AreEqual(0, reopened.PowerUpUse.Lent(PowerUpId.ColourBurst));
            Assert.AreEqual(2, reopened.Inventory.Owned(PowerUpId.ColourBurst));
            Assert.IsNotNull(reopened.LendIntroPowerUpFor(burstLevel));
            Assert.AreEqual(1, reopened.PowerUpUse.Lent(PowerUpId.ColourBurst));
            Assert.IsFalse(store.Main.Contains("pu.trial."), "no once-only claim is recorded any more");
        }

        [Test]
        public void EveryPowerUpIntroductionLendsItsDemonstration()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 200;
            foreach (var def in ctx.PowerUps.All)
            {
                if (def.unlockLevel <= 0 || def.trialUses <= 0) continue;
                Assert.AreEqual(def.id, ctx.LendIntroPowerUpFor(def.unlockLevel).id, def.displayName);
                Assert.AreEqual(def.trialUses, ctx.PowerUpUse.Lent(def.id), def.displayName);
                Assert.AreEqual(0, ctx.Inventory.Trials(def.id), def.displayName);
            }
        }

        [Test]
        public void EarlyCampaignCoinsKeepTheFirstPowerUpMeaningful()
        {
            // Playtest feedback: power-ups were too easy to buy. Levels 1-10 with two stars each earn about one Blast
            // (update 3.1: 3 coins a first clear, 2 + 3 for two stars, 20 for the milestone), when the Blast unlocks; its
            // two free uses cover the introduction, so buying one is a choice rather than a given.
            var ctx = TestUtil.Context();
            int total = 0;
            for (int level = 1; level <= 10; level++) total += ctx.Rewards.ForCampaignWin(ctx.Catalog.Get(level), 2).TotalCoins;
            int blast = ctx.PowerUps.Get(PowerUpId.Blast).price;
            Assert.AreEqual(10 * 3 + 10 * (2 + 3) + 20, total);
            Assert.LessOrEqual(total, blast, "early campaign coins buy at most one Blast");
            Assert.GreaterOrEqual(total, blast * 3 / 4, "but about one");
        }

        [Test]
        public void OlderConfigsMoveToTheCurrentRewardTuning()
        {
            var defaults = GameConfigData.CreateDefault();
            var old = GameConfigData.CreateDefault();
            EconomyTuning.ApplyVersion1(old);
            Assert.AreEqual(30, old.Daily.completionReward, "version 1 values");
            old.Economy.milestoneReward = 75; // edited by hand: must be kept

            var changed = EconomyTuning.Upgrade(old, 0);
            Assert.AreEqual(defaults.Economy.firstClearBase, old.Economy.firstClearBase);
            Assert.AreEqual(defaults.Economy.firstClearMax, old.Economy.firstClearMax);
            CollectionAssert.AreEqual(new[] { 2, 3, 5 }, old.Economy.starRewards);
            Assert.AreEqual(defaults.Daily.completionReward, old.Daily.completionReward);
            CollectionAssert.AreEqual(defaults.Rush.tierRewards, old.Rush.tierRewards);
            CollectionAssert.AreEqual(defaults.Endless.milestoneRewards, old.Endless.milestoneRewards);
            Assert.AreEqual(20, old.Monetisation.ads.shopCoins.reward);
            Assert.AreEqual(3, old.Monetisation.ads.shopCoins.dailyLimit);
            Assert.AreEqual(3, old.Monetisation.ads.coinShortfall.dailyLimit);
            Assert.AreEqual(AdConfig.CoinAdvertGroup, old.Monetisation.ads.shopCoins.dailyLimitGroup);
            Assert.AreEqual(AdConfig.CoinAdvertGroup, old.Monetisation.ads.coinShortfall.dailyLimitGroup);
            Assert.AreEqual(75, old.Economy.milestoneReward);
            CollectionAssert.DoesNotContain(changed, "economy.milestoneReward");
            CollectionAssert.Contains(changed, "economy.starRewards");
            CollectionAssert.Contains(changed, "ads.dailyLimitGroup");

            Assert.IsEmpty(EconomyTuning.Upgrade(old, EconomyTuning.Current), "an up-to-date config is left alone");
        }

        [Test]
        public void AVersion2ConfigMovesToTheVersion3Economy()
        {
            // The config asset as saved before update 3.1, with one value edited by hand.
            var old = GameConfigData.CreateDefault();
            EconomyTuning.ApplyVersion(old, 2);
            Assert.AreEqual(15, old.Economy.firstClearMax, "version 2 values");
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, old.Economy.starRewards);
            Assert.AreEqual(50, old.Monetisation.ads.shopCoins.reward);
            old.Daily.threeStarBonus = 8;
            var changed = EconomyTuning.Upgrade(old, 2);
            var e = old.Economy;
            Assert.AreEqual(3, e.FirstClearReward(1));
            Assert.AreEqual(8, e.FirstClearReward(126));
            CollectionAssert.AreEqual(new[] { 2, 3, 5 }, e.starRewards);
            Assert.AreEqual(20, e.milestoneReward, "unchanged in 3.1");
            Assert.AreEqual(40, e.regionFinaleReward, "unchanged in 3.1");
            Assert.AreEqual(10, old.Daily.completionReward);
            Assert.AreEqual(8, old.Daily.threeStarBonus, "edited by hand: kept");
            CollectionAssert.AreEqual(new[] { 10, 15, 25 }, old.Rush.tierRewards, "Weekly Rush unchanged");
            CollectionAssert.AreEqual(new[] { 5, 10, 15, 25, 40 }, old.Endless.milestoneRewards);
            Assert.AreEqual(20, old.Monetisation.ads.shopCoins.reward);
            Assert.AreEqual(50, old.Monetisation.ads.coinShortfall.reward, "the shortfall advert still pays up to 50");
            CollectionAssert.DoesNotContain(changed, "daily.threeStarBonus");
        }

        [Test]
        public void AHandEditedStarRewardIsKeptForEveryStar()
        {
            var old = GameConfigData.CreateDefault();
            EconomyTuning.ApplyVersion(old, 2);
            old.Economy.coinsPerNewStar = 4;
            old.Economy.starRewards = new[] { 4, 4, 4 };
            EconomyTuning.Upgrade(old, 2);
            CollectionAssert.AreEqual(new[] { 4, 4, 4 }, old.Economy.starRewards);
            Assert.AreEqual(4, old.Economy.StarReward(3));
            // An asset saved before the field existed loads it with the new default, and keeps it.
            var fresh = GameConfigData.CreateDefault();
            fresh.Economy.coinsPerNewStar = 1;
            EconomyTuning.Upgrade(fresh, 2);
            CollectionAssert.AreEqual(new[] { 2, 3, 5 }, fresh.Economy.starRewards);
            fresh.Economy.starRewards = new int[0];
            Assert.AreEqual(1, fresh.Economy.StarReward(2), "no per-star values: the old single value");
        }

        [Test]
        public void CurrentTuningTableMatchesTheCodeDefaults()
        {
            // Upgrading version-1 values must land exactly on the defaults a new config gets.
            var fresh = GameConfigData.CreateDefault();
            for (int version = 1; version < EconomyTuning.Current; version++)
            {
                var upgraded = GameConfigData.CreateDefault();
                EconomyTuning.ApplyVersion(upgraded, version);
                Assert.AreNotEqual(Json.Write(Snapshot(fresh)), Json.Write(Snapshot(upgraded)), "version " + version + " differs");
                EconomyTuning.Upgrade(upgraded, version);
                Assert.AreEqual(Json.Write(Snapshot(fresh)), Json.Write(Snapshot(upgraded)), "from version " + version);
            }
        }

        static Dictionary<string, object> Snapshot(GameConfigData c) => new Dictionary<string, object>
        {
            ["e"] = new List<object> { (long)c.Economy.firstClearBase, (long)c.Economy.firstClearStep, (long)c.Economy.firstClearStepEvery, (long)c.Economy.firstClearMax, (long)c.Economy.coinsPerNewStar, (long)c.Economy.milestoneReward, (long)c.Economy.regionFinaleReward },
            ["d"] = new List<object> { (long)c.Daily.completionReward, (long)c.Daily.threeStarBonus },
            ["w"] = c.Rush.tierRewards.Select(v => (object)(long)v).ToList(),
            ["x"] = c.Endless.milestoneRewards.Select(v => (object)(long)v).ToList(),
            ["s"] = c.Economy.starRewards.Select(v => (object)(long)v).ToList(),
            ["a"] = new List<object> { (long)c.Monetisation.ads.shopCoins.reward, (long)c.Monetisation.ads.shopCoins.dailyLimit, (long)c.Monetisation.ads.coinShortfall.reward,
                (long)c.Monetisation.ads.coinShortfall.dailyLimit, c.Monetisation.ads.shopCoins.dailyLimitGroup, c.Monetisation.ads.coinShortfall.dailyLimitGroup },
        };
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
