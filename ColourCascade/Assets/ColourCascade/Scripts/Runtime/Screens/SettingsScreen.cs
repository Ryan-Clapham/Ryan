using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>Player preferences. Every option here has a real effect and is saved immediately.</summary>
    public sealed class SettingsScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Settings;

        ScrollRect scroll;
        RectTransform list;
        readonly List<Image> previewSymbols = new List<Image>();

        protected override void Build()
        {
            TopBar("Settings", () => Nav.Back());
            var host = UI.Rect("Page", Safe);
            host.Fill(0, 0, 0, 170);
            var viewport = UI.Rect("Viewport", host);
            viewport.Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            scroll = host.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
        }

        public override void OnShow(object args)
        {
            App.SetBackground(-1);
            Rebuild();
        }

        public override void OnHide()
        {
            Ctx.Save();
        }

        void Rebuild()
        {
            if (list != null) Destroy(list.gameObject);
            previewSymbols.Clear();
            list = UI.Rect("List", scroll.viewport);
            list.anchorMin = new Vector2(0, 1);
            list.anchorMax = new Vector2(1, 1);
            list.pivot = new Vector2(0.5f, 1);
            var v = UI.VStack(list, 22, new RectOffset((int)Theme.Margin, (int)Theme.Margin, 10, 80));
            v.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list;

            var s = Ctx.Settings;
            Section("SOUND");
            SliderRow(Icon.SoundOn, "Sound effects", s.SfxVolume, x => s.SfxVolume = x, () => AudioService.Sfx(SfxNames.Pop(3)));
            SliderRow(Icon.Music, "Music", s.MusicVolume, x => s.MusicVolume = x, null);

            Section("GAMEPLAY");
            ToggleRow(Icon.Vibrate, "Vibration", "Short vibrations when you clear tiles", s.Haptics, on =>
            {
                s.Haptics = on;
                if (on) Haptics.Medium();
            });
            ToggleRow(Icon.Eye, "Symbols on tiles", "Gives every colour its own shape, so tiles can be told apart without relying on colour", s.Symbols, on =>
            {
                s.Symbols = on;
                UpdateSymbolPreview(true);
            });
            SymbolPreview();
            ToggleRow(Icon.Sparkle, "Reduced motion", "Fewer particles, no screen shake and calmer animations", s.ReducedMotion, on => s.ReducedMotion = on);

            Section("HELP");
            ActionRow(Icon.Help, "How to Play", ButtonStyle.Secondary, "Open", () => Nav.Show(ScreenId.HowToPlay));
            ActionRow(Icon.Info, "Show tutorial hints again", ButtonStyle.Ghost, "Reset", () =>
            {
                Ctx.Tutorials.ResetAll();
                Ctx.Save();
                Toast.Show("Tutorial hints will appear again", Icon.Check, Theme.Success);
            });

            Section("YOUR PROGRESS");
            var d = Ctx.Data;
            int completed = 0;
            foreach (var r in d.Levels) if (r.Completed) completed++;
            StatsRow(("Levels", completed.ToString()), ("Stars", Ctx.Campaign.TotalStars.ToString()), ("Games", d.Stats.GamesPlayed.ToString()), ("Best group", d.Stats.BiggestGroup.ToString()));
            ActionRow(Icon.Restart, "Reset all progress", ButtonStyle.Danger, "Reset", ConfirmReset);

            if (Application.isEditor || Debug.isDebugBuild) DeveloperTools();

            var footer = UI.Text(list, "Colour Cascade " + Application.version + "\nFont: Fredoka (SIL Open Font License)", Theme.FontTiny, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Footer");
            footer.Layout(preferredHeight: 120);
            scroll.verticalNormalizedPosition = 1f;
        }

        void Section(string title)
        {
            var t = UI.Text(list, title, Theme.FontSmall, Theme.TextSecondary, TextAnchor.LowerLeft, true, "Section");
            t.Layout(preferredHeight: 70);
        }

        RectTransform RowCard(string name, float height)
        {
            var root = UI.Rect(name, list);
            root.Layout(preferredHeight: height);
            return UI.Card(root, "Card", Theme.Surface, 40);
        }

        void SliderRow(Icon icon, string label, float value, System.Action<float> changed, System.Action released)
        {
            var card = RowCard("Slider_" + label, 190);
            var i = UI.Icon(card, icon, Theme.TextSecondary, 60);
            i.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(64, -54), new Vector2(60, 60));
            var t = UI.Text(card, label, Theme.FontBody, Theme.Text, TextAnchor.MiddleLeft, true, "Label");
            t.rectTransform.TopStrip(70, 20, 120, 30);
            var slider = SliderControl.Create(card, value, changed, released);
            ((RectTransform)slider.transform).BottomStrip(84, 20, 20, 20);
        }

        void ToggleRow(Icon icon, string label, string description, bool value, System.Action<bool> changed)
        {
            var card = RowCard("Toggle_" + label, 200);
            var i = UI.Icon(card, icon, Theme.TextSecondary, 60);
            i.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(64, -60), new Vector2(60, 60));
            var t = UI.FitText(card, label, Theme.FontBody, Theme.Text, TextAnchor.MiddleLeft, true, "Label");
            t.rectTransform.TopStrip(70, 26, 120, 210);
            var dsc = UI.Text(card, description, Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Description");
            dsc.rectTransform.Fill(120, 14, 210, 98);
            var toggle = ToggleSwitch.Create(card, value, changed);
            ((RectTransform)toggle.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(150, 84));
        }

        void SymbolPreview()
        {
            var card = RowCard("SymbolPreview", 170);
            var row = UI.Rect("Row", card);
            row.Fill(30, 20, 30, 20);
            var h = UI.HStack(row, 14, null, TextAnchor.MiddleCenter, false);
            h.childControlWidth = false;
            h.childControlHeight = false;
            for (int c = 0; c < TileColours.Max; c++)
            {
                var tile = UI.Image(row, ArtLibrary.Instance.Tile(c), Color.white, "Tile" + c);
                tile.rectTransform.sizeDelta = new Vector2(124, 124);
                var sym = UI.Image(tile.rectTransform, ArtLibrary.Instance.Symbol(c), Color.white, "Symbol");
                sym.rectTransform.Centre(68, 68, 0, 2);
                previewSymbols.Add(sym);
            }
            UpdateSymbolPreview(false);
        }

        void UpdateSymbolPreview(bool animate)
        {
            bool on = Ctx.Settings.Symbols;
            foreach (var s in previewSymbols)
            {
                if (s == null) continue;
                bool was = s.gameObject.activeSelf;
                s.gameObject.SetActive(on);
                if (on && !was && animate)
                {
                    s.transform.localScale = Vector3.zero;
                    Tw.Scale(s.transform, 1f, 0.3f, Ease.OutBack);
                }
            }
        }

        void ActionRow(Icon icon, string label, ButtonStyle style, string action, System.Action onClick)
        {
            var card = RowCard("Action_" + label, 150);
            var i = UI.Icon(card, icon, Theme.TextSecondary, 60);
            i.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(60, 60));
            var t = UI.FitText(card, label, Theme.FontBody, Theme.Text, TextAnchor.MiddleLeft, true, "Label");
            t.rectTransform.Fill(120, 0, 240, 0);
            var b = UI.Button(card, action, style, onClick, null, Theme.FontBody - 2, 36, 8, "Action");
            ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 104));
        }

        void StatsRow(params (string label, string value)[] stats)
        {
            var root = UI.Rect("Stats", list);
            root.Layout(preferredHeight: 160);
            UI.HStack(root, 16);
            foreach (var s in stats)
            {
                var cell = UI.Rect(s.label, root);
                var card = UI.Card(cell, "Card", Theme.Surface, 36);
                var v = UI.FitText(card, s.value, Theme.FontHeading, Theme.Text, TextAnchor.MiddleCenter, true, "Value");
                v.rectTransform.Fill(8, 56, 8, 14);
                var l = UI.FitText(card, s.label, Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Label");
                l.rectTransform.Fill(8, 18, 8, 104);
            }
        }

        void ConfirmReset()
        {
            Nav.Open<ConfirmPopup>().Setup("Reset progress?", "This erases your levels, stars, coins, power-ups and records. It can't be undone.", "Continue", () =>
            {
                Nav.Open<ConfirmPopup>().Setup("Are you sure?", "Everything will be erased permanently.", "Erase all", () =>
                {
                    Ctx.ResetProgress();
                    Toast.Show("Progress reset", Icon.Restart);
                    Rebuild();
                }, ButtonStyle.Danger);
            }, ButtonStyle.Danger);
        }

        /// <summary>Testing aids, only in the Editor and development builds.</summary>
        void DeveloperTools()
        {
            Section("DEVELOPER (EDITOR / DEV BUILDS ONLY)");
            ActionRow(Icon.Plus, "Add 500 coins", ButtonStyle.Ghost, "+500", () =>
            {
                Ctx.Wallet.Credit(500, "debug.grant");
                Ctx.Save();
            });
            ActionRow(Icon.Forward, "Unlock next 10 levels", ButtonStyle.Ghost, "Unlock", () =>
            {
                Ctx.Data.HighestUnlocked += 10;
                Ctx.Save();
                Toast.Show("Unlocked up to level " + Ctx.Data.HighestUnlocked, Icon.Check);
            });
            ActionRow(Icon.Calendar, "Advance clock one day", ButtonStyle.Ghost, "+1 day", () =>
            {
                App.Clock.OffsetDays++;
                Toast.Show("Clock now " + Ctx.TodayKey + " (" + Ctx.ThisWeekKey + ")", Icon.Calendar);
            });
            ActionRow(Icon.Info, "Show coin ledger", ButtonStyle.Ghost, "Show", () =>
            {
                var sb = new System.Text.StringBuilder();
                var ledger = Ctx.Wallet.Ledger;
                for (int i = ledger.Count - 1; i >= 0 && i >= ledger.Count - 8; i--)
                    sb.Append(ledger[i].Delta > 0 ? "+" : "").Append(ledger[i].Delta).Append("  ").Append(ledger[i].Reason).Append("  = ").Append(ledger[i].Balance).Append('\n');
                Nav.Open<ConfirmPopup>().Setup("Recent coins", sb.Length > 0 ? sb.ToString() : "No transactions yet", "OK", null, ButtonStyle.Secondary);
            });
        }
    }
}
