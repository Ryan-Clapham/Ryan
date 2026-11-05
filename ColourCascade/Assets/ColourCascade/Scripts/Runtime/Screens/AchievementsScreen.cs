using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Every achievement, by category (docs/ACHIEVEMENTS.md): its name, what it asks, its tier, its reward, and either
    /// the progress towards it (locked), a Claim button (unlocked) or a tick (claimed). The summary at the top shows
    /// how many are completed, the percentage, and Claim all. Cards are built once and updated in place, so claiming
    /// never moves the list.
    /// </summary>
    public sealed class AchievementsScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Achievements;

        const float SummaryTop = 172f;
        const float SummaryHeight = 280f;
        const float CardHeight = 240f;

        sealed class Card
        {
            public AchievementDefinition Def;
            public RectTransform Root;
            public RectTransform Face;
            public Image Badge, BadgeIcon, Glow;
            public Text Reward, Progress, Claimed;
            public Image Lock, Tick;
            public ProgressBar Bar;
            public CCButton Claim;
        }

        readonly List<Card> cards = new List<Card>();
        readonly Dictionary<AchievementCategory, Text> sectionCounts = new Dictionary<AchievementCategory, Text>();
        Text summaryCount, summaryPercent, summaryHint;
        ProgressBar summaryBar;
        CCButton claimAll;
        ScrollRect scroll;
        RectTransform list;
        bool subscribed;
        bool claimingAll;

        protected override void Build()
        {
            var bar = TopBar("Achievements", () => Nav.Back());
            var coins = CoinCounter.Create(bar);
            ((RectTransform)coins.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(270, 92));
            var title = bar.Find("Title") as RectTransform;
            if (title != null) title.offsetMax = new Vector2(-290, title.offsetMax.y);

            BuildSummary();

            var host = UI.Rect("Page", Safe);
            host.Fill(0, 0, 0, SummaryTop + SummaryHeight + 24);
            var viewport = UI.Rect("Viewport", host);
            viewport.Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            scroll = host.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;

            list = UI.Rect("List", viewport);
            list.TopStrip(0);
            var v = UI.VStack(list, 22, new RectOffset((int)Theme.Margin, (int)Theme.Margin, 16, 80));
            v.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list;

            foreach (AchievementCategory category in Enum.GetValues(typeof(AchievementCategory)))
            {
                var defs = Ctx.Achievements.InCategory(category);
                if (defs.Count == 0) continue;
                Section(category);
                foreach (var d in defs) cards.Add(CreateCard(d));
            }
            var footer = UI.Text(list, "Rewards are claimed here, once each. Coins go to your balance and power-ups to your inventory.", Theme.FontTiny, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Footer");
            footer.Layout(preferredHeight: 110);
        }

        void BuildSummary()
        {
            var root = UI.Rect("Summary", Safe);
            root.TopStrip(SummaryHeight, SummaryTop, Theme.Margin, Theme.Margin);
            var card = UI.Card(root, "Card", Theme.Surface, 48);
            var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Gold, "Trophy");
            circle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -30), new Vector2(150, 150));
            UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss").rectTransform.Fill();
            UI.Icon(circle.rectTransform, Icon.Trophy, Color.white, 84).rectTransform.Centre(84, 84);
            summaryCount = UI.FitText(card, "", Theme.FontLarge, Theme.Text, TextAnchor.MiddleLeft, true, "Completed");
            summaryCount.rectTransform.TopStrip(64, 30, 204, 224);
            summaryPercent = UI.FitText(card, "", Theme.FontHeading, Theme.Gold, TextAnchor.MiddleRight, true, "Percent");
            summaryPercent.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -26), new Vector2(180, 72));
            summaryBar = ProgressBar.Create(card, Theme.Gold, 34);
            ((RectTransform)summaryBar.transform).TopStrip(30, 112, 204, 32);
            summaryHint = UI.FitText(card, "", Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleLeft, false, "Hint");
            summaryHint.rectTransform.BottomStrip(60, 28, 32, 390);
            claimAll = UI.Button(card, "Claim all", ButtonStyle.Gold, ClaimAll, Icon.Gift, Theme.FontBody, 36, 8, "ClaimAll");
            ((RectTransform)claimAll.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 24), new Vector2(350, 96));
        }

        void Section(AchievementCategory category)
        {
            var root = UI.Rect("Section_" + category, list);
            root.Layout(preferredHeight: 84);
            var icon = UI.Icon(root, AchievementVisuals.CategoryIcon(category), Theme.TextSecondary, 46);
            icon.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(6, 12), new Vector2(46, 46));
            var name = UI.FitText(root, AchievementText.Category(category).ToUpperInvariant(), Theme.FontSmall, Theme.TextSecondary, TextAnchor.LowerLeft, true, "Name");
            name.rectTransform.Fill(66, 10, 200, 20);
            var count = UI.FitText(root, "", Theme.FontSmall, Theme.TextSecondary, TextAnchor.LowerRight, true, "Count");
            count.rectTransform.Fill(0, 10, 8, 20);
            sectionCounts[category] = count;
        }

        Card CreateCard(AchievementDefinition d)
        {
            var c = new Card { Def = d };
            var root = UI.Rect("Ach_" + d.id, list);
            root.Layout(preferredHeight: CardHeight);
            c.Root = root;
            var face = UI.Card(root, "Card", Theme.Surface, 44);
            c.Face = face;
            c.Glow = UI.Rounded(face, Theme.Gold, 44, "Glow", ArtNames.RoundStroke);
            c.Glow.rectTransform.Fill();
            c.Badge = AchievementVisuals.Badge(face, d, 150, out c.BadgeIcon);
            c.Badge.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(150, 150));
            var tierColour = AchievementVisuals.TierColour(d.tier);
            var chip = UI.Chip(face, AchievementText.Tier(d.tier).ToUpperInvariant(), tierColour.WithAlpha(0.22f), tierColour, Theme.FontTiny - 5, "Tier");
            ((RectTransform)chip.transform.parent).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -22), new Vector2(196, 44));
            var title = UI.FitText(face, d.title, Theme.FontBody + 4, Theme.Text, TextAnchor.MiddleLeft, true, "Title");
            title.rectTransform.TopStrip(58, 20, 200, 236);
            var desc = UI.Text(face, d.description, Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Description");
            desc.rectTransform.Fill(200, 100, 250, 80);
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            c.Reward = UI.FitText(face, "", Theme.FontTiny, Theme.Gold, TextAnchor.MiddleLeft, true, "Reward");
            c.Reward.rectTransform.BottomStrip(42, 54, 200, 250);
            c.Bar = ProgressBar.Create(face, tierColour, 24);
            ((RectTransform)c.Bar.transform).BottomStrip(24, 26, 200, 250);

            // The right-hand column: progress (locked), Claim (unlocked) or a tick (claimed).
            c.Lock = UI.Icon(face, Icon.Lock, Theme.TextMuted, 40);
            c.Lock.rectTransform.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-129, 106), new Vector2(40, 40));
            c.Progress = UI.FitText(face, "", Theme.FontSmall - 2, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Progress");
            c.Progress.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 26), new Vector2(210, 52));
            c.Tick = UI.Icon(face, Icon.Check, Theme.Success, 56);
            c.Tick.rectTransform.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-129, 100), new Vector2(56, 56));
            c.Claimed = UI.FitText(face, "Claimed", Theme.FontSmall - 2, Theme.Success, TextAnchor.MiddleCenter, true, "Claimed");
            c.Claimed.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 26), new Vector2(210, 52));
            return c;
        }

        /// <summary>The Claim button is made only for cards that need one (most are locked), keeping the list light.</summary>
        void EnsureClaimButton(Card c)
        {
            if (c.Claim != null) return;
            var card = c;
            c.Claim = UI.Button(c.Face, "Claim", ButtonStyle.Gold, () => Claim(card), null, Theme.FontBody, 36, 8, "Claim");
            ((RectTransform)c.Claim.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(210, 104));
        }

        public override void OnShow(object args)
        {
            App.SetBackground(-1);
            if (!subscribed)
            {
                Ctx.Achievements.Changed += Refresh;
                subscribed = true;
            }
            Refresh();
            scroll.verticalNormalizedPosition = 1f;
        }

        public override void OnHide()
        {
            if (subscribed) Ctx.Achievements.Changed -= Refresh;
            subscribed = false;
        }

        void OnDestroy()
        {
            if (subscribed && GameApp.Instance != null && GameApp.Instance.Ctx != null) GameApp.Instance.Ctx.Achievements.Changed -= Refresh;
        }

        void Refresh()
        {
            if (claimingAll) return;
            var a = Ctx.Achievements;
            int completed = a.Completed, total = a.Total, unclaimed = a.Unclaimed;
            summaryCount.text = completed + " / " + total + " completed";
            summaryPercent.text = a.Percent + "%";
            summaryBar.SetFraction(total == 0 ? 0f : completed / (float)total, false);
            claimAll.gameObject.SetActive(unclaimed > 0);
            claimAll.SetLabel(unclaimed > 1 ? "Claim all (" + unclaimed + ")" : "Claim");
            summaryHint.rectTransform.offsetMax = new Vector2(unclaimed > 0 ? -390 : -32, summaryHint.rectTransform.offsetMax.y);
            summaryHint.text = unclaimed > 0 ? (unclaimed == 1 ? "1 reward is waiting" : unclaimed + " rewards are waiting") :
                completed == total && total > 0 ? "Every achievement completed!" : "Unlock achievements to earn coins and power-ups.";
            summaryHint.color = unclaimed > 0 ? Theme.Gold : Theme.TextSecondary;

            var done = new Dictionary<AchievementCategory, int>();
            var all = new Dictionary<AchievementCategory, int>();
            var medals = Ctx.Config.Rush != null ? Ctx.Config.Rush.tierNames : null;
            foreach (var c in cards)
            {
                var d = c.Def;
                var state = a.State(d);
                int n;
                all.TryGetValue(d.category, out n);
                all[d.category] = n + 1;
                if (state != AchievementState.Locked)
                {
                    done.TryGetValue(d.category, out n);
                    done[d.category] = n + 1;
                }
                bool locked = state == AchievementState.Locked, ready = state == AchievementState.Unlocked, claimed = state == AchievementState.Claimed;
                AchievementVisuals.SetBadgeLit(c.Badge, c.BadgeIcon, d, !locked);
                c.Glow.gameObject.SetActive(ready);
                string reward = AchievementText.Reward(d.reward, Ctx.PowerUps);
                c.Reward.text = (claimed ? "Claimed: " : "Reward: ") + reward;
                c.Reward.color = claimed ? Theme.TextMuted : Theme.Gold;
                c.Bar.gameObject.SetActive(locked);
                if (locked) c.Bar.SetFraction(a.Fraction(d), false);
                c.Lock.gameObject.SetActive(locked);
                c.Progress.gameObject.SetActive(locked);
                if (locked) c.Progress.text = AchievementText.Progress(d, a.Current(d), medals);
                c.Tick.gameObject.SetActive(claimed);
                c.Claimed.gameObject.SetActive(claimed);
                if (ready) EnsureClaimButton(c);
                if (c.Claim != null) c.Claim.gameObject.SetActive(ready);
            }
            foreach (var kv in sectionCounts)
            {
                int d, t;
                done.TryGetValue(kv.Key, out d);
                all.TryGetValue(kv.Key, out t);
                kv.Value.text = d + " / " + t;
                kv.Value.color = t > 0 && d == t ? Theme.Success : Theme.TextSecondary;
            }
        }

        void Claim(Card c)
        {
            if (!AchievementVisuals.Claim(c.Def, c.Claim != null ? c.Claim.transform : c.Root)) return;
            c.Root.localScale = Vector3.one * 1.03f;
            Tw.Scale(c.Root, 1f, 0.3f, Ease.OutBack);
        }

        void ClaimAll()
        {
            var a = Ctx.Achievements;
            int powerUps = 0;
            foreach (var d in a.All)
                if (a.State(d) == AchievementState.Unlocked && d.reward != null && d.reward.powerUps != null)
                    foreach (var g in d.reward.powerUps)
                        if (g != null && g.count > 0 && a.PaysOut(g.id)) powerUps += g.count;
            int coinsBefore = Ctx.Wallet.Balance;
            int n;
            // One refresh after all the claims, not one per claim.
            claimingAll = true;
            try
            {
                n = a.ClaimAll();
            }
            finally
            {
                claimingAll = false;
            }
            Refresh();
            if (n == 0) return;
            AudioService.Sfx(SfxNames.Coin);
            Haptics.Medium();
            var at = App.GlobalFx.ToLocal(claimAll.transform.position);
            App.GlobalFx.Sparkle(at, Theme.Gold, 20, 90f);
            App.GlobalFx.Ring(at, Theme.Gold, 260f);
            int coins = Ctx.Wallet.Balance - coinsBefore;
            var parts = new List<string>();
            if (coins > 0) parts.Add("+" + NumberFormat.Thousands(coins) + " coins");
            if (powerUps > 0) parts.Add("+" + powerUps + (powerUps == 1 ? " power-up" : " power-ups"));
            Toast.Show("Claimed " + n + (n == 1 ? " reward" : " rewards") + (parts.Count > 0 ? ": " + string.Join(", ", parts.ToArray()) : ""), Icon.Gift, Theme.Gold);
        }
    }
}
