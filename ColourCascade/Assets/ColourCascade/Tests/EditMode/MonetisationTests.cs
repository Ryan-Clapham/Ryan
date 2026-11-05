using System;
using System.Collections.Generic;
using System.Text;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.0 (docs/MONETISATION.md): rewarded adverts, interstitial timing, purchases and No Ads, Out of Moves
    /// and shortfall offers, consent and saves. The SDKs are replaced by the scripted fakes in MonetisationFakes.cs,
    /// so these test the game's rules for every callback order, not the SDKs or a real store.
    /// </summary>
    public class MonetisationTests
    {
        static readonly string[] Stripes = { "000", "111", "222" };

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// A campaign level that has just run out of moves, with ice and goals in play (one met, one not). Special
        /// tiles never survive to this point: since 2.1 any left on the board go off before the level fails.
        /// </summary>
        static PuzzleSession OutOfMoves()
        {
            var s = TestUtil.Session(GameMode.Campaign, 1, 100000, 3, Stripes, new[] { "i..", "...", "..." },
                new GoalSpec { kind = GoalKind.CollectColour, colour = 2, amount = 3 }, new GoalSpec { kind = GoalKind.Score, amount = 100000 });
            Assert.IsTrue(s.TryPlay(new GridPos(0, 0), new MoveOutcome()), "the bottom row is a group");
            if (s.FinalePending) s.RunFinale(new MoveOutcome());
            Assert.AreEqual(SessionState.Failed, s.State);
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
            return s;
        }

        /// <summary>Everything about a session that a continue must leave alone: every tile, the score and the goals.</summary>
        static string Snapshot(PuzzleSession s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Board.CellCount; i++)
            {
                var t = s.Board.GetByIndex(i);
                sb.Append((int)t.Type).Append(':').Append(t.Colour).Append(':').Append(t.Id).Append(':').Append((int)t.Obstacle)
                  .Append(':').Append(t.Strength).Append(':').Append((int)t.Special).Append('|');
            }
            sb.Append(" score ").Append(s.Score).Append(" made ").Append(s.MovesMade).Append(" groups ").Append(s.GroupsCleared);
            for (int i = 0; i < s.Goals.Count; i++) sb.Append(" goal").Append(i).Append('=').Append(s.Goals.Progress(i)).Append(s.Goals.IsComplete(i) ? "done" : "");
            return sb.ToString();
        }

        static string Inventory(GameContext ctx)
        {
            var sb = new StringBuilder();
            foreach (var e in ctx.Data.Inventory) sb.Append(e.Id).Append('=').Append(e.Owned).Append('+').Append(e.Trials).Append(' ');
            return sb.ToString();
        }

        static void WinCampaignLevel(MonetisationRig rig)
        {
            var s = TestUtil.Session(GameMode.Campaign, 5, 90, 3, Stripes);
            Assert.IsTrue(s.TryPlay(new GridPos(0, 2), new MoveOutcome()));
            Assert.IsTrue(s.Finish(new MoveOutcome()));
            Assert.AreEqual(SessionState.Won, s.State);
            rig.Ctx.FinishRun(s);
        }

        static void FailCampaignLevel(MonetisationRig rig)
        {
            var s = TestUtil.Session(GameMode.Campaign, 1, 100000, 3, Stripes);
            s.TryPlay(new GridPos(0, 2), new MoveOutcome());
            rig.Ctx.FinishRun(s);
        }

        static MonetisationRig Started(Action<GameConfigData> configure = null)
        {
            var rig = new MonetisationRig(configure).Start();
            Assert.AreEqual(AdServiceState.Ready, rig.M.Ads.State);
            return rig;
        }

        // ================================================================== Rewarded adverts

        [Test]
        public void ACompletedRewardedAdvertGrantsItsRewardExactlyOnce()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 120;
            AdOutcome? outcome = null;
            int added = -1;
            Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow);
            Assert.IsTrue(rig.M.Coins.WatchForShopCoins((o, c) => { outcome = o; added = c; }));
            Assert.AreEqual(120, rig.Ctx.Wallet.Balance, "nothing is granted when the advert is requested or opened");
            rig.Network.Open();
            Assert.AreEqual(120, rig.Ctx.Wallet.Balance);
            rig.Network.Earn();
            Assert.AreEqual(140, rig.Ctx.Wallet.Balance, "the SDK's reward callback grants exactly 20 coins (update 3.1)");
            rig.Network.Close();
            Assert.AreEqual(AdOutcome.Rewarded, outcome);
            Assert.AreEqual(20, added);
            Assert.AreEqual(140, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RewardGranted));
            Assert.AreEqual("ad.coins", rig.Ctx.Wallet.Ledger[rig.Ctx.Wallet.Ledger.Count - 1].Reason);
            Assert.AreEqual(140, SaveCodec.Decode(rig.Storage.Main).Coins, "the coins are saved at once");
        }

        [Test]
        public void ADuplicateRewardCallbackIsIgnored()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 0;
            rig.M.Coins.WatchForShopCoins(null);
            rig.Network.Open();
            rig.Network.Earn();
            rig.Network.Earn();
            rig.Network.Close();
            rig.Network.Close();
            rig.Network.Earn();
            Assert.AreEqual(20, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(2, rig.M.Analytics.Count(MonetisationEvents.RewardDuplicateIgnored));
            Assert.AreEqual(2, rig.M.Coins.ShopOffer().RemainingToday, "one advert counted towards the daily limit of three");
        }

        [Test]
        public void AnAdvertClosedEarlyGrantsNothing()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 10;
            AdOutcome? outcome = null;
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.Network.Open();
            rig.Network.Close();
            Assert.IsNull(outcome, "a reward may still arrive just after the close");
            rig.Wait(1.9);
            Assert.IsNull(outcome);
            rig.Wait(0.2);
            Assert.AreEqual(AdOutcome.ClosedEarly, outcome);
            Assert.AreEqual(10, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RewardedClosedEarly));
            Assert.IsFalse(rig.Ctx.Data.Monetisation.AdsToday.ContainsKey(RewardedPlacements.ShopCoins), "an unfinished advert doesn't use up the daily limit");
        }

        [Test]
        public void ARewardCallbackJustAfterTheCloseStillCounts()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 0;
            AdOutcome? outcome = null;
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.Network.Open();
            rig.Network.Close();
            rig.Wait(0.5);
            rig.Network.Earn();
            Assert.AreEqual(AdOutcome.Rewarded, outcome);
            Assert.AreEqual(20, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void ARewardTheSdkConfirmsAfterTheGracePeriodIsStillGrantedOnce()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 0;
            var outcomes = new List<AdOutcome>();
            string late = null;
            rig.M.Ads.LateRewardGranted += p => late = p;
            rig.M.Coins.WatchForShopCoins((o, c) => outcomes.Add(o));
            rig.Network.Open();
            rig.Network.Close();
            rig.Wait(3);
            CollectionAssert.AreEqual(new[] { AdOutcome.ClosedEarly }, outcomes);
            rig.Network.Earn();
            rig.Network.Earn();
            Assert.AreEqual(20, rig.Ctx.Wallet.Balance, "the SDK confirmed the reward, so the player gets it, once");
            Assert.AreEqual(RewardedPlacements.ShopCoins, late);
            CollectionAssert.AreEqual(new[] { AdOutcome.ClosedEarly }, outcomes, "the advert's ending is reported once");
        }

        [Test]
        public void NoRewardCallbackMeansNoReward()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 5;
            AdOutcome? outcome = null;
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.Network.Open();
            rig.Network.Close();
            rig.Wait(60);
            Assert.AreEqual(AdOutcome.ClosedEarly, outcome);
            Assert.AreEqual(5, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void AnAdvertThatFailsToShowGrantsNothingAndAnotherLoads()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 0;
            AdOutcome? outcome = null;
            int loads = rig.Network.LoadCalls[AdFormat.Rewarded];
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.Network.Fail("internal error");
            Assert.AreEqual(AdOutcome.FailedToShow, outcome);
            Assert.AreEqual(0, rig.Ctx.Wallet.Balance);
            Assert.IsFalse(rig.M.Ads.IsShowing);
            Assert.AreEqual(loads + 1, rig.Network.LoadCalls[AdFormat.Rewarded], "a fresh advert is requested");
            Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow);
        }

        [Test]
        public void AnAdvertThatFailsToLoadIsNotOfferedAndIsRetriedLater()
        {
            var rig = new MonetisationRig();
            rig.Network.LoadSucceeds[AdFormat.Rewarded] = false;
            rig.Start();
            var offer = rig.M.Coins.ShopOffer();
            Assert.IsFalse(offer.CanShow);
            Assert.AreEqual(OfferStatus.Unavailable, offer.Status);
            Assert.IsFalse(rig.M.Coins.WatchForShopCoins(null), "nothing is shown without a loaded advert");
            Assert.AreEqual(0, rig.Network.ShowCalls);
            rig.Network.LoadSucceeds[AdFormat.Rewarded] = true;
            rig.Wait(5);
            Assert.AreEqual(1, rig.Network.LoadCalls[AdFormat.Rewarded], "retries wait (10 s first)");
            rig.Wait(6);
            Assert.AreEqual(2, rig.Network.LoadCalls[AdFormat.Rewarded]);
            Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow);
        }

        [Test]
        public void FailedLoadsBackOffUpToTheMaximum()
        {
            var rig = new MonetisationRig();
            rig.Network.LoadSucceeds[AdFormat.Rewarded] = false;
            rig.Start();
            var times = new List<double>();
            int last = rig.Network.LoadCalls[AdFormat.Rewarded];
            for (int i = 0; i < 2000; i++)
            {
                rig.Wait(1);
                if (rig.Network.LoadCalls[AdFormat.Rewarded] != last)
                {
                    last = rig.Network.LoadCalls[AdFormat.Rewarded];
                    times.Add(rig.Seconds);
                }
            }
            Assert.AreEqual(10, times[0], 1.01);
            Assert.AreEqual(30, times[1], 1.01, "then 20 s");
            Assert.AreEqual(70, times[2], 1.01, "then 40 s");
            Assert.AreEqual(300, times[times.Count - 1] - times[times.Count - 2], 1.01, "never more than 300 s apart");
        }

        [Test]
        public void AnAdvertTheSdkNeverAnswersCountsAsFailed()
        {
            var rig = Started();
            AdOutcome? outcome = null;
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.Wait(10);
            Assert.IsNull(outcome);
            rig.Wait(6);
            Assert.AreEqual(AdOutcome.FailedToShow, outcome);
            Assert.IsFalse(rig.M.Ads.IsShowing, "the game is never stuck behind an advert");
        }

        [Test]
        public void AnAdvertStillOpenAfterThePlayerIsBackCountsAsClosed()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 0;
            AdOutcome? outcome = null;
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.M.NotifyAppPaused(true);
            rig.Network.Open();
            rig.Network.Earn();
            rig.Wait(30);
            Assert.IsNull(outcome, "no timeout while the advert has the screen");
            rig.M.NotifyAppPaused(false);
            rig.Wait(5);
            Assert.IsNull(outcome);
            rig.Wait(4);
            Assert.AreEqual(AdOutcome.Rewarded, outcome, "the reward was confirmed; the missing close callback doesn't take it away");
            Assert.AreEqual(20, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void CallbacksDeliveredAfterTheGameResumesAreStillHandled()
        {
            // Android: the game pauses while the advert plays and the SDK's callbacks arrive just after it resumes.
            var rig = Started();
            rig.Ctx.Data.Coins = 0;
            AdOutcome? outcome = null;
            rig.M.Coins.WatchForShopCoins((o, c) => outcome = o);
            rig.M.NotifyAppPaused(true);
            rig.Seconds += 35;
            rig.M.NotifyAppPaused(false);
            rig.M.Update();
            Assert.IsNull(outcome, "35 s in the advert is not a 'didn't open' timeout");
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(AdOutcome.Rewarded, outcome);
            Assert.AreEqual(20, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void TheDailyLimitAndCooldownSurviveARestart()
        {
            var rig = Started();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow, "advert " + (i + 1));
                rig.M.Coins.WatchForShopCoins(null);
                rig.Network.WatchToTheEnd();
                Assert.AreEqual(i < 2 ? OfferStatus.CoolingDown : OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status);
                rig.Wait(31);
            }
            var offer = rig.M.Coins.ShopOffer();
            Assert.AreEqual(OfferStatus.LimitReached, offer.Status);
            Assert.AreEqual(0, offer.RemainingToday);
            var again = rig.Restart().Start();
            Assert.AreEqual(OfferStatus.LimitReached, again.M.Coins.ShopOffer().Status, "closing the game doesn't reset the limit");
            again.Wait(TimeSpan.FromHours(13).TotalSeconds);
            Assert.IsTrue(again.M.Coins.ShopOffer().CanShow, "a new day brings new adverts");
        }

        [Test]
        public void WindingTheClockBackDoesNotResetTheDailyLimit()
        {
            var rig = Started(c => c.Monetisation.ads.shopCoins.cooldownSeconds = 0);
            for (int i = 0; i < 3; i++)
            {
                rig.M.Coins.WatchForShopCoins(null);
                rig.Network.WatchToTheEnd();
            }
            rig.Clock.Now = rig.Clock.Now.AddDays(-2);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Coins.ShopOffer().Status);
        }

        [Test]
        public void AnotherAdvertCantStartWhileOneIsShowing()
        {
            var rig = Started();
            rig.M.Coins.WatchForShopCoins(null);
            Assert.AreEqual(OfferStatus.Busy, rig.M.Coins.ForPrice(1000).Ad.Status);
            Assert.IsFalse(rig.M.Coins.WatchForShortfall(1000, "x", null));
            Assert.AreEqual(1, rig.Network.ShowCalls);
        }

        [Test]
        public void TheTestAdUnitsAreUsedAndNoLiveIdsAreBuiltIn()
        {
            var rig = Started();
            CollectionAssert.Contains(rig.Network.LoadedUnits, "ca-app-pub-3940256099942544/5224354917");
            CollectionAssert.Contains(rig.Network.LoadedUnits, "ca-app-pub-3940256099942544/1033173712");
            Assert.IsTrue(rig.Network.Settings.TestMode);
            var ads = new AdConfig();
            Assert.IsTrue(ads.forceTestAds);
            Assert.AreEqual("", ads.androidLive.rewarded + ads.androidLive.interstitial + ads.iosLive.rewarded + ads.iosLive.interstitial);
        }

        [Test]
        public void ANewOpportunityCanBeAddedWithoutChangingTheService()
        {
            var rig = Started();
            rig.M.Ads.RegisterPlacement("double_daily", new RewardedPlacementConfig { reward = 2, dailyLimit = 1 });
            int doubled = 0;
            Assert.IsTrue(rig.M.Ads.ShowRewarded("double_daily", null, () => { doubled++; return true; }, null));
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(1, doubled);
            Assert.AreEqual(OfferStatus.LimitReached, rig.M.Ads.CheckRewarded("double_daily").Status);
            Assert.AreEqual(OfferStatus.Disabled, rig.M.Ads.CheckRewarded("not_registered").Status);
        }

        // ================================================================== Out of Moves

        [Test]
        public void TheExtraMovesAdvertAddsExactlyTenMovesAndKeepsTheLevelAsItWas()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 340;
            rig.Ctx.Inventory.AddOwned(PowerUpId.Blast, 2);
            var s = OutOfMoves();
            string before = Snapshot(s), inventory = Inventory(rig.Ctx);
            var options = rig.M.Continue.Options(s);
            Assert.IsTrue(options.Available);
            Assert.IsTrue(options.Ad.CanShow);
            Assert.AreEqual(10, options.AdMoves);
            AdOutcome? outcome = null;
            Assert.IsTrue(rig.M.Continue.WatchAd(s, o => outcome = o));
            Assert.AreEqual(0, s.MovesLeft, "no moves until the SDK confirms the reward");
            Assert.AreEqual(SessionState.Failed, s.State);
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(AdOutcome.Rewarded, outcome);
            Assert.AreEqual(10, s.MovesLeft);
            Assert.AreEqual(SessionState.Playing, s.State, "the same level continues: nothing restarts");
            Assert.AreEqual(before, Snapshot(s), "board, obstacles, special tiles, score and goals are untouched");
            Assert.AreEqual(inventory, Inventory(rig.Ctx), "power-ups untouched");
            Assert.AreEqual(340, rig.Ctx.Wallet.Balance, "the advert costs no coins");
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.ExtraMovesAd));
        }

        [Test]
        public void AnInterruptedExtraMovesAdvertLeavesTheLevelReadyForAnotherChoice()
        {
            var rig = Started();
            var s = OutOfMoves();
            string before = Snapshot(s);
            AdOutcome? outcome = null;
            rig.M.Continue.WatchAd(s, o => outcome = o);
            rig.Network.Open();
            rig.Network.Close();
            rig.Wait(3);
            Assert.AreEqual(AdOutcome.ClosedEarly, outcome);
            Assert.AreEqual(0, s.MovesLeft);
            Assert.AreEqual(SessionState.Failed, s.State);
            Assert.AreEqual(before, Snapshot(s), "nothing was lost");
            Assert.IsTrue(ContinueService.CanContinue(s), "the coin option (or another advert) is still possible");
        }

        [Test]
        public void ARewardArrivingAfterThePlayerGaveUpAddsNothingToAFinishedAttempt()
        {
            var rig = Started();
            var s = OutOfMoves();
            rig.M.Continue.WatchAd(s, null);
            rig.Network.Open();
            rig.Network.Close();
            rig.Wait(3);
            // The player chose "Give up" and started a new attempt; this session is gone. A late reward can't revive it.
            var replacement = OutOfMoves();
            Assert.IsTrue(replacement.AddMoves(3));
            rig.Network.Earn();
            Assert.AreEqual(3, replacement.MovesLeft);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RewardGranted) + rig.M.Analytics.Count(MonetisationEvents.RewardNotApplied));
        }

        [Test]
        public void ContinuingWithCoinsTakesExactly100AndAddsExactlyFiveMoves()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 130;
            rig.Ctx.Inventory.AddOwned(PowerUpId.Swap, 1);
            var s = OutOfMoves();
            string before = Snapshot(s), inventory = Inventory(rig.Ctx);
            var options = rig.M.Continue.Options(s);
            Assert.IsTrue(options.CoinAffordable);
            Assert.AreEqual(100, options.CoinCost);
            Assert.AreEqual(5, options.CoinMoves);
            Assert.AreEqual(130, rig.Ctx.Wallet.Balance, "nothing is taken just by showing the choice");
            Assert.AreEqual(CoinContinueResult.Done, rig.M.Continue.ContinueWithCoins(s));
            Assert.AreEqual(30, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(5, s.MovesLeft);
            Assert.AreEqual(SessionState.Playing, s.State);
            Assert.AreEqual(before, Snapshot(s));
            Assert.AreEqual(inventory, Inventory(rig.Ctx));
            Assert.AreEqual("continue.L1", rig.Ctx.Wallet.Ledger[rig.Ctx.Wallet.Ledger.Count - 1].Reason);
            Assert.AreEqual(CoinContinueResult.NotAvailable, rig.M.Continue.ContinueWithCoins(s), "only an out-of-moves level can be continued");
            Assert.AreEqual(30, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void WithoutEnoughCoinsNothingIsTaken()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 99;
            var s = OutOfMoves();
            var options = rig.M.Continue.Options(s);
            Assert.IsFalse(options.CoinAffordable);
            Assert.AreEqual(1, options.CoinShortfall);
            Assert.AreEqual(CoinContinueResult.InsufficientCoins, rig.M.Continue.ContinueWithCoins(s));
            Assert.AreEqual(99, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(0, s.MovesLeft);
            Assert.AreEqual(SessionState.Failed, s.State);
        }

        [Test]
        public void ExtraMovesAdvertsAreLimitedPerAttemptButNotPerDay()
        {
            var rig = Started();
            var s = OutOfMoves();
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(rig.M.Continue.WatchAd(s, null), "advert " + (i + 1));
                rig.Network.WatchToTheEnd();
                // Play the moves out again.
                while (s.AcceptsMoves)
                {
                    bool played = false;
                    for (int c = 0; c < 3 && !played; c++)
                        for (int r = 0; r < 3 && !played; r++)
                            played = s.TryPlay(new GridPos(c, r), new MoveOutcome());
                    if (!played) break;
                }
                if (s.State != SessionState.Failed) Assert.Inconclusive("the board ran out of groups");
            }
            var options = rig.M.Continue.Options(s);
            Assert.AreEqual(OfferStatus.LimitReached, options.Ad.Status);
            Assert.AreEqual(0, options.Ad.RemainingThisRun);
            Assert.IsTrue(rig.M.Continue.Options(OutOfMoves()).Ad.CanShow, "a new attempt has its own adverts");
        }

        [Test]
        public void OnlyCampaignLevelsCanBeContinued()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 1000;
            foreach (var mode in new[] { GameMode.Daily, GameMode.Endless })
            {
                var s = TestUtil.Session(mode, 1, 100000, 3, Stripes);
                s.TryPlay(new GridPos(0, 2), new MoveOutcome());
                Assert.IsTrue(s.IsOver, mode + " is over");
                Assert.IsFalse(rig.M.Continue.Options(s).Available, mode + ": challenges stay fair, Endless runs end");
                Assert.IsFalse(rig.M.Continue.WatchAd(s, null));
                Assert.AreEqual(CoinContinueResult.NotAvailable, rig.M.Continue.ContinueWithCoins(s));
            }
            Assert.AreEqual(1000, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(0, rig.Network.ShowCalls);
        }

        // ================================================================== Not enough coins

        [Test]
        public void TheShortfallOfferPaysTheMissingCoinsAndBuysNothing()
        {
            var rig = Started();
            rig.Ctx.Data.HighestUnlocked = 100;
            rig.Ctx.Data.Coins = 100;
            int price = rig.Ctx.PowerUps.Get(PowerUpId.ColourShift).price;
            Assert.AreEqual(150, price);
            Assert.AreEqual(PurchaseBlock.InsufficientFunds, rig.Ctx.Shop.Check(PowerUpId.ColourShift).Block);
            var offer = rig.M.Coins.ForPrice(price);
            Assert.AreEqual(50, offer.Shortfall);
            Assert.AreEqual(50, offer.AdCoins);
            Assert.IsTrue(offer.Covers);
            StringAssert.Contains("50 more coins", offer.Explanation);
            int ownedBefore = rig.Ctx.Inventory.Owned(PowerUpId.ColourShift);
            int added = 0;
            Assert.IsTrue(rig.M.Coins.WatchForShortfall(price, "ColourShift", (o, c) => added = c));
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(50, added);
            Assert.AreEqual(150, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(ownedBefore, rig.Ctx.Inventory.Owned(PowerUpId.ColourShift), "the power-up is not bought automatically");
            Assert.IsTrue(rig.Ctx.Shop.Check(PowerUpId.ColourShift).Allowed, "the player can now confirm the purchase themselves");
            Assert.IsTrue(rig.Ctx.Shop.TryBuy(PowerUpId.ColourShift).Allowed);
            Assert.AreEqual(0, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void AShortfallBiggerThanTheAdvertIsExplained()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 30;
            var offer = rig.M.Coins.ForPrice(250);
            Assert.AreEqual(220, offer.Shortfall);
            Assert.AreEqual(50, offer.AdCoins);
            Assert.IsFalse(offer.Covers);
            StringAssert.Contains("still be 170 short", offer.Explanation);
            rig.M.Coins.WatchForShortfall(250, "ColourBurst", null);
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(80, rig.Ctx.Wallet.Balance, "exactly the advertised 50");
        }

        [Test]
        public void ASmallShortfallPaysOnlyWhatIsMissing()
        {
            var rig = Started();
            rig.Ctx.Data.Coins = 140;
            var offer = rig.M.Coins.ForPrice(150);
            Assert.AreEqual(10, offer.AdCoins);
            Assert.IsTrue(offer.Covers);
            rig.M.Coins.WatchForShortfall(150, "ColourShift", null);
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(150, rig.Ctx.Wallet.Balance);
        }

        // ================================================================== Interstitials

        [Test]
        public void NoInterstitialBeforeFiveCompletedLevels()
        {
            var rig = Started();
            for (int i = 0; i < 4; i++)
            {
                WinCampaignLevel(rig);
                bool continued = false;
                Assert.AreEqual(InterstitialDecision.NotEnoughLevels, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, () => continued = true));
                Assert.IsTrue(continued);
            }
            Assert.AreEqual(0, rig.Network.ShowCalls);
            Assert.AreEqual(4, rig.M.Interstitials.CompletionsSinceLast);
        }

        [Test]
        public void AfterFiveCompletedLevelsAnInterstitialShowsBetweenLevels()
        {
            var rig = Started();
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            bool continued = false;
            Assert.AreEqual(InterstitialDecision.Show, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, () => continued = true));
            Assert.AreEqual(AdFormat.Interstitial, rig.Network.LastShownFormat);
            Assert.IsFalse(continued, "the next level waits for the advert");
            rig.Network.Open();
            Assert.AreEqual(0, rig.M.Interstitials.CompletionsSinceLast, "the count restarts once it is displayed");
            rig.Network.Close();
            Assert.IsTrue(continued);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.InterstitialShown));
        }

        [Test]
        public void NoInterstitialAfterAFailureARetryAbandoningOrDuringPlay()
        {
            var rig = Started();
            for (int i = 0; i < 9; i++) WinCampaignLevel(rig);
            FailCampaignLevel(rig);
            Assert.AreEqual(9, rig.M.Interstitials.CompletionsSinceLast, "failed levels don't count");
            foreach (var t in new[] { AdTransition.LevelFailed, AdTransition.LevelRetry, AdTransition.LevelAbandoned, AdTransition.Gameplay })
            {
                bool continued = false;
                Assert.AreEqual(InterstitialDecision.NotATransition, rig.M.Ads.AtTransition(t, () => continued = true), t.ToString());
                Assert.IsTrue(continued);
            }
            Assert.AreEqual(0, rig.Network.ShowCalls);
            Assert.IsTrue(rig.M.Interstitials.OpportunityPending, "kept for the next natural break");
            Assert.AreEqual(InterstitialDecision.Show, rig.M.Ads.AtTransition(AdTransition.LevelWonExit, null));
        }

        [Test]
        public void InterstitialsAreAtLeastFiveMinutesApart()
        {
            var rig = Started();
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null);
            rig.Network.Open();
            rig.Network.Close();
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            rig.Wait(240);
            Assert.AreEqual(InterstitialDecision.TooSoon, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null));
            Assert.AreEqual(5, rig.M.Interstitials.CompletionsSinceLast, "the opportunity is kept");
            rig.Wait(61);
            Assert.AreEqual(InterstitialDecision.Show, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null), "shown at the next break once five minutes have passed");
        }

        [Test]
        public void TheCountIsKeptWhenNoInterstitialIsReadyOrItFails()
        {
            var rig = new MonetisationRig();
            rig.Network.LoadSucceeds[AdFormat.Interstitial] = false;
            rig.Start();
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            bool continued = false;
            Assert.AreEqual(InterstitialDecision.NotLoaded, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, () => continued = true));
            Assert.IsTrue(continued, "the player carries on normally");
            Assert.AreEqual(5, rig.M.Interstitials.CompletionsSinceLast);

            rig.Network.LoadSucceeds[AdFormat.Interstitial] = true;
            rig.Wait(11);
            continued = false;
            Assert.AreEqual(InterstitialDecision.Show, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, () => continued = true));
            rig.Network.Fail("no activity");
            Assert.IsTrue(continued);
            Assert.AreEqual(5, rig.M.Interstitials.CompletionsSinceLast, "not displayed: not reset");
            Assert.AreEqual(0L, rig.Ctx.Data.Monetisation.LastInterstitialTicks);
        }

        [Test]
        public void NoInterstitialStraightAfterARewardedAdvertOrAPurchase()
        {
            var rig = Started();
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            rig.M.Coins.WatchForShopCoins(null);
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(InterstitialDecision.QuietAfterRewardOrPurchase, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null));
            rig.Wait(121);
            Assert.AreEqual(InterstitialDecision.Show, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null));
            rig.Network.Open();
            rig.Network.Close();

            rig.Wait(400);
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            rig.M.Purchases.Buy("coins_small");
            rig.Store.Pay("coins_small");
            Assert.AreEqual(InterstitialDecision.QuietAfterRewardOrPurchase, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null), "after a coin pack");
            rig.Wait(121);
            rig.Ctx.Data.HighestUnlocked = 100;
            rig.Ctx.Shop.TryBuy(PowerUpId.Blast);
            Assert.AreEqual(InterstitialDecision.QuietAfterRewardOrPurchase, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null), "after a coin purchase");
        }

        [Test]
        public void NoAdsStopsInterstitialsButNotRewardedAdverts()
        {
            var rig = Started();
            rig.M.Purchases.Buy("no_ads");
            rig.Store.Pay("no_ads");
            Assert.IsTrue(rig.M.NoAds);
            rig.Wait(500);
            for (int i = 0; i < 20; i++) WinCampaignLevel(rig);
            Assert.AreEqual(InterstitialDecision.NoAds, rig.M.Ads.AtTransition(AdTransition.LevelWonNext, null));
            Assert.AreEqual(InterstitialDecision.NoAds, rig.M.Ads.AtTransition(AdTransition.LevelWonExit, null));
            Assert.AreEqual(0, rig.Network.ShowCalls);
            Assert.IsTrue(rig.M.Coins.ShopOffer().CanShow, "rewarded adverts stay available by choice");
            Assert.IsTrue(rig.M.Continue.Options(OutOfMoves()).Ad.CanShow);
            rig.M.Coins.WatchForShopCoins(null);
            rig.Network.WatchToTheEnd();
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RewardGranted));
        }

        [Test]
        public void WithNoAdsNoInterstitialIsEvenLoaded()
        {
            var rig = new MonetisationRig();
            rig.Ctx.Data.Monetisation.NoAds = true;
            rig.Store.Owned["no_ads"] = true;
            rig.Start();
            Assert.AreEqual(0, rig.Network.LoadCalls[AdFormat.Interstitial]);
            Assert.AreEqual(1, rig.Network.LoadCalls[AdFormat.Rewarded]);
        }

        [Test]
        public void TheInterstitialCountSurvivesARestart()
        {
            var rig = Started();
            for (int i = 0; i < 3; i++) WinCampaignLevel(rig);
            var again = rig.Restart().Start();
            for (int i = 0; i < 2; i++) WinCampaignLevel(again);
            Assert.AreEqual(InterstitialDecision.Show, again.M.Ads.AtTransition(AdTransition.LevelWonNext, null));
        }

        // ================================================================== Consent

        [Test]
        public void TheAdSdkStartsOnlyAfterConsentAllowsAdRequests()
        {
            var rig = new MonetisationRig();
            rig.Consent.AnswerImmediately = false;
            rig.Start();
            Assert.AreEqual(AdServiceState.WaitingForConsent, rig.M.Ads.State);
            Assert.AreEqual(0, rig.Network.InitCalls, "no ad SDK before consent");
            Assert.AreEqual(OfferStatus.Loading, rig.M.Coins.ShopOffer().Status);
            rig.Consent.Respond();
            Assert.AreEqual(1, rig.Network.InitCalls);
            Assert.AreEqual(AdServiceState.Ready, rig.M.Ads.State);
            Assert.AreEqual(1, rig.Consent.GatherCalls);
        }

        [Test]
        public void APreviousSessionsConsentStartsTheSdkWithoutWaiting()
        {
            var rig = new MonetisationRig();
            rig.Consent.AnswerImmediately = false;
            rig.Consent.CanRequestAds = true;
            rig.Start();
            Assert.AreEqual(1, rig.Network.InitCalls);
            Assert.AreEqual(1, rig.Consent.GatherCalls, "the consent status is still refreshed on every start");
        }

        [Test]
        public void WithoutConsentThereAreNoAdvertsButTheGameStillWorks()
        {
            var rig = new MonetisationRig();
            rig.Consent.Answer = new ConsentOutcome { CanRequestAds = false };
            rig.Ctx.Data.Coins = 150;
            rig.Start();
            Assert.AreEqual(0, rig.Network.InitCalls);
            var s = OutOfMoves();
            var options = rig.M.Continue.Options(s);
            Assert.AreEqual(OfferStatus.NoConsent, options.Ad.Status);
            StringAssert.Contains("privacy", options.Ad.Message);
            Assert.AreEqual(CoinContinueResult.Done, rig.M.Continue.ContinueWithCoins(s), "the coin option still works");
            Assert.IsTrue(rig.M.Purchases.Buy("coins_small") == PurchaseStart.Started, "purchases don't depend on ad consent");
        }

        [Test]
        public void AConsentErrorWithoutAnEarlierAnswerMeansNoAdverts()
        {
            var rig = new MonetisationRig();
            rig.Consent.Answer = new ConsentOutcome { CanRequestAds = false, Error = "network" };
            rig.Start();
            Assert.AreEqual(AdServiceState.Unavailable, rig.M.Ads.State);
            Assert.AreEqual(0, rig.Network.InitCalls);
        }

        [Test]
        public void ChangingThePrivacyChoiceCanStartAdverts()
        {
            var rig = new MonetisationRig();
            rig.Consent.Answer = new ConsentOutcome { CanRequestAds = false };
            rig.Consent.PrivacyOptionsRequired = true;
            rig.Start();
            Assert.IsTrue(rig.M.Ads.PrivacyOptionsRequired);
            rig.Consent.PrivacyOptionsChangeTo = true;
            string error = "not called";
            rig.M.Ads.ShowPrivacyOptions(e => error = e);
            Assert.IsNull(error);
            Assert.AreEqual(1, rig.Network.InitCalls);
            Assert.AreEqual(AdServiceState.Ready, rig.M.Ads.State);
        }

        [Test]
        public void ConsentSettingsArePassedToTheSdks()
        {
            var rig = new MonetisationRig(c =>
            {
                c.Monetisation.privacy.maxAdContentRating = "G";
                c.Monetisation.privacy.debugGeographyEea = true;
                c.Monetisation.privacy.testDeviceIds = new[] { "HASH1" };
            }).Start();
            Assert.AreEqual("G", rig.Network.Settings.MaxAdContentRating);
            CollectionAssert.AreEqual(new[] { "HASH1" }, rig.Network.Settings.TestDeviceIds);
            Assert.IsTrue(rig.Consent.LastRequest.DebugGeographyEea, "test builds may pretend to be in the EEA");
            Assert.IsFalse(rig.Network.Settings.ChildDirected);
        }

        [Test]
        public void WithoutAnAdSdkEveryAdvertOfferIsUnavailable()
        {
            var rig = new MonetisationRig().Start(ads: false);
            Assert.AreEqual(AdServiceState.Unavailable, rig.M.Ads.State);
            Assert.AreEqual(OfferStatus.Unavailable, rig.M.Coins.ShopOffer().Status);
            Assert.IsFalse(rig.M.Coins.WatchForShopCoins(null));
            bool continued = false;
            for (int i = 0; i < 5; i++) WinCampaignLevel(rig);
            rig.M.Ads.AtTransition(AdTransition.LevelWonNext, () => continued = true);
            Assert.IsTrue(continued);
        }

        // ================================================================== Purchases

        [Test]
        public void PricesComeFromTheStoreOnly()
        {
            var rig = new MonetisationRig();
            rig.Store.Prices["coins_small"] = "£1.49";
            rig.Store.Prices["coins_premium"] = null;
            foreach (var p in rig.M.Purchases.Products) Assert.IsNull(p.Price, "no price before the store answers: " + p.Id);
            rig.Start();
            Assert.AreEqual("£1.49", rig.M.Purchases.Find("coins_small").Price);
            Assert.IsFalse(rig.M.Purchases.Find("coins_premium").Available, "a product the store doesn't price can't be bought");
            Assert.AreEqual(PurchaseStart.ProductUnavailable, rig.M.Purchases.Buy("coins_premium"));
            CollectionAssert.AreEqual(new[] { 500, 1200, 2750, 6000 }, new List<StoreProduct>(rig.M.Purchases.CoinPacks).ConvertAll(p => p.Coins));
            var requested = rig.Store.Requested;
            Assert.AreEqual(5, requested.Count);
            Assert.AreEqual(ProductKind.NonConsumable, requested[4].Kind);
            Assert.AreEqual("no_ads", requested[4].Id);
        }

        [Test]
        public void ACompletedCoinPurchaseIsGrantedSavedAndThenConfirmed()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 40;
            bool savedBeforeConfirm = false;
            rig.Store.Confirmed.Clear();
            rig.Store.OnConfirm = r =>
            {
                var saved = SaveCodec.Decode(rig.Storage.Main);
                savedBeforeConfirm = saved.Monetisation.HasTransaction("t:GPA.1234") && saved.Coins == 1240;
            };
            Assert.AreEqual(PurchaseStart.Started, rig.M.Purchases.Buy("coins_medium"));
            Assert.AreEqual(40, rig.Ctx.Wallet.Balance, "nothing until the store reports the payment");
            Assert.IsTrue(rig.M.Purchases.IsPurchasing);
            var receipt = new PurchaseReceipt { ProductId = "coins_medium", TransactionId = "GPA.1234", Receipt = "r" };
            int confirmsBefore = rig.Store.Confirmed.Count;
            rig.M.Purchases.OnPurchaseSucceeded(receipt);
            Assert.AreEqual(1240, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(confirmsBefore + 1, rig.Store.Confirmed.Count);
            Assert.IsTrue(savedBeforeConfirm, "the coins and the transaction were saved before the store was told");
            Assert.AreEqual(1240, SaveCodec.Decode(rig.Storage.Main).Coins);
            Assert.IsFalse(rig.M.Purchases.IsPurchasing);
            var e = rig.PurchaseEvents[rig.PurchaseEvents.Count - 1];
            Assert.AreEqual(PurchaseEventKind.Completed, e.Kind);
            Assert.AreEqual(1200, e.Coins);
            Assert.AreEqual("iap.coins_medium", rig.Ctx.Wallet.Ledger[rig.Ctx.Wallet.Ledger.Count - 1].Reason);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.CoinPackCompleted));
        }

        [Test]
        public void TheSameTransactionIsNeverGrantedTwice()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 0;
            rig.M.Purchases.Buy("coins_small");
            var r = rig.Store.Pay("coins_small", "GPA.77");
            rig.M.Purchases.OnPurchaseSucceeded(r);
            rig.M.Purchases.OnPurchaseSucceeded(new PurchaseReceipt { ProductId = "coins_small", TransactionId = "GPA.77", Restored = true });
            Assert.AreEqual(500, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(3, rig.Store.Confirmed.Count, "each delivery is still finished with the store");
            Assert.AreEqual(2, rig.M.Analytics.Count(MonetisationEvents.PurchaseDuplicateIgnored));
        }

        [Test]
        public void APurchaseDeliveredAgainAfterACrashIsNotPaidTwice()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 0;
            rig.M.Purchases.Buy("coins_large");
            rig.Store.Pay("coins_large", "GPA.900");
            Assert.AreEqual(2750, rig.Ctx.Wallet.Balance);
            // The app is killed before the store learns of the confirmation; next start, it delivers the purchase again.
            var again = new MonetisationRig(null, rig.Storage, rig.Clock).Start();
            again.Store.Pay("coins_large", "GPA.900", restored: true);
            Assert.AreEqual(2750, again.Ctx.Wallet.Balance);
            Assert.AreEqual(1, again.Store.Confirmed.Count);
        }

        [Test]
        public void IfTheSaveFailsThePurchaseIsLeftForTheStoreToDeliverAgain()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 0;
            rig.Storage.FailWrites = true;
            rig.M.Purchases.Buy("coins_small");
            rig.Store.Pay("coins_small", "GPA.5");
            Assert.AreEqual(0, rig.Store.Confirmed.Count, "not confirmed: the store keeps it");
            // The app closes without saving; the next start gets the purchase again and grants it.
            rig.Storage.FailWrites = false;
            var again = new MonetisationRig(null, rig.Storage, rig.Clock).Start();
            int before = again.Ctx.Wallet.Balance;
            again.Store.Pay("coins_small", "GPA.5", restored: true);
            Assert.AreEqual(before + 500, again.Ctx.Wallet.Balance);
            Assert.AreEqual(1, again.Store.Confirmed.Count);
        }

        [Test]
        public void ACancelledPurchaseChangesNothing()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 75;
            rig.M.Purchases.Buy("coins_small");
            rig.M.Purchases.OnPurchaseFailed("coins_small", PurchaseFailure.UserCancelled, "user");
            Assert.AreEqual(75, rig.Ctx.Wallet.Balance);
            Assert.IsFalse(rig.M.Purchases.IsPurchasing);
            Assert.AreEqual(0, rig.Store.Confirmed.Count);
            Assert.AreEqual(PurchaseEventKind.Cancelled, rig.PurchaseEvents[rig.PurchaseEvents.Count - 1].Kind);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.PurchaseCancelled));
            Assert.AreEqual(PurchaseStart.Started, rig.M.Purchases.Buy("coins_small"), "the player can try again");
        }

        [Test]
        public void AFailedPurchaseChangesNothing()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 75;
            rig.M.Purchases.Buy("no_ads");
            rig.M.Purchases.OnPurchaseFailed("no_ads", PurchaseFailure.PaymentDeclined, "declined");
            Assert.AreEqual(75, rig.Ctx.Wallet.Balance);
            Assert.IsFalse(rig.M.NoAds);
            var e = rig.PurchaseEvents[rig.PurchaseEvents.Count - 1];
            Assert.AreEqual(PurchaseEventKind.Failed, e.Kind);
            StringAssert.Contains("declined", e.Message);
        }

        [Test]
        public void APendingPaymentGrantsNothingUntilItIsPaid()
        {
            var rig = new MonetisationRig().Start();
            rig.Ctx.Data.Coins = 0;
            rig.M.Purchases.Buy("coins_premium");
            rig.M.Purchases.OnPurchaseDeferred("coins_premium");
            Assert.AreEqual(0, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(0, rig.Store.Confirmed.Count);
            Assert.IsFalse(rig.M.Purchases.IsPurchasing, "the shop isn't locked while waiting for the payment");
            Assert.AreEqual(PurchaseEventKind.Pending, rig.PurchaseEvents[rig.PurchaseEvents.Count - 1].Kind);
            // Paid later: Google Play delivers it at the next start.
            var again = rig.Restart().Start();
            again.Store.Pay("coins_premium", "GPA.cash", restored: true);
            Assert.AreEqual(6000, again.Ctx.Wallet.Balance);
            var e = again.PurchaseEvents[again.PurchaseEvents.Count - 1];
            Assert.AreEqual(PurchaseEventKind.Completed, e.Kind);
            Assert.IsTrue(e.Restored);
        }

        [Test]
        public void PurchasesCantStartWhenTheyShouldnt()
        {
            var rig = new MonetisationRig();
            rig.Store.ReadyImmediately = false;
            rig.Start();
            Assert.AreEqual(PurchaseStart.StoreNotReady, rig.M.Purchases.Buy("coins_small"));
            rig.Store.BecomeReady();
            Assert.AreEqual(PurchaseStart.UnknownProduct, rig.M.Purchases.Buy("gems"));
            Assert.AreEqual(PurchaseStart.Started, rig.M.Purchases.Buy("coins_small"));
            Assert.AreEqual(PurchaseStart.AnotherInProgress, rig.M.Purchases.Buy("coins_medium"));
            Assert.AreEqual(1, rig.Store.Purchases.Count);
            rig.Store.Pay("coins_small");
            rig.Ctx.Activity.BeginRun(GameMode.Daily);
            Assert.AreEqual(PurchaseStart.BlockedDuringChallenge, rig.M.Purchases.Buy("coins_small"));
            rig.Ctx.Activity.EndRun();
            rig.Store.Pay("no_ads");
            Assert.AreEqual(PurchaseStart.AlreadyOwned, rig.M.Purchases.Buy("no_ads"));
        }

        [Test]
        public void AStoreThatNeverAnswersUnlocksTheShopAgain()
        {
            var rig = new MonetisationRig().Start();
            rig.M.Purchases.Buy("coins_small");
            rig.M.NotifyAppPaused(true);
            rig.Wait(600);
            Assert.IsTrue(rig.M.Purchases.IsPurchasing, "the player may be in the store's payment sheet");
            rig.M.NotifyAppPaused(false);
            rig.Wait(121);
            Assert.IsFalse(rig.M.Purchases.IsPurchasing);
            Assert.AreEqual(PurchaseEventKind.Failed, rig.PurchaseEvents[rig.PurchaseEvents.Count - 1].Kind);
            int balance = rig.Ctx.Wallet.Balance;
            rig.Store.Pay("coins_small", "GPA.late");
            Assert.AreEqual(balance + 500, rig.Ctx.Wallet.Balance, "a payment that does arrive later is still granted");
        }

        [Test]
        public void AnUnknownProductIsNeitherGrantedNorConfirmed()
        {
            var rig = new MonetisationRig().Start();
            int coins = rig.Ctx.Wallet.Balance;
            rig.M.Purchases.OnPurchaseSucceeded(new PurchaseReceipt { ProductId = "old_gem_pack", TransactionId = "GPA.x" });
            Assert.AreEqual(coins, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(0, rig.Store.Confirmed.Count);
        }

        [Test]
        public void NoAdsIsAPermanentEntitlementThatSurvivesARestart()
        {
            var rig = new MonetisationRig().Start();
            Assert.IsFalse(rig.M.NoAds);
            rig.M.Purchases.Buy("no_ads");
            var r = rig.Store.Pay("no_ads", "GPA.noads");
            Assert.IsTrue(rig.M.NoAds);
            Assert.AreSame(r, rig.Store.Confirmed[0], "acknowledged with the store (not consumed: the backend decides by product type)");
            Assert.AreEqual("GPA.noads", rig.Ctx.Data.Monetisation.NoAdsTransaction);
            rig.Store.Owned["no_ads"] = true;

            var restarted = rig.Restart();
            Assert.IsTrue(restarted.M.NoAds, "known at once from the save, before the store answers");
            restarted.Store.Owned["no_ads"] = true;
            restarted.Start();
            Assert.AreEqual(1, restarted.Store.OwnershipChecks, "and confirmed with the store on every start");
            Assert.IsTrue(restarted.M.NoAds);

            var offline = rig.Restart();
            offline.Start(store: false);
            Assert.IsTrue(offline.M.NoAds, "without a connection the last confirmed answer stands");
        }

        [Test]
        public void NoAdsIsRestoredOnANewDevice()
        {
            var rig = new MonetisationRig();
            rig.Store.Owned["no_ads"] = true;
            Assert.IsFalse(rig.M.NoAds, "a fresh install knows nothing yet");
            rig.Start();
            Assert.IsTrue(rig.M.NoAds, "the start-up ownership check restores it");
            Assert.AreEqual(PurchaseEventKind.Completed, rig.PurchaseEvents[0].Kind);
            Assert.IsTrue(rig.PurchaseEvents[0].Restored);
        }

        [Test]
        public void RestorePurchasesAsksTheStoreAndReportsBack()
        {
            var rig = new MonetisationRig().Start();
            rig.Store.Owned["no_ads"] = true;
            Assert.IsTrue(rig.M.Purchases.Restore());
            Assert.AreEqual(1, rig.Store.Restores);
            Assert.IsTrue(rig.M.NoAds);
            Assert.IsFalse(rig.M.Purchases.IsRestoring);
            Assert.AreEqual(PurchaseEventKind.RestoreFinished, rig.PurchaseEvents[rig.PurchaseEvents.Count - 1].Kind);
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RestoreAttempted));
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RestoreCompleted));
            Assert.IsTrue(rig.M.Purchases.Restore(), "can be asked again");
            Assert.IsTrue(rig.M.NoAds);
        }

        [Test]
        public void ARestoreTheStoreNeverAnswersEndsAsFailed()
        {
            var rig = new MonetisationRig().Start();
            rig.Store.AnswerRestore = false;
            Assert.IsTrue(rig.M.Purchases.Restore());
            Assert.AreEqual(PurchaseStart.AnotherInProgress, rig.M.Purchases.Buy("coins_small"), "no purchase during a restore");
            rig.Wait(31);
            Assert.IsFalse(rig.M.Purchases.IsRestoring);
            var e = rig.PurchaseEvents[rig.PurchaseEvents.Count - 1];
            Assert.AreEqual(PurchaseEventKind.RestoreFinished, e.Kind);
            Assert.IsFalse(e.Success);
            rig.M.Purchases.OnRestoreFinished(true, "");
            Assert.AreEqual(1, rig.M.Analytics.Count(MonetisationEvents.RestoreCompleted), "a late answer isn't reported twice");
            Assert.AreEqual(PurchaseStart.Started, rig.M.Purchases.Buy("coins_small"));
        }

        [Test]
        public void ARefundedNoAdsIsWithdrawnOnlyAfterTwoStoreChecks()
        {
            var rig = new MonetisationRig().Start();
            rig.M.Purchases.Buy("no_ads");
            rig.Store.Pay("no_ads");
            rig.M.Purchases.OnOwnershipChecked("no_ads", false, null);
            Assert.IsTrue(rig.M.NoAds, "one check may be out of date");
            var second = rig.Restart().Start();
            Assert.IsFalse(second.M.NoAds, "the store said 'not owned' on two separate starts: refunded");
            Assert.AreEqual(PurchaseEventKind.NoAdsRevoked, second.PurchaseEvents[second.PurchaseEvents.Count - 1].Kind);
        }

        [Test]
        public void ARestoredNoAdsCancelsAnEarlierNotOwnedCheck()
        {
            var rig = new MonetisationRig().Start();
            rig.Store.Pay("no_ads");
            rig.M.Purchases.OnOwnershipChecked("no_ads", false, null);
            rig.M.Purchases.OnOwnershipChecked("no_ads", true, "GPA.1");
            var second = rig.Restart();
            second.Store.Owned["no_ads"] = false;
            second.Start();
            Assert.IsTrue(second.M.NoAds, "the count of 'not owned' answers started again");
        }

        [Test]
        public void AlreadyOwnedAsksTheStoreForOwnership()
        {
            var rig = new MonetisationRig().Start();
            rig.Store.Owned["no_ads"] = true;
            int checks = rig.Store.OwnershipChecks;
            rig.M.Purchases.OnPurchaseFailed("no_ads", PurchaseFailure.AlreadyOwned, "owned");
            Assert.AreEqual(checks + 1, rig.Store.OwnershipChecks);
            Assert.IsTrue(rig.M.NoAds);
        }

        // ================================================================== Saves and regression

        [Test]
        public void SavesFromBeforeUpdate3LoadWithEmptyMonetisationData()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 120);
            d.Coins = 777;
            var json = SaveCodec.Encode(d);
            var root = (Dictionary<string, object>)Json.Parse(json);
            root.Remove("monetisation");
            var loaded = SaveCodec.Decode(Json.Write(root));
            Assert.AreEqual(777, loaded.Coins);
            Assert.AreEqual(0, loaded.Monetisation.CompletionsSinceInterstitial);
            Assert.IsFalse(loaded.Monetisation.NoAds);
            Assert.AreEqual(0, loaded.Monetisation.Transactions.Count);
        }

        [Test]
        public void MonetisationDataRoundTripsThroughTheSave()
        {
            var d = SaveData.CreateNew(TestUtil.Today, 0);
            var m = d.Monetisation;
            m.CompletionsSinceInterstitial = 4;
            m.LastInterstitialTicks = 12345;
            m.LastQuietTicks = 678;
            m.AdDay = "2026-10-25";
            m.AdsToday["shop_coins"] = 3;
            m.LastAdTicks["shop_coins"] = 999;
            m.AddTransaction("t:GPA.1");
            m.NoAds = true;
            m.NoAdsTransaction = "GPA.2";
            m.NoAdsCheckedTicks = 55;
            m.NoAdsMissingChecks = 1;
            m.EventCounts["reward_granted"] = 7;
            var back = SaveCodec.Decode(SaveCodec.Encode(d)).Monetisation;
            Assert.AreEqual(4, back.CompletionsSinceInterstitial);
            Assert.AreEqual(12345, back.LastInterstitialTicks);
            Assert.AreEqual(678, back.LastQuietTicks);
            Assert.AreEqual("2026-10-25", back.AdDay);
            Assert.AreEqual(3, back.AdsToday["shop_coins"]);
            Assert.AreEqual(999, back.LastAdTicks["shop_coins"]);
            Assert.IsTrue(back.HasTransaction("t:GPA.1"));
            Assert.IsTrue(back.NoAds);
            Assert.AreEqual("GPA.2", back.NoAdsTransaction);
            Assert.AreEqual(55, back.NoAdsCheckedTicks);
            Assert.AreEqual(1, back.NoAdsMissingChecks);
            Assert.AreEqual(7, back.EventCounts["reward_granted"]);
        }

        [Test]
        public void TheTransactionRecordIsCapped()
        {
            var m = new MonetisationRecord();
            for (int i = 0; i < MonetisationRecord.MaxTransactions + 50; i++) m.AddTransaction("t:" + i);
            Assert.AreEqual(MonetisationRecord.MaxTransactions, m.Transactions.Count);
            Assert.IsFalse(m.HasTransaction("t:0"));
            Assert.IsTrue(m.HasTransaction("t:" + (MonetisationRecord.MaxTransactions + 49)));
        }

        [Test]
        public void AProgressResetKeepsPurchasesButResetsProgress()
        {
            var rig = new MonetisationRig().Start();
            rig.M.Purchases.Buy("no_ads");
            rig.Store.Pay("no_ads", "GPA.n");
            rig.M.Purchases.Buy("coins_small");
            rig.Store.Pay("coins_small", "GPA.c");
            rig.Ctx.ResetProgress();
            Assert.IsTrue(rig.M.NoAds, "No Ads is a purchase, not progress");
            Assert.IsTrue(rig.Ctx.Data.Monetisation.HasTransaction("t:GPA.c"), "so a redelivered purchase still isn't paid twice");
            Assert.AreEqual(rig.Ctx.Config.Economy.startingCoins, rig.Ctx.Wallet.Balance);
        }

        [Test]
        public void StartingMonetisationChangesNoProgressCoinsOrPowerUps()
        {
            var rig = new MonetisationRig();
            rig.Ctx.Data.Coins = 4321;
            rig.Ctx.Inventory.AddOwned(PowerUpId.ColourBurst, 3);
            rig.Ctx.Data.HighestUnlocked = 57;
            string before = SaveCodec.Encode(rig.Ctx.Data);
            rig.Start();
            for (int i = 0; i < 100; i++) rig.Wait(1);
            Assert.AreEqual(4321, rig.Ctx.Wallet.Balance);
            Assert.AreEqual(3, rig.Ctx.Inventory.Owned(PowerUpId.ColourBurst));
            Assert.AreEqual(57, rig.Ctx.Data.HighestUnlocked);
            var a = (Dictionary<string, object>)Json.Parse(before);
            var b = (Dictionary<string, object>)Json.Parse(SaveCodec.Encode(rig.Ctx.Data));
            foreach (var k in new[] { "monetisation", "saved", "saveCount" })
            {
                a.Remove(k);
                b.Remove(k);
            }
            Assert.AreEqual(Json.Write(a), Json.Write(b), "only the monetisation record (event counts) changes");
        }

        [Test]
        public void LedgerReasonsForMonetisationAreReadable()
        {
            Assert.AreEqual("Advert reward", RewardKeys.Describe("ad.coins"));
            Assert.AreEqual("Advert: coins for a power-up", RewardKeys.Describe("ad.shortfall"));
            Assert.AreEqual("Bought coins", RewardKeys.Describe("iap.coins_small"));
            Assert.AreEqual("Extra moves, level 12", RewardKeys.Describe("continue.L12"));
        }

        [Test]
        public void ARandomStormOfCallbacksNeverPaysMoreThanOnceAnAdvert()
        {
            var rng = new Rng(2024);
            var rig = Started(c => { c.Monetisation.ads.shopCoins.dailyLimit = 0; c.Monetisation.ads.shopCoins.cooldownSeconds = 0; });
            rig.Ctx.Data.Coins = 0;
            int earnedTickets = 0;
            for (int round = 0; round < 300; round++)
            {
                if (!rig.M.Coins.ShopOffer().CanShow)
                {
                    rig.Wait(400);
                    continue;
                }
                bool finished = false;
                rig.M.Coins.WatchForShopCoins((o, c) => finished = true);
                bool earned = false;
                int steps = 1 + rng.Next(8);
                for (int k = 0; k < steps; k++)
                {
                    switch (rng.Next(6))
                    {
                        case 0: rig.Network.Open(); break;
                        case 1: rig.Network.Earn(); earned = true; break;
                        case 2: rig.Network.Close(); break;
                        case 3: rig.Network.Fail(); break;
                        case 4: rig.M.NotifyAppPaused(rng.Next(2) == 0); break;
                        default: rig.Wait(rng.Next(20)); break;
                    }
                }
                rig.M.NotifyAppPaused(false);
                for (int w = 0; w < 40 && !finished; w++) rig.Wait(10);
                Assert.IsTrue(finished, "every advert ends (round " + round + ")");
                Assert.IsFalse(rig.M.Ads.IsShowing);
                if (earned) earnedTickets++;
            }
            Assert.AreEqual(earnedTickets * 20, rig.Ctx.Wallet.Balance, "20 coins for every advert the SDK rewarded, never more");
            Assert.Greater(earnedTickets, 50);
        }
    }
}
