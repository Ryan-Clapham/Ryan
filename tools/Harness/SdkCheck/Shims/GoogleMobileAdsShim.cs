// API SHIM, not the real SDK: the parts of the Google Mobile Ads Unity plugin (v9-v11) and its User Messaging
// Platform that Scripts/Integrations/AdMob uses, as documented by Google. Only for compile-checking in this harness.
using System;
using System.Collections.Generic;

namespace GoogleMobileAds.Api
{
    public class InitializationStatus { }

    public static class MobileAds
    {
        public static bool RaiseAdEventsOnUnityMainThread { get; set; }
        public static void SetRequestConfiguration(RequestConfiguration config) { }
        public static void Initialize(Action<InitializationStatus> initCompleteAction) { }
    }

    public enum TagForChildDirectedTreatment { Unspecified = -1, False = 0, True = 1 }
    public enum TagForUnderAgeOfConsent { Unspecified = -1, False = 0, True = 1 }

    public class MaxAdContentRating
    {
        public static MaxAdContentRating G { get; } = new MaxAdContentRating();
        public static MaxAdContentRating PG { get; } = new MaxAdContentRating();
        public static MaxAdContentRating T { get; } = new MaxAdContentRating();
        public static MaxAdContentRating MA { get; } = new MaxAdContentRating();
    }

    public class RequestConfiguration
    {
        public MaxAdContentRating MaxAdContentRating;
        public TagForChildDirectedTreatment? TagForChildDirectedTreatment;
        public TagForUnderAgeOfConsent? TagForUnderAgeOfConsent;
        public List<string> TestDeviceIds = new List<string>();
    }

    public class AdRequest { }

    public class AdError
    {
        public int GetCode() => 0;
        public string GetMessage() => "";
    }

    public class LoadAdError : AdError { }

    public class Reward
    {
        public string Type;
        public double Amount;
    }

    public class RewardedAd
    {
        public static void Load(string adUnitId, AdRequest request, Action<RewardedAd, LoadAdError> adLoadCallback) { }
        public bool CanShowAd() => false;
        public void Show(Action<Reward> userRewardEarnedCallback) { }
        public void Destroy() { }
        public event Action OnAdFullScreenContentOpened;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        internal void Touch() { OnAdFullScreenContentOpened?.Invoke(); OnAdFullScreenContentClosed?.Invoke(); OnAdFullScreenContentFailed?.Invoke(null); }
    }

    public class InterstitialAd
    {
        public static void Load(string adUnitId, AdRequest request, Action<InterstitialAd, LoadAdError> adLoadCallback) { }
        public bool CanShowAd() => false;
        public void Show() { }
        public void Destroy() { }
        public event Action OnAdFullScreenContentOpened;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        internal void Touch() { OnAdFullScreenContentOpened?.Invoke(); OnAdFullScreenContentClosed?.Invoke(); OnAdFullScreenContentFailed?.Invoke(null); }
    }
}

namespace GoogleMobileAds.Ump.Api
{
    public enum DebugGeography { Disabled = 0, EEA = 1, NotEEA = 2 }
    public enum PrivacyOptionsRequirementStatus { Unknown = 0, NotRequired = 1, Required = 2 }

    public class FormError
    {
        public int ErrorCode;
        public string Message;
    }

    public class ConsentDebugSettings
    {
        public DebugGeography DebugGeography;
        public List<string> TestDeviceHashedIds = new List<string>();
    }

    public class ConsentRequestParameters
    {
        public bool TagForUnderAgeOfConsent;
        public ConsentDebugSettings ConsentDebugSettings;
    }

    public static class ConsentInformation
    {
        public static PrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus => PrivacyOptionsRequirementStatus.Unknown;
        public static bool CanRequestAds() => false;
        public static void Update(ConsentRequestParameters request, Action<FormError> consentInfoUpdateCallback) { }
        public static void Reset() { }
    }

    public static class ConsentForm
    {
        public static void LoadAndShowConsentFormIfRequired(Action<FormError> onDismissed) { }
        public static void ShowPrivacyOptionsForm(Action<FormError> onDismissed) { }
    }
}
