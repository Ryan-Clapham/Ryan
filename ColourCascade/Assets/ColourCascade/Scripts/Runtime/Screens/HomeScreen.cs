using System.Collections;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// The hub. Hierarchy: one big "Play Level N" action, the world map beside it, three challenge modes,
    /// then the shop and How to Play. Settings and coins sit in the top corners.
    /// </summary>
    public sealed class HomeScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Home;

        Text regionLabel;
        Text levelLabel;
        Text levelNote;
        Text starsLabel;
        CCButton play;
        Text endlessStatus;
        Text dailyStatus;
        Text weeklyStatus;
        Image dailyDot;
        RectTransform logo;
        bool introPlayed;
        Coroutine pulse;
        float nextRefresh;

        protected override void Build()
        {
            var column = UI.Rect("Column", Safe);
            column.Fill(Theme.Margin, 30, Theme.Margin, 16);
            UI.VStack(column, 0);

            // Top row: settings (left) and coins (right).
            var top = UI.Rect("TopRow", column);
            top.Layout(preferredHeight: 120);
            var gear = UI.IconButton(top, Icon.Gear, ButtonStyle.Secondary, () => Nav.Show(ScreenId.Settings), Theme.IconButtonSize, "Settings");
            ((RectTransform)gear.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(Theme.IconButtonSize, Theme.IconButtonSize));
            var coins = CoinCounter.Create(top, () => Nav.Show(ScreenId.Shop, ShopScreen.TabCoins));
            ((RectTransform)coins.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(300, 96));

            UI.Spacer(column, 0, 1);
            logo = UI.Rect("Logo", column);
            logo.Layout(preferredHeight: 400);
            BuildLogo(logo);
            UI.Spacer(column, 24, 1);

            // Campaign card.
            var campaign = UI.Rect("Campaign", column);
            campaign.Layout(preferredHeight: 440);
            var card = UI.Card(campaign, "Card", Theme.Surface, 52);
            regionLabel = UI.FitText(card, "", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Region");
            regionLabel.rectTransform.TopStrip(50, 34, 44, 260);
            var starsHolder = UI.Rect("Stars", card);
            starsHolder.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -24), new Vector2(200, 70));
            var starIcon = UI.Sprite(starsHolder, ArtNames.StarGold, 62);
            starIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 2), new Vector2(62, 62));
            starsLabel = UI.Text(starsHolder, "0", Theme.FontBody, Theme.Text, TextAnchor.MiddleLeft, true, "Count");
            starsLabel.rectTransform.Fill(82, 0, 0, 0);
            levelLabel = UI.Text(card, "Level 1", Theme.FontTitle + 8, Theme.Text, TextAnchor.MiddleLeft, true, "Level");
            levelLabel.rectTransform.TopStrip(96, 90, 44, 44);
            UI.AddShadow(levelLabel, -5, 0.4f);
            levelNote = UI.FitText(card, "", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, false, "Note");
            levelNote.rectTransform.TopStrip(50, 190, 44, 44);
            play = UI.Button(card, "Play", ButtonStyle.Primary, PlayCampaign, Icon.Play, Theme.FontHeading, 50, 12, "Play");
            ((RectTransform)play.transform).BottomStrip(160, 36, 36, 220);
            var map = UI.Button(card, "Map", ButtonStyle.Secondary, () => Nav.Show(ScreenId.Map, new MapArgs()), Icon.Map, Theme.FontBody, 50, 12, "Map");
            ((RectTransform)map.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 36), new Vector2(168, 160));
            map.Label.rectTransform.sizeDelta = new Vector2(10, 46);
            var mapRow = map.Label.transform.parent.GetComponent<HorizontalLayoutGroup>();
            if (mapRow != null)
            {
                mapRow.enabled = false;
                map.IconImage.rectTransform.Centre(64, 64, 0, 22);
                map.Label.rectTransform.Centre(140, 44, 0, -40);
                map.Label.fontSize = Theme.FontSmall;
            }

            UI.Spacer(column, 30, 1);
            var header = UI.Text(column, "CHALLENGES", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "ChallengesHeader");
            header.Layout(preferredHeight: 60);
            var modes = UI.Rect("Modes", column);
            modes.Layout(preferredHeight: 310);
            UI.HStack(modes, 22);
            endlessStatus = ModeCard(modes, "Endless", Icon.Infinity, Theme.Teal, () => Nav.Open<ModePopup>().Setup(GameMode.Endless), out _);
            dailyStatus = ModeCard(modes, "Daily", Icon.Calendar, Theme.Gold, () => Nav.Open<ModePopup>().Setup(GameMode.Daily), out dailyDot);
            weeklyStatus = ModeCard(modes, "Weekly Rush", Icon.Bolt, Theme.Primary, () => Nav.Open<ModePopup>().Setup(GameMode.WeeklyRush), out _);

            UI.Spacer(column, 30, 1);
            var dock = UI.Rect("Dock", column);
            dock.Layout(preferredHeight: 150);
            UI.HStack(dock, 22);
            UI.Button(dock, "Shop", ButtonStyle.Gold, () => Nav.Show(ScreenId.Shop, ShopScreen.TabPowerUps), Icon.Shop, Theme.FontLarge, 44, 10, "Shop");
            UI.Button(dock, "How to Play", ButtonStyle.Secondary, () => Nav.Show(ScreenId.HowToPlay), Icon.Help, Theme.FontBody, 44, 10, "HowToPlay");
        }

        void BuildLogo(RectTransform root)
        {
            // A little cascade of tiles above the word mark.
            int[] colours = { 0, 1, 2, 3, 4 };
            float[] xs = { -240f, -120f, 0f, 120f, 240f };
            float[] ys = { 84f, 120f, 140f, 120f, 84f };
            float[] rot = { 14f, 7f, 0f, -7f, -14f };
            for (int i = 0; i < colours.Length; i++)
            {
                var tile = UI.Image(root, ArtLibrary.Instance.Tile(colours[i]), Color.white, "LogoTile" + i);
                tile.rectTransform.Centre(104, 104, xs[i], ys[i]);
                tile.rectTransform.localEulerAngles = new Vector3(0, 0, rot[i]);
            }
            var word = UI.Text(root, UI.RainbowWord("COLOUR"), 132, Color.white, TextAnchor.MiddleCenter, true, "Colour");
            word.rectTransform.Centre(1000, 150, 0, -20);
            word.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = word.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.02f, 0.16f, 0.9f);
            o.effectDistance = new Vector2(5, -5);
            UI.AddShadow(word, -10, 0.5f);
            var cascade = UI.Text(root, "CASCADE", 104, Color.white, TextAnchor.MiddleCenter, true, "Cascade");
            cascade.rectTransform.Centre(1000, 130, 0, -140);
            cascade.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o2 = cascade.gameObject.AddComponent<Outline>();
            o2.effectColor = new Color(0.05f, 0.02f, 0.16f, 0.9f);
            o2.effectDistance = new Vector2(5, -5);
            UI.AddShadow(cascade, -10, 0.5f);
        }

        Text ModeCard(RectTransform parent, string name, Icon icon, Color accent, System.Action onTap, out Image dot)
        {
            var root = UI.Rect("Mode_" + name, parent);
            var card = UI.Card(root, "Card", Theme.Surface, 44);
            var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), accent, "Circle");
            circle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(124, 124));
            UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss").rectTransform.Fill();
            var i = UI.Icon(circle.rectTransform, icon, Color.white, 70);
            i.rectTransform.Centre(70, 70);
            var title = UI.FitText(card, name, Theme.FontBody, Theme.Text, TextAnchor.MiddleCenter, true, "Title");
            title.rectTransform.BottomStrip(56, 84, 12, 12);
            var status = UI.FitText(card, "", Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Status");
            status.rectTransform.BottomStrip(48, 30, 12, 12);
            dot = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Danger, "Dot");
            dot.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(34, 34));
            dot.gameObject.SetActive(false);
            var btn = root.gameObject.AddComponent<CCButton>();
            var hit = UI.Image(root, ArtLibrary.Instance.Get(ArtNames.Round), new Color(0, 0, 0, 0), "Hit");
            hit.rectTransform.Fill();
            hit.raycastTarget = true;
            btn.OnClick = onTap;
            btn.Configure(ButtonStyle.Ghost, 0);
            return status;
        }

        public override void OnShow(object args)
        {
            App.SetBackground(-1);
            Refresh();
            if (!introPlayed)
            {
                introPlayed = true;
                for (int i = 0; i < logo.childCount; i++)
                {
                    var c = logo.GetChild(i);
                    c.localScale = Vector3.zero;
                    Tw.Scale(c, 1f, 0.45f, Ease.OutBackStrong, 0.1f + i * 0.06f);
                }
            }
            if (pulse != null) StopCoroutine(pulse);
            pulse = !Ctx.Campaign.IsCompleted(1) && !Tw.Reduced ? StartCoroutine(PulsePlay()) : null;
        }

        public override void OnHide()
        {
            if (pulse != null) StopCoroutine(pulse);
            pulse = null;
            if (play != null) play.transform.localScale = Vector3.one;
        }

        IEnumerator PulsePlay()
        {
            var t = play.transform;
            while (true)
            {
                Tw.Scale(t, 1.05f, 0.5f, Ease.InOutQuad);
                yield return new WaitForSecondsRealtime(0.55f);
                Tw.Scale(t, 1f, 0.5f, Ease.InOutQuad);
                yield return new WaitForSecondsRealtime(0.9f);
            }
        }

        void Update()
        {
            if (Time.unscaledTime >= nextRefresh && Ctx != null)
            {
                nextRefresh = Time.unscaledTime + 30f;
                RefreshModes();
            }
        }

        void Refresh()
        {
            int level = Ctx.Campaign.CurrentLevel;
            var region = Ctx.Catalog.RegionForLevel(level);
            regionLabel.text = "REGION " + (region.Index + 1) + "  ·  " + region.Name.ToUpperInvariant();
            levelLabel.text = "Level " + level;
            var spec = Ctx.Catalog.Get(level);
            string note = !string.IsNullOrEmpty(spec.Note) ? spec.Note : "Score " + NumberFormat.Thousands(spec.Target) + " in " + spec.Moves + " moves";
            levelNote.text = note;
            starsLabel.text = Ctx.Campaign.TotalStars.ToString();
            play.SetLabel(Ctx.Campaign.IsCompleted(1) ? "Play Level " + level : "Play");
            RefreshModes();
        }

        void RefreshModes()
        {
            var endless = Ctx.Data.Endless;
            endlessStatus.text = endless.BestScore > 0 ? "Best " + NumberFormat.Thousands(endless.BestScore) : "No timer";

            var today = Ctx.TodayRecord;
            bool done = today != null && today.Completed;
            dailyStatus.text = done ? "Done  ·  " + NumberFormat.Duration(Ctx.TimeToDailyReset) : today != null && today.Plays > 0 ? "Best " + NumberFormat.Thousands(today.BestScore) : "New puzzle!";
            dailyStatus.color = done ? Theme.Success : today == null ? Theme.Gold : Theme.TextSecondary;
            dailyDot.gameObject.SetActive(today == null || today.Plays == 0);

            var week = Ctx.ThisWeekRecord;
            weeklyStatus.text = week != null && week.BestScore > 0 ? "Best " + NumberFormat.Thousands(week.BestScore) : Ctx.Config.Rush.seconds + "s  ·  " + NumberFormat.Duration(Ctx.TimeToWeeklyReset);
        }

        void PlayCampaign()
        {
            Nav.Show(ScreenId.Game, GameArgs.ForCampaign(Ctx.Campaign.CurrentLevel));
        }
    }
}
