// Google Mobile Ads (AdMob) and Google's User Messaging Platform (consent) for Colour Cascade.
//
// Compiled only when the Google Mobile Ads Unity plugin is installed: the editor sets the CC_ADMOB scripting define
// when it finds the plugin (Scripts/Setup/MonetisationSdkSetup.cs) and the assembly's Define Constraints require it.
// Menu: Colour Cascade > Monetisation > Turn AdMob integration off, if this file ever fails to compile against a
// newer plugin (the game then runs without adverts until it is fixed).
//
// Written against the plugin's documented API (v9 to v11: RewardedAd/InterstitialAd.Load with AdRequest, CanShowAd,
// the OnAdFullScreenContent* events, MobileAds.RaiseAdEventsOnUnityMainThread, RequestConfiguration, and
// GoogleMobileAds.Ump.Api). It has NOT yet been compiled against the real plugin: see docs/MONETISATION.md §11.

using System;
using System.Collections.Generic;
using System.Reflection;
using ColourCascade.App;
using ColourCascade.Core;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace ColourCascade.Integrations
{
    static class AdMobRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            MonetisationBackends.AdNetwork = () => new AdMobNetwork();
            MonetisationBackends.Consent = () => new UmpConsentProvider();
        }
    }

    /// <summary>
    /// IAdNetwork on Google Mobile Ads. Keeps at most one loaded advert per format; every SDK callback is passed to the
    /// game on the main thread, in the order the SDK sent it. Rewards are reported only from the SDK's reward callback.
    /// </summary>
    sealed class AdMobNetwork : IAdNetwork
    {
        RewardedAd rewarded;
        InterstitialAd interstitial;

        public string Name => "Google Mobile Ads";

        public void Initialize(AdNetworkSettings settings, Action<bool, string> done)
        {
            try
            {
                // Callbacks on Unity's main thread (they arrive in order; MainThread.Post keeps that order).
                MobileAds.RaiseAdEventsOnUnityMainThread = true;
                var config = new RequestConfiguration();
                config.TestDeviceIds = new List<string>(settings.TestDeviceIds ?? new string[0]);
                config.MaxAdContentRating = Rating(settings.MaxAdContentRating);
                // Audience flags are only set when the config asks for them. They are set by name because v11.3 of the
                // plugin deprecated them in favour of a new age setting; a missing property is logged, not a build error.
                if (settings.ChildDirected) SetEnum(config, "TagForChildDirectedTreatment", "True");
                if (settings.UnderAgeOfConsent) SetEnum(config, "TagForUnderAgeOfConsent", "True");
                MobileAds.SetRequestConfiguration(config);
                MobileAds.Initialize(status => MainThread.Post(() => done(true, null)));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                done(false, e.Message);
            }
        }

        static MaxAdContentRating Rating(string rating)
        {
            switch ((rating ?? "").Trim().ToUpperInvariant())
            {
                case "G": return MaxAdContentRating.G;
                case "T": return MaxAdContentRating.T;
                case "MA": return MaxAdContentRating.MA;
                default: return MaxAdContentRating.PG;
            }
        }

        static void SetEnum(object target, string property, string value)
        {
            var p = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite)
            {
                Debug.LogWarning("[ColourCascade] RequestConfiguration." + property + " isn't available in this Google Mobile Ads version; set the audience in docs/MONETISATION.md §7.");
                return;
            }
            var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!type.IsEnum) return;
            p.SetValue(target, Enum.Parse(type, value));
        }

        public void Load(AdFormat format, string adUnitId, Action<bool, string> done)
        {
            var request = new AdRequest();
            if (format == AdFormat.Rewarded)
            {
                RewardedAd.Load(adUnitId, request, (RewardedAd ad, LoadAdError error) => MainThread.Post(() =>
                {
                    if (error != null || ad == null)
                    {
                        done(false, error != null ? error.GetMessage() : "no advert");
                        return;
                    }
                    if (rewarded != null) rewarded.Destroy();
                    rewarded = ad;
                    done(true, null);
                }));
            }
            else
            {
                InterstitialAd.Load(adUnitId, request, (InterstitialAd ad, LoadAdError error) => MainThread.Post(() =>
                {
                    if (error != null || ad == null)
                    {
                        done(false, error != null ? error.GetMessage() : "no advert");
                        return;
                    }
                    if (interstitial != null) interstitial.Destroy();
                    interstitial = ad;
                    done(true, null);
                }));
            }
        }

        public bool IsReady(AdFormat format)
        {
            if (format == AdFormat.Rewarded) return rewarded != null && rewarded.CanShowAd();
            return interstitial != null && interstitial.CanShowAd();
        }

        public void Show(AdFormat format, AdShowCallbacks cb)
        {
            if (format == AdFormat.Rewarded)
            {
                var ad = rewarded;
                rewarded = null;
                if (ad == null || !ad.CanShowAd())
                {
                    cb.Failed?.Invoke("No rewarded advert is loaded.");
                    return;
                }
                ad.OnAdFullScreenContentOpened += () => MainThread.Post(() => cb.Opened?.Invoke());
                ad.OnAdFullScreenContentClosed += () => MainThread.Post(() =>
                {
                    cb.Closed?.Invoke();
                    ad.Destroy();
                });
                ad.OnAdFullScreenContentFailed += (AdError error) => MainThread.Post(() =>
                {
                    cb.Failed?.Invoke(error != null ? error.GetMessage() : "failed to show");
                    ad.Destroy();
                });
                // The reward is granted from this callback only: the SDK calls it when the player has earned it.
                ad.Show((Reward reward) => MainThread.Post(() => cb.Earned?.Invoke()));
            }
            else
            {
                var ad = interstitial;
                interstitial = null;
                if (ad == null || !ad.CanShowAd())
                {
                    cb.Failed?.Invoke("No interstitial advert is loaded.");
                    return;
                }
                ad.OnAdFullScreenContentOpened += () => MainThread.Post(() => cb.Opened?.Invoke());
                ad.OnAdFullScreenContentClosed += () => MainThread.Post(() =>
                {
                    cb.Closed?.Invoke();
                    ad.Destroy();
                });
                ad.OnAdFullScreenContentFailed += (AdError error) => MainThread.Post(() =>
                {
                    cb.Failed?.Invoke(error != null ? error.GetMessage() : "failed to show");
                    ad.Destroy();
                });
                ad.Show();
            }
        }
    }

    /// <summary>
    /// IConsentProvider on Google's User Messaging Platform (included in the Google Mobile Ads plugin). The consent
    /// message itself (GDPR for the EEA, UK and Switzerland; optionally US states) is created in the AdMob console
    /// under Privacy &amp; messaging; this only asks for it and shows it when the player has to answer.
    /// </summary>
    sealed class UmpConsentProvider : IConsentProvider
    {
        public bool CanRequestAds
        {
            get
            {
                try
                {
                    return ConsentInformation.CanRequestAds();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public bool PrivacyOptionsRequired
        {
            get
            {
                try
                {
                    return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public void Gather(ConsentRequest request, Action<ConsentOutcome> done)
        {
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = request.UnderAgeOfConsent };
            var ids = request.TestDeviceIds ?? new string[0];
            if (request.DebugGeographyEea || ids.Length > 0)
            {
                parameters.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = request.DebugGeographyEea ? DebugGeography.EEA : DebugGeography.Disabled,
                    TestDeviceHashedIds = new List<string>(ids),
                };
            }
            ConsentInformation.Update(parameters, (FormError updateError) => MainThread.Post(() =>
            {
                if (updateError != null)
                {
                    // An earlier session's answer may still allow ads (CanRequestAds then stays true).
                    done(new ConsentOutcome { CanRequestAds = CanRequestAds, Error = updateError.Message });
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) => MainThread.Post(() =>
                    done(new ConsentOutcome { CanRequestAds = CanRequestAds, Error = formError != null ? formError.Message : null })));
            }));
        }

        public void ShowPrivacyOptions(Action<string> done)
        {
            ConsentForm.ShowPrivacyOptionsForm((FormError error) => MainThread.Post(() => done?.Invoke(error != null ? error.Message : null)));
        }
    }
}
