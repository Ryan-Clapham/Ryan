using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    // The only contact points with the advertising, consent and store SDKs. The game's rules (AdService,
    // PurchaseService) talk to these interfaces; the Unity project supplies implementations for Google Mobile Ads,
    // Google's User Messaging Platform and Unity IAP (Scripts/Integrations), and the tests supply scripted fakes.
    // Implementations must call back on Unity's main thread.

    public enum AdFormat
    {
        Rewarded,
        Interstitial,
    }

    /// <summary>What the ad SDK is told when it starts (from PrivacyConfig and the build type).</summary>
    public sealed class AdNetworkSettings
    {
        public bool TestMode;
        public string MaxAdContentRating = "PG";
        public bool ChildDirected;
        public bool UnderAgeOfConsent;
        public string[] TestDeviceIds = new string[0];
    }

    /// <summary>Callbacks for one attempt to show an advert. Each may be called at most once per attempt.</summary>
    public sealed class AdShowCallbacks
    {
        /// <summary>The advert is on screen.</summary>
        public Action Opened;
        /// <summary>Rewarded only: the SDK confirms the player earned the reward.</summary>
        public Action Earned;
        /// <summary>The advert was dismissed.</summary>
        public Action Closed;
        /// <summary>The advert could not be shown (with the SDK's message).</summary>
        public Action<string> Failed;
    }

    public interface IAdNetwork
    {
        string Name { get; }
        /// <summary>Starts the SDK. Only called once the player's consent allows ad requests.</summary>
        void Initialize(AdNetworkSettings settings, Action<bool, string> done);
        /// <summary>Requests an advert; done(true) when it is ready to show, done(false, error) otherwise.</summary>
        void Load(AdFormat format, string adUnitId, Action<bool, string> done);
        /// <summary>Whether a loaded advert of this format can be shown now (false once shown or expired).</summary>
        bool IsReady(AdFormat format);
        void Show(AdFormat format, AdShowCallbacks callbacks);
    }

    public struct ConsentOutcome
    {
        /// <summary>Whether ads may be requested (consent given, or not required where the player is).</summary>
        public bool CanRequestAds;
        public string Error;
    }

    /// <summary>Google's User Messaging Platform (or another certified consent tool).</summary>
    public interface IConsentProvider
    {
        /// <summary>Updates the consent status and shows the consent form if the player has to answer it.</summary>
        void Gather(ConsentRequest request, Action<ConsentOutcome> done);
        /// <summary>Whether ads may be requested now (also true from a previous session's answer).</summary>
        bool CanRequestAds { get; }
        /// <summary>Whether the player must be offered a way to change their choice (Settings shows "Privacy options").</summary>
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions(Action<string> done);
    }

    public sealed class ConsentRequest
    {
        public bool UnderAgeOfConsent;
        /// <summary>Development builds only: treat the device as being in the EEA.</summary>
        public bool DebugGeographyEea;
        public string[] TestDeviceIds = new string[0];
    }

    public enum ProductKind
    {
        Consumable,
        NonConsumable,
    }

    public sealed class StoreProductRequest
    {
        public string Id;
        public ProductKind Kind;
    }

    /// <summary>What the store says about a product (prices are localised by the store).</summary>
    public sealed class StoreProductInfo
    {
        public string Id;
        public string LocalizedPrice;
        public string LocalizedTitle;
        public bool Available;
    }

    /// <summary>A paid purchase delivered by the store, to be granted and then confirmed.</summary>
    public sealed class PurchaseReceipt
    {
        public string ProductId;
        public string TransactionId;
        public string Receipt;
        /// <summary>Delivered at start-up or by a restore, not by a purchase made in this session.</summary>
        public bool Restored;
        /// <summary>The store backend's own object for this purchase (passed back to Confirm).</summary>
        public object Handle;
    }

    public enum PurchaseFailure
    {
        None,
        UserCancelled,
        PaymentDeclined,
        ProductUnavailable,
        AlreadyOwned,
        StoreUnavailable,
        PurchaseInProgress,
        NetworkError,
        Unknown,
    }

    /// <summary>The store backend reports to this (PurchaseService implements it).</summary>
    public interface IStoreCallbacks
    {
        void OnStoreReady(IList<StoreProductInfo> products);
        void OnStoreUnavailable(string reason);
        /// <summary>A paid purchase: grant it, save, then call IStoreBackend.Confirm.</summary>
        void OnPurchaseSucceeded(PurchaseReceipt receipt);
        /// <summary>The purchase is waiting for payment (e.g. cash at a shop): nothing is granted yet.</summary>
        void OnPurchaseDeferred(string productId);
        void OnPurchaseFailed(string productId, PurchaseFailure reason, string detail);
        /// <summary>The store's answer to "does the player own this non-consumable?".</summary>
        void OnOwnershipChecked(string productId, bool owned, string transactionId);
        void OnRestoreFinished(bool success, string message);
    }

    public interface IStoreBackend
    {
        string Name { get; }
        void Initialize(IList<StoreProductRequest> products, IStoreCallbacks callbacks);
        void Purchase(string productId);
        /// <summary>Finishes a purchase with the store (consume or acknowledge). Only after it has been granted and saved.</summary>
        void Confirm(PurchaseReceipt receipt);
        /// <summary>Asks the store for every purchase the player owns (non-consumables are reported again).</summary>
        void Restore();
        /// <summary>Asks the store whether the player owns this non-consumable; answers via OnOwnershipChecked.</summary>
        void CheckOwnership(string productId);
    }
}
