using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.1, the coin economy rebalance: one shared daily allowance for the two coin adverts on the player's own
    /// calendar day, three-packs in the shop, moving older configs across, and existing players keeping everything.
    /// (Star, first-clear, Daily and Endless rewards: EconomyTests; achievement rewards: AchievementTests; the Endless
    /// finale: EndlessFinaleTests.)
    /// </summary>
    public class Update31Tests
    {
        static MonetisationRig Rig(Action<GameConfigData> configure = null)
        {
            var rig = new MonetisationRig(configure).Start();
            Assert.AreEqual(AdServiceState.Ready, rig.M.Ads.State);
            rig.Ctx.Data.Coins = 0;
            return rig;
        }

        static void WatchShopAdvert(MonetisationRig rig)
        {
            Assert.IsTrue(rig.M.Coins.WatchForShopCoins(null), "the Coins tab advert starts");
            rig.Network.WatchToTheEnd();
            rig.Wait(31); // past the Coins tab advert's 30-second cooldown
        }

        static void WatchShortfallAdvert(MonetisationRig rig, int price)
        {
            Assert.IsTrue(rig.M.Coins.WatchForShortfall(price, "ColourBurst", null), "the not-enough-coins advert starts");
            rig.Network.WatchToTheEnd();
            rig.Wait(1);
        }

        // ================================================================== Coin adverts

        [Test]
        public void TheCoinsTabAdvertPays20CoinsThreeTimesADay()
        {
            var rig = Rig();
            Assert.AreEqual(20, rig.M.Coins.ShopReward);
            Assert.AreEqual(3, rig.M.Coins.ShopOffer().RemainingToday);
            for (int i = 0; i < 3; i++) WatchShopAdvert(rig);
            Assert.AreEqual(60, rig.Ctx.Wallet.Balance);
            var offer = rig.M.Coins.ShopOffer();
            Assert.AreEqual(OfferStatus.LimitReached, offer.Status);
            Assert.AreEqual(0, offer.RemainingToday);
            Assert.IsFalse(rig.M.Coins.WatchForShopCoins(null), "no fourth advert today");
        }

        [Test]
        public void ThreeCoinsTabAdvertsUseUpTheNotEnoughCoinsAdvertToo()
        {
            var rig = Rig();
            for (int i = 0; i < 3; i++) WatchShopAdvert(rig);
            var offer = rig.M.Coins.ForPrice(250);
            Assert.AreEqual(OfferStatus.LimitReached, offer.Ad.Status, "the same allowance");
            Assert.IsFalse(rig.M.Coins.WatchForShortfall(250, "ColourBurst", null));
            Assert.AreEqual(60, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void NotEnoughCoinsAdvertsUseUpTheCoinsTabToo()
        {
            var rig = Rig();
            WatchShortfallAdvert(rig, 250);
            Assert.AreEqual(50, rig.Ctx.Wallet.Balance, "the shortfall advert still pays up to 50");
            Assert.AreEqual(2, rig.M.Coins.ShopOffer().RemainingToday, "one of the day's three is gone");
            WatchShortfallAdvert(rig, 250);
            WatchShopAdvert(rig);
            Assert.AreEqual(50 + 50 + 20, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ForPrice(1000).Ad.Status);
        }

        [Test]
        public void FailedCancelledOrMissingAdvertsCostNothing()
        {
            var rig = Rig();
            // Closed before the reward: nothing paid, nothing counted.
            Assert.IsTrue(rig.M.Coins.WatchForShopCoins(null));
            rig.Network.Open();
            rig.Network.Close();
            rig.Wait(5);
            Assert.AreEqual(0, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(3, rig.M.Coins.ShopOffer().RemainingToday);
            Assert.AreEqual(OfferStatus.Ready, rig.M.Coins.ShopOffer().Status, "and no cooldown");
            // Failed to show.
            Assert.IsTrue(rig.M.Coins.WatchForShortfall(250, "ColourBurst", null));
            rig.Network.Fail();
            rig.Wait(5);
            Assert.AreEqual(0, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(3, rig.M.Coins.ShopOffer().RemainingToday);
            // None available.
            rig.Network.LoadSucceeds[AdFormat.Rewarded] = false;
            rig.Network.Ready[AdFormat.Rewarded] = false;
            rig.Wait(400);
            Assert.IsFalse(rig.M.Coins.ShopOffer().CanShow);
            Assert.IsFalse(rig.M.Coins.WatchForShopCoins(null));
            Assert.AreEqual(0, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(3, rig.M.Coins.ShopOffer().RemainingToday);
        }

        [Test]
        public void TheAllowanceResetsAtThePlayersOwnMidnight()
        {
            // Ten hours ahead of UTC: 12:00 UTC is 22:00 local. Local midnight comes two hours later, long before UTC's.
            var rig = Rig();
            rig.LocalOffset = TimeSpan.FromHours(10);
            Assert.AreEqual("2026-10-25", rig.M.Ads.LocalDayKey());
            for (int i = 0; i < 3; i++) WatchShopAdvert(rig);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status);
            rig.Wait(TimeSpan.FromMinutes(100).TotalSeconds);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status, "23:4x local: still the same day");
            rig.Wait(TimeSpan.FromMinutes(20).TotalSeconds);
            Assert.AreEqual("2026-10-26", rig.M.Ads.LocalDayKey());
            Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow, "a new local day, though it is still 25 October in UTC");
            Assert.AreEqual(3, rig.M.Coins.ShopOffer().RemainingToday);
        }

        [Test]
        public void BehindUtcTheAllowanceWaitsForLocalMidnight()
        {
            // Eight hours behind UTC: 12:00 UTC is 04:00 local.
            var rig = Rig();
            rig.LocalOffset = TimeSpan.FromHours(-8);
            for (int i = 0; i < 3; i++) WatchShopAdvert(rig);
            rig.Wait(TimeSpan.FromHours(13).TotalSeconds);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status, "a new UTC day, but still the 25th locally");
            rig.Wait(TimeSpan.FromHours(7).TotalSeconds);
            Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow, "local midnight has passed");
            // Winding the clock back never brings adverts back early.
            for (int i = 0; i < 3; i++) WatchShopAdvert(rig);
            rig.Clock.Now = rig.Clock.Now.AddDays(-1);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status);
        }

        [Test]
        public void AdvertsCountedOnTheDayOfTheUpdateStillCount()
        {
            // Before 3.1 each coin advert was counted under its own name. Those counts, for today, still use up the
            // shared allowance, so nobody watches three more on the day the update arrives.
            var rig = Rig();
            var rec = rig.Ctx.Data.Monetisation;
            rec.AdDay = rig.M.Ads.LocalDayKey();
            rec.AdsToday[RewardedPlacements.ShopCoins] = 2;
            Assert.AreEqual(1, rig.M.Coins.ShopOffer().RemainingToday);
            rec.AdsToday[RewardedPlacements.CoinShortfall] = 1;
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status);
            rig.Wait(TimeSpan.FromDays(1).TotalSeconds);
            Assert.AreEqual(3, rig.M.Coins.ShopOffer().RemainingToday, "the next day starts clean");
        }

        [Test]
        public void TheSharedAllowanceSurvivesARestart()
        {
            var rig = Rig();
            WatchShopAdvert(rig);
            WatchShortfallAdvert(rig, 250);
            var again = rig.Restart().Start();
            Assert.AreEqual(1, again.M.Coins.ShopOffer().RemainingToday);
        }

        // ================================================================== Three-packs

        static readonly Dictionary<PowerUpId, int> PackPrices = new Dictionary<PowerUpId, int>
        {
            [PowerUpId.Swap] = 200, [PowerUpId.Blast] = 270, [PowerUpId.TileBreaker] = 340, [PowerUpId.ColourShift] = 400, [PowerUpId.ColourBurst] = 675,
        };

        [Test]
        public void EveryPowerUpHasAThreePackAtAboutTenPercentOff()
        {
            var ctx = TestUtil.Context();
            CollectionAssert.AreEquivalent(new[] { 75, 100, 125, 150, 250 }, ctx.PowerUps.All.Select(p => p.price), "single prices unchanged");
            foreach (var p in ctx.PowerUps.All)
            {
                Assert.AreEqual(PackPrices[p.id], ctx.Shop.PackPrice(p.id), p.displayName);
                double saving = 1.0 - ctx.Shop.PackPrice(p.id) / (3.0 * p.price);
                Assert.That(saving, Is.InRange(0.08, 0.12), p.displayName + " saves about 10 %");
            }
        }

        [Test]
        public void AThreePackTakesItsPriceOnceAndAddsExactlyThree()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 70;
            ctx.Data.Coins = 1000;
            var bought = new List<(PowerUpId, int)>();
            ctx.Shop.Purchased += (id, n) => bought.Add((id, n));
            int owned = ctx.Inventory.Owned(PowerUpId.Blast);
            var r = ctx.Shop.TryBuyPack(PowerUpId.Blast);
            Assert.IsTrue(r.Allowed);
            Assert.AreEqual(270, r.Price);
            Assert.AreEqual(730, ctx.Wallet.Balance);
            Assert.AreEqual(owned + 3, ctx.Inventory.Owned(PowerUpId.Blast));
            CollectionAssert.AreEqual(new[] { (PowerUpId.Blast, 3) }, bought);
            var entry = ctx.Wallet.Ledger.Last();
            Assert.AreEqual(-270, entry.Delta);
            Assert.AreEqual("shop.Blast.x3", entry.Reason);
            Assert.AreEqual("Bought Blast ×3", RewardKeys.Describe(entry.Reason));
            // A single one still costs the single price.
            Assert.AreEqual(100, ctx.Shop.TryBuy(PowerUpId.Blast).Price);
            Assert.AreEqual(630, ctx.Wallet.Balance);
            Assert.AreEqual(owned + 4, ctx.Inventory.Owned(PowerUpId.Blast));
        }

        [Test]
        public void AThreePackIsRefusedWhenItCantBeBought()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 70;
            ctx.Data.Coins = 269;
            var check = ctx.Shop.CheckPack(PowerUpId.Blast);
            Assert.AreEqual(PurchaseBlock.InsufficientFunds, check.Block);
            Assert.AreEqual(1, check.Shortfall);
            Assert.IsFalse(ctx.Shop.TryBuyPack(PowerUpId.Blast).Allowed);
            Assert.AreEqual(269, ctx.Wallet.Balance, "nothing taken");
            Assert.AreEqual(0, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.IsTrue(ctx.Shop.Check(PowerUpId.Blast).Allowed, "one can still be bought");

            ctx.Data.HighestUnlocked = 15;
            Assert.AreEqual(PurchaseBlock.Locked, ctx.Shop.CheckPack(PowerUpId.TileBreaker).Block, "locked like a single one");

            ctx.Data.HighestUnlocked = 70;
            ctx.Data.Coins = 5000;
            var daily = new PuzzleSession(ctx.CreateDailyRun());
            daily.Start();
            ctx.BeginRun(daily);
            Assert.AreEqual(PurchaseBlock.ChallengeInProgress, ctx.Shop.CheckPack(PowerUpId.Blast).Block, "the shop is closed during challenges");
            Assert.IsFalse(ctx.Shop.TryBuyPack(PowerUpId.Blast).Allowed);
            Assert.AreEqual(5000, ctx.Wallet.Balance);
        }

        [Test]
        public void PackPricesComeFromTheConfig()
        {
            var config = GameConfigData.CreateDefault();
            foreach (var p in config.PowerUps)
            {
                if (p.id == PowerUpId.Blast) p.packPrice = 0;          // an older config asset: the built-in pack price
                if (p.id == PowerUpId.Swap) p.packPrice = -1;          // switched off
                if (p.id == PowerUpId.TileBreaker) p.packPrice = 999;  // never more than three singles
                if (p.id == PowerUpId.ColourShift) p.packPrice = 390;
            }
            var ctx = new GameContext(config, new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            ctx.Load();
            Assert.AreEqual(270, ctx.Shop.PackPrice(PowerUpId.Blast));
            Assert.IsFalse(ctx.Shop.HasPack(PowerUpId.Swap));
            Assert.AreEqual(PurchaseBlock.UnknownItem, ctx.Shop.CheckPack(PowerUpId.Swap).Block);
            Assert.AreEqual(375, ctx.Shop.PackPrice(PowerUpId.TileBreaker));
            Assert.AreEqual(390, ctx.Shop.PackPrice(PowerUpId.ColourShift));
        }

        // ================================================================== Existing players

        /// <summary>A player's save from before 3.1: level 327 reached, three stars everywhere, 4,000 coins, two
        /// achievements claimed under the old rewards and one unlocked but not claimed, No Ads owned.</summary>
        static string ExistingSave()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            d.Coins = 4000;
            d.HighestUnlocked = 327;
            for (int n = 1; n <= 326; n++)
            {
                var l = d.GetOrCreateLevel(n);
                l.Completed = true;
                l.Stars = 3;
                l.BestScore = 5000;
                d.Claims.Add(RewardKeys.CampaignFirstClear(n));
                for (int s = 1; s <= 3; s++) d.Claims.Add(RewardKeys.CampaignStar(n, s));
                if (n % 10 == 0) d.Claims.Add(RewardKeys.CampaignMilestone(n));
            }
            d.GetOrCreateInventory(PowerUpId.Blast).Owned = 4;
            d.Endless.BestScore = 21000;
            d.Endless.BestStage = 9;
            foreach (var m in new[] { 2500, 5000, 10000, 20000 }) d.Claims.Add(RewardKeys.EndlessMilestone(m));
            foreach (var id in new[] { "level_10", "level_25", "level_300" }) d.Achievements.Unlocked[id] = TestUtil.Today.Ticks;
            d.Claims.Add(AchievementService.ClaimKey("level_10"));
            d.Claims.Add(AchievementService.ClaimKey("level_25"));
            d.Monetisation.NoAds = true;
            return SaveCodec.Encode(d);
        }

        [Test]
        public void AnExistingPlayerKeepsTheirCoinsAndEverythingElse()
        {
            var store = new MemorySaveStorage { Main = ExistingSave() };
            var ctx = TestUtil.Context(null, store);
            Assert.AreEqual(4000, ctx.Wallet.Balance, "no coins taken and none added");
            Assert.AreEqual(327, ctx.Campaign.HighestUnlocked);
            Assert.AreEqual(326 * 3, ctx.Campaign.TotalStars);
            Assert.AreEqual(4, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.IsTrue(ctx.Monetisation.NoAds, "No Ads stays owned");
            Assert.AreEqual(21000, ctx.Data.Endless.BestScore);
            Assert.AreEqual(AchievementState.Claimed, ctx.Achievements.State(ctx.Achievements.Catalog.Get("level_10")));
            // Saved and loaded again: still the same.
            ctx.Save();
            var again = TestUtil.Context(null, store);
            Assert.AreEqual(4000, again.Wallet.Balance);
            Assert.IsTrue(again.Monetisation.NoAds);
        }

        [Test]
        public void ClaimedAchievementsAreNeverPaidAgainAndUnclaimedOnesPayTheNewReward()
        {
            var ctx = TestUtil.Context(null, new MemorySaveStorage { Main = ExistingSave() });
            int bursts = ctx.Inventory.Owned(PowerUpId.ColourBurst);
            Assert.AreEqual(AchievementClaim.AlreadyClaimed, ctx.Achievements.Claim("level_10"), "claimed under the old rewards: no second payment");
            Assert.AreEqual(AchievementClaim.AlreadyClaimed, ctx.Achievements.Claim("level_25"));
            Assert.AreEqual(4000, ctx.Wallet.Balance, "and no top-up of the difference");
            // Unlocked before the update, claimed after it: the revised reward (a Blast and a Colour Burst), once.
            Assert.AreEqual(AchievementClaim.Claimed, ctx.Achievements.Claim("level_300"));
            Assert.AreEqual(4000, ctx.Wallet.Balance, "Trailblazer now pays power-ups, not 600 coins");
            Assert.AreEqual(5, ctx.Inventory.Owned(PowerUpId.Blast));
            Assert.AreEqual(bursts + 1, ctx.Inventory.Owned(PowerUpId.ColourBurst));
            Assert.AreEqual(AchievementClaim.AlreadyClaimed, ctx.Achievements.Claim("level_300"));
            Assert.AreEqual(5, ctx.Inventory.Owned(PowerUpId.Blast));
            // Achievements the save already proves (stars, levels) unlock on load and pay the new rewards when claimed.
            var stars500 = ctx.Achievements.Catalog.Get("stars_500");
            Assert.AreEqual(AchievementState.Unlocked, ctx.Achievements.State(stars500));
            int breakers = ctx.Inventory.Owned(PowerUpId.TileBreaker);
            ctx.Achievements.Claim("stars_500");
            Assert.AreEqual(breakers + 1, ctx.Inventory.Owned(PowerUpId.TileBreaker));
            Assert.AreEqual(4000, ctx.Wallet.Balance);
        }

        [Test]
        public void StarsAlreadyEarnedAreNeverPaidAgain()
        {
            var ctx = TestUtil.Context(null, new MemorySaveStorage { Main = ExistingSave() });
            Assert.AreEqual(0, ctx.Rewards.ForCampaignWin(ctx.Catalog.Get(5), 3).TotalCoins, "replaying a three-star level pays nothing");
            Assert.AreEqual(0, ctx.Rewards.ForEndless(25000).TotalCoins, "Endless milestones already reached pay nothing");
            Assert.AreEqual(40, ctx.Rewards.ForEndless(41000).TotalCoins, "a new one pays its 3.1 reward");
            var next = ctx.Rewards.ForCampaignWin(ctx.Catalog.Get(327), 3);
            Assert.AreEqual(8 + 2 + 3 + 5, next.TotalCoins, "a new level pays the 3.1 rewards");
            Assert.AreEqual(4000 + 40 + 18, ctx.Wallet.Balance);
        }

        // ================================================================== Config migration

        [Test]
        public void AConfigWithThePre31AchievementRewardsMovesToTheNewOnes()
        {
            var current = AchievementCatalog.CreateDefaults();
            var old = AchievementRewardTuning.WithVersion2Rewards(current);
            Assert.AreEqual(22265, old.Sum(d => d.reward.coins), "the rewards before 3.1");
            Assert.AreEqual(126, old.Sum(d => d.reward.powerUps.Sum(g => g.count)));
            old.First(d => d.id == "daily_7").reward = new AchievementReward { coins = 999 }; // edited by hand
            var changed = AchievementRewardTuning.Upgrade(old);
            Assert.AreEqual(66, changed.Count);
            CollectionAssert.DoesNotContain(changed, "daily_7");
            Assert.AreEqual(999, old.First(d => d.id == "daily_7").reward.coins, "an edited reward is kept");
            foreach (var d in old.Where(x => x.id != "daily_7"))
            {
                var c = current.First(x => x.id == d.id);
                Assert.AreEqual(c.reward.coins, d.reward.coins, d.id);
                Assert.AreEqual(c.reward.powerUps.Sum(g => g.count), d.reward.powerUps.Sum(g => g.count), d.id);
                Assert.AreEqual(c.title, d.title, "titles, requirements and ids never change");
                Assert.AreEqual(c.target, d.target);
                Assert.AreEqual(c.tier, d.tier);
            }
            Assert.IsEmpty(AchievementRewardTuning.Upgrade(old), "an upgraded list is left alone");
        }
    }
}
