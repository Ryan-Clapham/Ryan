// API SHIM, not the real package: the parts of Unity In-App Purchasing 5 (StoreController API) that
// Scripts/Integrations/UnityIAP uses, as documented by Unity. Only for compile-checking in this harness.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine.Purchasing
{
    public enum ProductType { Consumable, NonConsumable, Subscription }
    public enum PurchaseFailureReason { PurchasingUnavailable, ExistingPurchasePending, ProductUnavailable, SignatureInvalid, UserCancelled, PaymentDeclined, DuplicateTransaction, Unknown }
    public enum EntitlementStatus { Unknown, FullyEntitled, EntitledUntilConsumed, EntitledButNotFinished, NotEntitled }

    public class ProductDefinition
    {
        public ProductDefinition(string id, ProductType type) { this.id = id; this.type = type; }
        public string id;
        public ProductType type;
    }

    public class ProductMetadata
    {
        public string localizedPriceString;
        public string localizedTitle;
    }

    public class Product
    {
        public ProductDefinition definition;
        public ProductMetadata metadata;
        public bool availableToPurchase;
    }

    public class CartItem { public Product Product; }

    public class Cart
    {
        public IReadOnlyList<CartItem> Items() => new List<CartItem>();
    }

    public class OrderInfo
    {
        public string TransactionID;
        public string Receipt;
    }

    public abstract class Order
    {
        public Cart CartOrdered;
        public OrderInfo Info;
    }

    public class PendingOrder : Order { }
    public class ConfirmedOrder : Order { }
    public class DeferredOrder : Order { }

    public class FailedOrder : Order
    {
        public PurchaseFailureReason FailureReason;
        public string Details;
    }

    public class Orders
    {
        public IReadOnlyList<ConfirmedOrder> ConfirmedOrders = new List<ConfirmedOrder>();
        public IReadOnlyList<PendingOrder> PendingOrders = new List<PendingOrder>();
        public IReadOnlyList<DeferredOrder> DeferredOrders = new List<DeferredOrder>();
    }

    public class Entitlement
    {
        public Product Product;
        public EntitlementStatus Status;
    }

    public class StoreConnectionFailureDescription { }
    public class ProductFetchFailed { }
    public class PurchasesFetchFailureDescription { }

    public class StoreController
    {
        public event Action<StoreConnectionFailureDescription> OnStoreDisconnected;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<ProductFetchFailed> OnProductsFetchFailed;
        public event Action<PendingOrder> OnPurchasePending;
        public event Action<Order> OnPurchaseConfirmed;
        public event Action<FailedOrder> OnPurchaseFailed;
        public event Action<DeferredOrder> OnPurchaseDeferred;
        public event Action<Orders> OnPurchasesFetched;
        public event Action<PurchasesFetchFailureDescription> OnPurchasesFetchFailed;
        public event Action<Entitlement> OnCheckEntitlement;

        public Task Connect() => Task.CompletedTask;
        public void FetchProducts(List<ProductDefinition> products) { }
        public void FetchPurchases() { }
        public void PurchaseProduct(string productId) { }
        public void ConfirmPurchase(PendingOrder order) { }
        public void RestoreTransactions(Action<bool, string> callback) { }
        public void CheckEntitlement(Product product) { }

        internal void Touch()
        {
            OnStoreDisconnected?.Invoke(null); OnProductsFetched?.Invoke(null); OnProductsFetchFailed?.Invoke(null); OnPurchasePending?.Invoke(null);
            OnPurchaseConfirmed?.Invoke(null); OnPurchaseFailed?.Invoke(null); OnPurchaseDeferred?.Invoke(null); OnPurchasesFetched?.Invoke(null);
            OnPurchasesFetchFailed?.Invoke(null); OnCheckEntitlement?.Invoke(null);
        }
    }

    public static class UnityIAPServices
    {
        public static StoreController StoreController() => new StoreController();
    }
}
