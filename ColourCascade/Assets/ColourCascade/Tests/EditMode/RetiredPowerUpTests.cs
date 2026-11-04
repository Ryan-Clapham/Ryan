using System.Collections.Generic;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.0.1 retired the Extra Moves power-up (3 moves for 200 coins), now that Out of Moves offers 10 moves for
    /// an advert or 5 for 100 coins. It must appear nowhere, saves that mention it must still load, and uses a player
    /// still owned are paid back once, at their price.
    /// </summary>
    public class RetiredPowerUpTests
    {
        static MemorySaveStorage SaveWith(int coins, int ownedExtraMoves, int trialExtraMoves, int ownedBlast = 0)
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            d.Coins = coins;
            d.HighestUnlocked = 70;
            var e = d.GetOrCreateInventory(PowerUpId.ExtraMoves);
            e.Owned = ownedExtraMoves;
            e.Trials = trialExtraMoves;
            if (ownedBlast > 0) d.GetOrCreateInventory(PowerUpId.Blast).Owned = ownedBlast;
            d.GetOrCreateLevel(5).Completed = true;
            d.GetOrCreateLevel(5).Stars = 3;
            return new MemorySaveStorage { Main = SaveCodec.Encode(d) };
        }

        [Test]
        public void ExtraMovesIsInNoListBarShopOrIntroduction()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 100;
            ctx.Data.Coins = 1000;
            foreach (var def in ctx.PowerUps.All) Assert.AreNotEqual(PowerUpId.ExtraMoves, def.id);
            Assert.AreEqual(5, ctx.PowerUps.All.Count);
            Assert.IsNull(ctx.PowerUps.Get(PowerUpId.ExtraMoves));
            Assert.IsNull(PowerUpEffects.Get(PowerUpId.ExtraMoves), "no effect left to apply");
            Assert.IsFalse(ctx.PowerUpUse.IsVisibleIn(PowerUpId.ExtraMoves, GameMode.Campaign), "not on the in-game bar");
            Assert.AreEqual(PurchaseBlock.UnknownItem, ctx.Shop.Check(PowerUpId.ExtraMoves).Block, "not for sale");
            Assert.AreEqual(PurchaseBlock.UnknownItem, ctx.Shop.TryBuy(PowerUpId.ExtraMoves).Block);
            Assert.AreEqual(1000, ctx.Wallet.Balance);
            Assert.IsNull(ctx.PowerUps.IntroducedAt(50), "level 50 no longer introduces a power-up");
            Assert.IsNull(ctx.LendIntroPowerUpFor(50));
            Assert.IsNotNull(ctx.PowerUps.IntroducedAt(60), "the other introductions are unchanged");
        }

        [Test]
        public void AnOlderConfigThatStillListsExtraMovesIsIgnored()
        {
            var config = GameConfigData.CreateDefault();
            var defs = new List<PowerUpDefinition>(config.PowerUps)
            {
                new PowerUpDefinition { id = PowerUpId.ExtraMoves, displayName = "Extra Moves", price = 200, unlockLevel = 50, amount = 3 },
            };
            config.PowerUps = defs.ToArray();
            var ctx = new GameContext(config, new MemorySaveStorage(), new FixedClock(TestUtil.Today));
            ctx.Load();
            Assert.IsNull(ctx.PowerUps.Get(PowerUpId.ExtraMoves));
            Assert.AreEqual(5, ctx.PowerUps.All.Count);
        }

        [Test]
        public void OwnedExtraMovesArePaidBackOnceWhenTheSaveLoads()
        {
            var storage = SaveWith(50, 2, 1, ownedBlast: 3);
            var ctx = TestUtil.Context(null, storage);
            Assert.AreEqual(450, ctx.Wallet.Balance, "2 owned × 200 coins");
            Assert.AreEqual(1, ctx.RetirementRefunds.Count);
            Assert.AreEqual(2, ctx.RetirementRefunds[0].Uses);
            Assert.AreEqual(400, ctx.RetirementRefunds[0].Coins);
            Assert.AreEqual("Extra Moves", ctx.RetirementRefunds[0].Name);
            Assert.AreEqual(0, ctx.Inventory.Owned(PowerUpId.ExtraMoves));
            Assert.AreEqual(0, ctx.Inventory.Trials(PowerUpId.ExtraMoves), "free trial uses simply lapse");
            Assert.AreEqual(3, ctx.Inventory.Owned(PowerUpId.Blast), "other power-ups untouched");
            Assert.AreEqual(3, ctx.Campaign.StarsFor(5), "progress untouched");
            var last = ctx.Wallet.Ledger[ctx.Wallet.Ledger.Count - 1];
            Assert.AreEqual("refund.ExtraMoves.x2", last.Reason);
            Assert.AreEqual("Extra Moves retired: refund for 2", RewardKeys.Describe(last.Reason));
            Assert.AreEqual(450, SaveCodec.Decode(storage.Main).Coins, "saved at once");

            var again = TestUtil.Context(null, storage);
            Assert.AreEqual(450, again.Wallet.Balance, "never paid twice");
            Assert.AreEqual(0, again.RetirementRefunds.Count);
        }

        [Test]
        public void OnlyFreeTrialUsesGetNoRefund()
        {
            var storage = SaveWith(80, 0, 1);
            var ctx = TestUtil.Context(null, storage);
            Assert.AreEqual(80, ctx.Wallet.Balance);
            Assert.AreEqual(0, ctx.RetirementRefunds.Count);
            Assert.AreEqual(0, ctx.Inventory.Trials(PowerUpId.ExtraMoves));
        }

        [Test]
        public void TheRefundUsesThePriceTheConfigCharged()
        {
            var config = GameConfigData.CreateDefault();
            var defs = new List<PowerUpDefinition>(config.PowerUps) { new PowerUpDefinition { id = PowerUpId.ExtraMoves, price = 180 } };
            config.PowerUps = defs.ToArray();
            var storage = SaveWith(0, 2, 0);
            var ctx = new GameContext(config, storage, new FixedClock(TestUtil.Today));
            ctx.Load();
            Assert.AreEqual(360, ctx.Wallet.Balance);
        }

        [Test]
        public void ASaveThatNeverHadExtraMovesIsUnchanged()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            d.Coins = 321;
            d.GetOrCreateInventory(PowerUpId.Swap).Owned = 2;
            var before = SaveCodec.Encode(d);
            var storage = new MemorySaveStorage { Main = before };
            var ctx = TestUtil.Context(null, storage);
            Assert.AreEqual(321, ctx.Wallet.Balance);
            Assert.AreEqual(0, ctx.RetirementRefunds.Count);
            Assert.IsFalse(ctx.Data.Claims.Contains(RetiredPowerUps.ClaimKey(PowerUpId.ExtraMoves)), "nothing recorded for players who never had it");
            Assert.AreEqual(before, storage.Main, "nothing written either");
        }
    }
}
