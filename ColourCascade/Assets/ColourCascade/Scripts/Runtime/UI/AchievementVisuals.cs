using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>How achievements look (tier colours, category icons) and what claiming one does on screen.</summary>
    public static class AchievementVisuals
    {
        /// <summary>Easy green, Medium teal, Hard gold, Very hard pink: the four tiers are told apart at a glance.</summary>
        public static Color TierColour(AchievementTier t)
        {
            switch (t)
            {
                case AchievementTier.Easy: return Theme.Success;
                case AchievementTier.Medium: return Theme.Teal;
                case AchievementTier.Hard: return Theme.Gold;
                default: return Theme.Danger;
            }
        }

        public static Icon CategoryIcon(AchievementCategory c)
        {
            switch (c)
            {
                case AchievementCategory.Campaign: return Icon.Map;
                case AchievementCategory.Stars: return Icon.Star;
                case AchievementCategory.Regions: return Icon.Crown;
                case AchievementCategory.Endless: return Icon.Infinity;
                case AchievementCategory.Skill: return Icon.Target;
                case AchievementCategory.Mechanics: return Icon.Bolt;
                default: return Icon.Calendar;
            }
        }

        /// <summary>A round badge in the tier colour with the category icon (dimmed while locked).</summary>
        public static Image Badge(Transform parent, AchievementDefinition d, float size, out Image icon)
        {
            var circle = UI.Image(parent, ArtLibrary.Instance.Get(ArtNames.Circle), TierColour(d.tier), "Badge");
            circle.rectTransform.sizeDelta = new Vector2(size, size);
            UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss").rectTransform.Fill();
            icon = UI.Icon(circle.rectTransform, CategoryIcon(d.category), Color.white, size * 0.52f);
            icon.rectTransform.Centre(size * 0.52f, size * 0.52f);
            return circle;
        }

        public static void SetBadgeLit(Image circle, Image icon, AchievementDefinition d, bool lit)
        {
            var c = TierColour(d.tier);
            circle.color = lit ? c : Color.Lerp(c, Theme.Disabled, 0.7f);
            icon.color = lit ? Color.white : new Color(1, 1, 1, 0.55f);
        }

        /// <summary>Claims a reward and shows what was paid. Returns whether it was paid (it never pays twice).</summary>
        public static bool Claim(AchievementDefinition d, Transform from)
        {
            var app = GameApp.Instance;
            var ctx = app.Ctx;
            var result = ctx.Achievements.Claim(d.id);
            if (result != AchievementClaim.Claimed)
            {
                if (result == AchievementClaim.AlreadyClaimed) Toast.Show("Already claimed", Icon.Check, Theme.TextSecondary);
                return false;
            }
            AudioService.Sfx(SfxNames.Coin);
            Haptics.Medium();
            if (from != null)
            {
                var at = app.GlobalFx.ToLocal(from.position);
                app.GlobalFx.Sparkle(at, Theme.Gold, 16, 80f);
                app.GlobalFx.Ring(at, Theme.Gold, 220f);
            }
            Toast.Show("Claimed: " + AchievementText.Reward(d.reward, ctx.PowerUps), Icon.Gift, Theme.Gold);
            return true;
        }
    }

    /// <summary>
    /// Shows a banner when achievements unlock: one at a time, with the name, the reward and a Claim button; several
    /// waiting at once become one summary banner. Banners wait while a popup is open (the result popup lists the
    /// run's unlocks itself) and while a game is being played, so they never cover the board or a decision.
    /// </summary>
    public sealed class AchievementNotifier : MonoBehaviour
    {
        const float ShowSeconds = 5f;
        const int SummaryFrom = 3;

        readonly List<AchievementDefinition> queue = new List<AchievementDefinition>();
        AchievementService service;
        RectTransform banner;
        CanvasGroup bannerGroup;
        float bannerUntil;
        float nextAt;
        bool fromLoad;

        public static AchievementNotifier Create(RectTransform overlay)
        {
            var rt = UI.Rect("AchievementNotices", overlay);
            rt.Fill();
            var n = rt.gameObject.AddComponent<AchievementNotifier>();
            n.service = GameApp.Instance.Ctx.Achievements;
            n.service.Unlocked += n.Enqueue;
            return n;
        }

        void OnDestroy()
        {
            if (service != null) service.Unlocked -= Enqueue;
        }

        void Enqueue(IReadOnlyList<AchievementDefinition> list)
        {
            foreach (var d in list)
                if (d != null && !queue.Contains(d)) queue.Add(d);
        }

        /// <summary>Achievements an existing save already proved at startup: announced together, once the home screen is up.</summary>
        public void AnnounceOnLoad(List<AchievementDefinition> list)
        {
            if (list == null || list.Count == 0) return;
            fromLoad = true;
            Enqueue(list);
            nextAt = Mathf.Max(nextAt, Time.unscaledTime + 3.6f);
        }

        void Update()
        {
            if (banner != null)
            {
                if (Time.unscaledTime >= bannerUntil) Hide();
                return;
            }
            if (queue.Count == 0 || Time.unscaledTime < nextAt) return;
            var app = GameApp.Instance;
            if (app == null || !app.Ready || app.Router == null) return;
            var nav = app.Router;
            if (nav.Current == ScreenId.Achievements)
            {
                // The achievements screen shows them all.
                queue.Clear();
                fromLoad = false;
                return;
            }
            if (nav.AnyPopup || nav.Current == ScreenId.Game || Toast.IsShowing) return;
            // Claimed meanwhile, or no longer in this save (a progress reset, the developer test save): nothing to say.
            queue.RemoveAll(d => service.State(d) != AchievementState.Unlocked);
            if (queue.Count == 0)
            {
                fromLoad = false;
                return;
            }
            if (queue.Count >= SummaryFrom || (fromLoad && queue.Count > 1))
            {
                ShowSummary(queue.Count, fromLoad);
                queue.Clear();
            }
            else
            {
                var d = queue[0];
                queue.RemoveAt(0);
                ShowOne(d);
            }
            fromLoad = false;
        }

        RectTransform Frame(Color accent, out RectTransform card)
        {
            var rt = UI.Rect("AchievementBanner", transform);
            rt.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(980, 214));
            card = UI.Card(rt, "Card", Theme.SurfaceHigh, 44);
            var stroke = UI.Rounded(card, accent.WithAlpha(0.9f), 44, "Stroke", ArtNames.RoundStroke);
            stroke.rectTransform.Fill();
            // Tapping the banner (away from its button) opens the achievements screen. The hit area sits beneath the
            // card, so the card's own button stays on top of it.
            var hit = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.Round), new Color(0, 0, 0, 0), "Hit");
            hit.rectTransform.Fill();
            hit.raycastTarget = true;
            hit.transform.SetAsFirstSibling();
            var open = rt.gameObject.AddComponent<CCButton>();
            open.OnClick = OpenScreen;
            open.Configure(ButtonStyle.Ghost, 0);
            bannerGroup = rt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            rt.anchoredPosition = new Vector2(0, -60);
            Tw.Fade(bannerGroup, 1f, 0.2f);
            Tw.Move(rt, new Vector2(0, -150), 0.35f, Ease.OutBack);
            banner = rt;
            bannerUntil = Time.unscaledTime + ShowSeconds;
            AudioService.Sfx(SfxNames.Unlock);
            Haptics.Light();
            return rt;
        }

        void ShowOne(AchievementDefinition d)
        {
            var accent = AchievementVisuals.TierColour(d.tier);
            Frame(accent, out var card);
            var badge = AchievementVisuals.Badge(card, d, 140, out _);
            badge.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(140, 140));
            var label = UI.FitText(card, "ACHIEVEMENT UNLOCKED  ·  " + AchievementText.Tier(d.tier).ToUpperInvariant(), Theme.FontTiny - 2, accent, TextAnchor.MiddleLeft, true, "Label");
            label.rectTransform.TopStrip(40, 22, 190, 250);
            var title = UI.FitText(card, d.title, Theme.FontLarge, Theme.Text, TextAnchor.MiddleLeft, true, "Title");
            title.rectTransform.TopStrip(62, 62, 190, 250);
            var reward = UI.FitText(card, "Reward: " + AchievementText.Reward(d.reward, GameApp.Instance.Ctx.PowerUps), Theme.FontTiny, Theme.Gold, TextAnchor.MiddleLeft, false, "Reward");
            reward.rectTransform.BottomStrip(46, 24, 190, 250);
            var claim = UI.Button(card, "Claim", ButtonStyle.Gold, null, null, Theme.FontBody, 36, 8, "Claim");
            ((RectTransform)claim.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(210, 110));
            claim.OnClick = () =>
            {
                AchievementVisuals.Claim(d, claim.transform);
                Hide();
            };
        }

        void ShowSummary(int count, bool earnedEarlier)
        {
            Frame(Theme.Gold, out var card);
            var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Gold, "Badge");
            circle.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(140, 140));
            UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss").rectTransform.Fill();
            UI.Icon(circle.rectTransform, Icon.Trophy, Color.white, 76).rectTransform.Centre(76, 76);
            var label = UI.FitText(card, "ACHIEVEMENTS", Theme.FontTiny - 2, Theme.Gold, TextAnchor.MiddleLeft, true, "Label");
            label.rectTransform.TopStrip(40, 22, 190, 250);
            var title = UI.FitText(card, AchievementText.Several(count), Theme.FontLarge, Theme.Text, TextAnchor.MiddleLeft, true, "Title");
            title.rectTransform.TopStrip(62, 62, 190, 250);
            var line = UI.FitText(card, earnedEarlier ? "Earned by your progress so far. Claim the rewards!" : "Their rewards are waiting to be claimed.", Theme.FontTiny, Theme.Gold, TextAnchor.MiddleLeft, false, "Line");
            line.rectTransform.BottomStrip(46, 24, 190, 250);
            var view = UI.Button(card, "View", ButtonStyle.Primary, OpenScreen, null, Theme.FontBody, 36, 8, "View");
            ((RectTransform)view.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(210, 110));
        }

        void OpenScreen()
        {
            Hide();
            queue.Clear();
            var app = GameApp.Instance;
            if (app != null && app.Router != null && !app.Router.AnyPopup && app.Router.Current != ScreenId.Game) app.Router.Show(ScreenId.Achievements);
        }

        void Hide()
        {
            if (banner == null) return;
            var rt = banner;
            var group = bannerGroup;
            banner = null;
            bannerGroup = null;
            nextAt = Time.unscaledTime + 0.45f;
            group.blocksRaycasts = false;
            Tw.Fade(group, 0f, 0.22f, Ease.InQuad).OnComplete(() =>
            {
                if (rt != null) Destroy(rt.gameObject);
            });
        }
    }
}
