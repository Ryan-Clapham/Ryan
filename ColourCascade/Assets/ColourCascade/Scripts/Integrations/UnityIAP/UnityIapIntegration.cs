// Unity In-App Purchasing 5 (Google Play Billing on Android) for Colour Cascade.
//
// Compiled only when Unity IAP 5 or later is installed: the editor sets the CC_UNITY_IAP scripting define when it
// finds UnityIAPServices (Scripts/Setup/MonetisationSdkSetup.cs) and the assembly's Define Constraints require it.
// Menu: Colour Cascade > Monetisation > Turn Unity IAP integration off, if this file ever fails to compile against a
// newer package (the game then runs without purchases until it is fixed).
//
// Written against Unity IAP 5's StoreController API (UnityIAPServices.StoreController, Connect, FetchProducts,
// FetchPurchases, PurchaseProduct, OnPurchasePending/ConfirmPurchase, CheckEntitlement). Enum values whose names have
// changed between releases are compared by name. It has NOT yet been compiled against the real package: see
// docs/MONETISATION.md §11.

using System;
using System.Collections.Generic;
using ColourCascade.App;
using ColourCascade.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace ColourCascade.Integrations
{
    static class UnityIapRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            MonetisationBackends.Store = () => new UnityIapStore();
        }
    }

    /// <summary>
    /// IStoreBackend on Unity IAP 5. Paid purchases arrive as pending orders and are handed to the game (which grants
    /// and saves them) before ConfirmPurchase is called; Google Play refunds purchases that are never confirmed.
    /// No Ads ownership comes from CheckEntitlement and from the purchases Google Play reports at start-up.
    /// </summary>
    sealed class UnityIapStore : IStoreBackend
    {
        StoreController store;
        IStoreCallbacks callbacks;
        readonly Dictionary<string, ProductKind> kinds = new Dictionary<string, ProductKind>();
        readonly Dictionary<string, Product> products = new Dictionary<string, Product>();
        bool ready;
        bool restoreWaitingForEntitlement;

        public string Name => "Unity IAP";

        public async void Initialize(IList<StoreProductRequest> requested, IStoreCallbacks cb)
        {
            callbacks = cb;
            var definitions = new List<ProductDefinition>();
            foreach (var r in requested)
            {
                kinds[r.Id] = r.Kind;
                definitions.Add(new ProductDefinition(r.Id, r.Kind == ProductKind.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
            }
            try
            {
                store = UnityIAPServices.StoreController();
                store.OnStoreDisconnected += failure => Unavailable("The store disconnected: " + Describe(failure));
                store.OnProductsFetched += OnProductsFetched;
                store.OnProductsFetchFailed += failure => Unavailable("Products couldn't be loaded: " + Describe(failure));
                store.OnPurchasePending += OnPurchasePending;
                store.OnPurchaseConfirmed += order => { };
                store.OnPurchaseFailed += OnPurchaseFailed;
                store.OnPurchaseDeferred += order => callbacks.OnPurchaseDeferred(FirstProductId(order));
                store.OnPurchasesFetched += OnPurchasesFetched;
                store.OnPurchasesFetchFailed += failure => Debug.LogWarning("[ColourCascade] Purchases couldn't be fetched: " + Describe(failure));
                store.OnCheckEntitlement += OnCheckEntitlement;
                await store.Connect();
                store.FetchProducts(definitions);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Unavailable(e.Message);
            }
        }

        void Unavailable(string reason)
        {
            ready = false;
            callbacks.OnStoreUnavailable(reason);
        }

        void OnProductsFetched(List<Product> fetched)
        {
            var infos = new List<StoreProductInfo>();
            foreach (var p in fetched)
            {
                if (p == null || p.definition == null) continue;
                products[p.definition.id] = p;
                infos.Add(new StoreProductInfo
                {
                    Id = p.definition.id,
                    LocalizedPrice = p.metadata != null ? p.metadata.localizedPriceString : null,
                    LocalizedTitle = p.metadata != null ? p.metadata.localizedTitle : null,
                    Available = p.availableToPurchase,
                });
            }
            ready = true;
            callbacks.OnStoreReady(infos);
            // Purchases the player already has (No Ads on a new install, a pending payment that completed, a purchase
            // interrupted before it was confirmed) are delivered again now.
            store.FetchPurchases();
        }

        void OnPurchasePending(PendingOrder order)
        {
            if (order == null) return;
            string id = FirstProductId(order);
            callbacks.OnPurchaseSucceeded(new PurchaseReceipt
            {
                ProductId = id,
                TransactionId = order.Info != null ? order.Info.TransactionID : null,
                Receipt = order.Info != null ? order.Info.Receipt : null,
                Handle = order,
            });
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            string id = order != null ? FirstProductId(order) : null;
            string reason = order != null ? order.FailureReason.ToString() : "Unknown";
            callbacks.OnPurchaseFailed(id, Map(reason), order != null ? order.Details : "");
        }

        static PurchaseFailure Map(string reason)
        {
            switch (reason)
            {
                case "UserCancelled": return PurchaseFailure.UserCancelled;
                case "PaymentDeclined": return PurchaseFailure.PaymentDeclined;
                case "ProductUnavailable": return PurchaseFailure.ProductUnavailable;
                case "DuplicateTransaction": return PurchaseFailure.AlreadyOwned;
                case "PurchasingUnavailable": return PurchaseFailure.StoreUnavailable;
                case "ExistingPurchasePending": return PurchaseFailure.PurchaseInProgress;
                default: return PurchaseFailure.Unknown;
            }
        }

        void OnPurchasesFetched(Orders orders)
        {
            if (orders == null) return;
            // Confirmed orders are purchases already finished with the store: for No Ads, they prove ownership.
            foreach (var order in orders.ConfirmedOrders)
            {
                string id = FirstProductId(order);
                ProductKind kind;
                if (id != null && kinds.TryGetValue(id, out kind) && kind == ProductKind.NonConsumable)
                    callbacks.OnOwnershipChecked(id, true, order.Info != null ? order.Info.TransactionID : null);
            }
        }

        void OnCheckEntitlement(Entitlement entitlement)
        {
            if (entitlement == null || entitlement.Product == null || entitlement.Product.definition == null) return;
            string status = entitlement.Status.ToString();
            bool owned = status == "FullyEntitled" || status == "EntitledButNotFinished" || status == "EntitledUntilConsumed";
            if (status != "Unknown") callbacks.OnOwnershipChecked(entitlement.Product.definition.id, owned, null);
            if (restoreWaitingForEntitlement)
            {
                // A restore is finished once the store has answered for No Ads (so the player hears one result).
                restoreWaitingForEntitlement = false;
                callbacks.OnRestoreFinished(status != "Unknown", status == "Unknown" ? "The store couldn't confirm your purchases." : "");
            }
        }

        public void Purchase(string productId)
        {
            if (!ready || store == null)
            {
                callbacks.OnPurchaseFailed(productId, PurchaseFailure.StoreUnavailable, "store not ready");
                return;
            }
            store.PurchaseProduct(productId);
        }

        public void Confirm(PurchaseReceipt receipt)
        {
            var order = receipt != null ? receipt.Handle as PendingOrder : null;
            if (order != null && store != null) store.ConfirmPurchase(order);
        }

        public void Restore()
        {
            if (store == null)
            {
                callbacks.OnRestoreFinished(false, "store not ready");
                return;
            }
#if UNITY_IOS
            store.RestoreTransactions((success, error) =>
            {
                if (success) store.FetchPurchases();
                CheckNoAds();
                callbacks.OnRestoreFinished(success, error);
            });
#else
            // Google Play keeps the player's purchases with their account: fetching them again restores them. The
            // restore is reported finished when the No Ads entitlement answer arrives (PurchaseService times it out).
            store.FetchPurchases();
            restoreWaitingForEntitlement = true;
            CheckNoAds();
#endif
        }

        void CheckNoAds()
        {
            foreach (var kv in kinds)
                if (kv.Value == ProductKind.NonConsumable) CheckOwnership(kv.Key);
        }

        public void CheckOwnership(string productId)
        {
            Product p;
            if (store != null && productId != null && products.TryGetValue(productId, out p)) store.CheckEntitlement(p);
        }

        static string FirstProductId(Order order)
        {
            if (order == null || order.CartOrdered == null) return null;
            foreach (var item in order.CartOrdered.Items())
                if (item != null && item.Product != null && item.Product.definition != null) return item.Product.definition.id;
            return null;
        }

        static string Describe(object failure) => failure == null ? "unknown" : failure.ToString();
    }
}
