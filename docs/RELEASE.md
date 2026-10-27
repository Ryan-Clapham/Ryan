# Colour Cascade: Releasing on Google Play

A step-by-step path from "works in the Unity editor" to "live on Google Play", for a first release.
Google's rules change often; the facts below were checked in October 2026, and the linked pages are the
places to confirm them.

## At a glance

| Stage | What happens | Typical time |
|---|---|---|
| 1. Finish and test on real phones | Play through on 2–3 Android phones, fix what you find | 1–3 weeks |
| 2. Prepare the release in Unity | App icon, version, signing key, Android App Bundle | A day |
| 3. Google Play developer account | Pay the fee, verify your identity | A few days |
| 4. Store listing and policy forms | Text, graphics, privacy policy, content rating, Data safety | A day |
| 5. Internal testing | Upload, install from the Play Store yourself | A day |
| 6. Closed testing (new personal accounts) | **At least 12 testers opted in for 14 days in a row** | 2+ weeks |
| 7. Production access and launch | Apply, Google reviews, release (optionally staged) | About a week |
| 8. After launch | Watch crashes and reviews, ship updates | Ongoing |

Allow roughly **4–6 weeks** from "finished" to "live", mostly because of the closed test.

## 1. Finish and test on real phones

* Build a development APK (**Colour Cascade → Build → Android APK (Development)**) and install it over USB
  (see the README). Development builds show the developer tools in Settings, which help with testing.
* Use at least one cheap or older phone, one modern phone and, if possible, a tablet. Check: touch feel, text
  size, the notch and gesture bar, Android Back everywhere, leaving and returning to the app mid-level, sound,
  vibration, and battery and heat after 20 minutes of play.
* Work through the manual checklist in `docs/TESTING.md`.
* Ask a few friends to play without help. Where they get stuck is more useful than anything else.

## 2. Prepare the release in Unity

* **App icon.** Not set yet, so Android shows Unity's default icon. Set one under **Player Settings → Icon**
  (Android adaptive icon: a foreground and background layer, 432 × 432 px). You also need a 512 × 512 PNG for
  the store listing.
* **Package name.** `com.ryanclapham.colourcascade` (in `ColourCascadeSetup.cs`). It is permanent once the
  first build is uploaded, so change it before then if you want a different one.
* **Version.** Every upload needs a higher **Bundle Version Code** (Player Settings → Other Settings: 1, 2,
  3…). The **Version** text (`1.0.0`) is what players see.
* **Target API level.** New apps and updates must target **Android 16 (API level 36)** or higher (required
  since 31 August 2026). Leave **Target API Level** on *Automatic (highest installed)* and check the Android SDK
  that came with Unity includes API 36; if not, install it with the Android SDK manager.
  [Target API requirements](https://developer.android.com/google/play/requirements/target-sdk)
* **Splash screen.** Unity 6 lets Personal users turn off or customise the "Made with Unity" splash
  (Player Settings → Splash Image). The game has its own splash either way.
  [Unity support article](https://support.unity.com/hc/en-us/articles/212165803)
* **Signing key ("upload key").** Player Settings → Publishing Settings → **Keystore Manager** → create a new
  keystore. **Back it up somewhere safe along with its passwords.** Google Play keeps the real app-signing key
  (Play App Signing), so a lost upload key can be reset through support, but it's slow.
* **Build.** **Colour Cascade → Build → Android App Bundle (Release)** writes
  `Builds/Android/ColourCascade.aab`. Google Play only accepts App Bundles for new apps. Release builds hide
  the developer tools automatically.

## 3. Google Play developer account

* Sign up at the Google Play Console with a Google account you'll keep. There is a one-time **US$25** fee.
* Choose **Personal** (just you) or **Organization** (a registered business; needs a D-U-N-S number, which
  can take weeks). Personal is normal for a first game.
* Verify your identity: legal name, address and an official ID document. Your name (and, for some
  accounts, address) can appear on the store page.

## 4. Store listing and policy forms

Play Console walks you through these on the app's **Dashboard**:

* **Store listing:** name (up to 30 characters), short description (80), full description (4,000), app icon
  (512 × 512), feature graphic (1,024 × 500) and at least 2 phone screenshots. Game view screenshots at
  1080 × 1920 work.
* **Name check:** search Google Play for "Colour Cascade" and "Color Cascade" first, to avoid a clash with an
  existing app or trademark.
* **Privacy policy:** Play Console asks for a link. This game collects nothing itself, but a short public
  page must still say so (and mention anything Unity's engine collects; see Unity's privacy documentation).
* **Data safety form:** what data the app collects and shares. For this version: no ads, no accounts, no
  analytics added by the game; everything is saved on the device.
* **Content rating:** a questionnaire (IARC). A puzzle game with no violence normally rates PEGI 3 /
  Everyone.
* **Target audience:** choosing ages under 13 brings Google's Families policy into play. The game already
  fits it (no ads, no data collection), but if you add ads or purchases later, keep it in mind.
* **Ads:** declare "no ads". **In-app purchases:** none in this version.

## 5. Internal testing

Create an **Internal testing** release, upload the `.aab`, add your own Google account as a tester and
install the game from the Play Store link. This is the first time the store-delivered build runs, so play it
properly. Play Console's **pre-launch report** also runs the game on a range of devices automatically and
lists crashes, screenshots and accessibility issues.

## 6. Closed testing (required for new personal accounts)

Personal developer accounts created after 13 November 2023 must run a **closed test with at least 12
testers who stay opted in for 14 days in a row** before they can apply for production access. Friends and
family with Android phones are the usual testers. They join with an opt-in link and must install from Google
Play. Ask them to play a few times over the two weeks and to send feedback; Google asks about the test when
you apply. Organization accounts are exempt.
[Google's help page on testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465)

## 7. Production access and launch

Apply for production access from the Dashboard (Google reviews it; allow about a week). Then create a
**Production** release from the same build. A **staged rollout** (for example 20% of users first) limits the
damage if something slipped through. Each later update needs a higher Bundle Version Code and goes through
review again, usually within a day or two.

## 8. After launch

* **Android vitals** in Play Console shows crash and "not responding" rates; keep both low.
* Reply to reviews; they're the best source of what to fix next.
* Keep up with the yearly target API rise (Google announces it each year and enforces it from about August).

## Later: in-app purchases (coin packs)

Real-money purchases are deliberately not in this version (no fake or non-working buy buttons). Adding them is
an update after launch, because purchases can only be tested against a live Play Console app:

1. In Play Console, set up a **payments profile** (bank and tax details) and create the products (for example
   consumable coin packs) with prices.
2. In Unity, add the **In-App Purchasing** package (Google Play Billing). Digital goods on Google Play must use
   Google Play Billing.
3. Add a "Coin packs" tab to the shop (the shop's tabs are data, see `docs/ARCHITECTURE.md`), grant coins
   only after Google confirms the purchase, handle pending and refunded purchases, and verify receipts.
4. Test with Play Console **license testers** (purchases aren't charged), then update the Data safety form,
   the "Contains in-app purchases" listing label and, if children are in your audience, check the Families rules.

Selling coins also changes how the coin economy should feel, so revisit the reward tuning (`docs/DESIGN.md` §8)
at the same time: generous enough to stay fun without paying, and never a paywall.
