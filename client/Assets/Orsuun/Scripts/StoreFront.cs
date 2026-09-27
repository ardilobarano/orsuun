using System;
using System.Collections;
using Orsuun.Rules;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Services.Core;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace Orsuun.Client
{
    /// <summary>
    /// Amber in the App Store and Google Play (owner, 27 Sep 2026: "Amber store purchases"). Unity IAP sells the Caravan's
    /// packs (Rules.Amber.Packs, store ids "orsuun.amber.60"...); each purchase goes to the server, which checks it with the
    /// store and credits the login once per store transaction (/v1/caravan/purchase). The purchase is finished with the
    /// store only after the server has answered for good, so a purchase the server could not take yet (no answer, the store
    /// not set up there) is handed over again on the next launch. Each purchase names the login it is for (Apple's
    /// applicationUsername, Google's obfuscated account id), so another account cannot claim it. Phones only: the Mac and
    /// the editor keep the playtest's free packs.
    /// </summary>
    public sealed class StoreFront : MonoBehaviour
#if UNITY_ANDROID || UNITY_IOS
        , IDetailedStoreListener
#endif
    {
        private GameRoot _root;
        private Action<string, string> _pending;

        /// <summary>The stores are open for this phone (initialised, with products).</summary>
        public bool Ready { get; private set; }

        public void Init(GameRoot root)
        {
            _root = root;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            StartCoroutine(Open());
#endif
        }

        /// <summary>The pack's price in the player's store currency, or null when the store has not said.</summary>
        public string Price(AmberPack pack)
        {
#if UNITY_ANDROID || UNITY_IOS
            Product product = _controller?.products.WithID(pack.StoreId);
            return product != null && product.availableToPurchase ? product.metadata.localizedPriceString : null;
#else
            return null;
#endif
        }

        /// <summary>Buys a pack: completes with (message, error) once the store and the server have answered.</summary>
        public void Buy(AmberPack pack, Action<string, string> done)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Ready || _controller == null) { done(null, "The store is not open yet. Try again in a moment."); return; }
            if (_pending != null) { done(null, "A purchase is already under way."); return; }
            TagLogin();
            _pending = done;
            _controller.InitiatePurchase(pack.StoreId);
#else
            done(null, "Store purchases are on phones.");
#endif
        }

#if UNITY_ANDROID || UNITY_IOS
        private IStoreController _controller;
        private IExtensionProvider _extensions;
        private IGooglePlayConfiguration _google;

        private IEnumerator Open()
        {
            // The login must be known first: purchases are tagged with it.
            while (string.IsNullOrEmpty(_root.Server.LoginId)) yield return new WaitForSecondsRealtime(1f);
            var services = UnityServices.InitializeAsync();
            while (!services.IsCompleted) yield return null;
            if (services.IsFaulted) Debug.LogWarning("Unity services did not start: " + services.Exception?.GetBaseException().Message);
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (AmberPack pack in Amber.Packs) builder.AddProduct(pack.StoreId, ProductType.Consumable);
            _google = builder.Configure<IGooglePlayConfiguration>();
            TagLogin();
            UnityPurchasing.Initialize(this, builder);
        }

        /// <summary>Names the login on the next purchase (a sign-in may have changed it since the store opened).</summary>
        private void TagLogin()
        {
            string login = _root.Server.LoginId;
            if (string.IsNullOrEmpty(login)) return;
            _google?.SetObfuscatedAccountId(login);
            _extensions?.GetExtension<IAppleExtensions>()?.SetApplicationUsername(login);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            TagLogin();
            Ready = true;
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Ready = false;
            Debug.LogWarning("Store did not open: " + error + " " + message);
        }

        /// <summary>A purchase to credit (a new one, or one left from an earlier run): the server first, then the store.</summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            StartCoroutine(Credit(e.purchasedProduct));
            return PurchaseProcessingResult.Pending;
        }

        private IEnumerator Credit(Product product)
        {
            string store = Application.platform == RuntimePlatform.IPhonePlayer ? "apple" : "google";
            string receipt = store == "apple" ? product.transactionID : GoogleToken(product) ?? product.transactionID;
            string message = null, error = null;
            bool final = false;
            // A server that does not answer is asked again for a while; after that the store keeps it for the next run.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                yield return _root.Server.AmberPurchase(store, product.definition.id, receipt, (m, err, f) => { message = m; error = err; final = f; });
                if (error == null || final) break;
                yield return new WaitForSecondsRealtime(3f + attempt * 3f);
            }
            if (error == null || final) _controller.ConfirmPendingPurchase(product);
            Action<string, string> done = _pending;
            _pending = null;
            if (done != null) done(message, error);
            else _root.Hud.Log(error ?? message);
        }

        /// <summary>Google's purchase token, from the receipt Unity IAP hands over (JSON: Payload, then json).</summary>
        private static string GoogleToken(Product product)
        {
            try
            {
                var outer = JsonUtility.FromJson<Receipt>(product.receipt);
                var payload = JsonUtility.FromJson<GooglePayload>(outer.Payload);
                return JsonUtility.FromJson<GooglePurchase>(payload.json).purchaseToken;
            }
            catch (Exception) { return null; }
        }

        [Serializable] private class Receipt { public string Store; public string TransactionID; public string Payload; }
        [Serializable] private class GooglePayload { public string json; public string signature; }
        [Serializable] private class GooglePurchase { public string purchaseToken; public string orderId; }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failure) => Failed(failure.reason);

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) => Failed(reason);

        private void Failed(PurchaseFailureReason reason)
        {
            Action<string, string> done = _pending;
            _pending = null;
            done?.Invoke(null, reason == PurchaseFailureReason.UserCancelled ? "Purchase cancelled." : "The purchase did not go through (" + reason + ").");
        }
#endif
    }
}
