using System;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    public sealed class ConfirmPopup : PopupBase
    {
        const float MessageTop = 160f;
        const float MessageBottom = 240f;

        Text title;
        Text message;
        CCButton confirm;
        CCButton cancel;
        Action onConfirm;

        protected override float PanelHeight => 640f;

        protected override void Build()
        {
            title = Title("", false, 50);
            message = UI.Text(Content, "", Theme.FontBody, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Message");
            message.rectTransform.Fill(70, MessageBottom, 70, MessageTop);
            confirm = FooterButton("OK", ButtonStyle.Primary, () =>
            {
                var a = onConfirm;
                Close();
                a?.Invoke();
            }, null, 60, 140, 500, 60);
            cancel = FooterButton("Cancel", ButtonStyle.Ghost, Close, null, 60, 140, 60, 500);
        }

        public void Setup(string titleText, string body, string confirmLabel, Action onConfirmed, ButtonStyle style = ButtonStyle.Primary)
        {
            title.text = titleText;
            message.text = body;
            confirm.SetLabel(confirmLabel);
            confirm.SetStyle(style);
            onConfirm = onConfirmed;
            // The message's width is fixed by the panel, so its wrapped height is known now.
            SetHeight(MessageTop + message.preferredHeight + 20f + MessageBottom, 640f);
        }

        /// <summary>A message with a single button that just closes it.</summary>
        public void SetupInfo(string titleText, string body, TextAnchor align = TextAnchor.MiddleCenter)
        {
            message.alignment = align;
            Setup(titleText, body, "OK", null, ButtonStyle.Secondary);
            cancel.gameObject.SetActive(false);
            ((RectTransform)confirm.transform).BottomStrip(140, 60, 60, 60);
        }
    }

    /// <summary>Pause menu. Pausing stops the Weekly Rush timer and hides its board.</summary>
    public sealed class PausePopup : PopupBase
    {
        GameScreen game;
        protected override float PanelHeight => 1180f;

        protected override void Build()
        {
            Title("Paused", false, 50);
            var list = UI.Rect("Options", Content);
            list.TopStrip(390, 180, 70, 70);
            UI.VStack(list, 18);
            ToggleRow(list, Icon.SoundOn, "Sound effects", Ctx.Settings.SfxOn, on => Ctx.Settings.SfxOn = on);
            ToggleRow(list, Icon.Music, "Music", Ctx.Settings.MusicOn, on => Ctx.Settings.MusicOn = on);
            ToggleRow(list, Icon.Eye, "Symbols on tiles", Ctx.Settings.Symbols, on => Ctx.Settings.Symbols = on);
            FooterButton("Resume", ButtonStyle.Primary, Close, Icon.Play, 400, 150);
            FooterButton("Restart", ButtonStyle.Secondary, () => { Close(); if (game != null) game.RestartRun(); }, Icon.Restart, 226, 140);
            FooterButton("Quit", ButtonStyle.Ghost, () => { Close(); if (game != null) game.QuitRun(); }, Icon.Home, 60, 140);
        }

        public static void ToggleRow(Transform parent, Icon icon, string label, bool value, Action<bool> changed)
        {
            var row = UI.Rect("Row_" + label, parent);
            row.Layout(preferredHeight: 110);
            var bg = UI.Rounded(row, new Color(0, 0, 0, 0.22f), 34, "Bg");
            bg.rectTransform.Fill();
            var i = UI.Icon(row, icon, Theme.TextSecondary, 60);
            i.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(60, 60));
            var t = UI.Text(row, label, Theme.FontBody, Theme.Text, TextAnchor.MiddleLeft, true, "Label");
            t.rectTransform.Fill(120, 0, 200, 0);
            var toggle = ToggleSwitch.Create(row, value, changed);
            ((RectTransform)toggle.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(150, 84));
        }

        public void Setup(GameScreen screen, string quitLabel)
        {
            game = screen;
            Closed += () =>
            {
                if (game != null) game.OnPauseClosed();
            };
            var quit = Content.Find("Button_Quit");
            if (quit != null) quit.GetComponent<CCButton>().SetLabel(quitLabel);
        }
    }

    /// <summary>Campaign level card opened from the map.</summary>
    public sealed class LevelPopup : PopupBase
    {
        protected override float PanelHeight => 1150f;
        Text title;
        Text region;
        StarRow stars;
        RectTransform info;
        Text reward;
        int level;

        protected override void Build()
        {
            title = Title("Level", true, 46);
            region = UI.Text(Content, "", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Region");
            region.rectTransform.TopStrip(50, 140, 80, 80);
            stars = StarRow.Create(Content, 120, 14, true);
            ((RectTransform)stars.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(400, 160));
            info = UI.Rect("Info", Content);
            info.TopStrip(420, 400, 70, 70);
            UI.VStack(info, 14);
            reward = UI.Chip(Content, "", new Color(0, 0, 0, 0.25f), Theme.Gold, Theme.FontSmall, "Reward");
            reward.transform.parent.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 236), new Vector2(560, 72));
            FooterButton("Play", ButtonStyle.Primary, () =>
            {
                Close();
                Nav.Show(ScreenId.Game, GameArgs.ForCampaign(level));
            }, Icon.Play, 60, 156);
        }

        public void Setup(int levelNumber)
        {
            level = levelNumber;
            var spec = Ctx.Catalog.Get(level);
            var reg = Ctx.Catalog.RegionForLevel(level);
            title.text = "Level " + level;
            region.text = reg.Name.ToUpperInvariant() + (spec.IsMilestone ? "  ·  MILESTONE" : "");
            stars.Set(Ctx.Campaign.StarsFor(level));
            UI.ClearChildren(info);
            // Four rows fit: the goals (or the target), the moves, then the board and a note while there is room.
            int rows = 0;
            if (spec.Goals != null && spec.Goals.Length > 0)
            {
                foreach (var g in spec.Goals)
                {
                    InfoRow(info, Icon.Target, GoalVisuals.Describe(g), "");
                    rows++;
                }
            }
            else
            {
                InfoRow(info, Icon.Target, "Target score", NumberFormat.Thousands(spec.Target));
                rows++;
            }
            InfoRow(info, Icon.Restart, "Moves", spec.Moves.ToString());
            rows++;
            if (rows < 4)
            {
                InfoRow(info, Icon.Map, "Board", spec.Width + " × " + spec.Height + "  ·  " + spec.Colours + " colours" + (spec.StoneCount > 0 ? "  ·  stones" : ""));
                rows++;
            }
            if (rows < 4)
            {
                if (Ctx.Campaign.IsCompleted(level)) InfoRow(info, Icon.Trophy, "Your best", NumberFormat.Thousands(Ctx.Campaign.BestScore(level)));
                else if (!string.IsNullOrEmpty(spec.Note)) InfoRow(info, Icon.Info, spec.Note, "");
            }
            int coins = Ctx.Rewards.FirstClearPreview(spec);
            reward.text = coins > 0 ? "First clear:  +" + coins + " coins" : "Replay to beat your best and earn new stars";
            reward.color = coins > 0 ? Theme.Gold : Theme.TextSecondary;
        }

        public static void InfoRow(Transform parent, Icon icon, string label, string value)
        {
            var row = UI.Rect("Row", parent);
            row.Layout(preferredHeight: 92);
            var bg = UI.Rounded(row, new Color(0, 0, 0, 0.2f), 30, "Bg");
            bg.rectTransform.Fill();
            var i = UI.Icon(row, icon, Theme.TextSecondary, 52);
            i.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56, 0), new Vector2(52, 52));
            var l = UI.FitText(row, label, Theme.FontBody - 2, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Label");
            l.rectTransform.Fill(104, 0, string.IsNullOrEmpty(value) ? 24 : 300, 0);
            if (!string.IsNullOrEmpty(value))
            {
                var v = UI.FitText(row, value, Theme.FontBody, Theme.Text, TextAnchor.MiddleRight, true, "Value");
                v.rectTransform.Fill(300, 0, 28, 0);
            }
        }
    }

    /// <summary>Introduction card for Endless, Daily Challenge and Weekly Rush.</summary>
    public sealed class ModePopup : PopupBase
    {
        protected override float PanelHeight => 1240f;
        GameMode mode;
        RectTransform body;
        Text title;
        Image badge;
        Image badgeIcon;
        CCButton play;

        protected override void Build()
        {
            title = Title("", true, 46);
            var circle = UI.Image(Content, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Teal, "Badge");
            circle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(170, 170));
            UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.6f), "Gloss").rectTransform.Fill();
            badge = circle;
            badgeIcon = UI.Icon(circle.rectTransform, Icon.Infinity, Color.white, 96);
            badgeIcon.rectTransform.Centre(96, 96);
            body = UI.Rect("Body", Content);
            body.Fill(64, 250, 64, 360);
            UI.VStack(body, 14);
            play = FooterButton("Play", ButtonStyle.Primary, Play, Icon.Play, 60, 156);
        }

        public void Setup(GameMode m)
        {
            mode = m;
            UI.ClearChildren(body);
            var now = Ctx.Clock.UtcNow;
            switch (m)
            {
                case GameMode.Endless:
                    {
                        title.text = "Endless";
                        badge.color = Theme.Teal;
                        badgeIcon.sprite = ArtLibrary.Instance.Icon(Icon.Infinity);
                        Para("A fresh board every run and no timer. Each group costs a move, and big groups of 6 or more win bonus moves. How long can you keep the cascade going?");
                        var e = Ctx.Data.Endless;
                        LevelPopup.InfoRow(body, Icon.Trophy, "Best score", e.BestScore > 0 ? NumberFormat.Thousands(e.BestScore) : "—");
                        LevelPopup.InfoRow(body, Icon.Star, "Biggest group", e.BestGroup > 0 ? e.BestGroup + " tiles" : "—");
                        LevelPopup.InfoRow(body, Icon.Gift, "Score milestones", "one-time coin rewards");
                        break;
                    }
                case GameMode.Daily:
                    {
                        title.text = "Daily Challenge";
                        badge.color = Theme.Gold;
                        badgeIcon.sprite = ArtLibrary.Instance.Icon(Icon.Calendar);
                        var spec = Ctx.Challenges.Daily(Ctx.TodayKey);
                        var rec = Ctx.TodayRecord;
                        Para("Today's puzzle is the same for everyone. You have " + spec.Moves + " moves and no timer. Reach the target to complete it.");
                        LevelPopup.InfoRow(body, Icon.Target, "Target  ·  2  ·  3 stars", NumberFormat.Thousands(spec.Target) + " · " + NumberFormat.Thousands(spec.Star2) + " · " + NumberFormat.Thousands(spec.Star3));
                        LevelPopup.InfoRow(body, Icon.Trophy, "Your best today", rec != null && rec.Plays > 0 ? NumberFormat.Thousands(rec.BestScore) + (rec.Completed ? "  ·  done" : "") : "Not played yet");
                        bool rewarded = Ctx.Rewards.IsClaimed(RewardKeys.DailyComplete(Ctx.TodayKey));
                        LevelPopup.InfoRow(body, Icon.Gift, rewarded ? "Reward collected" : "Completion reward", rewarded ? "Done" : "+" + Ctx.Config.Daily.completionReward + " coins");
                        LevelPopup.InfoRow(body, Icon.Timer, "New puzzle in", NumberFormat.Duration(Ctx.TimeToDailyReset));
                        break;
                    }
                default:
                    {
                        title.text = "Weekly Rush";
                        badge.color = Theme.Primary;
                        badgeIcon.sprite = ArtLibrary.Instance.Icon(Icon.Bolt);
                        var rush = Ctx.Config.Rush;
                        Para(rush.seconds + " seconds on this week's board — the only timed mode. Clear as fast as you can and reach the medal scores for coins.");
                        var rec = Ctx.ThisWeekRecord;
                        LevelPopup.InfoRow(body, Icon.Trophy, "Best this week", rec != null && rec.BestScore > 0 ? NumberFormat.Thousands(rec.BestScore) : "—");
                        for (int i = 0; i < rush.tierScores.Length && i < rush.tierRewards.Length; i++)
                        {
                            bool got = Ctx.Rewards.IsClaimed(RewardKeys.WeeklyTier(Ctx.ThisWeekKey, i));
                            string name = i < rush.tierNames.Length ? rush.tierNames[i] : "Tier " + (i + 1);
                            LevelPopup.InfoRow(body, got ? Icon.Check : Icon.Star, name + "  " + NumberFormat.Thousands(rush.tierScores[i]), got ? "Collected" : "+" + rush.tierRewards[i] + " coins");
                        }
                        LevelPopup.InfoRow(body, Icon.Timer, "Ends in", NumberFormat.Duration(Ctx.TimeToWeeklyReset));
                        break;
                    }
            }
            FitHeight(body, 360f, 250f, 1000f);
        }

        void Para(string text)
        {
            var t = UI.Text(body, text, Theme.FontBody - 2, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Description");
            t.Layout(preferredHeight: 190);
        }

        void Play()
        {
            Close();
            Nav.Show(ScreenId.Game, GameArgs.For(mode));
        }
    }

    /// <summary>Shown the first time a power-up becomes available, with its free trial uses.</summary>
    public sealed class PowerUpIntroPopup : PopupBase
    {
        protected override float PanelHeight => 1040f;
        Image circle;
        Image art;
        Text nameText;
        Text desc;
        Text trial;

        protected override void Build()
        {
            Title("New Power-up!", false, 46);
            circle = UI.Image(Content, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Circle");
            circle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(230, 230));
            UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.6f), "Gloss").rectTransform.Fill();
            art = UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Art");
            art.rectTransform.Centre(190, 190);
            nameText = UI.Text(Content, "", Theme.FontHeading, Theme.Text, TextAnchor.MiddleCenter, true, "Name");
            nameText.rectTransform.TopStrip(80, 420, 60, 60);
            desc = UI.Text(Content, "", Theme.FontBody, Theme.TextSecondary, TextAnchor.UpperCenter, false, "Description");
            desc.rectTransform.TopStrip(170, 515, 80, 80);
            trial = UI.Chip(Content, "", new Color(0.2f, 0.82f, 0.78f, 0.25f), Theme.Teal, Theme.FontBody, "Trial");
            trial.transform.parent.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(620, 84));
            FooterButton("Let's try it!", ButtonStyle.Primary, Close, null, 60, 150);
        }

        public void Setup(PowerUpDefinition def)
        {
            circle.color = Theme.Hex(def.accentHex);
            art.sprite = ArtLibrary.Instance.PowerUp(def.id);
            nameText.text = def.displayName;
            desc.text = def.description;
            trial.text = def.trialUses == 1 ? "1 free use to try it out" : def.trialUses + " free uses to try it out";
            AudioService.Sfx(SfxNames.Unlock);
            circle.transform.localScale = Vector3.zero;
            Tw.Scale(circle.transform, 1f, 0.5f, Ease.OutBackStrong, 0.15f);
        }
    }

    /// <summary>Introduces a campaign mechanic on its first level: a picture, a name and how it works.</summary>
    public sealed class MechanicIntroPopup : PopupBase
    {
        protected override float PanelHeight => 1080f;
        RectTransform artHolder;
        Text nameText;
        Text desc;

        protected override void Build()
        {
            Title("Something new!", false, 46);
            var glow = UI.Image(Content, ArtLibrary.Instance.Get(ArtNames.Glow), new Color(1, 1, 1, 0.35f), "Glow");
            glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(330, 330));
            artHolder = UI.Rect("Art", Content);
            artHolder.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(230, 230));
            nameText = UI.Text(Content, "", Theme.FontHeading, Theme.Text, TextAnchor.MiddleCenter, true, "Name");
            nameText.rectTransform.TopStrip(80, 420, 60, 60);
            desc = UI.Text(Content, "", Theme.FontBody, Theme.TextSecondary, TextAnchor.UpperCenter, false, "Description");
            desc.rectTransform.TopStrip(330, 515, 70, 70);
            FooterButton("Got it!", ButtonStyle.Primary, Close, null, 60, 150);
        }

        public void Setup(MechanicTips.Tip tip)
        {
            UI.ClearChildren(artHolder);
            Image art;
            if (!string.IsNullOrEmpty(tip.ArtTile))
            {
                art = UI.Image(artHolder, ArtLibrary.Instance.Get(tip.ArtTile), Color.white, "Piece");
                art.rectTransform.Fill();
            }
            else
            {
                art = UI.Image(artHolder, ArtLibrary.Instance.Tile(tip.ArtColour), Color.white, "Tile");
                art.rectTransform.Fill();
                if (!string.IsNullOrEmpty(tip.ArtOverlay)) UI.Image(art.rectTransform, ArtLibrary.Instance.Get(tip.ArtOverlay), Color.white, "Overlay").rectTransform.Fill();
            }
            nameText.text = tip.Title;
            desc.text = tip.Body;
            AudioService.Sfx(SfxNames.Unlock);
            artHolder.localScale = Vector3.zero;
            Tw.Scale(artHolder, 1f, 0.5f, Ease.OutBackStrong, 0.15f);
        }
    }

    /// <summary>Buy a power-up without leaving the level (Campaign and Endless only; the shop service enforces this).</summary>
    public sealed class QuickBuyPopup : PopupBase
    {
        protected override float PanelHeight => 980f;
        PowerUpDefinition def;
        Image circle;
        Image art;
        Text nameText;
        Text desc;
        Text status;
        CCButton buy;
        Action bought;

        protected override void Build()
        {
            Title("Get more", true, 46);
            circle = UI.Image(Content, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Circle");
            circle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(190, 190));
            art = UI.Image(circle.rectTransform, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Art");
            art.rectTransform.Centre(160, 160);
            nameText = UI.Text(Content, "", Theme.FontHeading, Theme.Text, TextAnchor.MiddleCenter, true, "Name");
            nameText.rectTransform.TopStrip(80, 370, 60, 60);
            desc = UI.Text(Content, "", Theme.FontBody - 2, Theme.TextSecondary, TextAnchor.UpperCenter, false, "Description");
            desc.rectTransform.TopStrip(120, 460, 80, 80);
            status = UI.Text(Content, "", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Status");
            status.rectTransform.BottomStrip(70, 230, 60, 60);
            buy = FooterButton("Buy", ButtonStyle.Gold, Buy, null, 60, 150);
            buy.OnDisabledClick = () => Toast.Show(Ctx.Shop.Check(def.id).Message, Icon.Info, Theme.Danger);
        }

        public void Setup(PowerUpDefinition definition, Action onBought)
        {
            def = definition;
            bought = onBought;
            circle.color = Theme.Hex(def.accentHex);
            art.sprite = ArtLibrary.Instance.PowerUp(def.id);
            nameText.text = def.displayName;
            desc.text = def.description;
            Refresh();
        }

        void Refresh()
        {
            var check = Ctx.Shop.Check(def.id);
            buy.SetLabel("Buy for " + NumberFormat.Thousands(def.price));
            buy.Interactable = check.Allowed;
            status.text = check.Allowed
                ? "You have " + NumberFormat.Thousands(Ctx.Wallet.Balance) + " coins"
                : check.Message + (check.Block == PurchaseBlock.InsufficientFunds ? "\nEarn coins by clearing new levels and daily challenges." : "");
            status.color = check.Allowed ? Theme.TextSecondary : Theme.Danger;
        }

        void Buy()
        {
            var result = Ctx.Shop.TryBuy(def.id);
            if (!result.Allowed)
            {
                Refresh();
                Toast.Show(result.Message, Icon.Info, Theme.Danger);
                return;
            }
            Ctx.Save();
            AudioService.Sfx(SfxNames.Purchase);
            Haptics.Medium();
            var cb = bought;
            Close();
            cb?.Invoke();
        }
    }
}
