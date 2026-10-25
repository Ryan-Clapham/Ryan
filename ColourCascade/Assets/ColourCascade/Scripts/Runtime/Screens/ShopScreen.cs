using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Shop with data-driven tabs. Adding a category (e.g. cosmetics) means adding one ShopTab subclass to
    /// the list in Build; the tab bar, scrolling and layout are shared. All purchase rules live in ShopService.
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        public const int TabPowerUps = 0;
        public const int TabCoins = 1;

        public override ScreenId Id => ScreenId.Shop;

        abstract class ShopTab
        {
            public string Title;
            public Icon Icon;
            public ShopScreen Owner;
            public abstract void Build(RectTransform list);
            public virtual void Refresh() { }
        }

        readonly List<ShopTab> tabs = new List<ShopTab>();
        TabBar tabBar;
        RectTransform pageHost;
        ScrollRect scroll;
        RectTransform list;
        int currentTab = -1;

        protected override void Build()
        {
            var bar = TopBar("Shop", () => Nav.Back());
            var coins = CoinCounter.Create(bar);
            ((RectTransform)coins.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(270, 92));
            var title = bar.Find("Title") as RectTransform;
            if (title != null) title.offsetMax = new Vector2(-290, title.offsetMax.y);

            tabs.Add(new PowerUpsTab { Title = "Power-ups", Icon = Icon.Bolt, Owner = this });
            tabs.Add(new CoinsTab { Title = "Coins", Icon = Icon.Gift, Owner = this });

            var titles = new List<string>();
            var icons = new List<Icon>();
            foreach (var t in tabs)
            {
                titles.Add(t.Title);
                icons.Add(t.Icon);
            }
            tabBar = TabBar.Create(Safe, titles, icons, ShowTab);
            ((RectTransform)tabBar.transform).TopStrip(124, 172, Theme.Margin, Theme.Margin);

            pageHost = UI.Rect("Page", Safe);
            pageHost.Fill(0, 0, 0, 316);
            var viewport = UI.Rect("Viewport", pageHost);
            viewport.Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            scroll = pageHost.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
        }

        public override void OnShow(object args)
        {
            App.SetBackground(-1);
            int tab = args is int i ? i : TabPowerUps;
            tabBar.Select(Mathf.Clamp(tab, 0, tabs.Count - 1), true);
        }

        void ShowTab(int index)
        {
            currentTab = index;
            if (list != null) Destroy(list.gameObject);
            list = UI.Rect("List", scroll.viewport);
            list.anchorMin = new Vector2(0, 1);
            list.anchorMax = new Vector2(1, 1);
            list.pivot = new Vector2(0.5f, 1);
            list.anchoredPosition = Vector2.zero;
            var v = UI.VStack(list, 24, new RectOffset((int)Theme.Margin, (int)Theme.Margin, 20, 60));
            v.childForceExpandHeight = false;
            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list;
            tabs[index].Build(list);
            scroll.verticalNormalizedPosition = 1f;
        }

        void RefreshCurrent()
        {
            if (currentTab >= 0) tabs[currentTab].Refresh();
        }

        // ================================================================== Power-ups

        sealed class PowerUpsTab : ShopTab
        {
            sealed class Row
            {
                public PowerUpDefinition Def;
                public CCButton Price;
                public Text Owned;
                public RectTransform Root;
            }

            readonly List<Row> rows = new List<Row>();
            GameContext Ctx => GameApp.Instance.Ctx;

            public override void Build(RectTransform list)
            {
                rows.Clear();
                var intro = UI.Text(list, "Use power-ups in Campaign and Endless. Challenges are always power-up free.", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Intro");
                intro.Layout(preferredHeight: 90);
                foreach (var def in Ctx.PowerUps.All) rows.Add(CreateRow(list, def));
                Refresh();
            }

            Row CreateRow(RectTransform list, PowerUpDefinition def)
            {
                var row = new Row { Def = def };
                var root = UI.Rect("Item_" + def.id, list);
                root.Layout(preferredHeight: 250);
                row.Root = root;
                var card = UI.Card(root, "Card", Theme.Surface, 46);
                var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Hex(def.accentHex), "Circle");
                circle.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(170, 170));
                UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss").rectTransform.Fill();
                UI.Image(circle.rectTransform, ArtLibrary.Instance.PowerUp(def.id), Color.white, "Art").rectTransform.Centre(150, 150);
                var name = UI.FitText(card, def.displayName, Theme.FontLarge, Theme.Text, TextAnchor.MiddleLeft, true, "Name");
                name.rectTransform.TopStrip(62, 24, 222, 270);
                var desc = UI.Text(card, def.description, Theme.FontTiny + 2, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Description");
                desc.rectTransform.Fill(222, 64, 270, 92);
                row.Owned = UI.FitText(card, "", Theme.FontTiny, Theme.Teal, TextAnchor.MiddleLeft, true, "Owned");
                row.Owned.rectTransform.BottomStrip(48, 22, 222, 270);
                row.Price = UI.Button(card, "", ButtonStyle.Gold, () => Buy(row), null, Theme.FontBody, 40, 10, "Price");
                ((RectTransform)row.Price.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(226, 130));
                var coin = UI.Sprite(row.Price.FaceRect.Find("Content"), ArtNames.Coin, 48);
                coin.transform.SetAsFirstSibling();
                return row;
            }

            public override void Refresh()
            {
                foreach (var r in rows)
                {
                    var check = Ctx.Shop.Check(r.Def.id);
                    bool locked = check.Block == PurchaseBlock.Locked;
                    r.Price.SetLabel(locked ? "Lvl " + r.Def.unlockLevel : NumberFormat.Thousands(r.Def.price));
                    var coin = r.Price.FaceRect.Find("Content/" + ArtNames.Coin);
                    if (coin != null) coin.gameObject.SetActive(!locked);
                    r.Price.Interactable = check.Allowed;
                    var row = r;
                    r.Price.OnDisabledClick = () =>
                    {
                        var c = Ctx.Shop.Check(row.Def.id);
                        Toast.Show(c.Message, c.Block == PurchaseBlock.Locked ? Icon.Lock : Icon.Info, c.Block == PurchaseBlock.Locked ? Theme.TextSecondary : Theme.Danger);
                        Shake(row.Price.transform as RectTransform);
                    };
                    int owned = Ctx.Inventory.Owned(r.Def.id), trials = Ctx.Inventory.Trials(r.Def.id);
                    if (locked) r.Owned.text = "Unlocks at level " + r.Def.unlockLevel;
                    else r.Owned.text = "Owned: " + owned + (trials > 0 ? "  (+" + trials + " free)" : "");
                    r.Owned.color = locked ? Theme.TextMuted : Theme.Teal;
                }
            }

            void Buy(Row row)
            {
                var result = Ctx.Shop.TryBuy(row.Def.id);
                if (!result.Allowed)
                {
                    Toast.Show(result.Message, Icon.Info, Theme.Danger);
                    Refresh();
                    return;
                }
                Ctx.Save();
                AudioService.Sfx(SfxNames.Purchase);
                Haptics.Medium();
                Toast.Show(row.Def.displayName + " added (" + Ctx.Inventory.Owned(row.Def.id) + " owned)", Icon.Check, Theme.Success);
                row.Root.localScale = Vector3.one * 1.04f;
                Tw.Scale(row.Root, 1f, 0.3f, Ease.OutBack);
                GameApp.Instance.GlobalFx.Sparkle(GameApp.Instance.GlobalFx.ToLocal(row.Price.transform.position), Theme.Gold, 14, 70f);
                Refresh();
            }

            static void Shake(RectTransform rt)
            {
                if (rt == null) return;
                var start = rt.anchoredPosition;
                Tw.Move(rt, start + new Vector2(14, 0), 0.05f).OnComplete(() =>
                    Tw.Move(rt, start - new Vector2(14, 0), 0.08f).OnComplete(() => Tw.Move(rt, start, 0.06f)));
            }
        }

        // ================================================================== Coins

        sealed class CoinsTab : ShopTab
        {
            GameContext Ctx => GameApp.Instance.Ctx;

            public override void Build(RectTransform list)
            {
                var hero = UI.Rect("Balance", list);
                hero.Layout(preferredHeight: 260);
                var card = UI.Card(hero, "Card", Theme.SurfaceHigh, 50);
                var coin = UI.Sprite(card, ArtNames.Coin, 170);
                coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(170, 170));
                var amount = UI.Text(card, NumberFormat.Thousands(Ctx.Wallet.Balance), 92, Theme.Text, TextAnchor.MiddleLeft, true, "Amount");
                amount.rectTransform.Fill(236, 90, 30, 30);
                UI.AddShadow(amount, -5, 0.4f);
                var sub = UI.Text(card, "Every coin is earned by playing.", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Sub");
                sub.rectTransform.Fill(240, 36, 30, 160);

                var header = UI.Text(list, "WAYS TO EARN COINS", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Header");
                header.Layout(preferredHeight: 70);

                var eco = Ctx.Config.Economy;
                Earn(list, Icon.Map, Theme.Primary, "Clear new levels",
                    "+" + eco.FirstClearReward(1) + " to +" + eco.firstClearMax + " coins the first time you clear a level, plus " + eco.coinsPerNewStar + " for each new star.",
                    "Play", () => Nav().Show(ScreenId.Game, GameArgs.ForCampaign(Ctx.Campaign.CurrentLevel)));
                Earn(list, Icon.Trophy, Theme.Gold, "Milestones",
                    "Every " + Ctx.Config.Campaign.milestoneEvery + "th level pays +" + eco.milestoneReward + ", and finishing a region pays +" + eco.regionFinaleReward + ".",
                    "Map", () => Nav().Show(ScreenId.Map, new MapArgs()));
                bool dailyDone = Ctx.Rewards.IsClaimed(RewardKeys.DailyComplete(Ctx.TodayKey));
                Earn(list, Icon.Calendar, Theme.Gold, "Daily Challenge",
                    "+" + Ctx.Config.Daily.completionReward + " for completing today's puzzle, +" + Ctx.Config.Daily.threeStarBonus + " more for three stars." + (dailyDone ? "  Collected today ✓" : ""),
                    dailyDone ? null : "Play", () => Nav().Open<ModePopup>().Setup(GameMode.Daily));
                int weeklyTotal = 0;
                foreach (var r in Ctx.Config.Rush.tierRewards) weeklyTotal += r;
                Earn(list, Icon.Bolt, Theme.Primary, "Weekly Rush",
                    "Reach the Bronze, Silver and Gold scores to collect up to +" + weeklyTotal + " each week.",
                    "Play", () => Nav().Open<ModePopup>().Setup(GameMode.WeeklyRush));
                Earn(list, Icon.Infinity, Theme.Teal, "Endless milestones",
                    "One-time rewards the first time a run reaches " + MilestoneList() + " points.",
                    "Play", () => Nav().Open<ModePopup>().Setup(GameMode.Endless));

                var note = UI.Rect("Note", list);
                note.Layout(preferredHeight: 170);
                var noteCard = UI.Card(note, "Card", new Color(0.03f, 0.02f, 0.12f, 0.4f), 40, false);
                var noteText = UI.Text(noteCard, "Coin packs aren't sold in this version of Colour Cascade. Power-ups are bought only with coins you earn.", Theme.FontTiny + 2, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Text");
                noteText.rectTransform.Fill(40, 12, 40, 12);
            }

            static Router Nav() => GameApp.Instance.Router;

            string MilestoneList()
            {
                var parts = new List<string>();
                foreach (var m in Ctx.Config.Endless.milestoneScores) parts.Add(NumberFormat.Thousands(m));
                return string.Join(" / ", parts);
            }

            static void Earn(RectTransform list, Icon icon, Color accent, string title, string body, string action, Action onAction)
            {
                var root = UI.Rect("Earn_" + title, list);
                root.Layout(preferredHeight: 230);
                var card = UI.Card(root, "Card", Theme.Surface, 44);
                var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), accent, "Circle");
                circle.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(120, 120));
                UI.Icon(circle.rectTransform, icon, Color.white, 64).rectTransform.Centre(64, 64);
                float right = action != null ? 230 : 30;
                var t = UI.FitText(card, title, Theme.FontBody + 2, Theme.Text, TextAnchor.MiddleLeft, true, "Title");
                t.rectTransform.TopStrip(56, 26, 176, right);
                var b = UI.Text(card, body, Theme.FontTiny + 1, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Body");
                b.rectTransform.Fill(176, 18, right, 88);
                if (action != null)
                {
                    var btn = UI.Button(card, action, ButtonStyle.Secondary, onAction, null, Theme.FontBody, 36, 8, "Action");
                    ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(180, 112));
                }
            }
        }
    }
}
