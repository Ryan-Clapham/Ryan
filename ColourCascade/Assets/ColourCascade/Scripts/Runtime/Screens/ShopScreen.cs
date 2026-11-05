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
    /// the list in Build; the tab bar, scrolling and layout are shared. Coin purchases follow ShopService; adverts
    /// and real-money purchases follow the monetisation services (docs/MONETISATION.md).
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        public const int TabPowerUps = 0;
        public const int TabCoins = 1;
        public const int TabPremium = 2;

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
            tabs.Add(new PremiumTab { Title = "Premium", Icon = Icon.Crown, Owner = this });

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
            // Balances, advert availability and store prices change while the shop is open: keep the page current.
            App.CoinsChanged -= OnCoins;
            App.CoinsChanged += OnCoins;
            Ctx.Monetisation.Ads.Changed -= RefreshCurrent;
            Ctx.Monetisation.Ads.Changed += RefreshCurrent;
            Ctx.Monetisation.Purchases.Changed -= RefreshCurrent;
            Ctx.Monetisation.Purchases.Changed += RefreshCurrent;
            int tab = args is int i ? i : TabPowerUps;
            tabBar.Select(Mathf.Clamp(tab, 0, tabs.Count - 1), true);
        }

        public override void OnHide()
        {
            App.CoinsChanged -= OnCoins;
            Ctx.Monetisation.Ads.Changed -= RefreshCurrent;
            Ctx.Monetisation.Purchases.Changed -= RefreshCurrent;
        }

        void OnCoins(int before, int after) => RefreshCurrent();

        void ShowTab(int index)
        {
            currentTab = index;
            if (list != null) Destroy(list.gameObject);
            list = UI.Rect("List", scroll.viewport);
            list.TopStrip(0);
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
                /// <summary>The three-pack (update 3.1), under the single price.</summary>
                public CCButton Pack;
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
                // One at the single price, and a three-pack beneath it (about 10 % off).
                row.Price = UI.Button(card, "", ButtonStyle.Gold, () => Buy(row), null, Theme.FontBody, 40, 10, "Price");
                ((RectTransform)row.Price.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 56), new Vector2(226, 104));
                var coin = UI.Sprite(row.Price.FaceRect.Find("Content"), ArtNames.Coin, 44);
                coin.Layout(preferredWidth: 44);
                coin.transform.SetAsFirstSibling();
                row.Pack = UI.Button(card, "", ButtonStyle.Secondary, () => BuyPack(row), null, Theme.FontSmall, 36, 8, "Pack");
                ((RectTransform)row.Pack.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, -58), new Vector2(226, 92));
                var packContent = row.Pack.FaceRect.Find("Content");
                var packCoin = UI.Sprite(packContent, ArtNames.Coin, 36);
                packCoin.Layout(preferredWidth: 36);
                packCoin.transform.SetAsFirstSibling();
                var times = UI.Text(packContent, "\u00d7" + ShopService.PackSize, Theme.FontSmall, Color.white, TextAnchor.MiddleCenter, true, "Count");
                times.horizontalOverflow = HorizontalWrapMode.Overflow;
                times.rectTransform.sizeDelta = new Vector2(10, Theme.FontSmall * 1.3f);
                times.Layout(preferredWidth: 40);
                times.transform.SetAsFirstSibling();
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
                        if (c.Block == PurchaseBlock.InsufficientFunds)
                        {
                            // Not enough coins: the same buy screen as in a level, with an advert for the missing coins.
                            // The player confirms the purchase there themselves.
                            GameApp.Instance.Router.Open<QuickBuyPopup>().Setup(row.Def, () => Bought(row));
                            return;
                        }
                        Toast.Show(c.Message, c.Block == PurchaseBlock.Locked ? Icon.Lock : Icon.Info, c.Block == PurchaseBlock.Locked ? Theme.TextSecondary : Theme.Danger);
                        Shake(row.Price.transform as RectTransform);
                    };
                    int owned = Ctx.Inventory.Owned(r.Def.id), trials = Ctx.Inventory.Trials(r.Def.id);
                    if (locked) r.Owned.text = "Unlocks at level " + r.Def.unlockLevel;
                    else r.Owned.text = "Owned: " + owned + (trials > 0 ? "  (+" + trials + " free)" : "");
                    r.Owned.color = locked ? Theme.TextMuted : Theme.Teal;

                    // The three-pack: shown once the power-up is unlocked, at its own price.
                    bool pack = !locked && Ctx.Shop.HasPack(r.Def.id);
                    r.Pack.gameObject.SetActive(pack);
                    if (!pack) continue;
                    var packCheck = Ctx.Shop.CheckPack(r.Def.id);
                    r.Pack.SetLabel(NumberFormat.Thousands(Ctx.Shop.PackPrice(r.Def.id)));
                    r.Pack.Interactable = packCheck.Allowed;
                    r.Pack.OnDisabledClick = () =>
                    {
                        var c = Ctx.Shop.CheckPack(row.Def.id);
                        string message = c.Block == PurchaseBlock.InsufficientFunds ? "You need " + NumberFormat.Thousands(c.Shortfall) + " more coins for three" : c.Message;
                        Toast.Show(message, Icon.Info, Theme.Danger);
                        Shake(row.Pack.transform as RectTransform);
                    };
                }
            }

            void BuyPack(Row row)
            {
                var result = Ctx.Shop.TryBuyPack(row.Def.id);
                if (!result.Allowed)
                {
                    Toast.Show(result.Message, Icon.Info, Theme.Danger);
                    Refresh();
                    return;
                }
                Ctx.Save();
                AudioService.Sfx(SfxNames.Purchase);
                Haptics.Medium();
                Toast.Show(ShopService.PackSize + " \u00d7 " + row.Def.displayName + " added (" + Ctx.Inventory.Owned(row.Def.id) + " owned)", Icon.Check, Theme.Success);
                row.Root.localScale = Vector3.one * 1.04f;
                Tw.Scale(row.Root, 1f, 0.3f, Ease.OutBack);
                GameApp.Instance.GlobalFx.Sparkle(GameApp.Instance.GlobalFx.ToLocal(row.Pack.transform.position), Theme.Gold, 18, 80f);
                Refresh();
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
                Bought(row);
            }

            void Bought(Row row)
            {
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
            MonetisationServices M => Ctx.Monetisation;
            Text amount;
            CCButton adButton;
            Text adBody;
            Text storeNote;
            readonly List<KeyValuePair<StoreProduct, CCButton>> packButtons = new List<KeyValuePair<StoreProduct, CCButton>>();
            bool watching;

            public override void Build(RectTransform list)
            {
                packButtons.Clear();
                var hero = UI.Rect("Balance", list);
                hero.Layout(preferredHeight: 260);
                var card = UI.Card(hero, "Card", Theme.SurfaceHigh, 50);
                var coin = UI.Sprite(card, ArtNames.Coin, 170);
                coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(170, 170));
                amount = UI.Text(card, NumberFormat.Thousands(Ctx.Wallet.Balance), 92, Theme.Text, TextAnchor.MiddleLeft, true, "Amount");
                amount.rectTransform.Fill(236, 90, 30, 30);
                UI.AddShadow(amount, -5, 0.4f);
                var sub = UI.Text(card, "Earn coins by playing, watching adverts or buying a pack.", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Sub");
                sub.rectTransform.Fill(240, 36, 30, 160);

                // Free coins for an advert (docs/MONETISATION.md §2.2): the reward is shown before watching.
                if (M.Coins.ShopReward > 0 && M.Coins.ShopOffer().Status != OfferStatus.Disabled)
                {
                    Header(list, "FREE COINS");
                    var root = UI.Rect("AdOffer", list);
                    root.Layout(preferredHeight: 250);
                    var adCard = UI.Card(root, "Card", Theme.Surface, 44);
                    var circle = UI.Image(adCard, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Success, "Circle");
                    circle.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(120, 120));
                    UI.Icon(circle.rectTransform, Icon.Play, Color.white, 64).rectTransform.Centre(64, 64);
                    var t = UI.FitText(adCard, "Watch an advert: +" + NumberFormat.Thousands(M.Coins.ShopReward) + " coins", Theme.FontBody + 2, Theme.Text, TextAnchor.MiddleLeft, true, "Title");
                    t.rectTransform.TopStrip(56, 26, 176, 230);
                    adBody = UI.Text(adCard, "", Theme.FontTiny + 1, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Body");
                    adBody.rectTransform.Fill(176, 18, 230, 92);
                    adButton = UI.Button(adCard, "Watch", ButtonStyle.Success, Watch, null, Theme.FontBody, 36, 8, "Action");
                    ((RectTransform)adButton.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(180, 112));
                    M.Ads.NoteOffered(RewardedPlacements.ShopCoins, M.Coins.ShopOffer());
                }
                else adButton = null;

                // Coin packs (docs/MONETISATION.md §4.1): the amounts come from the config, the prices only from the store.
                if (M.Config.store.enabled)
                {
                    Header(list, "COIN PACKS");
                    foreach (var p in M.Purchases.CoinPacks) packButtons.Add(new KeyValuePair<StoreProduct, CCButton>(p, PackRow(list, p)));
                    var noteRoot = UI.Rect("StoreNote", list);
                    noteRoot.Layout(preferredHeight: 110);
                    storeNote = UI.Text(noteRoot, "", Theme.FontTiny + 1, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Text");
                    storeNote.rectTransform.Fill(40, 0, 40, 0);
                }

                Header(list, "WAYS TO EARN COINS");
                var eco = Ctx.Config.Economy;
                Earn(list, Icon.Map, Theme.Primary, "Clear new levels",
                    "+" + eco.FirstClearReward(1) + " to +" + eco.firstClearMax + " coins the first time you clear a level, and +" + eco.StarReward(1) + ", +" + eco.StarReward(2) +
                    " and +" + eco.StarReward(3) + " the first time you earn its first, second and third star.",
                    "Play", () => Nav().Show(ScreenId.Game, GameArgs.ForCampaign(Ctx.Campaign.CurrentLevel)));
                Earn(list, Icon.Trophy, Theme.Gold, "Milestones",
                    "Every " + Ctx.Config.Campaign.milestoneEvery + "th level pays +" + eco.milestoneReward + ", and finishing a region pays +" + eco.regionFinaleReward + ".",
                    "Map", () => Nav().Show(ScreenId.Map, new MapArgs()));
                bool dailyDone = Ctx.Rewards.IsClaimed(RewardKeys.DailyComplete(Ctx.TodayKey));
                Earn(list, Icon.Calendar, Theme.Gold, "Daily Challenge",
                    "+" + Ctx.Config.Daily.completionReward + " for completing today's puzzle, +" + Ctx.Config.Daily.threeStarBonus + " more for three stars." + (dailyDone ? "  Collected today." : ""),
                    dailyDone ? null : "Play", () => Nav().Open<ModePopup>().Setup(GameMode.Daily));
                int weeklyTotal = 0;
                foreach (var r in Ctx.Config.Rush.tierRewards) weeklyTotal += r;
                Earn(list, Icon.Bolt, Theme.Primary, "Weekly Rush",
                    "Reach the Bronze, Silver and Gold scores to collect up to +" + weeklyTotal + " each week.",
                    "Play", () => Nav().Open<ModePopup>().Setup(GameMode.WeeklyRush));
                Earn(list, Icon.Infinity, Theme.Teal, "Endless milestones",
                    "One-time rewards the first time a run reaches " + MilestoneList() + " points.",
                    "Play", () => Nav().Open<ModePopup>().Setup(GameMode.Endless));
                Refresh();
            }

            public override void Refresh()
            {
                if (amount != null) amount.text = NumberFormat.Thousands(Ctx.Wallet.Balance);
                if (adButton != null)
                {
                    var offer = M.Coins.ShopOffer();
                    adButton.Interactable = offer.CanShow && !watching;
                    adButton.OnDisabledClick = () => Toast.Show(M.Coins.ShopOffer().Message, Icon.Info, Theme.TextSecondary);
                    string left = offer.RemainingToday >= 0 ? (offer.RemainingToday == 1 ? "1 advert left today." : offer.RemainingToday + " adverts left today.") : "";
                    adBody.text = offer.CanShow || watching
                        ? "Coins are added when the advert finishes. " + left
                        : offer.Message;
                }
                var store = M.Purchases;
                foreach (var kv in packButtons)
                {
                    var p = kv.Key;
                    var b = kv.Value;
                    if (b == null) continue;
                    bool buying = store.PurchasingProduct == p.Id;
                    b.SetLabel(buying ? "..." : p.Available ? p.Price : store.State == StoreState.Initialising || store.State == StoreState.NotStarted ? "..." : "Unavailable");
                    b.Interactable = store.State == StoreState.Ready && p.Available && !store.IsPurchasing && !store.IsRestoring;
                    var product = p;
                    b.OnDisabledClick = () => Toast.Show(StoreProblem(product), Icon.Info, Theme.TextSecondary);
                }
                if (storeNote != null)
                    storeNote.text = store.State == StoreState.Unavailable
                        ? "Coin packs can't be bought right now: " + store.UnavailableReason
                        : "Prices are set by " + MonetisationHost.StoreName + " in your local currency. Payments are handled by " + MonetisationHost.StoreName + ".";
            }

            string StoreProblem(StoreProduct p)
            {
                var store = M.Purchases;
                if (store.IsPurchasing || store.IsRestoring) return "Another purchase is in progress.";
                if (store.State == StoreState.Unavailable) return "The store isn't available right now. Check your connection and try again.";
                if (store.State != StoreState.Ready) return "Connecting to the store...";
                return "This pack isn't available right now.";
            }

            void Watch()
            {
                if (watching) return;
                bool started = M.Coins.WatchForShopCoins((outcome, coins) =>
                {
                    watching = false;
                    if (outcome == AdOutcome.Rewarded && coins > 0)
                    {
                        AudioService.Sfx(SfxNames.Purchase);
                        Haptics.Medium();
                        Toast.Show("+" + NumberFormat.Thousands(coins) + " coins", Icon.Gift, Theme.Gold);
                        if (adButton != null) GameApp.Instance.GlobalFx.Sparkle(GameApp.Instance.GlobalFx.ToLocal(adButton.transform.position), Theme.Gold, 14, 70f);
                    }
                    else if (outcome == AdOutcome.ClosedEarly) Toast.Show("The advert ended early, so no coins were added.", Icon.Info, Theme.TextSecondary);
                    else if (outcome == AdOutcome.FailedToShow) Toast.Show("The advert couldn't be shown. Please try again later.", Icon.Info, Theme.Danger);
                    Refresh();
                });
                if (started)
                {
                    watching = true;
                    Refresh();
                }
                else Toast.Show(M.Coins.ShopOffer().Message, Icon.Info, Theme.TextSecondary);
            }

            CCButton PackRow(RectTransform list, StoreProduct p)
            {
                var root = UI.Rect("Pack_" + p.Id, list);
                root.Layout(preferredHeight: 210);
                var card = UI.Card(root, "Card", p.Highlight ? Theme.SurfaceHigh : Theme.Surface, 44);
                var coin = UI.Sprite(card, ArtNames.Coin, 120);
                coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(120, 120));
                var t = UI.FitText(card, NumberFormat.Thousands(p.Coins) + " coins", Theme.FontLarge, Theme.Text, TextAnchor.MiddleLeft, true, "Coins");
                t.rectTransform.TopStrip(48, 30, 190, 270);
                var name = UI.FitText(card, p.Title ?? "", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, false, "Title");
                name.rectTransform.BottomStrip(40, 34, 190, 270);
                if (p.Highlight)
                {
                    // Between the coin amount (30-78 from the top) and the pack name (136-176), clear of both and of the
                    // price button on the right, at every screen width (the card is at least 990 wide).
                    var chipHolder = UI.Rect("BestValue", card);
                    chipHolder.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -84), new Vector2(210, 46));
                    var chip = UI.Chip(chipHolder, "Best value", Theme.Gold.WithAlpha(0.25f), Theme.Gold, Theme.FontTiny);
                    ((RectTransform)chip.transform.parent).Fill();
                }
                var product = p;
                var b = UI.Button(card, "...", ButtonStyle.Gold, () => Buy(product), null, Theme.FontBody, 40, 10, "Price");
                ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(226, 120));
                return b;
            }

            void Buy(StoreProduct p)
            {
                var r = M.Purchases.Buy(p.Id);
                if (r == PurchaseStart.Started) Refresh();
                else Toast.Show(PurchaseStartMessage(r), Icon.Info, Theme.TextSecondary);
            }

            static void Header(RectTransform list, string text)
            {
                var header = UI.Text(list, text, Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Header");
                header.Layout(preferredHeight: 70);
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

        static string PurchaseStartMessage(PurchaseStart r)
        {
            switch (r)
            {
                case PurchaseStart.StoreNotReady: return "The store isn't available right now. Check your connection and try again.";
                case PurchaseStart.AnotherInProgress: return "Another purchase is in progress.";
                case PurchaseStart.AlreadyOwned: return "You already own this.";
                case PurchaseStart.BlockedDuringChallenge: return "The shop is closed during challenges.";
                default: return "This item isn't available right now.";
            }
        }

        // ================================================================== Premium

        /// <summary>
        /// Permanent purchases (docs/MONETISATION.md §4.2, §5). Only No Ads for now; further premium items or cosmetics
        /// are added as more cards here. Includes Restore Purchases.
        /// </summary>
        sealed class PremiumTab : ShopTab
        {
            GameContext Ctx => GameApp.Instance.Ctx;
            MonetisationServices M => Ctx.Monetisation;
            CCButton price;
            CCButton restore;
            Text status;

            public override void Build(RectTransform list)
            {
                var intro = UI.Text(list, "Permanent upgrades, bought once and kept.", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Intro");
                intro.Layout(preferredHeight: 90);

                // Card layout (540 tall), top to bottom: icon, name and badge (40-190); the description in its own block
                // (210-350, shrinks to fit rather than overflow); the button (370-500), the same in every state
                // (price, "Connecting...", "Unavailable", "Owned").
                var root = UI.Rect("NoAds", list);
                root.Layout(preferredHeight: 540);
                var card = UI.Card(root, "Card", Theme.SurfaceHigh, 50);
                var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Gold, "Circle");
                circle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(150, 150));
                UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss").rectTransform.Fill();
                UI.Icon(circle.rectTransform, Icon.Crown, Color.white, 84).rectTransform.Centre(84, 84);
                var name = UI.FitText(card, "No Ads", Theme.FontLarge + 6, Theme.Text, TextAnchor.MiddleLeft, true, "Name");
                name.rectTransform.TopStrip(64, 48, 220, 40);
                var chipHolder = UI.Rect("Permanent", card);
                chipHolder.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(220, -122), new Vector2(320, 52));
                var chip = UI.Chip(chipHolder, "Permanent purchase", Theme.Gold.WithAlpha(0.25f), Theme.Gold, Theme.FontTiny);
                ((RectTransform)chip.transform.parent).Fill();
                var desc = UI.FitText(card, "Removes the adverts between levels, for good. You can still choose to watch adverts for extra moves and coins.",
                    Theme.FontTiny + 2, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Description");
                desc.rectTransform.TopStrip(140, 210, 40, 40);
                price = UI.Button(card, "...", ButtonStyle.Gold, Buy, null, Theme.FontBody, 40, 10, "Price");
                ((RectTransform)price.transform).BottomStrip(130, 40, 40, 40);

                var statusRoot = UI.Rect("Status", list);
                statusRoot.Layout(preferredHeight: 110);
                status = UI.Text(statusRoot, "", Theme.FontTiny + 1, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Text");
                status.rectTransform.Fill(40, 0, 40, 0);

                var restoreRoot = UI.Rect("Restore", list);
                restoreRoot.Layout(preferredHeight: 230);
                var restoreCard = UI.Card(restoreRoot, "Card", Theme.Surface, 44);
                var rt = UI.FitText(restoreCard, "Restore purchases", Theme.FontBody + 2, Theme.Text, TextAnchor.MiddleLeft, true, "Title");
                rt.rectTransform.TopStrip(56, 26, 40, 260);
                var rb = UI.Text(restoreCard, "Bought No Ads before, on this or another device? Restore it here with the same " + (MonetisationHost.StoreName == "Google Play" ? "Google account." : "account."),
                    Theme.FontTiny + 1, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Body");
                rb.rectTransform.Fill(40, 18, 260, 88);
                restore = UI.Button(restoreCard, "Restore", ButtonStyle.Secondary, Restore, null, Theme.FontBody, 36, 8, "Action");
                ((RectTransform)restore.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(210, 112));
                Refresh();
            }

            public override void Refresh()
            {
                if (price == null) return;
                var store = M.Purchases;
                var p = store.NoAdsProduct;
                bool owned = store.NoAdsOwned;
                if (owned)
                {
                    price.SetLabel("Owned");
                    price.SetStyle(ButtonStyle.Success);
                    price.Interactable = false;
                    price.OnDisabledClick = () => Toast.Show("No Ads is active. Thank you!", Icon.Check, Theme.Success);
                }
                else
                {
                    bool buying = p != null && store.PurchasingProduct == p.Id;
                    price.SetStyle(ButtonStyle.Gold);
                    price.SetLabel(buying ? "..." : p != null && p.Available ? "Buy for " + p.Price
                        : store.State == StoreState.Initialising || store.State == StoreState.NotStarted ? "Connecting to the store..." : "Unavailable");
                    price.Interactable = p != null && p.Available && store.State == StoreState.Ready && !store.IsPurchasing && !store.IsRestoring;
                    price.OnDisabledClick = () => Toast.Show(store.State == StoreState.Unavailable
                        ? "The store isn't available right now. Check your connection and try again." : store.IsPurchasing ? "Another purchase is in progress." : "Connecting to the store...", Icon.Info, Theme.TextSecondary);
                }
                restore.Interactable = store.State == StoreState.Ready && !store.IsPurchasing && !store.IsRestoring;
                restore.SetLabel(store.IsRestoring ? "..." : "Restore");
                restore.OnDisabledClick = () => Toast.Show(store.State == StoreState.Ready ? "Please wait for the current purchase to finish." : "The store isn't available right now.", Icon.Info, Theme.TextSecondary);
                status.text = owned
                    ? "No Ads is active: no adverts between levels."
                    : store.State == StoreState.Unavailable
                        ? "The store isn't available right now: " + store.UnavailableReason
                        : "One payment through " + MonetisationHost.StoreName + ", linked to your account and restored on a new device.";
            }

            void Buy()
            {
                var p = M.Purchases.NoAdsProduct;
                if (p == null) return;
                var r = M.Purchases.Buy(p.Id);
                if (r == PurchaseStart.Started) Refresh();
                else Toast.Show(PurchaseStartMessage(r), Icon.Info, Theme.TextSecondary);
            }

            void Restore()
            {
                if (M.Purchases.Restore()) Refresh();
                else Toast.Show("The store isn't available right now.", Icon.Info, Theme.TextSecondary);
            }
        }
    }
}
