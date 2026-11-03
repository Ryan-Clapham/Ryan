using System;

namespace ColourCascade.Core
{
    /// <summary>The Out of Moves choices for one level attempt.</summary>
    public struct ContinueOptions
    {
        /// <summary>The level can be continued at all (a campaign level that has just run out of moves).</summary>
        public bool Available;
        public RewardedOffer Ad;
        public int AdMoves;
        public int CoinCost;
        public int CoinMoves;
        public bool CoinAffordable;
        public int CoinShortfall;
        public bool CoinLimitReached;
    }

    public enum CoinContinueResult
    {
        Done,
        NotAvailable,
        LimitReached,
        InsufficientCoins,
    }

    /// <summary>
    /// Out of Moves (docs/MONETISATION.md §2.1): watch an advert for extra moves, or spend coins for fewer. Both add
    /// moves to the same session (board, score, goals, obstacles, specials and power-ups untouched); nothing restarts.
    /// Coins are taken only when the moves are actually added, and the advert's moves only after the SDK confirms the
    /// reward. Campaign only: the Daily Challenge and Weekly Rush stay fair, and Endless runs simply end.
    /// </summary>
    public sealed class ContinueService
    {
        readonly AdConfig config;
        readonly AdService ads;
        readonly Func<Wallet> wallet;
        readonly MonetisationAnalytics analytics;
        readonly InterstitialPolicy interstitials;
        object coinRun;
        int coinRunCount;

        public ContinueService(AdConfig config, AdService ads, Func<Wallet> wallet, MonetisationAnalytics analytics, InterstitialPolicy interstitials)
        {
            this.config = config ?? new AdConfig();
            this.ads = ads;
            this.wallet = wallet;
            this.analytics = analytics;
            this.interstitials = interstitials;
        }

        public int AdMoves => Math.Max(0, config.continueMoves.reward);
        public int CoinCost => Math.Max(0, config.continueCoinCost);
        public int CoinMoves => Math.Max(0, config.continueCoinMoves);

        public static bool CanContinue(PuzzleSession s) =>
            s != null && s.Mode == GameMode.Campaign && s.State == SessionState.Failed && s.EndReason == EndReason.OutOfMoves;

        public ContinueOptions Options(PuzzleSession s)
        {
            var o = new ContinueOptions { Available = CanContinue(s), AdMoves = AdMoves, CoinCost = CoinCost, CoinMoves = CoinMoves };
            o.Ad = ads != null ? ads.CheckRewarded(RewardedPlacements.ContinueMoves, s) : new RewardedOffer { Status = OfferStatus.Unavailable };
            if (AdMoves <= 0) o.Ad.Status = OfferStatus.Disabled;
            var w = wallet();
            o.CoinAffordable = w != null && w.CanAfford(CoinCost);
            o.CoinShortfall = w != null ? Math.Max(0, CoinCost - w.Balance) : CoinCost;
            o.CoinLimitReached = config.continueCoinPerRunLimit > 0 && ReferenceEquals(coinRun, s) && coinRunCount >= config.continueCoinPerRunLimit;
            return o;
        }

        /// <summary>
        /// Shows the advert; the moves are added only when the SDK confirms the reward, and only if this attempt can
        /// still be continued. done reports how the advert ended. False if no advert can be shown now.
        /// </summary>
        public bool WatchAd(PuzzleSession s, Action<AdOutcome> done)
        {
            if (!CanContinue(s) || ads == null || AdMoves <= 0) return false;
            int moves = AdMoves;
            return ads.ShowRewarded(RewardedPlacements.ContinueMoves, s, () =>
            {
                if (!CanContinue(s) || !s.AddMoves(moves)) return false;
                analytics?.Record(MonetisationEvents.ExtraMovesAd, ("level", s.Level.Number), ("moves", moves));
                return true;
            }, done);
        }

        /// <summary>Takes the coins and adds the moves together, or neither.</summary>
        public CoinContinueResult ContinueWithCoins(PuzzleSession s)
        {
            if (!CanContinue(s) || CoinMoves <= 0) return CoinContinueResult.NotAvailable;
            if (config.continueCoinPerRunLimit > 0 && ReferenceEquals(coinRun, s) && coinRunCount >= config.continueCoinPerRunLimit)
                return CoinContinueResult.LimitReached;
            var w = wallet();
            string reason = "continue.L" + s.Level.Number;
            if (w == null || !w.TryDebit(CoinCost, reason)) return CoinContinueResult.InsufficientCoins;
            if (!s.AddMoves(CoinMoves))
            {
                // Can't happen after CanContinue, but never keep the coins for moves that weren't given.
                w.Credit(CoinCost, reason + ".refund");
                return CoinContinueResult.NotAvailable;
            }
            if (!ReferenceEquals(coinRun, s))
            {
                coinRun = s;
                coinRunCount = 0;
            }
            coinRunCount++;
            interstitials?.NoteRewardedOrPurchase();
            analytics?.Record(MonetisationEvents.ExtraMovesCoins, ("level", s.Level.Number), ("moves", CoinMoves), ("coins", CoinCost));
            return CoinContinueResult.Done;
        }
    }

    /// <summary>Not enough coins for something: what an advert could add.</summary>
    public struct ShortfallOffer
    {
        public int Price;
        public int Balance;
        public int Shortfall;
        /// <summary>Coins the advert would give: the shortfall, up to the placement's reward.</summary>
        public int AdCoins;
        /// <summary>The advert's coins are enough to afford the price.</summary>
        public bool Covers;
        public RewardedOffer Ad;

        public string Explanation
        {
            get
            {
                if (Shortfall <= 0) return "You have enough coins.";
                string need = "You need " + NumberFormat.Thousands(Shortfall) + " more coins.";
                if (AdCoins <= 0) return need;
                if (Covers) return need + " Watch an advert to earn the " + NumberFormat.Thousands(AdCoins) + (AdCoins == 1 ? " coin" : " coins") + " you're missing.";
                return need + " An advert gives up to " + NumberFormat.Thousands(AdCoins) + " coins, so you'd still be " +
                       NumberFormat.Thousands(Shortfall - AdCoins) + " short.";
            }
        }
    }

    /// <summary>
    /// Coins for adverts (docs/MONETISATION.md §2.2–2.3): the Coins tab offer, and the offer made when a power-up
    /// costs more than the player has. Coins are added only after the SDK confirms the reward; nothing is bought
    /// automatically afterwards: the player goes back to the purchase and confirms it.
    /// </summary>
    public sealed class CoinOffers
    {
        readonly AdConfig config;
        readonly AdService ads;
        readonly Func<Wallet> wallet;
        readonly MonetisationAnalytics analytics;
        readonly Func<bool> save;

        public CoinOffers(AdConfig config, AdService ads, Func<Wallet> wallet, MonetisationAnalytics analytics, Func<bool> save = null)
        {
            this.config = config ?? new AdConfig();
            this.ads = ads;
            this.wallet = wallet;
            this.analytics = analytics;
            this.save = save ?? (() => true);
        }

        public int ShopReward => Math.Max(0, config.shopCoins.reward);

        public RewardedOffer ShopOffer() => ads != null ? ads.CheckRewarded(RewardedPlacements.ShopCoins) : new RewardedOffer { Status = OfferStatus.Unavailable };

        /// <summary>Watch an advert for the Coins tab reward. done(outcome, coins added).</summary>
        public bool WatchForShopCoins(Action<AdOutcome, int> done)
        {
            int coins = ShopReward;
            if (coins <= 0 || ads == null) return false;
            int granted = 0;
            return ads.ShowRewarded(RewardedPlacements.ShopCoins, null, () =>
            {
                var w = wallet();
                if (w == null || !w.Credit(coins, "ad.coins")) return false;
                granted = coins;
                save();
                analytics?.Record(MonetisationEvents.CoinsAd, ("coins", coins));
                return true;
            }, outcome => done?.Invoke(outcome, granted));
        }

        public ShortfallOffer ForPrice(int price)
        {
            var w = wallet();
            int balance = w != null ? w.Balance : 0;
            var o = new ShortfallOffer { Price = Math.Max(0, price), Balance = balance };
            o.Shortfall = Math.Max(0, o.Price - balance);
            int cap = Math.Max(0, config.coinShortfall.reward);
            o.AdCoins = Math.Min(o.Shortfall, cap);
            o.Covers = o.Shortfall > 0 && o.AdCoins >= o.Shortfall;
            o.Ad = ads != null ? ads.CheckRewarded(RewardedPlacements.CoinShortfall) : new RewardedOffer { Status = OfferStatus.Unavailable };
            if (o.AdCoins <= 0) o.Ad.Status = OfferStatus.Disabled;
            o.Ad.Reward = o.AdCoins;
            return o;
        }

        /// <summary>
        /// Watch an advert for the coins this price is short of (fixed when the advert starts, so the player gets what
        /// was offered). Never buys anything. done(outcome, coins added).
        /// </summary>
        public bool WatchForShortfall(int price, string item, Action<AdOutcome, int> done)
        {
            var offer = ForPrice(price);
            if (offer.AdCoins <= 0 || ads == null) return false;
            int coins = offer.AdCoins, granted = 0;
            return ads.ShowRewarded(RewardedPlacements.CoinShortfall, null, () =>
            {
                var w = wallet();
                if (w == null || !w.Credit(coins, "ad.shortfall")) return false;
                granted = coins;
                save();
                analytics?.Record(MonetisationEvents.ShortfallAd, ("coins", coins), ("item", item ?? ""), ("covers", offer.Covers));
                return true;
            }, outcome => done?.Invoke(outcome, granted));
        }

        public void NoteShortfallOffered(ShortfallOffer offer, string item)
        {
            analytics?.Record(MonetisationEvents.ShortfallOffered, ("item", item ?? ""), ("shortfall", offer.Shortfall), ("ad_coins", offer.AdCoins),
                ("covers", offer.Covers), ("status", offer.Ad.Status));
        }
    }
}
