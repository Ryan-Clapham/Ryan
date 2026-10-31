using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>What to play when the game screen opens.</summary>
    public sealed class GameArgs
    {
        public GameMode Mode;
        public int Level;

        public static GameArgs ForCampaign(int level) => new GameArgs { Mode = GameMode.Campaign, Level = level };
        public static GameArgs For(GameMode mode) => new GameArgs { Mode = mode };
    }

    /// <summary>
    /// Plays one run in any mode. The PuzzleSession owns every rule; this screen turns player input into
    /// session calls, animates the outcomes, keeps the HUD in sync and hands finished runs to GameContext.
    /// </summary>
    public sealed class GameScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Game;

        GameArgs args;
        PuzzleSession session;
        readonly MoveOutcome outcome = new MoveOutcome();
        readonly List<GridPos> scratch = new List<GridPos>();

        BoardView board;
        RectTransform boardArea;
        Text title;
        Text subtitle;
        CCButton symbolsButton;
        RectTransform statusCard;
        Text scoreText;
        Text scoreLabel;
        Text targetText;
        ProgressBar progress;
        RectTransform badge;
        Image badgeFill;
        Image badgeRing;
        Text badgeNumber;
        Text badgeLabel;
        RectTransform powerBar;
        RectTransform powerRow;
        Text powerNote;
        readonly List<PowerUpSlot> slots = new List<PowerUpSlot>();
        RectTransform targetingBanner;
        Text targetingText;
        RectTransform colourPicker;
        GoalsPanel goalsPanel;
        bool goalsMode;
        // A prism was tapped: the player is choosing its colour (swatches, or tap any tile of that colour).
        bool prismPending;
        GridPos prismCell;
        RectTransform previewBubble;
        Text previewText;
        RectTransform banner;
        Text bannerText;
        RectTransform countdown;
        Text countdownText;

        PowerUpDefinition targeting;
        bool hasFirstPick;
        GridPos firstPick;
        int shownScore;
        int shownSecond = -1;
        bool endScheduled;
        bool paused;
        bool countingDown;
        bool invalidHintShown;
        bool runClosed = true;
        int lastMoves = -1;

        sealed class PowerUpSlot
        {
            public PowerUpDefinition Def;
            public CCButton Button;
            public RectTransform Root;
            public Image Badge;
            public Text BadgeText;
        }

        // ================================================================== Build

        protected override void Build()
        {
            // Top bar.
            var top = UI.Rect("TopBar", Safe);
            top.TopStrip(118, 16, Theme.Margin, Theme.Margin);
            var pause = UI.IconButton(top, Icon.Pause, ButtonStyle.Secondary, OpenPause, 108, "Pause");
            ((RectTransform)pause.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(108, 108));
            symbolsButton = UI.IconButton(top, Icon.Eye, ButtonStyle.Ghost, ToggleSymbols, 108, "Symbols");
            ((RectTransform)symbolsButton.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(108, 108));
            title = UI.FitText(top, "", Theme.FontLarge + 4, Theme.Text, TextAnchor.MiddleCenter, true, "Title");
            title.rectTransform.Fill(140, 46, 140, 0);
            UI.AddShadow(title, -4, 0.35f);
            subtitle = UI.FitText(top, "", Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Subtitle");
            subtitle.rectTransform.Fill(140, 0, 140, 76);

            // Status card: score, progress, moves/timer badge.
            statusCard = UI.Rect("Status", Safe);
            statusCard.TopStrip(224, 152, Theme.Margin, Theme.Margin);
            var card = UI.Card(statusCard, "Card", Theme.Surface, 44);
            scoreLabel = UI.Text(card, "SCORE", Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft, true, "ScoreLabel");
            scoreLabel.rectTransform.Fill(40, 0, 260, 24);
            scoreText = UI.Text(card, "0", 78, Theme.Text, TextAnchor.UpperLeft, true, "Score");
            scoreText.rectTransform.Fill(36, 0, 260, 52);
            UI.AddShadow(scoreText, -4, 0.35f);
            targetText = UI.FitText(card, "", Theme.FontSmall - 2, Theme.TextSecondary, TextAnchor.MiddleRight, true, "Target");
            targetText.rectTransform.Fill(320, 130, 270, 44);
            progress = ProgressBar.Create(card, Theme.Gold, 36);
            ((RectTransform)progress.transform).BottomStrip(60, 28, 40, 270);
            // Goal levels show their goals here instead of the target and bar.
            goalsPanel = GoalsPanel.Create(card);
            ((RectTransform)goalsPanel.transform).Fill(300, 14, 236, 14);
            goalsPanel.gameObject.SetActive(false);

            badge = UI.Rect("Badge", card);
            badge.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(196, 196));
            var badgeBg = UI.Image(badge, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Secondary, "Bg");
            badgeBg.rectTransform.Fill();
            badgeRing = UI.Image(badge, ArtLibrary.Instance.Get(ArtNames.Ring), new Color(1, 1, 1, 0.18f), "Ring");
            badgeRing.rectTransform.Fill(-4, -4, -4, -4);
            badgeFill = UI.Image(badge, ArtLibrary.Instance.Get(ArtNames.Ring), Theme.Gold, "Fill");
            badgeFill.rectTransform.Fill(-4, -4, -4, -4);
            badgeFill.type = Image.Type.Filled;
            badgeFill.fillMethod = Image.FillMethod.Radial360;
            badgeFill.fillOrigin = (int)Image.Origin360.Top;
            badgeFill.fillClockwise = false;
            UI.Image(badge, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.5f), "Gloss").rectTransform.Fill();
            badgeNumber = UI.Text(badge, "0", 84, Theme.Text, TextAnchor.MiddleCenter, true, "Number");
            badgeNumber.rectTransform.Fill(0, 40, 0, 10);
            UI.AddShadow(badgeNumber, -4, 0.35f);
            badgeLabel = UI.Text(badge, "MOVES", Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Label");
            badgeLabel.rectTransform.Fill(0, 30, 0, 130);

            // Power-up bar.
            powerBar = UI.Rect("PowerBar", Safe);
            powerBar.BottomStrip(196, 22, Theme.Margin, Theme.Margin);
            powerRow = UI.Rect("Row", powerBar);
            powerRow.Fill();
            var row = UI.HStack(powerRow, 18, null, TextAnchor.MiddleCenter, false);
            row.childControlWidth = false;
            row.childControlHeight = false;
            powerNote = UI.Text(powerBar, "", Theme.FontSmall, Theme.TextMuted, TextAnchor.MiddleCenter, true, "Note");
            powerNote.rectTransform.Fill(20, 0, 20, 0);

            // Board.
            boardArea = UI.Rect("BoardArea", Safe);
            boardArea.Fill(26, 232, 26, 392);
            board = BoardView.Create(boardArea);
            board.PreviewProvider = Preview;
            board.PreviewShown = OnPreviewShown;
            board.PreviewHidden = () => previewBubble.gameObject.SetActive(false);
            board.Committed = OnCommitted;
            board.InvalidPress = OnInvalidPress;
            board.ShuffleStarted = OnShuffle;

            // Overlays inside the screen.
            previewBubble = UI.Rect("Preview", Safe);
            previewBubble.sizeDelta = new Vector2(220, 92);
            var pbg = UI.Rounded(previewBubble, new Color(0.05f, 0.03f, 0.15f, 0.85f), 46, "Bg");
            pbg.rectTransform.Fill();
            previewText = UI.Text(previewBubble, "", Theme.FontLarge, Theme.Gold, TextAnchor.MiddleCenter, true, "Points");
            previewText.rectTransform.Fill();
            previewBubble.gameObject.SetActive(false);

            targetingBanner = UI.Rect("Targeting", Safe);
            // Sits over the score card (not needed while aiming) so it never hides a row of tiles.
            targetingBanner.TopStrip(170, 179, Theme.Margin, Theme.Margin);
            var tb = UI.Card(targetingBanner, "Card", Theme.SurfaceHigh, 40);
            targetingText = UI.FitText(tb, "", Theme.FontBody - 2, Theme.Text, TextAnchor.MiddleLeft, true, "Hint");
            targetingText.rectTransform.Fill(36, 10, 190, 10);
            var cancel = UI.Button(tb, "Cancel", ButtonStyle.Ghost, CancelTargeting, null, Theme.FontSmall, 34, 6);
            ((RectTransform)cancel.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(160, 96));
            targetingBanner.gameObject.SetActive(false);

            colourPicker = UI.Rect("ColourPicker", Safe);
            colourPicker.BottomStrip(196, 22, Theme.Margin, Theme.Margin);
            colourPicker.gameObject.SetActive(false);

            banner = UI.Rect("Banner", Safe);
            banner.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 170));
            var bannerCard = UI.Card(banner, "Card", Theme.SurfaceHigh, 50);
            bannerText = UI.FitText(bannerCard, "", Theme.FontLarge, Theme.Text, TextAnchor.MiddleCenter, true, "Text");
            bannerText.rectTransform.Fill(30, 10, 30, 10);
            banner.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            banner.gameObject.SetActive(false);

            countdown = UI.Rect("Countdown", Safe);
            countdown.Fill();
            countdownText = UI.Text(countdown, "3", 260, Theme.Text, TextAnchor.MiddleCenter, true, "Number");
            countdownText.rectTransform.Fill();
            var outline = countdownText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.02f, 0.15f, 0.8f);
            outline.effectDistance = new Vector2(6, -6);
            countdown.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            // Keep the board fitted to its area (handles rotation-free resizes such as the editor game view).
            if (session != null)
            {
                var size = boardArea.rect.size;
                if (Mathf.Abs(size.x - lastArea.x) > 0.5f || Mathf.Abs(size.y - lastArea.y) > 0.5f)
                {
                    lastArea = size;
                    board.FitInto(size);
                }
            }
        }

        Vector2 lastArea;

        // ================================================================== Lifecycle

        public override void OnShow(object a)
        {
            if (a is GameArgs ga) args = ga;
            if (args == null) args = GameArgs.ForCampaign(Ctx.Campaign.CurrentLevel);
            App.AppPaused -= OnAppPaused;
            App.AppPaused += OnAppPaused;
            Ctx.Settings.Changed -= OnSettingChanged;
            Ctx.Settings.Changed += OnSettingChanged;
            StartRun();
        }

        public override void OnHide()
        {
            CloseRun();
            App.AppPaused -= OnAppPaused;
            Ctx.Settings.Changed -= OnSettingChanged;
            CoachMark.Hide();
            board.CancelPress();
            Tw.Kill(this);
        }

        public override bool OnBack()
        {
            if (countingDown) return true;
            if (targeting != null)
            {
                CancelTargeting();
                return true;
            }
            if (!endScheduled) OpenPause();
            return true;
        }

        void OnAppPaused(bool isPaused)
        {
            if (isPaused && session != null && !session.IsOver && !paused && !endScheduled) OpenPause();
        }

        void OnSettingChanged(SettingKind kind)
        {
            if (kind == SettingKind.Symbols)
            {
                board.SetSymbols(Ctx.Settings.Symbols);
                RefreshSymbolsButton();
                if (goalsMode && session != null)
                {
                    goalsPanel.Configure(session.Goals, Ctx.Settings.Symbols);
                    goalsPanel.Refresh(session.Goals, false);
                }
            }
        }

        // ================================================================== Runs

        /// <summary>Ends the current run's bookkeeping exactly once: an unfinished run is abandoned (counted as an
        /// attempt) and the activity tracker is released so the shop reopens.</summary>
        void CloseRun()
        {
            if (runClosed || session == null) return;
            runClosed = true;
            if (!session.IsOver) Ctx.AbandonRun(session);
            else Ctx.Activity.EndRun();
        }

        void StartRun()
        {
            CloseRun();
            Tw.Kill(this);
            CoachMark.Hide();
            CancelTargeting();
            endScheduled = false;
            paused = false;
            invalidHintShown = false;
            shownSecond = -1;
            lastMoves = -1;
            banner.gameObject.SetActive(false);
            countdown.gameObject.SetActive(false);

            SessionSetup setup;
            switch (args.Mode)
            {
                case GameMode.Endless: setup = Ctx.CreateEndlessRun(); break;
                case GameMode.Daily: setup = Ctx.CreateDailyRun(); break;
                case GameMode.WeeklyRush: setup = Ctx.CreateWeeklyRun(); break;
                default:
                    if (!Ctx.Campaign.IsUnlocked(args.Level)) args.Level = Ctx.Campaign.CurrentLevel;
                    setup = Ctx.CreateCampaignRun(args.Level);
                    break;
            }
            session = new PuzzleSession(setup);
            session.Start();
            Ctx.BeginRun(session);
            runClosed = false;

            board.Bind(session, Ctx.Settings.Symbols);
            board.Speed = session.IsTimed ? 1.35f : 1f;
            lastArea = Vector2.zero;
            board.InputEnabled = true;
            shownScore = 0;
            ConfigureHud();
            RefreshHud(false);
            BuildPowerBar();
            RefreshSymbolsButton();

            App.SetBackground(-1);
            AudioService.Sfx(SfxNames.Whoosh, 0.5f);

            if (session.State == SessionState.Stuck)
            {
                ScheduleEnd(0.3f);
                return;
            }

            if (args.Mode == GameMode.WeeklyRush) StartCountdown();
            else ShowIntroBanner();

            if (args.Mode == GameMode.Campaign)
            {
                var intro = Ctx.GrantIntroTrialsFor(args.Level);
                if (intro != null)
                {
                    BuildPowerBar();
                    Tw.Delay(0.6f, () =>
                    {
                        var popup = Nav.Open<PowerUpIntroPopup>();
                        popup.Setup(intro);
                        popup.Closed += () => PointAtPowerUp(intro);
                    }, this);
                }
                else
                {
                    Tw.Delay(1.2f, ShowLevelTutorial, this);
                }
            }
            else
            {
                Tw.Delay(1.2f, ShowModeTutorial, this);
            }
        }

        void ConfigureHud()
        {
            var level = session.Level;
            switch (session.Mode)
            {
                case GameMode.Campaign:
                    // The level ends the moment the target is reached, so the bar fills to the target.
                    title.text = "Level " + level.Number;
                    subtitle.text = Ctx.Catalog.RegionForLevel(level.Number).Name.ToUpperInvariant();
                    badgeLabel.text = "MOVES";
                    progress.SetMarkers(1f);
                    break;
                case GameMode.Endless:
                    title.text = "Endless";
                    subtitle.text = "NO TIMER  ·  BIG GROUPS EARN MOVES";
                    badgeLabel.text = "MOVES";
                    progress.SetMarkers();
                    break;
                case GameMode.Daily:
                    title.text = "Daily Challenge";
                    subtitle.text = Ctx.TodayKey + "  ·  NO TIMER";
                    badgeLabel.text = "MOVES";
                    progress.SetMarkers(level.Target / (float)level.Star3, level.Star2 / (float)level.Star3, 1f);
                    break;
                case GameMode.WeeklyRush:
                    title.text = "Weekly Rush";
                    subtitle.text = Ctx.ThisWeekKey.ToUpperInvariant() + "  ·  " + level.TimeLimitSeconds + " SECONDS";
                    badgeLabel.text = "SECONDS";
                    progress.SetMarkers(level.Target / (float)level.Star3, level.Star2 / (float)level.Star3, 1f);
                    break;
            }
            badgeFill.gameObject.SetActive(session.IsTimed);
            badgeRing.gameObject.SetActive(true);
            goalsMode = session.Mode == GameMode.Campaign && level.Goals != null && level.Goals.Length > 0;
            goalsPanel.gameObject.SetActive(goalsMode);
            targetText.gameObject.SetActive(!goalsMode);
            progress.gameObject.SetActive(!goalsMode);
            if (goalsMode) goalsPanel.Configure(session.Goals, Ctx.Settings.Symbols);
        }

        void RefreshHud(bool animate)
        {
            var level = session.Level;
            int score = session.Score;
            if (animate && score != shownScore)
            {
                int from = shownScore;
                Tw.Value(from, score, 0.45f, v => scoreText.text = NumberFormat.Thousands(Mathf.RoundToInt(v)), Ease.OutCubic, 0f, this);
                scoreText.transform.localScale = Vector3.one * 1.12f;
                Tw.Scale(scoreText.transform, 1f, 0.25f, Ease.OutBack);
            }
            else
            {
                scoreText.text = NumberFormat.Thousands(score);
            }
            shownScore = score;

            switch (session.Mode)
            {
                case GameMode.Campaign:
                    if (goalsMode)
                    {
                        var done = goalsPanel.Refresh(session.Goals, animate);
                        if (done.HasValue)
                        {
                            AudioService.Sfx(SfxNames.Star(1), 0.7f);
                            board.Fx.Sparkle(board.Fx.ToLocal(done.Value), Theme.Success, 14, 70f);
                        }
                        break;
                    }
                    targetText.text = "Target " + NumberFormat.Thousands(level.Target);
                    progress.SetFraction(score / (float)Mathf.Max(1, level.Target), animate);
                    progress.SetColour(score >= level.Target ? Theme.Success : Theme.Gold);
                    progress.SetMarkerLit(0, score >= level.Target);
                    break;
                case GameMode.Endless:
                    {
                        int stage = session.Stage;
                        int next = Ctx.Config.Endless.ScoreForStage(stage + 1);
                        int prev = Ctx.Config.Endless.ScoreForStage(stage);
                        targetText.text = "Stage " + stage + "  ·  next " + NumberFormat.Thousands(next);
                        progress.SetFraction((score - prev) / (float)Mathf.Max(1, next - prev), animate);
                        progress.SetColour(Theme.Teal);
                        break;
                    }
                default:
                    targetText.text = (session.Mode == GameMode.Daily ? "Target " : "Bronze ") + NumberFormat.Thousands(level.Target);
                    progress.SetFraction(score / (float)Mathf.Max(1, level.Star3), animate);
                    progress.SetColour(score >= level.Target ? Theme.Success : Theme.Gold);
                    progress.SetMarkerLit(0, score >= level.Target);
                    progress.SetMarkerLit(1, score >= level.Star2);
                    progress.SetMarkerLit(2, score >= level.Star3);
                    break;
            }

            if (!session.IsTimed)
            {
                int moves = session.MovesLeft;
                badgeNumber.text = moves.ToString();
                badgeNumber.color = moves <= 3 ? Theme.Hex("#FFB3BD") : Theme.Text;
                if (animate && lastMoves >= 0 && moves != lastMoves)
                {
                    badge.localScale = Vector3.one * (moves > lastMoves ? 1.15f : 1.06f);
                    Tw.Scale(badge, 1f, 0.25f, Ease.OutBack);
                }
                lastMoves = moves;
            }
            else
            {
                UpdateTimer(true);
            }
        }

        void UpdateTimer(bool force)
        {
            float left = (float)session.TimeLeft;
            int secs = Mathf.CeilToInt(left);
            badgeFill.fillAmount = Mathf.Clamp01(left / Mathf.Max(1, session.Level.TimeLimitSeconds));
            if (!force && secs == shownSecond) return;
            bool tick = shownSecond > 0 && secs < shownSecond && secs <= 10 && secs > 0;
            shownSecond = secs;
            badgeNumber.text = secs.ToString();
            badgeNumber.color = secs <= 10 ? Theme.Hex("#FFB3BD") : Theme.Text;
            badgeFill.color = secs <= 10 ? Theme.Danger : Theme.Gold;
            if (tick)
            {
                AudioService.Sfx(SfxNames.Tick, 0.7f);
                badge.localScale = Vector3.one * 1.08f;
                Tw.Scale(badge, 1f, 0.2f, Ease.OutQuad);
            }
        }

        void Update()
        {
            if (session == null || !session.IsTimed || paused || countingDown) return;
            if (session.Tick(Time.unscaledDeltaTime))
            {
                UpdateTimer(true);
                board.CancelPress();
                board.InputEnabled = false;
                CancelTargeting();
                ShowBanner("Time's up!", 1.2f);
                AudioService.Sfx(SfxNames.Go, 0.8f, 0.8f);
                ScheduleEnd(1.3f);
                return;
            }
            if (session.TimerRunning) UpdateTimer(false);
        }

        // ================================================================== Input

        int Preview(GridPos cell, List<GridPos> cells)
        {
            cells.Clear();
            if (session == null || !session.AcceptsMoves) return 0;
            if (prismPending)
            {
                // Choosing the prism's colour: pressing a tile shows every tile of its colour.
                var pt = session.Board[cell];
                if (pt.Type != CellType.Colour) return 0;
                var b = session.Board;
                for (int i = 0; i < b.CellCount; i++)
                    if (b.GetByIndex(i).Type == CellType.Colour && b.GetByIndex(i).Colour == pt.Colour) cells.Add(b.PosOf(i));
                return cells.Count;
            }
            if (targeting == null && session.IsSpecialAt(cell))
            {
                // A special tile: show what it would reach (just the prism while its colour is still to be picked).
                GridPos partner;
                bool combo;
                if (session.NeedsColourChoice(cell))
                {
                    cells.Add(cell);
                    if (SpecialEffects.FindPartner(session.Board, cell, out partner)) cells.Add(partner);
                    return cells.Count;
                }
                session.PreviewSpecial(cell, -1, cells, out partner, out combo);
                cells.Add(cell);
                if (combo) cells.Add(partner);
                return cells.Count;
            }
            if (targeting == null)
            {
                int n = session.PreviewGroup(cell, cells);
                if (n < 2)
                {
                    cells.Clear();
                    return 0;
                }
                return n;
            }
            var effect = PowerUpEffects.Get(targeting.id);
            switch (targeting.targeting)
            {
                case PowerUpTargeting.AdjacentPair:
                    if (!hasFirstPick || !cell.IsAdjacentTo(firstPick))
                    {
                        if (!session.Board.IsColourAt(cell.Col, cell.Row)) return 0;
                        cells.Add(cell);
                        return 1;
                    }
                    {
                        var req = PowerUpRequest.Pair(targeting.id, firstPick, cell);
                        PowerUpFailure f;
                        if (!session.CanApplyPowerUp(req, out f)) return 0;
                        cells.Add(firstPick);
                        cells.Add(cell);
                        return 2;
                    }
                case PowerUpTargeting.CellAndColour:
                    if (!session.Board.IsColourAt(cell.Col, cell.Row)) return 0;
                    cells.Add(cell);
                    return 1;
                default:
                    {
                        var req = PowerUpRequest.AtCell(targeting.id, cell);
                        PowerUpFailure f;
                        if (!session.CanApplyPowerUp(req, out f)) return 0;
                        effect.Preview(session, req, cells);
                        if (cells.Count == 0) cells.Add(cell);
                        return cells.Count;
                    }
            }
        }

        void OnPreviewShown(GridPos cell, List<GridPos> cells, int n)
        {
            if (targeting != null || prismPending)
            {
                previewBubble.gameObject.SetActive(false);
                return;
            }
            if (session.IsSpecialAt(cell))
            {
                GridPos partner;
                bool combo = SpecialEffects.FindPartner(session.Board, cell, out partner);
                previewText.text = combo ? "Combo!" : SpecialName(session.Board[cell].Special) + "!";
                previewText.color = Theme.Gold;
            }
            else
            {
                previewText.text = "+" + NumberFormat.Thousands(session.PointsForGroup(n));
                var special = Ctx.Config.Scoring.specials.KindFor(n, session.Level.Specials, false);
                if (special != SpecialKind.None) previewText.text += "  " + SpecialName(special);
                previewText.color = n >= Ctx.Config.Scoring.praiseGoodSize ? Theme.Gold : Theme.Text;
            }
            // Place the bubble above the highest tile of the group.
            var top = cells[0];
            foreach (var c in cells) if (c.Row > top.Row) top = c;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)previewBubble.parent, RectTransformUtility.WorldToScreenPoint(null, board.CellWorld(top)), null, out local);
            float half = ((RectTransform)previewBubble.parent).rect.width / 2f - 130f;
            previewBubble.anchorMin = previewBubble.anchorMax = new Vector2(0.5f, 0.5f);
            previewBubble.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -half, half), local.y + board.CellSize * 0.85f);
            previewBubble.gameObject.SetActive(true);
            previewBubble.SetAsLastSibling();
        }

        void OnCommitted(GridPos cell)
        {
            if (session == null || endScheduled) return;
            if (prismPending)
            {
                var t = session.Board[cell];
                if (t.Type == CellType.Colour) PickPrismColour(t.Colour);
                return;
            }
            if (targeting != null)
            {
                HandleTarget(cell);
                return;
            }
            if (session.NeedsColourChoice(cell))
            {
                ShowPrismPicker(cell);
                return;
            }
            PlayGroup(cell, -1);
        }

        static string SpecialName(SpecialKind kind)
        {
            switch (kind)
            {
                case SpecialKind.RocketRow:
                case SpecialKind.RocketColumn: return "Rocket";
                case SpecialKind.Bomb: return "Bomb";
                case SpecialKind.Prism: return "Prism";
                default: return "";
            }
        }

        /// <summary>A prism was tapped: the player picks the colour it clears (a swatch, or any tile of that colour).</summary>
        void ShowPrismPicker(GridPos cell)
        {
            CancelTargeting();
            prismPending = true;
            prismCell = cell;
            targetingText.text = "Pick a colour: the prism clears every tile of it";
            targetingBanner.gameObject.SetActive(true);
            targetingBanner.localScale = Vector3.one * 0.9f;
            Tw.Scale(targetingBanner, 1f, 0.2f, Ease.OutBack);
            scratch.Clear();
            scratch.Add(cell);
            GridPos partner;
            if (SpecialEffects.FindPartner(session.Board, cell, out partner)) scratch.Add(partner);
            board.Mark(scratch, Theme.Gold);
            var present = new List<int>();
            for (int c = 0; c < TileColours.Max; c++)
                if (session.Board.CountColour(c) > 0) present.Add(c);
            ShowSwatches(present, PickPrismColour);
            AudioService.Sfx(SfxNames.Select, 0.6f, 1.2f);
        }

        void PickPrismColour(int colour)
        {
            var cell = prismCell;
            CancelTargeting();
            PlayGroup(cell, colour);
        }

        void OnInvalidPress(GridPos cell)
        {
            if (session == null || !session.AcceptsMoves) return;
            if (targeting != null)
            {
                board.Shake(cell);
                AudioService.Sfx(SfxNames.Invalid, 0.6f);
                return;
            }
            var t = session.Board[cell];
            if (t.Type == CellType.Stone)
            {
                Toast.Show("Stones can't be cleared by matching", Icon.Info);
            }
            else if (t.Type == CellType.Treasure)
            {
                board.Shake(cell);
                Toast.Show("Clear the tiles under the treasure to bring it down", Icon.Info);
            }
            else if (t.Type == CellType.Colour && t.Obstacle == ObstacleKind.Ice)
            {
                board.Shake(cell);
                AudioService.Sfx(SfxNames.Invalid, 0.6f);
                Toast.Show("Frozen tiles can't be tapped. Clear the tiles next to them to crack the ice.", Icon.Info);
            }
            else if (t.Type == CellType.Colour && t.Obstacle == ObstacleKind.Chain)
            {
                board.Shake(cell);
                AudioService.Sfx(SfxNames.Invalid, 0.6f);
                Toast.Show("A chained tile needs a matching neighbour: clear its group to break a link", Icon.Info);
            }
            else if (t.Type == CellType.Colour)
            {
                board.Shake(cell);
                AudioService.Sfx(SfxNames.Invalid, 0.6f);
                Haptics.Light();
                if (!invalidHintShown && session.Mode == GameMode.Campaign && session.Level.Number <= 3)
                {
                    invalidHintShown = true;
                    Toast.Show("Tap 2 or more matching tiles that touch side by side", Icon.Info);
                }
            }
        }

        /// <summary>Plays a tap: clears a group or fires a special tile (prismColour is the colour a prism clears).</summary>
        void PlayGroup(GridPos cell, int prismColour)
        {
            if (!session.TryPlay(cell, outcome, prismColour))
            {
                board.Shake(cell);
                return;
            }
            float duration = board.Animate(outcome);
            CoachMark.Hide();
            if (outcome.Kind == OutcomeKind.Special)
            {
                Haptics.Heavy();
                ShakeBoard(outcome.SpecialsFired.Count > 1 || outcome.SpecialsFired[0].ComboWith != SpecialKind.None ? 20f : 12f);
            }
            else
            {
                if (AudioService.Instance != null) AudioService.Instance.PlayPop(outcome.GroupSize);
                if (outcome.GroupSize >= 8) Haptics.Medium();
                else Haptics.Light();
            }
            ShowMoveFeedback(outcome);
            RefreshHud(true);
            AfterMoveTutorials();
            if (outcome.Ended) ScheduleEnd(duration + 0.35f);
        }

        void ShowMoveFeedback(MoveOutcome o)
        {
            var fx = board.Fx;
            var at = board.CellInFx(o.Origin);
            if (o.ScoreGained > 0)
            {
                var colour = o.GroupColour >= 0 ? Color.Lerp(Theme.Tile(o.GroupColour), Color.white, 0.35f) : Theme.Gold;
                int size = Mathf.Clamp(46 + o.GroupSize * 4, 50, 104);
                fx.FloatText(at + new Vector2(0, 30), "+" + NumberFormat.Thousands(o.ScoreGained), colour, size, 140f, 0.9f);
            }
            if (o.Praise != PraiseTier.None)
            {
                string word = o.Praise == PraiseTier.Good ? "Nice!" : o.Praise == PraiseTier.Great ? "Great!" : o.Praise == PraiseTier.Amazing ? "Amazing!" : "Colour Cascade!";
                fx.FloatText(new Vector2(0, board.CellSize * 1.2f), word, Theme.Gold, o.Praise >= PraiseTier.Amazing ? 110 : 92, 90f, 1.1f, 0.12f, 1.15f);
                fx.Ring(at, Theme.Tile(Mathf.Max(0, o.GroupColour)), board.CellSize * (3f + (int)o.Praise));
                if (o.Praise >= PraiseTier.Great) AudioService.Sfx(SfxNames.Shimmer, 0.7f);
                if (o.Praise >= PraiseTier.Amazing)
                {
                    Haptics.Heavy();
                    ShakeBoard(o.Praise == PraiseTier.Cascade ? 22f : 14f);
                }
            }
            if (o.BonusMoves > 0)
            {
                var badgePos = board.Fx.ToLocal(badge.position);
                fx.FloatText(badgePos + new Vector2(0, -120), "+" + o.BonusMoves + (o.BonusMoves == 1 ? " move" : " moves"), Theme.Success, 58, 90f, 1.0f, 0.2f);
            }
            if (o.StageUp)
            {
                ShowBanner("Stage " + o.NewStage + (Ctx.Config.Endless.ColoursForStage(o.NewStage) > Ctx.Config.Endless.ColoursForStage(o.NewStage - 1) ? "  ·  a new colour appears" : ""), 1.6f);
                AudioService.Sfx(SfxNames.Unlock, 0.7f);
            }
        }

        void ShakeBoard(float amount)
        {
            if (Tw.Reduced) return;
            var f = board.Frame;
            Tw.Kill(f);
            f.anchoredPosition = new Vector2(amount, 0);
            Tw.Move(f, new Vector2(-amount * 0.6f, 0), 0.06f, Ease.InOutQuad).OnComplete(() =>
                Tw.Move(f, new Vector2(amount * 0.3f, 0), 0.06f, Ease.InOutQuad).OnComplete(() =>
                    Tw.Move(f, Vector2.zero, 0.08f, Ease.OutQuad)));
        }

        void OnShuffle()
        {
            ShowBanner("No moves left — shuffling!", 1.1f);
            AudioService.Sfx(SfxNames.Whoosh);
            if (Ctx.Tutorials.MarkSeen(TutorialKeys.Shuffle))
                Tw.Delay(1.3f, () => Toast.Show("When no group can be made, the board reshuffles for free.", Icon.Restart), this);
        }

        void ToggleSymbols()
        {
            Ctx.Settings.Symbols = !Ctx.Settings.Symbols;
            Toast.Show(Ctx.Settings.Symbols ? "Symbols on: every colour has its own shape" : "Symbols off", Icon.Eye, Theme.Teal);
        }

        void RefreshSymbolsButton()
        {
            symbolsButton.SetStyle(Ctx.Settings.Symbols ? ButtonStyle.Teal : ButtonStyle.Ghost);
        }

        // ================================================================== Power-ups

        void BuildPowerBar()
        {
            UI.ClearChildren(powerRow);
            slots.Clear();
            foreach (var def in Ctx.PowerUps.All)
            {
                if (!Ctx.PowerUpUse.IsVisibleIn(def.id, session.Mode)) continue;
                slots.Add(CreateSlot(def));
            }
            if (ActionPolicy.IsChallengeMode(session.Mode)) powerNote.text = "Power-ups are switched off in challenges, so everyone plays fair";
            else if (slots.Count == 0)
            {
                var first = Ctx.PowerUps.All.Count > 0 ? Ctx.PowerUps.All[0] : null;
                powerNote.text = first != null ? "Power-ups unlock at level " + first.unlockLevel : "";
            }
            else powerNote.text = "";
            powerNote.gameObject.SetActive(powerNote.text.Length > 0);
            RefreshPowerBar();
        }

        PowerUpSlot CreateSlot(PowerUpDefinition def)
        {
            var slot = new PowerUpSlot { Def = def };
            var root = UI.Rect("PowerUp_" + def.id, powerRow);
            root.sizeDelta = new Vector2(150, 190);
            slot.Root = root;
            var btn = UI.Button(root, null, ButtonStyle.Secondary, () => OnPowerUpTapped(slot), null, 30, 64, 8, "Button");
            ((RectTransform)btn.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(132, 132));
            btn.Face.color = Theme.Hex(def.accentHex);
            btn.Lip.color = Color.Lerp(Theme.Hex(def.accentHex), Color.black, 0.32f);
            var art = UI.Image(btn.FaceRect, ArtLibrary.Instance.PowerUp(def.id), Color.white, "Art");
            art.rectTransform.Centre(112, 112);
            slot.Button = btn;
            var label = UI.FitText(root, def.displayName, Theme.FontTiny - 3, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Name");
            label.rectTransform.BottomStrip(44, 0, -6, -6);
            var badgeImg = UI.Image(root, ArtLibrary.Instance.Get(ArtNames.Round), Theme.Primary, "Badge");
            badgeImg.type = Image.Type.Sliced;
            UI.SetRadius(badgeImg, 26);
            badgeImg.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(8, 8), new Vector2(76, 52));
            slot.Badge = badgeImg;
            slot.BadgeText = UI.Text(badgeImg.rectTransform, "0", Theme.FontTiny, Theme.Text, TextAnchor.MiddleCenter, true, "Count");
            slot.BadgeText.rectTransform.Fill();
            return slot;
        }

        void RefreshPowerBar()
        {
            foreach (var s in slots)
            {
                int trials = Ctx.Inventory.Trials(s.Def.id);
                int owned = Ctx.Inventory.Owned(s.Def.id);
                if (trials > 0)
                {
                    s.Badge.color = Theme.Teal;
                    s.BadgeText.text = "Free" + (trials > 1 ? " " + trials : "");
                    s.BadgeText.color = Theme.TextDark;
                    s.Badge.rectTransform.sizeDelta = new Vector2(trials > 1 ? 104 : 86, 52);
                }
                else if (owned > 0)
                {
                    s.Badge.color = Theme.Primary;
                    s.BadgeText.text = owned.ToString();
                    s.BadgeText.color = Theme.Text;
                    s.Badge.rectTransform.sizeDelta = new Vector2(64, 52);
                }
                else
                {
                    s.Badge.color = Theme.Gold;
                    s.BadgeText.text = "+";
                    s.BadgeText.color = Theme.TextDark;
                    s.Badge.rectTransform.sizeDelta = new Vector2(56, 52);
                }
                bool usable = session != null && (session.AcceptsMoves || s.Def.id == PowerUpId.ExtraMoves) && !endScheduled;
                s.Root.GetComponent<RectTransform>().localScale = targeting != null && targeting.id == s.Def.id ? Vector3.one * 1.1f : Vector3.one;
                var group = UI.GetOrAdd<CanvasGroup>(s.Root.gameObject);
                group.alpha = usable ? 1f : 0.5f;
            }
        }

        void OnPowerUpTapped(PowerUpSlot slot)
        {
            if (session == null || endScheduled || board.Busy) return;
            var def = slot.Def;
            if (targeting != null && targeting.id == def.id)
            {
                CancelTargeting();
                return;
            }
            var block = Ctx.PowerUpUse.CanSelect(session, def.id);
            switch (block)
            {
                case UseBlock.None:
                    if (def.targeting == PowerUpTargeting.Instant) ApplyPowerUp(PowerUpRequest.Instant(def.id));
                    else BeginTargeting(def);
                    break;
                case UseBlock.NoneOwned:
                    if (!ActionPolicy.ShopPurchasesAllowed(Ctx.Activity))
                    {
                        Toast.Show("The shop is closed during challenges", Icon.Lock);
                        return;
                    }
                    board.CancelPress();
                    var popup = Nav.Open<QuickBuyPopup>();
                    popup.Setup(def, () =>
                    {
                        RefreshPowerBar();
                        if (Ctx.PowerUpUse.CanSelect(session, def.id) == UseBlock.None)
                        {
                            if (def.targeting == PowerUpTargeting.Instant) ApplyPowerUp(PowerUpRequest.Instant(def.id));
                            else BeginTargeting(def);
                        }
                    });
                    break;
                case UseBlock.NotAllowedInMode:
                    Toast.Show(def.displayName + " can't be used in this mode", Icon.Lock);
                    break;
                case UseBlock.Locked:
                    Toast.Show(def.displayName + " unlocks at level " + def.unlockLevel, Icon.Lock);
                    break;
            }
        }

        void BeginTargeting(PowerUpDefinition def)
        {
            targeting = def;
            hasFirstPick = false;
            targetingText.text = def.targetingHint;
            targetingBanner.gameObject.SetActive(true);
            targetingBanner.localScale = Vector3.one * 0.9f;
            Tw.Scale(targetingBanner, 1f, 0.2f, Ease.OutBack);
            board.SetGlowColour(Theme.Hex(def.accentHex).WithAlpha(0.95f));
            board.ClearMarks();
            colourPicker.gameObject.SetActive(false);
            powerBar.gameObject.SetActive(true);
            CoachMark.Hide();
            RefreshPowerBar();
        }

        void CancelTargeting()
        {
            targeting = null;
            hasFirstPick = false;
            prismPending = false;
            if (targetingBanner != null) targetingBanner.gameObject.SetActive(false);
            if (colourPicker != null) colourPicker.gameObject.SetActive(false);
            if (powerBar != null) powerBar.gameObject.SetActive(true);
            if (board != null)
            {
                board.SetGlowColour(new Color(1, 1, 1, 0.85f));
                board.ClearMarks();
            }
            if (session != null) RefreshPowerBar();
        }

        void HandleTarget(GridPos cell)
        {
            var def = targeting;
            switch (def.targeting)
            {
                case PowerUpTargeting.AdjacentPair:
                    if (hasFirstPick && cell.IsAdjacentTo(firstPick))
                    {
                        ApplyPowerUp(PowerUpRequest.Pair(def.id, firstPick, cell));
                        return;
                    }
                    hasFirstPick = true;
                    firstPick = cell;
                    scratch.Clear();
                    PowerUpEffects.Get(def.id).Preview(session, PowerUpRequest.AtCell(def.id, cell), scratch);
                    scratch.Add(cell);
                    board.Mark(scratch, Theme.Hex(def.accentHex));
                    targetingText.text = "Now tap a neighbouring tile of another colour";
                    break;
                case PowerUpTargeting.CellAndColour:
                    hasFirstPick = true;
                    firstPick = cell;
                    scratch.Clear();
                    scratch.Add(cell);
                    board.Mark(scratch, Theme.Hex(def.accentHex));
                    ShowColourPicker(cell);
                    break;
                default:
                    ApplyPowerUp(PowerUpRequest.AtCell(def.id, cell));
                    break;
            }
        }

        void ShowColourPicker(GridPos cell)
        {
            targetingText.text = "Pick the new colour";
            int current = session.Board[cell].Colour;
            var colours = new List<int>();
            for (int c = 0; c < session.ColourCount; c++)
                if (c != current) colours.Add(c);
            ShowSwatches(colours, colour => ApplyPowerUp(PowerUpRequest.CellColour(PowerUpId.ColourShift, cell, colour)));
        }

        /// <summary>A row of colour swatches in place of the power-up bar (Colour Shift and prisms).</summary>
        void ShowSwatches(List<int> colours, System.Action<int> picked)
        {
            UI.ClearChildren(colourPicker);
            var bg = UI.Card(colourPicker, "Card", Theme.SurfaceHigh, 50);
            var row = UI.Rect("Row", bg);
            row.Fill(20, 20, 20, 20);
            var h = UI.HStack(row, 16, null, TextAnchor.MiddleCenter, false);
            h.childControlWidth = false;
            h.childControlHeight = false;
            foreach (int c in colours)
            {
                int colour = c;
                var swatch = UI.Rect("Swatch" + c, row);
                swatch.sizeDelta = new Vector2(132, 132);
                var img = UI.Image(swatch, ArtLibrary.Instance.Tile(c), Color.white, "Tile");
                img.rectTransform.Fill();
                img.raycastTarget = true;
                if (Ctx.Settings.Symbols) UI.Image(img.rectTransform, ArtLibrary.Instance.Symbol(c), Color.white, "Symbol").rectTransform.Centre(70, 70);
                var b = swatch.gameObject.AddComponent<CCButton>();
                b.OnClick = () => picked(colour);
                b.Configure(ButtonStyle.Ghost, 0);
            }
            powerBar.gameObject.SetActive(false);
            colourPicker.gameObject.SetActive(true);
            colourPicker.localScale = Vector3.one * 0.9f;
            Tw.Scale(colourPicker, 1f, 0.2f, Ease.OutBack);
        }

        void ApplyPowerUp(PowerUpRequest request)
        {
            UseBlock block;
            PowerUpFailure failure;
            bool revived = session.State == SessionState.Failed;
            if (!Ctx.PowerUpUse.TryUse(session, request, outcome, out block, out failure))
            {
                AudioService.Sfx(SfxNames.Invalid, 0.6f);
                if (failure == PowerUpFailure.SameColour) Toast.Show("Pick tiles of different colours", Icon.Info);
                else if (failure == PowerUpFailure.NotAdjacent) Toast.Show("Swap needs two neighbouring tiles", Icon.Info);
                else if (failure == PowerUpFailure.NothingToClear) Toast.Show("Nothing to clear there", Icon.Info);
                else if (block == UseBlock.NoneOwned) Toast.Show("You don't have any left", Icon.Info);
                return;
            }
            Ctx.Save();
            var def = Ctx.PowerUps.Get(request.Id);
            CancelTargeting();
            float duration = board.Animate(outcome);
            var at = board.CellInFx(request.A);
            switch (request.Id)
            {
                case PowerUpId.Blast:
                    AudioService.Sfx(SfxNames.Blast);
                    Haptics.Heavy();
                    board.Fx.Ring(at, Theme.Hex(def.accentHex), board.CellSize * 4.2f);
                    ShakeBoard(18f);
                    break;
                case PowerUpId.ColourBurst:
                    AudioService.Sfx(SfxNames.Shimmer);
                    AudioService.Sfx(SfxNames.Blast, 0.5f, 1.4f);
                    Haptics.Heavy();
                    board.Fx.Ring(at, Theme.Tile(Mathf.Max(0, outcome.GroupColour)), board.CellSize * 8f);
                    break;
                case PowerUpId.TileBreaker:
                    AudioService.Sfx(SfxNames.Break);
                    Haptics.Medium();
                    break;
                case PowerUpId.Swap:
                    AudioService.Sfx(SfxNames.Swap);
                    break;
                case PowerUpId.ColourShift:
                    AudioService.Sfx(SfxNames.Shimmer, 0.8f);
                    break;
                case PowerUpId.ExtraMoves:
                    AudioService.Sfx(SfxNames.Unlock);
                    Haptics.Medium();
                    var badgePos = board.Fx.ToLocal(badge.position);
                    board.Fx.FloatText(badgePos + new Vector2(0, -120), "+" + outcome.MovesAdded + " moves", Theme.Success, 62, 90f, 1.1f);
                    board.Fx.Sparkle(badgePos, Theme.Success, 16, 80f);
                    break;
            }
            if (outcome.UsedTrial) Toast.Show("Free " + def.displayName + " used", Icon.Gift, Theme.Teal);
            ShowMoveFeedback(outcome);
            RefreshHud(true);
            RefreshPowerBar();
            if (revived && session.State == SessionState.Playing)
            {
                endScheduled = false;
                board.InputEnabled = true;
            }
            if (outcome.Ended) ScheduleEnd(duration + 0.35f);
        }

        void PointAtPowerUp(PowerUpDefinition def)
        {
            foreach (var s in slots)
            {
                if (s.Def.id != def.id) continue;
                CoachMark.Show("Tap " + def.displayName + ", then choose where to use it", s.Button.transform.position, -380f, 5f);
                s.Root.localScale = Vector3.one * 1.25f;
                Tw.Scale(s.Root, 1f, 0.5f, Ease.OutElastic);
            }
        }

        // ================================================================== Tutorials

        float OverlayYAbove(RectTransform target, float offset)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(App.OverlayLayer, RectTransformUtility.WorldToScreenPoint(null, target.position), null, out local);
            return local.y + target.rect.height * 0.5f + offset;
        }

        void ShowLevelTutorial()
        {
            if (session == null || session.IsOver || Nav.AnyPopup) return;
            string key = session.Level.TutorialKey;
            if (string.IsNullOrEmpty(key) || Ctx.Tutorials.Seen(key)) return;
            switch (key)
            {
                case TutorialKeys.TapGroup:
                    {
                        var groups = new List<GroupInfo>();
                        new GroupFinder().FindAllGroups(session.Board, groups);
                        if (groups.Count == 0) return;
                        var best = groups[0];
                        foreach (var g in groups) if (g.Size > best.Size) best = g;
                        CoachMark.Show("Tap a group of 2 or more matching tiles that touch", board.CellWorld(best.Anchor), OverlayYAbove(board.Frame, 110f));
                        return;
                    }
                case TutorialKeys.HoldPreview:
                    Ctx.Tutorials.MarkSeen(key);
                    CoachMark.Show("Tip: press and hold to see a group's points. Slide away to cancel.", null, OverlayYAbove(board.Frame, 110f), 6f);
                    return;
                case TutorialKeys.Stars:
                    Ctx.Tutorials.MarkSeen(key);
                    CoachMark.Show("Reach the target before your moves run out. Spare moves become bonus points and stars!", null, OverlayYAbove(board.Frame, 110f), 6.5f);
                    return;
                case TutorialKeys.Symbols:
                    Ctx.Tutorials.MarkSeen(key);
                    CoachMark.Show("Tip: tap the eye button to give every colour its own shape", symbolsButton.transform.position, OverlayYAbove(board.Frame, 110f), 6f);
                    return;
                case TutorialKeys.Stones:
                    {
                        Ctx.Tutorials.MarkSeen(key);
                        Vector3? stone = null;
                        var b = session.Board;
                        for (int r = 0; r < b.Height && stone == null; r++)
                            for (int c = 0; c < b.Width; c++)
                                if (b[c, r].Type == CellType.Stone)
                                {
                                    stone = board.CellWorld(new GridPos(c, r));
                                    break;
                                }
                        CoachMark.Show("Stones never move. Tiles can't fall through them, so each section refills on its own.", stone, OverlayYAbove(board.Frame, 110f), 7f);
                        return;
                    }
                default:
                    {
                        // Campaign mechanics: a card that explains it, then a pointer at the first one on the board.
                        var tip = MechanicTips.For(key, Ctx.Config.Scoring);
                        if (tip == null) return;
                        Ctx.Tutorials.MarkSeen(key);
                        board.CancelPress();
                        var popup = Nav.Open<MechanicIntroPopup>();
                        popup.Setup(tip);
                        popup.Closed += () => PointAtMechanic(key, tip);
                        return;
                    }
            }
        }

        void PointAtMechanic(string key, MechanicTips.Tip tip)
        {
            if (session == null || session.IsOver) return;
            Vector3? at = null;
            if (key == TutorialKeys.ColourGoal) at = goalsPanel.PositionOf(session.Goals, GoalKind.CollectColour);
            else
            {
                var cellPos = FindMechanicCell(key);
                if (cellPos.HasValue) at = board.CellWorld(cellPos.Value);
            }
            CoachMark.Show(tip.Pointer, at, OverlayYAbove(board.Frame, 110f), 6.5f);
        }

        /// <summary>The first cell on the board showing the mechanic a tutorial is about.</summary>
        GridPos? FindMechanicCell(string key)
        {
            var b = session.Board;
            for (int r = b.Height - 1; r >= 0; r--)
                for (int c = 0; c < b.Width; c++)
                {
                    var t = b[c, r];
                    bool match;
                    switch (key)
                    {
                        case TutorialKeys.Ice: match = t.Obstacle == ObstacleKind.Ice; break;
                        case TutorialKeys.ThickIce: match = t.Obstacle == ObstacleKind.Ice && t.Strength >= 2; break;
                        case TutorialKeys.Chains: match = t.Obstacle == ObstacleKind.Chain; break;
                        case TutorialKeys.Moss: match = t.Obstacle == ObstacleKind.Moss; break;
                        case TutorialKeys.Treasure: match = t.Type == CellType.Treasure; break;
                        case TutorialKeys.Rockets: match = t.Type == CellType.Special && (t.Special == SpecialKind.RocketRow || t.Special == SpecialKind.RocketColumn); break;
                        case TutorialKeys.Bombs: match = t.Type == CellType.Special && t.Special == SpecialKind.Bomb; break;
                        case TutorialKeys.Prisms: match = t.Type == CellType.Special && t.Special == SpecialKind.Prism; break;
                        case TutorialKeys.Combos: match = t.Type == CellType.Special; break;
                        default: match = false; break;
                    }
                    if (match) return new GridPos(c, r);
                }
            return null;
        }

        void ShowModeTutorial()
        {
            if (session == null || session.IsOver || Nav.AnyPopup) return;
            switch (session.Mode)
            {
                case GameMode.Endless:
                    if (Ctx.Tutorials.MarkSeen(TutorialKeys.Endless))
                        CoachMark.Show("No timer here. Every group costs a move, and groups of 6+ give bonus moves!", null, OverlayYAbove(board.Frame, 110f), 6f);
                    break;
                case GameMode.Daily:
                    if (Ctx.Tutorials.MarkSeen(TutorialKeys.Daily))
                        CoachMark.Show("Everyone gets this same puzzle today. Plan your " + session.Level.Moves + " moves!", null, OverlayYAbove(board.Frame, 110f), 6f);
                    break;
            }
        }

        void AfterMoveTutorials()
        {
            if (session.Mode != GameMode.Campaign) return;
            if (outcome.HasCreated && Ctx.Tutorials.MarkSeen(TutorialKeys.FirstSpecial))
            {
                int id = outcome.Created.TileId;
                string name = SpecialName(outcome.Created.Kind).ToLowerInvariant();
                Tw.Delay(1.0f, () =>
                {
                    if (session == null || session.IsOver) return;
                    var b = session.Board;
                    for (int i = 0; i < b.CellCount; i++)
                        if (b.GetByIndex(i).Id == id && b.GetByIndex(i).Type == CellType.Special)
                        {
                            CoachMark.Show("You made a " + name + "! Tap it to fire it.", board.CellWorld(b.PosOf(i)), OverlayYAbove(board.Frame, 110f), 5f);
                            return;
                        }
                }, this);
            }
            if (session.Level.TutorialKey == TutorialKeys.TapGroup && session.MovesMade == 1 && Ctx.Tutorials.MarkSeen(TutorialKeys.TapGroup))
            {
                Ctx.Tutorials.MarkSeen(TutorialKeys.BiggerGroups);
                Tw.Delay(0.7f, () =>
                {
                    if (session != null && !session.IsOver)
                        CoachMark.Show("Bigger groups score far more: 2 tiles = 40, 5 tiles = 250!", null, OverlayYAbove(board.Frame, 110f), 5f);
                }, this);
            }
        }

        // ================================================================== Banners

        void ShowIntroBanner()
        {
            var l = session.Level;
            string text;
            switch (session.Mode)
            {
                case GameMode.Campaign:
                    text = l.Goals != null && l.Goals.Length > 0 ? GoalVisuals.IntroLine(l) : "Score " + NumberFormat.Thousands(l.Target) + " in " + l.Moves + " moves";
                    break;
                case GameMode.Daily: text = l.Moves + " moves  ·  target " + NumberFormat.Thousands(l.Target); break;
                case GameMode.Endless: text = l.Moves + " moves  ·  big groups add more"; break;
                default: text = l.Title; break;
            }
            ShowBanner(text, 1.5f);
        }

        void ShowBanner(string text, float hold)
        {
            bannerText.text = text;
            banner.gameObject.SetActive(true);
            banner.SetAsLastSibling();
            var group = banner.GetComponent<CanvasGroup>();
            Tw.Kill(group);
            group.alpha = 0;
            banner.localScale = Vector3.one * 0.8f;
            Tw.Fade(group, 1f, 0.18f);
            Tw.Scale(banner, 1f, 0.3f, Ease.OutBack);
            Tw.Fade(group, 0f, 0.25f, Ease.InQuad, hold).OnComplete(() =>
            {
                if (banner != null) banner.gameObject.SetActive(false);
            });
        }

        void StartCountdown()
        {
            countingDown = true;
            board.InputEnabled = false;
            countdown.gameObject.SetActive(true);
            countdown.SetAsLastSibling();
            int seconds = Mathf.Max(1, Ctx.Config.Rush.countdownSeconds);
            for (int i = 0; i <= seconds; i++)
            {
                int n = seconds - i;
                Tw.Delay(0.3f + i * 0.75f, () =>
                {
                    if (!countingDown) return;
                    countdownText.text = n > 0 ? n.ToString() : "GO!";
                    countdownText.color = n > 0 ? Theme.Text : Theme.Gold;
                    countdownText.transform.localScale = Vector3.one * 1.6f;
                    Tw.Scale(countdownText.transform, 1f, 0.35f, Ease.OutBack);
                    AudioService.Sfx(n > 0 ? SfxNames.Tick : SfxNames.Go);
                    if (n == 0)
                    {
                        Tw.Delay(0.45f, () =>
                        {
                            countingDown = false;
                            countdown.gameObject.SetActive(false);
                            if (session != null && !paused)
                            {
                                session.StartTimer();
                                board.InputEnabled = true;
                            }
                        }, this);
                    }
                }, this);
            }
        }

        // ================================================================== End of run

        void ScheduleEnd(float delay)
        {
            if (endScheduled) return;
            endScheduled = true;
            board.InputEnabled = false;
            board.CancelPress();
            CancelTargeting();
            RefreshPowerBar();
            Tw.Delay(delay, ShowResult, this);
        }

        void ShowResult()
        {
            if (session == null || !session.IsOver) return;
            var result = Ctx.FinishRun(session);
            CoachMark.Hide();
            if (result.Success && session.Mode == GameMode.Campaign) AudioService.Sfx(SfxNames.Win);
            else if (session.State == SessionState.Failed) AudioService.Sfx(SfxNames.Lose);
            else AudioService.Sfx(SfxNames.Win, 0.8f);
            if (result.Success) Haptics.Medium();
            if (session.Mode == GameMode.Campaign && session.State == SessionState.Won && session.LeftoverBonus > 0)
                board.Fx.Sparkle(board.Fx.ToLocal(badge.position), Theme.Gold, 18, 90f);
            var popup = Nav.Open<ResultPopup>();
            popup.Setup(result, session, this);
        }

        /// <summary>Uses (or buys and uses) Extra Moves to continue a campaign level that ran out of moves.</summary>
        public bool TryRevive()
        {
            if (session == null || session.State != SessionState.Failed) return false;
            if (Ctx.PowerUpUse.CanSelect(session, PowerUpId.ExtraMoves) == UseBlock.NoneOwned)
            {
                var buy = Ctx.Shop.TryBuy(PowerUpId.ExtraMoves);
                if (!buy.Allowed)
                {
                    Toast.Show(buy.Message, Icon.Info, Theme.Danger);
                    return false;
                }
                AudioService.Sfx(SfxNames.Purchase);
            }
            ApplyPowerUp(PowerUpRequest.Instant(PowerUpId.ExtraMoves));
            if (session.State != SessionState.Playing) return false;
            endScheduled = false;
            board.InputEnabled = true;
            Ctx.BeginRun(session);
            RefreshPowerBar();
            return true;
        }

        public void RestartRun()
        {
            StartRun();
        }

        public void NextLevel()
        {
            int next = session != null && session.Mode == GameMode.Campaign ? session.Level.Number + 1 : Ctx.Campaign.CurrentLevel;
            args = GameArgs.ForCampaign(Mathf.Min(next, Ctx.Campaign.HighestUnlocked));
            StartRun();
        }

        public void PlayAgain()
        {
            StartRun();
        }

        /// <summary>Leaves the game for the map (campaign) or home (other modes), abandoning an unfinished run.</summary>
        public void QuitRun()
        {
            CloseRun();
            if (session != null && session.Mode == GameMode.Campaign) ExitTo(ScreenId.Map, new MapArgs { Focus = session.Level.Number });
            else ExitTo(ScreenId.Home, null);
        }

        public void ExitTo(ScreenId target, object screenArgs)
        {
            Nav.CloseAllPopups();
            Nav.Replace(target, screenArgs);
        }

        public void OpenPause()
        {
            // No pausing during the 3-2-1 countdown: the timer has not started yet anyway.
            if (session == null || paused || endScheduled || countingDown || Nav.IsOpen<PausePopup>()) return;
            paused = true;
            session.SetPaused(true);
            board.CancelPress();
            CoachMark.Hide();
            if (session.IsTimed) board.SetBoardVisible(false);
            var popup = Nav.Open<PausePopup>();
            popup.Setup(this, session.Mode == GameMode.Campaign ? "Quit to Map" : "Quit to Home");
        }

        public void OnPauseClosed()
        {
            paused = false;
            if (session == null) return;
            session.SetPaused(false);
            board.SetBoardVisible(true);
            // Safety net: a timed run that should be under way always resumes with its timer and input live.
            if (session.IsTimed && session.State == SessionState.Playing && !countingDown && !session.TimerRunning)
            {
                session.StartTimer();
                board.InputEnabled = true;
            }
        }

        /// <summary>Android Back on the result screen: leave to the map (campaign) or home.</summary>
        public void LeaveAfterResult()
        {
            if (session != null && session.Mode == GameMode.Campaign) ExitTo(ScreenId.Map, new MapArgs { Focus = session.Level.Number });
            else ExitTo(ScreenId.Home, null);
        }

        public PuzzleSession Session => session;
    }
}
