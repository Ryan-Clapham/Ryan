using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Six short pages. Pages 1 and 3 contain real, playable practice boards running on the same engine as the
    /// game (Practice mode: no score target, no moves, no rewards, nothing saved).
    /// </summary>
    public sealed class HowToPlayScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.HowToPlay;

        RectTransform page;
        Text pageTitle;
        Text pageBody;
        RectTransform stage;
        readonly List<Image> dots = new List<Image>();
        CCButton prev;
        CCButton next;
        int index;
        PuzzleSession practice;
        BoardView practiceBoard;
        readonly MoveOutcome outcome = new MoveOutcome();

        static readonly string[] Titles =
        {
            "Match colours", "Bigger is better", "Falling tiles & stones", "Levels and stars", "Coins and power-ups", "Game modes",
        };

        protected override void Build()
        {
            TopBar("How to Play", () => Nav.Back());
            page = UI.Rect("Page", Safe);
            page.Fill(Theme.Margin, 220, Theme.Margin, 170);
            var card = UI.Card(page, "Card", Theme.Surface, 56);
            pageTitle = UI.FitText(card, "", Theme.FontTitle - 6, Theme.Text, TextAnchor.MiddleCenter, true, "Title");
            pageTitle.rectTransform.TopStrip(100, 30, 40, 40);
            UI.AddShadow(pageTitle, -4, 0.35f);
            stage = UI.Rect("Stage", card);
            stage.Fill(30, 330, 30, 140);
            pageBody = UI.Text(card, "", Theme.FontBody - 2, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Body");
            pageBody.rectTransform.BottomStrip(300, 20, 50, 50);

            var nav = UI.Rect("Nav", Safe);
            nav.BottomStrip(150, 40, Theme.Margin, Theme.Margin);
            prev = UI.IconButton(nav, Icon.Back, ButtonStyle.Secondary, () => Go(index - 1), 130, "Prev");
            ((RectTransform)prev.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(130, 130));
            next = UI.Button(nav, "Next", ButtonStyle.Primary, () =>
            {
                if (index >= Titles.Length - 1) Nav.Back();
                else Go(index + 1);
            }, Icon.Forward, Theme.FontLarge, 46, 10, "Next");
            ((RectTransform)next.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(400, 140));
            var dotRow = UI.Rect("Dots", nav);
            dotRow.Centre(320, 40, -40, 0);
            var h = UI.HStack(dotRow, 14, null, TextAnchor.MiddleCenter, false);
            h.childControlWidth = false;
            h.childControlHeight = false;
            for (int i = 0; i < Titles.Length; i++)
            {
                var d = UI.Image(dotRow, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Dot" + i);
                d.rectTransform.sizeDelta = new Vector2(26, 26);
                dots.Add(d);
            }
        }

        public override void OnShow(object args)
        {
            App.SetBackground(-1);
            Go(0);
        }

        public override void OnHide()
        {
            ClearStage();
        }

        void Go(int i)
        {
            index = Mathf.Clamp(i, 0, Titles.Length - 1);
            pageTitle.text = Titles[index];
            for (int d = 0; d < dots.Count; d++)
            {
                dots[d].color = d == index ? Theme.Gold : new Color(1, 1, 1, 0.25f);
                dots[d].rectTransform.sizeDelta = d == index ? new Vector2(34, 34) : new Vector2(24, 24);
            }
            prev.gameObject.SetActive(index > 0);
            next.SetLabel(index >= Titles.Length - 1 ? "Let's play!" : "Next");
            ClearStage();
            switch (index)
            {
                case 0: PageMatch(); break;
                case 1: PageScoring(); break;
                case 2: PageGravity(); break;
                case 3: PageLevels(); break;
                case 4: PageEconomy(); break;
                default: PageModes(); break;
            }
            stage.localScale = Vector3.one * 0.95f;
            Tw.Scale(stage, 1f, 0.25f, Ease.OutBack);
        }

        void ClearStage()
        {
            CoachMark.Hide();
            practice = null;
            practiceBoard = null;
            UI.ClearChildren(stage);
        }

        // ------------------------------------------------------------------ Pages

        void PageMatch()
        {
            pageBody.text = "Tap a group of <b>2 or more</b> touching tiles of the same colour to clear it. Tiles touch side by side or above and below; diagonals don't count.\n<color=#FFC23A>Try it on this board!</color>";
            var layout = LevelLayout.Parse(new[] { "RRYBB", "RYYBR", "BBYRR", "BRRYY", "YYBBR" });
            StartPractice(layout, 3);
        }

        void PageScoring()
        {
            var scoring = Ctx.Config.Scoring;
            pageBody.text = "Points grow much faster than group size: <b>points = tiles × tiles × " + scoring.pointsPerTile + "</b>. Hold a finger on a group to preview its points before you let go.";
            var list = UI.Rect("Table", stage);
            list.Fill(30, 0, 30, 0);
            UI.VStack(list, 14, null, TextAnchor.MiddleCenter);
            int[] sizes = { 2, 3, 5, 8, 12 };
            foreach (int n in sizes)
            {
                var row = UI.Rect("Row" + n, list);
                row.Layout(preferredHeight: 92);
                UI.Rounded(row, new Color(0, 0, 0, 0.22f), 30, "Bg").rectTransform.Fill();
                var tiles = UI.Rect("Tiles", row);
                tiles.Fill(24, 0, 300, 0);
                var h = UI.HStack(tiles, 4, null, TextAnchor.MiddleLeft, false);
                h.childControlWidth = false;
                h.childControlHeight = false;
                int colour = n % 5;
                for (int k = 0; k < n; k++)
                {
                    var t = UI.Image(tiles, ArtLibrary.Instance.Tile(colour), Color.white, "T");
                    t.rectTransform.sizeDelta = new Vector2(38, 38);
                }
                var label = UI.Text(row, n + " tiles", Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleRight, true, "Count");
                label.rectTransform.Fill(0, 0, 160, 0);
                var pts = UI.Text(row, NumberFormat.Thousands(scoring.GroupPoints(n)), Theme.FontBody, Theme.Gold, TextAnchor.MiddleRight, true, "Points");
                pts.rectTransform.Fill(0, 0, 24, 0);
            }
        }

        void PageGravity()
        {
            pageBody.text = "Tiles above a gap fall down and new tiles drop in. <b>Stones</b> never move: tiles can't fall through them, so each section refills on its own. If no group is left, the board reshuffles for free.";
            var layout = LevelLayout.Parse(new[] { ".....", "#...#", ".....", "..#..", "....." });
            StartPractice(layout, 3);
        }

        void PageLevels()
        {
            pageBody.text = "Each level has a <b>target score</b> and a number of <b>moves</b>. Reach the target to win; your spare moves become bonus points. Score higher for up to <b>3 stars</b>. Replays can only improve your stars.";
            var mock = UI.Rect("Mock", stage);
            mock.Centre(820, 230, 0, 120);
            var card = UI.Card(mock, "Card", Theme.SurfaceHigh, 44);
            UI.Text(card, "SCORE", Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft, true, "Label").rectTransform.Fill(36, 0, 240, 24);
            UI.Text(card, "1,840", 70, Theme.Text, TextAnchor.UpperLeft, true, "Score").rectTransform.Fill(34, 0, 240, 52);
            var bar = ProgressBar.Create(card, Theme.Gold, 34);
            ((RectTransform)bar.transform).BottomStrip(56, 24, 36, 240);
            bar.SetMarkers(0.6f, 0.8f, 1f);
            bar.SetFraction(0.62f, false);
            bar.SetMarkerLit(0, true);
            var circle = UI.Image(card, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Secondary, "Moves");
            circle.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(180, 180));
            UI.Text(circle.rectTransform, "7", 76, Theme.Text, TextAnchor.MiddleCenter, true, "N").rectTransform.Fill(0, 36, 0, 8);
            UI.Text(circle.rectTransform, "MOVES", Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "L").rectTransform.Fill(0, 26, 0, 116);
            var stars = StarRow.Create(stage, 120, 16, true);
            ((RectTransform)stars.transform).Centre(420, 160, 0, -150);
            stars.Reveal(3, 0.3f);
        }

        void PageEconomy()
        {
            var first = Ctx.PowerUps.All.Count > 0 ? Ctx.PowerUps.All[0].unlockLevel : 10;
            pageBody.text = "Earn <color=#FFC23A>coins</color> by clearing new levels, earning stars, reaching milestones and completing challenges. Spend them in the Shop on <b>power-ups</b>, which unlock from level " + first + " — each new one comes with free tries.";
            var grid = UI.Rect("Grid", stage);
            grid.Fill(10, 0, 10, 0);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(250, 250);
            g.spacing = new Vector2(16, 16);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            foreach (var def in Ctx.PowerUps.All)
            {
                var cell = UI.Rect("PU_" + def.id, grid);
                var circle = UI.Image(cell, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Hex(def.accentHex), "Circle");
                circle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(150, 150));
                UI.Image(circle.rectTransform, ArtLibrary.Instance.PowerUp(def.id), Color.white, "Art").rectTransform.Centre(130, 130);
                var n = UI.FitText(cell, def.displayName, Theme.FontSmall, Theme.Text, TextAnchor.MiddleCenter, true, "Name");
                n.rectTransform.BottomStrip(44, 42, 0, 0);
                var u = UI.FitText(cell, "Level " + def.unlockLevel + "  ·  " + def.price + " coins", Theme.FontTiny - 3, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Unlock");
                u.rectTransform.BottomStrip(36, 4, 0, 0);
            }
        }

        void PageModes()
        {
            pageBody.text = "Only Weekly Rush has a timer. Daily Challenge and Weekly Rush are the same for everyone, so power-ups are switched off there.";
            var list = UI.Rect("Modes", stage);
            list.Fill(10, 0, 10, 0);
            UI.VStack(list, 14, null, TextAnchor.MiddleCenter);
            ModeRow(list, Icon.Map, Theme.Primary, "Campaign", "Travel the map. Reach each level's target before your moves run out.");
            ModeRow(list, Icon.Infinity, Theme.Teal, "Endless", "A fresh board every run, no timer. Groups of 6+ earn bonus moves.");
            ModeRow(list, Icon.Calendar, Theme.Gold, "Daily Challenge", "One puzzle per day, the same for everyone. No timer.");
            ModeRow(list, Icon.Bolt, Theme.Danger, "Weekly Rush", Ctx.Config.Rush.seconds + " seconds on this week's board. The only timed mode.");
        }

        static void ModeRow(RectTransform parent, Icon icon, Color accent, string name, string text)
        {
            var row = UI.Rect(name, parent);
            row.Layout(preferredHeight: 150);
            UI.Rounded(row, new Color(0, 0, 0, 0.22f), 34, "Bg").rectTransform.Fill();
            var circle = UI.Image(row, ArtLibrary.Instance.Get(ArtNames.Circle), accent, "Circle");
            circle.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(76, 0), new Vector2(104, 104));
            UI.Icon(circle.rectTransform, icon, Color.white, 58).rectTransform.Centre(58, 58);
            var t = UI.FitText(row, name, Theme.FontBody, Theme.Text, TextAnchor.MiddleLeft, true, "Name");
            t.rectTransform.TopStrip(54, 18, 150, 24);
            var d = UI.Text(row, text, Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft, false, "Text");
            d.rectTransform.Fill(150, 10, 24, 72);
        }

        // ------------------------------------------------------------------ Practice board

        void StartPractice(LevelLayout layout, int colours)
        {
            var area = UI.Rect("Practice", stage);
            area.Fill();
            lastSize = Vector2.zero;
            practiceBoard = BoardView.Create(area);
            var setup = Ctx.CreatePracticeRun(layout, colours, (ulong)DateTime.UtcNow.Ticks);
            practice = new PuzzleSession(setup);
            practice.Start();
            practiceBoard.Bind(practice, Ctx.Settings.Symbols);
            practiceBoard.PreviewProvider = (cell, cells) =>
            {
                if (practice == null) return 0;
                int n = practice.PreviewGroup(cell, cells);
                if (n < 2)
                {
                    cells.Clear();
                    return 0;
                }
                return n;
            };
            practiceBoard.Committed = cell =>
            {
                if (practice == null || !practice.TryPlay(cell, outcome)) return;
                practiceBoard.Animate(outcome);
                if (AudioService.Instance != null) AudioService.Instance.PlayPop(outcome.GroupSize);
                Haptics.Light();
                practiceBoard.Fx.FloatText(practiceBoard.CellInFx(outcome.Origin), "+" + NumberFormat.Thousands(outcome.ScoreGained), Theme.Gold, 64, 110f, 0.8f);
                CoachMark.Hide();
            };
            practiceBoard.InvalidPress = cell =>
            {
                practiceBoard.Shake(cell);
                AudioService.Sfx(SfxNames.Invalid, 0.6f);
            };
            practiceBoard.ShuffleStarted = () => AudioService.Sfx(SfxNames.Whoosh);
            Canvas.ForceUpdateCanvases();
            practiceBoard.FitInto(area.rect.size);
        }

        void LateUpdate()
        {
            if (practiceBoard == null) return;
            var area = (RectTransform)practiceBoard.transform.parent;
            var size = area.rect.size;
            if (Mathf.Abs(size.x - lastSize.x) > 0.5f || Mathf.Abs(size.y - lastSize.y) > 0.5f)
            {
                lastSize = size;
                practiceBoard.FitInto(size);
            }
        }

        Vector2 lastSize;
    }
}
