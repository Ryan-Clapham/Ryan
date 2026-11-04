# Colour Cascade: Monetisation (update 3.0)

Rewarded adverts, interstitial adverts, in-app purchases (coin packs and a permanent **No Ads**), the Premium
shop tab, consent and event tracking. This document covers how it works, every setting, everything that has to be
done **outside Unity** (AdMob, Google Play Console, policies), and exactly what has and hasn't been tested.

## Status at a glance

| Part | State |
|---|---|
| Game rules (reward tickets, limits, interstitial timing, purchase processing, No Ads, consent order) | Implemented in `Scripts/Core/Monetisation`; 68 automated tests with scripted SDK fakes |
| Screens (Out of Moves offers, shop Coins and Premium tabs, shortfall offers, Settings) | Implemented; compile-checked against Unity's assemblies; **not yet seen running** |
| Google Mobile Ads + UMP integration | Written (`Scripts/Integrations/AdMob`); compile-checked against API shims from Google's documentation, **not yet against the real plugin** |
| Unity IAP 5 integration (Google Play Billing) | Written (`Scripts/Integrations/UnityIAP`); compile-checked against API shims from Unity's documentation, **not yet against the real package** |
| Google test adverts on a device | **Not tested** |
| Google Play Billing with license testers | **Not tested** |
| Live adverts / real payments / revenue | **Not tested**, and needs the accounts in §8 first |

Why the SDK parts weren't compiled against the real SDKs: the build environment couldn't download the Google
Mobile Ads plugin or Unity IAP (its network policy blocks the package registries). The first real compile happens
when the project is opened in Unity with the SDKs installed (§6). If anything there fails to compile, the menu
**Colour Cascade > Monetisation > Turn … integration off** switches that integration off instantly, so the rest of
the game keeps working.

## 1. Architecture

```
Screens (ResultPopup, ShopScreen, QuickBuyPopup, SettingsScreen)
        │  ask, never talk to SDKs
        ▼
GameContext.Monetisation  (MonetisationServices, Scripts/Core/Monetisation, no Unity code)
   ├─ AdService           central advertising manager: consent gate, loading, reward tickets, watchdogs, limits
   ├─ InterstitialPolicy   when an interstitial may appear
   ├─ ContinueService      Out of Moves: advert or coins for extra moves
   ├─ CoinOffers           Coins tab advert, "not enough coins" advert
   ├─ PurchaseService      coin packs, No Ads entitlement, restore
   └─ MonetisationAnalytics event log
        │  through three small interfaces (SdkInterfaces.cs)
        ▼
IAdNetwork / IConsentProvider / IStoreBackend
   ├─ AdMobNetwork + UmpConsentProvider   (Scripts/Integrations/AdMob, compiled only with CC_ADMOB)
   ├─ UnityIapStore                        (Scripts/Integrations/UnityIAP, compiled only with CC_UNITY_IAP)
   └─ test fakes                           (Tests/EditMode/MonetisationFakes.cs)
```

* **One place for every rule.** Screens ask `AdService`/`PurchaseService` whether something is available and what
  happened; nothing outside `Scripts/Integrations` touches an SDK. New rewarded opportunities are added with
  `AdService.RegisterPlacement` and a `RewardedPlacementConfig`, without changing the service (tested).
* **Start-up** (`MonetisationHost.Begin`, after the home screen appears): consent (UMP) → ad SDK initialisation
  only once ads may be requested → one rewarded and (without No Ads) one interstitial advert kept loaded; the
  store connects in parallel and checks No Ads ownership.
* **Main thread.** The integrations pass every SDK callback through `MainThread.Post`, drained each frame.
* **Saved state** (`SaveData.Monetisation`): interstitial level count and times, today's advert counts per
  placement, last advert times (cooldowns), the last 300 processed purchase IDs, the No Ads entitlement cache and
  event counts. Old saves load with an empty record; a progress reset keeps it (purchases are not progress).

## 2. Rewarded adverts

**Placements** (all in the config asset, *Monetisation > Ads*):

| Placement | Reward | Default limits |
|---|---|---|
| `continue_moves` (Out of Moves) | +10 moves | 2 per level attempt, no daily limit |
| `shop_coins` (Shop, Coins tab) | +50 coins | 5 a day, 30 s apart |
| `coin_shortfall` (not enough coins for a power-up) | the missing coins, at most 50 | 5 a day |

**How a reward is granted** (`AdService`):

1. A screen asks `CheckRewarded`: Ready, Loading, LimitReached, CoolingDown, NoConsent, Unavailable, Busy or
   Disabled, each with a player-facing message. Nothing is shown unless an advert is loaded.
2. `ShowRewarded` creates a **ticket** with the reward's grant function. The SDK's callbacks (opened, earned,
   closed, failed) are tied to that ticket.
3. The grant runs **only** when the SDK's reward callback arrives, **once per ticket**. Repeated reward callbacks
   are ignored and logged. Requesting, opening or closing an advert grants nothing.
4. Closed without the reward: the ticket waits `rewardGraceSeconds` (2 s) for a late reward callback (some networks
   send it just after the close), then ends as **closed early** with nothing granted. A reward that the SDK
   confirms even later is still granted once (the player did earn it), and the screen is told.
5. Failed to show: nothing granted, the player keeps everything, a fresh advert is requested.
6. Watchdogs so the game can never be stuck: no answer at all within 15 s → failed; back in the game for 8 s after
   the advert with no close callback → closed; open for 180 s without the app ever leaving (iOS, editor) → closed.
7. Limits count only adverts that were **completed** (reward confirmed) and are saved, so closing the app doesn't
   reset them; days follow the game's daily reset (UTC) and never move backwards with the device clock.
8. While an advert is on screen the game's audio is paused, input is blocked, and the level's auto-pause menu is
   suppressed; failed loads retry after 10, 20, 40… seconds, up to 5 minutes.

### 2.1 Out of Moves

A campaign level that runs out of moves offers (`ResultPopup.CampaignFailed`):

* **Watch an advert: +10 moves**. The moves are added only after the SDK confirms the reward.
* **+5 moves for 100 coins**. The coins are taken only together with the moves (`ContinueService`, a single
  step; refunded if the moves couldn't be added). The tap on this clearly labelled button is the confirmation.
* **Give Up**. Shows the usual Try Again / Map / Home choices (the attempt was already recorded as not passed).

Both continues add moves to the **same session**: board, score, goals and their progress, obstacles and their
state, and the power-up inventory are untouched (tested tile by tile). Special tiles can't be lost here: since 2.1
any special left on the board goes off before the level counts as out of moves. A failed or interrupted advert
leaves the screen exactly as it was, so the player can try again, use coins or give up.

Only the campaign offers continues: the Daily Challenge and Weekly Rush stay fair (no extra moves for anyone) and
Endless runs end when the moves do, as before.

### 2.2 Coins tab

"Watch an advert: +50 coins", with the adverts left today. Coins are added when the advert finishes and the
balance on the page and in the header updates at once; the coins are saved immediately.

### 2.3 Not enough coins

Tapping an unaffordable power-up (shop or in-level bar) opens the buy screen with "Watch an advert: +N coins",
where N = the shortfall, capped at 50 (`coinShortfall.reward`). Example: Colour Shift costs 150, the player has 100
→ "You need 50 more coins. Watch an advert to earn the 50 coins you're missing." If the shortfall is larger:
"An advert gives up to 50 coins, so you'd still be 70 short." Nothing is bought automatically: after the advert the
same screen shows the new balance and the player taps **Buy** themselves; coins are only deducted then.

### 2.4 Adding opportunities later

`AdService.RegisterPlacement("double_daily", new RewardedPlacementConfig { … })` plus a grant function is all a
new opportunity needs (doubling daily rewards, a level-complete bonus, events). Limits, ticketing, analytics and
failure handling come with it.

## 3. Interstitial adverts

`InterstitialPolicy.Evaluate` decides; `AdService.AtTransition` shows one or just continues.

* **Counted:** campaign levels completed successfully (any win, replays included), counted once per win by
  `GameContext.FinishRun`. Failed levels don't count. Daily, Weekly and Endless neither count nor show.
* **Shown only** when the player leaves a **won** level's results screen: **Next Level**, **Map** or Android Back.
  Never after a failure, a retry (Replay or Try Again), abandoning a level, or during play.
* **All of these must hold:** count ≥ 5 (`everyCompletedLevels`), ≥ 300 s since the last interstitial
  (`minSecondsBetween`), ≥ 120 s since a rewarded advert or any purchase, including coins spent
  (`quietSecondsAfterRewardOrPurchase`), no No Ads, and an interstitial actually loaded.
* **Otherwise** the player continues at once and the opportunity is kept for the next natural break.
* **The count resets only when the SDK reports the interstitial on screen.** An interstitial that fails or
  isn't loaded keeps the count.
* Times are saved; a device clock moved back restarts the wait rather than blocking or skipping it.
* With No Ads, interstitials are never shown and never even loaded. Rewarded adverts stay available.

## 4. In-app purchases

**Products** (*Monetisation > Store*, product IDs configurable, prices only from the store):

| Product ID (default) | Type | Gives |
|---|---|---|
| `coins_small` | consumable | 500 coins |
| `coins_medium` | consumable | 1,200 coins |
| `coins_large` | consumable | 2,750 coins ("Best value") |
| `coins_premium` | consumable | 6,000 coins |
| `no_ads` | non-consumable | No Ads, permanently |

**Processing** (`PurchaseService`, with `UnityIapStore` reporting the store's events):

* **Paid purchase** (Unity IAP `OnPurchasePending`): grant (coins via the wallet, ledger reason `iap.<product>`, or
  the No Ads entitlement) → record the transaction ID → **save** → only then `ConfirmPurchase` (Unity IAP consumes
  consumables and acknowledges non-consumables by product type). If the app dies before confirming, Google Play
  delivers the purchase again at the next start and the saved transaction ID stops a second grant. If the save
  fails, the purchase is left unconfirmed so it is delivered again rather than lost.
* **Pending payment** (e.g. cash at a shop): nothing is granted; the player is told; when it's paid, Google Play
  delivers it (often at a later start) and it is granted then, with a message.
* **Cancelled / failed:** nothing changes, nothing is charged, a message says so.
* **Duplicate delivery:** confirmed with the store again, never granted twice.
* **Unknown product:** not granted and not confirmed (Google refunds unconfirmed purchases).
* One purchase at a time; buying is blocked during Daily/Weekly runs, as coin purchases already were. If the store
  never answers, the shop unlocks after 120 s back in the game.

### 4.2 No Ads

* Non-consumable, bought once. `NoAdsOwned` is the store's answer: on every start the store checks ownership
  (`CheckEntitlement`), and the purchases Google Play reports at start-up are processed (this is what restores No
  Ads on a reinstall or a new device signed in to the same Google account).
* The save keeps the last confirmed answer and its transaction ID, so No Ads works offline and from the first frame.
  It isn't a free-standing flag: it is set by store answers only and re-checked on every start.
* **Refunds:** if the store reports No Ads as not owned on two separate starts (`noAdsRevokeAfterChecks`), it is
  withdrawn. One report alone isn't trusted, because a store can be briefly out of date.
* **Restore Purchases** (Premium tab and Settings): fetches the player's purchases again (Android) or runs the App
  Store restore (iOS), then re-checks No Ads.
* Not yet secured against tampering: purchases aren't verified with Google's servers (§12).

## 5. Store screens

* **Power-ups:** the same power-ups, functions and prices (Extra Moves was retired in 3.0.1); an unaffordable price opens the buy screen
  with the advert offer (§2.3).
* **Coins:** balance (updates live), *Free coins* (the advert), *Coin packs* (amount, name, store price, "Best
  value" on the large pack; "..." while connecting, "Unavailable" when the store can't sell it), then the existing
  *Ways to earn coins*. The old "coin packs aren't sold" note is gone.
* **Premium** (new tab, crown icon): the No Ads card ("Permanent purchase", price from the store, "Owned" once
  bought) and Restore Purchases. Further premium items go in as more cards.
* Same cards, buttons, colours and type as before.

## 6. Installing the SDKs in Unity

1. Open the project in Unity 6000.6.3f1.
2. **Colour Cascade > Monetisation > Install Google Mobile Ads (AdMob)**. This adds Google's OpenUPM registry
   (scoped to `com.google.ads.mobile` and `com.google.external-dependency-manager`) to `Packages/manifest.json`
   and installs the latest plugin. The plugin includes the User Messaging Platform used for consent.
   (Alternatively follow Google's "Unity quick start" page.)
3. **Colour Cascade > Monetisation > Install Unity IAP**. This installs the latest `com.unity.purchasing`.
   Requirement: **Unity IAP 5.2.1 or later** (Google Play Billing Library 8.3), because Google Play rejects new apps
   and updates using Billing Library 7 or older since 31 August 2026. 5.4 or later uses Billing Library 9.
4. **Assets > Google Mobile Ads > Settings > Android App ID.** During development use Google's test App ID
   `ca-app-pub-3940256099942544~3347511713`; replace it with your own App ID (§8) for release. The app crashes at
   launch without an App ID.
5. **Android build settings:** Player Settings > Publishing Settings: tick *Custom Main Gradle Template* and *Custom
   Gradle Properties Template*, then **Assets > External Dependency Manager > Android Resolver > Resolve**. Other
   Settings: Target API Level **36** (required by Google Play since 31 August 2026); Minimum API Level as the
   plugin's release notes require (at least 23).
6. Check the Console:
   * "Monetisation integrations: AdMob on, Unity IAP on". The editor sets the `CC_ADMOB` / `CC_UNITY_IAP` defines
     itself when it finds the SDKs.
   * If `Scripts/Integrations/...` reports compile errors, use **Colour Cascade > Monetisation > Turn AdMob (or
     Unity IAP) integration off** and send me the errors. These files have not met the real SDKs yet.
7. **Play mode:** Google's plugin shows placeholder test adverts in the editor, and Unity IAP uses its "fake store"
   dialogs. Neither is the real thing; real testing is on a device (§11).

## 7. Privacy, consent and compliance

**What the game does:**

* Asks Google's **User Messaging Platform** for the consent status on **every start**, shows the consent message
  when the player must answer (EEA, UK, Switzerland; and US states if you enable that message), and **only then**
  initialises the ad SDK. If an earlier answer already allows ads, the SDK starts straight away (Google's
  recommended order).
* If consent doesn't allow ad requests, or can't be checked and there's no earlier answer, **no ad SDK starts and no
  ad is requested**. Advert offers say why ("Adverts aren't available with your privacy choices…"). The coin
  options, purchases and the whole game work normally.
* **Personalised vs non-personalised:** the Google Mobile Ads SDK reads the player's consent (IAB TCF v2.2 string
  written by UMP) and serves personalised, non-personalised or limited ads accordingly. No separate flag is needed.
* **Settings > Privacy options** appears when UMP says the player must be able to change their choice; a changed
  choice that now allows ads starts the SDK.
* **Settings > Privacy policy** opens `privacy.privacyPolicyUrl` (hidden while empty).
* Ad content rating capped at **PG** (`privacy.maxAdContentRating`).
* Child-directed / under-age-of-consent tags are off by default and only set if `privacy.childDirected` /
  `underAgeOfConsent` are ticked. Read the audience notes below first.
* The game itself collects no personal data: its event log stays on the device (§9).

**Your actions (outside Unity):**

1. **AdMob > Privacy & messaging:** create a **European regulations (GDPR)** message for the app (it covers the EEA,
   UK and Switzerland), add your privacy policy URL, choose the consent options and **publish** it. Without a
   published message UMP has nothing to show. Optional: a **US state regulations** message.
2. **Privacy policy** (required by Google Play for apps with ads or purchases): a public web page covering the Google
   Mobile Ads SDK (advertising ID, IP-based approximate location, device and usage data used for ads, measurement and
   fraud prevention), Google Play purchases, the consent choice and how to change it (Settings > Privacy options), and
   anything Unity's engine collects. Put the URL in Play Console, AdMob and `privacy.privacyPolicyUrl`.
3. **Play Console > App content:**
   * **Ads:** "Yes, my app contains ads".
   * **Data safety:** declare what the Google Mobile Ads SDK collects and shares (see Google's "Data disclosure" page
     for the Mobile Ads SDK) and purchase history for in-app purchases.
   * **Content rating** questionnaire: answer for ads and digital purchases.
4. **Target audience** (Play Console): this decides a lot.
   * If the target ages include **under 13**, Google Play's **Families policy** applies: ads must come from
     Families-self-certified ad networks, child-directed treatment must be on (`privacy.childDirected`), and the app
     needs Families review.
   * Choosing **13+** avoids that, but note that the **UK Children's Code** (ICO Age Appropriate Design Code) applies
     to services *likely to be accessed* by children, which a colourful puzzle game may be.
   * Get advice on this. It isn't something code can settle.
5. **app-ads.txt:** AdMob > Apps > app-ads.txt gives a line to publish at the root of the website listed in your Play
   Console developer profile.
6. **Advertising ID:** the plugin adds the `AD_ID` permission; declare advertising ID use in Play Console (App
   content > Advertising ID).
7. iOS (not targeted yet) would also need App Tracking Transparency (an `NSUserTrackingUsageDescription` and UMP's
   IDFA message), which isn't implemented.

## 8. External setup: step by step

### Google AdMob

1. Create an AdMob account at admob.google.com (the same Google account as Play Console is easiest), and complete
   payments details (needed to be paid, not to show test ads).
2. **Apps > Add app** → Android → if the game isn't on Google Play yet, choose "not published" (link the store listing
   later, after launch). Name: Colour Cascade.
3. Copy the **App ID** (`ca-app-pub-XXXXXXXXXXXXXXXX~NNNNNNNNNN`) → Unity: Assets > Google Mobile Ads > Settings >
   Android App ID.
4. **Ad units > Add ad unit > Rewarded:** name "Rewarded"; the reward amount/item you enter there is ignored by the game
   (it uses the config: 10 moves, 50 coins). Copy the **ad unit ID** (`ca-app-pub-…/…`).
5. **Ad units > Add ad unit > Interstitial:** name "Between levels". Copy the ad unit ID.
6. Unity: config asset (Assets/ColourCascade/Resources/ColourCascadeConfig) > Monetisation > Ads > **Android Live >
   Rewarded / Interstitial**: paste the two IDs.
7. **Privacy & messaging:** create and publish the GDPR message (§7).
8. **Blocking controls** (optional): match the content rating to PG.
9. **Test devices:** run a development build, find the line in the device log that names your hashed device ID, and
   add it in AdMob (Settings > Test devices) and to `privacy.testDeviceIds`. Never tap live adverts on your own
   devices: AdMob can suspend accounts for invalid traffic.
10. **Release only:** untick `ads.forceTestAds` when the live IDs are in and AdMob shows the app as ready. Development
    builds and the editor always use test adverts; if live IDs are missing, test adverts are used and a warning is
    logged.

### Google Play Console

1. Create the app (see docs/RELEASE.md §3–4 for the account, listing and forms).
2. Upload a build that includes Unity IAP to a testing track (**Internal testing**) first. Play Console only lets you
   create in-app products once a build with the billing permission has been uploaded.
3. **Monetise > Products > One-time products > Create**, for each product, with exactly these **Product IDs** (or
   change the IDs in the config to match yours): `coins_small`, `coins_medium`, `coins_large`, `coins_premium`,
   `no_ads`.
   * Give each a name, a description and prices. Pick a default price and let Play convert it for each country.
   * **Activate** each product.
   * Product IDs can't be changed or reused once created.
4. **Consumable vs non-consumable:** Play Console has no such setting; both are one-time products. The game decides:
   * coin packs are **consumed** after they're granted, so they can be bought again;
   * No Ads is only **acknowledged**, so the player keeps owning it.

   This comes from the product types in the game's config (`StoreProductRequest.Kind`).
5. **Settings > License testing:** add the Google accounts of your testers. Their purchases use test cards and are
   never charged. These include *always approves*, *always declines*, and the slow cards that approve or decline after
   a few minutes, which is how you test a **pending** purchase.
6. Testers install the game from the internal testing link (opt in first), then buy, restore, reinstall and refund
   (Order management > refund, to test the No Ads withdrawal).
7. Before production, complete App content (§7), the content rating, the "Contains ads" and "In-app purchases" labels,
   and Data safety.

### Where every value goes

| Value | Comes from | Goes into |
|---|---|---|
| AdMob App ID (Android) | AdMob > Apps > App settings | Unity: Assets > Google Mobile Ads > Settings |
| Rewarded ad unit ID | AdMob > Ad units | Config > Monetisation > Ads > Android Live > Rewarded |
| Interstitial ad unit ID | AdMob > Ad units | Config > Monetisation > Ads > Android Live > Interstitial |
| Product IDs | Play Console > One-time products | Config > Monetisation > Store (coin packs, No Ads product ID) |
| Prices | Play Console | Nowhere: fetched from Google Play at runtime |
| Privacy policy URL | Your website | Play Console, AdMob message, Config > Monetisation > Privacy |
| Hashed test device IDs | Device log | AdMob test devices; Config > Monetisation > Privacy (development) |
| License testers | Google accounts | Play Console > Settings > License testing |
| app-ads.txt line | AdMob | Root of your developer website |

No account passwords, keys or other secrets are needed in the project or the repository. Ad unit IDs and product IDs
are public identifiers.

## 9. Event tracking

`MonetisationAnalytics` records these events with short, non-identifying parameters (placement, product ID, reason,
amounts, level number):

* **Consent and SDK start:** `consent_result`, `ads_initialised`.
* **Rewarded adverts:** `rewarded_offered`, `rewarded_loaded`, `rewarded_load_failed`, `rewarded_started`,
  `rewarded_completed`, `reward_granted`, `reward_not_applied`, `reward_duplicate_ignored`, `rewarded_closed_early`,
  `rewarded_failed`.
* **Interstitials:** `interstitial_loaded`, `interstitial_load_failed`, `interstitial_shown`, `interstitial_failed`,
  `interstitial_skipped` (with the reason).
* **What the player used:** `extra_moves_ad`, `extra_moves_coins`, `coins_ad`, `shortfall_offered`, `shortfall_ad`.
* **Store:** `store_initialised`, `store_unavailable`, `purchase_attempted`, `purchase_completed`,
  `purchase_cancelled`, `purchase_failed`, `purchase_pending`, `purchase_duplicate_ignored`, `coin_pack_completed`,
  `no_ads_completed`, `no_ads_revoked`, `restore_attempted`, `restore_completed`.

**Where they go:** counts per event in the save, the last 120 events in memory (Settings > developer tools > *Show
monetisation status*), and the Unity log in development builds. **Nothing leaves the device** and there's no user
ID, so no extra account or Data safety entry is needed for it.

To see player behaviour across devices, add an `IAnalyticsSink` that forwards events to an analytics service:
* **Firebase Analytics** is the usual choice next to AdMob. It needs a Firebase project and a privacy policy and
  Data safety update, and should respect the consent choice.
* AdMob and Play Console already report impressions, revenue and purchases without it.

## 10. Configuration reference (config asset > Monetisation)

| Setting | Default | Meaning |
|---|---|---|
| ads.enabled | on | Master switch for all adverts |
| ads.forceTestAds | on | Test adverts even in release builds (turn off for release, §8) |
| ads.androidLive / iosLive | empty | Live ad unit IDs |
| ads.continueMoves | +10, 2 per attempt | Out of Moves advert |
| ads.continueCoinCost / continueCoinMoves / continueCoinPerRunLimit | 100 / 5 / no limit | Out of Moves coins |
| ads.shopCoins | +50, 5 a day, 30 s apart | Coins tab advert |
| ads.coinShortfall | up to 50, 5 a day | Not-enough-coins advert |
| ads.rewardGraceSeconds | 2 | Wait for a late reward callback |
| ads.showTimeoutSeconds / resumeWatchdogSeconds / maxShowSeconds | 15 / 8 / 180 | Watchdogs |
| ads.retryFirstSeconds / retryMaxSeconds | 10 / 300 | Load retries |
| interstitials.enabled | on | Interstitials on/off |
| interstitials.everyCompletedLevels | 5 | Levels per interstitial |
| interstitials.minSecondsBetween | 300 | Minimum gap |
| interstitials.quietSecondsAfterRewardOrPurchase | 120 | Gap after a rewarded advert or purchase |
| store.enabled | on | Purchases on/off |
| store.coinPacks | 500 / 1,200 / 2,750 / 6,000 | Product IDs, names, coins, highlight |
| store.noAdsProductId | no_ads | No Ads product |
| store.noAdsRevokeAfterChecks | 2 | Refund handling |
| store.purchaseTimeoutSeconds | 120 | Unlock the shop if the store never answers |
| privacy.privacyPolicyUrl | empty | Settings link |
| privacy.maxAdContentRating | PG | G, PG, T or MA |
| privacy.childDirected / underAgeOfConsent | off | Audience tags (§7) |
| privacy.debugGeographyEea | off | Development builds: test the consent form as if in the EEA |
| privacy.testDeviceIds | empty | Hashed device IDs for test adverts and consent debugging |

## 11. Testing: what was done and what wasn't

| Kind | Result |
|---|---|
| Automated tests (`dotnet test tools/Harness/Tests`, same as Unity's EditMode tests) | All pass, including 68 for monetisation (TESTING.md) |
| Compilation: game and screens against Unity's assemblies (4 variants), editor scripts | 0 errors, 0 warnings |
| Compilation: SDK integrations against API **shims** (`tools/Harness/SdkCheck`, Android and iOS paths) | 0 errors. Shims follow the documentation; **not the real SDKs** |
| Lint (font glyphs, serialization) | Clean |
| Monetisation logic with **simulated SDK responses** (scripted fakes) | Covered by the 68 tests: every success, failure, early close, missing and duplicate callback, late reward, watchdog, limit, interstitial rule, purchase outcome, restore (including a store that never answers), refund and consent case |
| Google's **test adverts** on a device | **Not done** |
| **Google Play Billing** with license testers | **Not done** |
| **Real adverts** served on an Android device | **Not done** |
| The screens in Unity (layout, flow, sound, input blocking) | **Not done** |

**Device test plan** (development build, internal testing track):

1. **First start in the EEA:** turn on `privacy.debugGeographyEea` and add your hashed ID to `privacy.testDeviceIds`.
   * The consent form appears.
   * **Consent:** test adverts load.
   * **Do not consent:** Out of Moves shows "Advert not available"; coins still work; Settings shows Privacy options.
2. **Out of Moves:**
   * Watch to the end: +10 moves, same board.
   * Close early: nothing added, offers stay.
   * Airplane mode: "not available".
   * The second advert in an attempt works; a third is refused.
   * 100 coins: +5 moves; too few coins: the button explains.
   * Give Up: the normal results choices.
3. **Coins tab:** +50 per advert; 5 a day; 30 s apart. **Not-enough-coins:** an exact shortfall, then a manual Buy.
4. **Interstitials:**
   * Win 5 levels: one at Next Level or Map, never at Replay or after a loss.
   * Win 5 more within 5 minutes: none until 5 minutes have passed.
   * After a rewarded advert: none for 2 minutes.
5. **Purchases (license tester):**
   * Each pack: coins added once; the balance updates.
   * Cancel: nothing.
   * Declined card: nothing.
   * Slow card: "pending", coins later.
   * No Ads: no interstitials; rewarded adverts still offered.
   * Restart the app: still owned.
   * Uninstall and reinstall: restored. Restore Purchases also works.
   * Refund in Play Console: No Ads goes after two starts.
6. **Kill the app during a purchase**, before it finishes: the purchase completes once at the next start.

## 12. Known limitations and recommendations

* **SDK integrations unverified against the real SDKs** (see the status table). They are isolated and can be switched
  off from the menu.
* **No server-side receipt validation.** A tampered device could fake purchases. Before relying on revenue, add
  verification via the Google Play Developer API on a server, or at least Unity IAP's local receipt validator
  (needs the app's licence key; obfuscated with Unity's tool). For coins and No Ads in a single-player game the risk is
  limited, but it is real.
* **Economy balance, to watch with real data:**
  * An active player earns about 60–100 coins a day by playing (DESIGN.md §8), so five 50-coin adverts double or
    triple daily income.
  * The Extra Moves power-up (3 moves for 200) was retired in 3.0.1, because the coin continue gives 5 moves for 100.
  * Every limit is a config value.
* **Interstitials can appear from the 5th level** of a new player. A "first level for interstitials" setting would be
  easy to add if retention data asks for it.
* **Bought coins are erased by "Reset all progress"** (the warning says so). No Ads and the purchase record are kept.
* **Analytics are on-device only** (§9).
* **iOS** isn't ready (no App Tracking Transparency prompt; App Store products and testing not done).
* The plugin deprecated the child-directed and under-age flags in v11.3 in favour of a new age setting. The
  integration sets the old ones by name, only when ticked; revisit if you target children.
* The **reward amount set in AdMob is ignored**: the game's config decides.
