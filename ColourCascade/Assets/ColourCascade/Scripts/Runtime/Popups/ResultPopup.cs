using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// End-of-run screen for every mode and outcome: level complete, out of moves, board locked, Endless run
    /// over, Daily result and Weekly Rush time up. Rewards were already granted (once) by GameContext.FinishRun;
    /// this only presents them. Buttons are explicit choices: nothing is bought or spent automatically.
    /// </summary>
    public sealed class ResultPopup : PopupBase
    {
        public override bool Cancellable => false;
        protected override float PanelHeight => 1420f;

        const float BodyTop = 150f;     // title
        const float BodyBottom = 390f;  // main button + secondary row
        const float WonBodyBottom = 350f; // Level Complete: a slightly smaller button pair
        float bodyBottom = BodyBottom;

        RunResult result;
        PuzzleSession session;
        GameScreen game;
        RectTransform body;
        CoinCounter coins;
        bool offersDeclined;
        bool transitioning;
        bool watching;
        CCButton adButton, coinButton;

        protected override void Build()
        {
        }

        void OnDestroy()
        {
            if (GameApp.Instance != null && GameApp.Instance.Ctx != null) Ctx.Monetisation.Ads.Changed -= RefreshOffers;
        }

        /// <summary>Android Back leaves the result screen the same way the "Map"/"Home" button does.</summary>
        public override void OnBack()
        {
            if (game == null) return;
            if (result != null && result.Mode == GameMode.Campaign && result.State == SessionState.Won) AfterWin(AdTransition.LevelWonExit, game.LeaveAfterResult);
            else game.LeaveAfterResult();
        }

        /// <summary>
        /// Leaving a won level's results (to the next level, the map or home) is the natural break where an interstitial
        /// may appear (docs/MONETISATION.md §3); the advert service decides, and the action runs after it either way.
        /// Retry and every other path never show one.
        /// </summary>
        void AfterWin(AdTransition transition, System.Action action)
        {
            if (transitioning) return;
            transitioning = true;
            Ctx.Monetisation.Ads.AtTransition(transition, () =>
            {
                transitioning = false;
                action?.Invoke();
            });
        }

        /// <param name="declinedOffers">Out of Moves: the player gave up on the extra-moves offers.</param>
        public void Setup(RunResult r, PuzzleSession s, GameScreen screen, bool declinedOffers = false)
        {
            result = r;
            session = s;
            game = screen;
            offersDeclined = declinedOffers;
            body = UI.Rect("Body", Content);
            body.Fill(60, BodyBottom, 60, BodyTop);
            UI.VStack(body, 16, null, TextAnchor.UpperCenter);

            switch (r.Mode)
            {
                case GameMode.Campaign:
                    if (r.State == SessionState.Won) CampaignWon();
                    else if (r.State == SessionState.Stuck) Stuck();
                    else CampaignFailed();
                    break;
                case GameMode.Endless:
                    if (r.State == SessionState.Stuck) Stuck(); else EndlessDone();
                    break;
                case GameMode.Daily:
                    if (r.State == SessionState.Stuck) Stuck(); else DailyDone();
                    break;
                default:
                    if (r.State == SessionState.Stuck) Stuck(); else RushDone();
                    break;
            }
            AchievementsUnlocked();
            FitHeight(body, BodyTop, bodyBottom, 860f);
        }

        /// <summary>One line for the achievements this run unlocked (their banners and Claim buttons follow on the menus).</summary>
        void AchievementsUnlocked()
        {
            var list = result.NewAchievements;
            if (list == null || list.Count == 0) return;
            var holder = UI.Rect("Achievements", body);
            holder.Layout(preferredHeight: 56);
            var chipRoot = UI.Rect("Chip", holder);
            chipRoot.Centre(820, 52);
            var bg = UI.Rounded(chipRoot, Theme.Gold.WithAlpha(0.2f), 26, "Bg");
            bg.rectTransform.Fill();
            var icon = UI.Icon(chipRoot, Icon.Trophy, Theme.Gold, 36);
            icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(36, 36));
            var text = UI.FitText(chipRoot, list.Count == 1 ? "Achievement unlocked: " + list[0].title : AchievementText.Several(list.Count), Theme.FontTiny, Theme.Gold, TextAnchor.MiddleCenter, true, "Label");
            text.rectTransform.Fill(70, 0, 24, 0);
        }

        // ------------------------------------------------------------------ Variants

        /// <summary>
        /// Level Complete (3.0.1 layout), in order of importance: the heading, the stars and the final score (the
        /// focus), what each star needed and what the player got, a compact two-column score breakdown, the coins
        /// earned, then Next Level with Map and Replay beneath. Fits the panel on every supported screen: about 1,420
        /// units at most (all eight score lines, a kept power-up and the finished-early note) against at least 1,590
        /// available on the shortest supported screens, so nothing scrolls or clips.
        /// </summary>
        void CampaignWon()
        {
            var level = result.Level;
            bodyBottom = WonBodyBottom;
            body.Fill(60, WonBodyBottom, 60, BodyTop);
            var stack = body.GetComponent<VerticalLayoutGroup>();
            if (stack != null) stack.spacing = 12;
            Title(level.IsRegionFinale ? "Region Complete!" : "Level Complete!", false, 40);
            var stars = StarRow.Create(body, 120, 14, true);
            stars.Layout(preferredHeight: 160);
            float t = stars.Reveal(result.Stars, 0.35f, App.GlobalFx);
            ScoreFocus(result.FinalScore, result.NewBest ? "New best score!" : "Final score", result.NewBest, t);
            // What each star needed, ticked, with the real result against it (this replaces the "why" sentence and the
            // tip, which said the same thing in words).
            StarList(t);
            if (result.EndReason == EndReason.FinishedEarly && result.MovesLeft > 0 && result.Stars < 3)
                Note("You finished with " + result.MovesLeft + (result.MovesLeft == 1 ? " move" : " moves") + " left.");
            // Where the points came from, two to a row; the lines add up to the final score (docs/MECHANICS.md §5).
            Breakdown(t);
            if (result.KeptPowerUps > 0 && result.KeptPowerUp != null)
                SmallChip("Kept " + result.KeptPowerUps + " free " + result.KeptPowerUp.displayName + (result.KeptPowerUps == 1 ? "" : "s"), Theme.Teal);
            CoinsEarned(t + 0.2f);
            bool hasNext = Ctx.Campaign.IsUnlocked(level.Number + 1);
            var next = FooterButton(hasNext ? "Next Level" : "Continue", ButtonStyle.Primary, () => AfterWin(AdTransition.LevelWonNext, () =>
            {
                Close();
                game.NextLevel();
            }), Icon.Play, 186, 140);
            FooterButton("Map", ButtonStyle.Secondary, () => AfterWin(AdTransition.LevelWonExit, () => game.ExitTo(ScreenId.Map, new MapArgs { Focus = level.Number + 1, JustCompleted = level.Number })),
                Icon.Map, 50, 120, 60, 480);
            FooterButton("Replay", ButtonStyle.Ghost, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 50, 120, 480, 60);
            next.transform.localScale = Vector3.zero;
            Tw.Scale(next.transform, 1f, 0.35f, Ease.OutBack, t);
        }

        /// <summary>The final score, large, with a one-line caption under it ("New best score!" in gold).</summary>
        void ScoreFocus(int score, string caption, bool highlight, float delay)
        {
            var block = UI.Rect("Score", body);
            block.Layout(preferredHeight: 150);
            var number = UI.Text(block, "0", 96, Theme.Text, TextAnchor.MiddleCenter, true, "Number");
            number.rectTransform.Fill(0, 36, 0, 0);
            UI.AddShadow(number, -6, 0.4f);
            Tw.Value(0, score, 0.8f, v => number.text = NumberFormat.Thousands(Mathf.RoundToInt(v)), Ease.OutCubic, delay, number);
            var c = UI.FitText(block, caption, Theme.FontSmall - 2, highlight ? Theme.Gold : Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Caption");
            c.rectTransform.BottomStrip(36, 0, 0, 0);
        }

        /// <summary>A single short line of secondary information.</summary>
        void Note(string text)
        {
            var t = UI.FitText(body, text, Theme.FontTiny, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Note");
            t.Layout(preferredHeight: 40);
        }

        void SmallChip(string text, Color colour)
        {
            var holder = UI.Rect("Chip", body);
            holder.Layout(preferredHeight: 52);
            var chip = UI.Chip(holder, text, colour.WithAlpha(0.2f), colour, Theme.FontTiny);
            ((RectTransform)chip.transform.parent).Centre(560, 50);
        }

        /// <summary>Every coin this level paid, in one row: the total, and where it came from underneath.</summary>
        void CoinsEarned(float delay)
        {
            var rewards = result.Rewards;
            if (rewards == null || !rewards.Any) return;
            coins = CoinCounter.Create(Content, null, 80);
            ((RectTransform)coins.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(250, 80));
            int finalBalance = Ctx.Wallet.Balance;
            coins.Show(finalBalance - rewards.TotalCoins);
            var row = UI.Rect("CoinsEarned", body);
            row.Layout(preferredHeight: 84);
            UI.Rounded(row, Theme.Gold.WithAlpha(0.14f), 30, "Bg").rectTransform.Fill();
            var title = UI.FitText(row, "Coins earned", Theme.FontBody - 6, Theme.Text, TextAnchor.MiddleLeft, true, "Label");
            title.rectTransform.TopStrip(40, 8, 28, 210);
            var parts = new System.Collections.Generic.List<string>();
            foreach (var line in rewards.Lines) parts.Add(line.Label + " +" + line.Coins);
            var detail = UI.FitText(row, string.Join("  ·  ", parts), Theme.FontTiny - 3, Theme.TextSecondary, TextAnchor.MiddleLeft, false, "Detail");
            detail.rectTransform.BottomStrip(32, 8, 28, 210);
            var amount = UI.FitText(row, "+" + NumberFormat.Thousands(rewards.TotalCoins), Theme.FontLarge - 4, Theme.Gold, TextAnchor.MiddleRight, true, "Amount");
            amount.rectTransform.Fill(0, 0, 96, 0);
            amount.rectTransform.anchorMin = new Vector2(1, 0);
            amount.rectTransform.offsetMin = new Vector2(-200, 0);
            var icon = UI.Sprite(row, ArtNames.Coin, 60);
            icon.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-52, 0), new Vector2(60, 60));
            row.localScale = Vector3.zero;
            Tw.Scale(row, 1f, 0.3f, Ease.OutBack, delay);
            int count = Mathf.Clamp(rewards.TotalCoins / 5, 3, 12);
            for (int k = 0; k < count; k++) FlyCoin(icon.rectTransform, delay + 0.25f + k * 0.05f);
            Tw.Delay(delay + 0.25f + count * 0.05f + 0.6f, () =>
            {
                if (coins != null) coins.AnimateTo(finalBalance);
                AudioService.Sfx(SfxNames.Coin);
            }, this);
        }

        void CampaignFailed()
        {
            var level = result.Level;
            Title("Out of Moves", false, 46);
            bool scoredEnough = result.Score >= level.Target;
            float ratio;
            var goals = session != null ? session.Goals : null;
            if (level.Goals != null && level.Goals.Length > 0 && goals != null)
            {
                // Goal levels: what is still to do, and how far along the goals are.
                var left = new System.Collections.Generic.List<string>();
                long done = 0, total = 0;
                for (int i = 0; i < goals.Count; i++)
                {
                    var g = goals.Goal(i);
                    int amount = Mathf.Max(1, g.amount);
                    total += amount;
                    done += Mathf.Min(amount, goals.Progress(i));
                    if (goals.IsComplete(i)) continue;
                    var remaining = g.Clone();
                    remaining.amount = goals.Remaining(i);
                    left.Add(GoalVisuals.Describe(remaining).ToLowerInvariant());
                }
                ratio = done / (float)Mathf.Max(1, total);
                // A high score never passes a level on its own: say exactly which goals were left.
                string lead = left.Count == 1 ? "1 goal left: " : left.Count + " goals left: ";
                if (scoredEnough && session.Goals.IndexOf(GoalKind.Score) < 0) lead = "Plenty of points, but " + lead.ToLowerInvariant();
                else if (ratio >= 0.8f) lead = "So close! " + lead;
                Para(lead + string.Join(", ", left) + ".");
                ScoreBlock(result.Score, "Goals " + Mathf.RoundToInt(ratio * 100) + "% done", 0.1f);
            }
            else
            {
                ratio = result.Score / (float)Mathf.Max(1, level.Target);
                Para(ratio >= 0.8f ? "So close! Just " + NumberFormat.Thousands(level.Target - result.Score) + " points short." : "Keep going — bigger groups score far more points.");
                ScoreBlock(result.Score, "Target " + NumberFormat.Thousands(level.Target), 0.1f);
            }
            var bar = ProgressBar.Create(body, ratio >= 0.8f ? Theme.Gold : Theme.Primary, 40);
            bar.Layout(preferredHeight: 60);
            bar.SetFraction(0, false);
            Tw.Delay(0.3f, () => bar.SetFraction(ratio), bar);

            // Out of Moves choices (docs/MONETISATION.md §2.1): an advert for extra moves, coins for fewer, or give up.
            // Every choice is explicit; nothing is spent or shown automatically. Both continue this same level.
            var options = Ctx.Monetisation.Continue.Options(session);
            bool adOffer = options.AdMoves > 0 && options.Ad.Status != OfferStatus.Disabled;
            bool anyOffer = adOffer || options.CoinMoves > 0;
            if (!offersDeclined && options.Available && anyOffer)
            {
                // The player's coins, before they decide (the counter follows the wallet, so a purchase shows at once).
                coins = CoinCounter.Create(Content, null, 80);
                ((RectTransform)coins.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(250, 80));
                coins.Show(Ctx.Wallet.Balance);
                if (adOffer)
                {
                    adButton = OfferButton("Watch an advert: +" + options.AdMoves + " moves", ButtonStyle.Success, Icon.Play, WatchAdToContinue);
                    Ctx.Monetisation.Ads.NoteOffered(RewardedPlacements.ContinueMoves, options.Ad);
                }
                if (options.CoinMoves > 0)
                {
                    coinButton = OfferButton("+" + options.CoinMoves + " moves for " + NumberFormat.Thousands(options.CoinCost), ButtonStyle.Gold, null, ContinueWithCoins);
                    var coin = UI.Sprite(coinButton.FaceRect.Find("Content"), ArtNames.Coin, 52);
                    coin.Layout(preferredWidth: 52);
                    coin.transform.SetAsFirstSibling();
                }
                Ctx.Monetisation.Ads.Changed -= RefreshOffers;
                Ctx.Monetisation.Ads.Changed += RefreshOffers;
                RefreshOffers();
                // Only one button below the offers, so the space reserved under the list is smaller.
                bodyBottom = 240f;
                body.Fill(60, bodyBottom, 60, BodyTop);
                FooterButton("Give Up", ButtonStyle.Ghost, GiveUp, null, 60, 150);
                return;
            }
            FooterButton("Try Again", ButtonStyle.Primary, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 210, 150);
            SecondaryRow("Map", Icon.Map, () => game.ExitTo(ScreenId.Map, new MapArgs { Focus = level.Number }), "Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null));
        }

        CCButton OfferButton(string label, ButtonStyle style, Icon? icon, System.Action onClick)
        {
            var holder = UI.Rect("Offer", body);
            holder.Layout(preferredHeight: 140);
            var b = UI.Button(holder, label, style, onClick, icon, Theme.FontBody - 2);
            ((RectTransform)b.transform).Fill(30, 8, 30, 8);
            return b;
        }

        /// <summary>Keeps the offer buttons honest: an advert still loading, a limit reached, too few coins.</summary>
        void RefreshOffers()
        {
            if (session == null || this == null) return;
            var options = Ctx.Monetisation.Continue.Options(session);
            if (adButton != null)
            {
                var ad = options.Ad;
                adButton.Interactable = ad.CanShow && !watching;
                adButton.SetLabel(ad.CanShow || watching ? "Watch an advert: +" + options.AdMoves + " moves"
                    : ad.Status == OfferStatus.Loading ? "Loading an advert..."
                    : ad.Status == OfferStatus.LimitReached ? "No more adverts for this level"
                    : "Advert not available");
                adButton.OnDisabledClick = () => Toast.Show(Ctx.Monetisation.Continue.Options(session).Ad.Message, Icon.Info, Theme.TextSecondary);
            }
            if (coinButton != null)
            {
                coinButton.Interactable = options.CoinAffordable && !options.CoinLimitReached && !watching;
                coinButton.OnDisabledClick = () =>
                {
                    var o = Ctx.Monetisation.Continue.Options(session);
                    Toast.Show(o.CoinLimitReached ? "No more coin continues for this level." : "You need " + NumberFormat.Thousands(o.CoinShortfall) + " more coins.", Icon.Info, Theme.Danger);
                };
            }
        }

        void WatchAdToContinue()
        {
            if (watching) return;
            var started = Ctx.Monetisation.Continue.WatchAd(session, outcome =>
            {
                watching = false;
                if (outcome == AdOutcome.Rewarded && session.State == SessionState.Playing)
                {
                    Close();
                    game.ResumeAfterContinue("+" + Ctx.Monetisation.Continue.AdMoves + " moves. Keep going!");
                    return;
                }
                if (outcome == AdOutcome.ClosedEarly) Toast.Show("The advert ended early, so no moves were added. Your level is still here.", Icon.Info, Theme.TextSecondary);
                else if (outcome == AdOutcome.FailedToShow) Toast.Show("The advert couldn't be shown. Your level is still here: try again or use coins.", Icon.Info, Theme.Danger);
                RefreshOffers();
            });
            if (started)
            {
                watching = true;
                RefreshOffers();
            }
            else Toast.Show(Ctx.Monetisation.Continue.Options(session).Ad.Message, Icon.Info, Theme.TextSecondary);
        }

        void ContinueWithCoins()
        {
            if (watching) return;
            var r = Ctx.Monetisation.Continue.ContinueWithCoins(session);
            if (r == CoinContinueResult.Done)
            {
                Ctx.Save();
                AudioService.Sfx(SfxNames.Purchase);
                Haptics.Medium();
                Close();
                game.ResumeAfterContinue("+" + Ctx.Monetisation.Continue.CoinMoves + " moves for " + NumberFormat.Thousands(Ctx.Monetisation.Continue.CoinCost) + " coins");
                return;
            }
            var o = Ctx.Monetisation.Continue.Options(session);
            Toast.Show(r == CoinContinueResult.InsufficientCoins ? "You need " + NumberFormat.Thousands(o.CoinShortfall) + " more coins."
                : r == CoinContinueResult.LimitReached ? "No more coin continues for this level." : "This level can't be continued.", Icon.Info, Theme.Danger);
            RefreshOffers();
        }

        /// <summary>"Give Up": the level ends as it is (already recorded as not passed); show the usual choices.</summary>
        void GiveUp()
        {
            if (watching) return;
            var r = result;
            var s = session;
            var g = game;
            Close();
            Nav.Open<ResultPopup>().Setup(r, s, g, true);
        }

        void Stuck()
        {
            Title("Board Locked", false, 46);
            Para("No group could be made and the tiles couldn't be reshuffled into one. This doesn't count against you — your progress is safe.");
            ScoreBlock(result.Score, "Score so far", 0.1f);
            FooterButton("Play Again", ButtonStyle.Primary, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 210, 150);
            if (result.Mode == GameMode.Campaign)
                SecondaryRow("Map", Icon.Map, () => game.ExitTo(ScreenId.Map, new MapArgs { Focus = result.Level.Number }), "Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null));
            else
                SecondaryRow("Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null), null, Icon.Home, null);
        }

        void EndlessDone()
        {
            Title("Run Complete!", false, 46);
            ScoreBlock(result.Score, result.NewBest ? "" : "Best " + NumberFormat.Thousands(Mathf.Max(result.PreviousBest, result.Score)), 0.1f);
            if (result.NewBest) Badge("New personal best!", Theme.Gold);
            StatsRow(("Moves", result.MovesMade.ToString()), ("Biggest group", result.BiggestGroup.ToString()), ("Stage", result.Stage.ToString()));
            // The journey: how far this run got, and the stage it reached.
            var plan = EndlessJourney.For(result.Stage, Ctx.Config.Endless.journey);
            if (result.Stage > result.BestStageBefore && result.BestStageBefore > 0) Badge("New best: stage " + result.Stage + " (" + plan.Title + ")", Theme.Gold);
            else Badge("Reached stage " + result.Stage + ": " + plan.Title, Theme.Teal);
            MechanicSet next;
            int nextAt = EndlessJourney.NextIntroduction(result.Stage, Ctx.Config.Endless.journey, out next);
            if (nextAt > 0) Para("Stage " + nextAt + " brings " + JourneyNames(next) + ". Big groups earn moves to get there.", Theme.FontSmall);
            Rewards(0.6f);
            FooterButton("Play Again", ButtonStyle.Primary, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 210, 150);
            SecondaryRow("Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null), null, Icon.Home, null);
        }

        void DailyDone()
        {
            Title(result.Success ? "Daily Complete!" : "Daily Challenge", false, 46);
            var stars = StarRow.Create(body, 130, 16, true);
            stars.Layout(preferredHeight: 170);
            float t = stars.Reveal(result.Stars, 0.3f, App.GlobalFx);
            ScoreBlock(result.FinalScore, result.Success ? "Best today " + NumberFormat.Thousands(Mathf.Max(result.PreviousBest, result.FinalScore)) : "Target " + NumberFormat.Thousands(result.Level.Target) + " — try again!", t);
            if (result.NewBest) Badge("New best today!", Theme.Gold);
            if (!result.Success && session != null && session.Goals != null && result.Level.Goals != null && result.Level.Goals.Length > 0)
            {
                var left = new System.Collections.Generic.List<string>();
                for (int i = 0; i < session.Goals.Count; i++)
                {
                    if (session.Goals.IsComplete(i)) continue;
                    var g = session.Goals.Goal(i);
                    left.Add(GoalVisuals.Describe(new GoalSpec { kind = g.kind, colour = g.colour, amount = session.Goals.Remaining(i) }).ToLowerInvariant());
                }
                if (left.Count > 0) Para("Still to do: " + string.Join(", ", left) + ".");
            }
            Rewards(t + 0.2f);
            Para("Next puzzle in " + NumberFormat.Duration(Ctx.TimeToDailyReset));
            FooterButton(result.Success ? "Play Again" : "Try Again", ButtonStyle.Primary, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 210, 150);
            SecondaryRow("Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null), null, Icon.Home, null);
        }

        void RushDone()
        {
            Title("Time's Up!", false, 46);
            ScoreBlock(result.Score, "Best this week " + NumberFormat.Thousands(Mathf.Max(result.PreviousBest, result.Score)), 0.1f);
            if (result.NewBest) Badge("New weekly best!", Theme.Gold);
            var rush = Ctx.Config.Rush;
            var row = UI.Rect("Tiers", body);
            row.Layout(preferredHeight: 150);
            UI.HStack(row, 16);
            // The medal scores of the board that was played (adjusted for its tile mechanics).
            var tiers = new[] { result.Level.Target, result.Level.Star2, result.Level.Star3 };
            for (int i = 0; i < rush.tierScores.Length && i < tiers.Length; i++)
            {
                bool got = result.Score >= tiers[i] || Ctx.Rewards.IsClaimed(RewardKeys.WeeklyTier(result.PeriodKey, i));
                var cell = UI.Rect("Tier" + i, row);
                var bg = UI.Rounded(cell, got ? Theme.Gold.WithAlpha(0.25f) : new Color(0, 0, 0, 0.25f), 30, "Bg");
                bg.rectTransform.Fill();
                var name = UI.Text(cell, i < rush.tierNames.Length ? rush.tierNames[i] : "Tier " + (i + 1), Theme.FontSmall, got ? Theme.Gold : Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Name");
                if (got) UI.Icon(cell, Icon.Check, Theme.Gold, 40).rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(40, 40));
                name.rectTransform.Fill(0, 70, 0, 14);
                var score = UI.Text(cell, NumberFormat.Thousands(tiers[i]), Theme.FontSmall, Theme.Text, TextAnchor.MiddleCenter, true, "Score");
                score.rectTransform.Fill(0, 14, 0, 70);
            }
            Rewards(0.5f);
            Para("New board in " + NumberFormat.Duration(Ctx.TimeToWeeklyReset));
            FooterButton("Play Again", ButtonStyle.Primary, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 210, 150);
            SecondaryRow("Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null), null, Icon.Home, null);
        }

        // ------------------------------------------------------------------ Building blocks

        /// <summary>What each star needs, ticked when earned, with the result against it (one compact row per star).</summary>
        void StarList(float delay)
        {
            var items = StarChecklist.For(result.Level, session != null ? session.Goals : null, result.Passed, result.FinalScore, result.Stars);
            var list = UI.Rect("StarChecklist", body);
            const float rowHeight = 52f, gap = 4f;
            list.Layout(preferredHeight: items.Count * rowHeight + Mathf.Max(0, items.Count - 1) * gap);
            UI.VStack(list, gap);
            foreach (var item in items)
            {
                var row = UI.Rect("Row" + item.Stars, list);
                row.Layout(preferredHeight: rowHeight);
                var bg = UI.Rounded(row, new Color(0, 0, 0, item.Met ? 0.12f : 0.24f), 24, "Bg");
                bg.rectTransform.Fill();
                for (int k = 0; k < 3; k++)
                {
                    bool show = k < item.Stars;
                    var star = UI.Image(row, ArtLibrary.Instance.Get(show && item.Met ? ArtNames.StarGold : ArtNames.StarEmpty), Color.white, "Star" + k);
                    star.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32 + k * 30, 0), new Vector2(30, 30));
                    star.gameObject.SetActive(show);
                }
                var text = UI.FitText(row, item.Text, Theme.FontSmall - 2, item.Met ? Theme.TextSecondary : Theme.Text, TextAnchor.MiddleLeft, true, "Text");
                text.rectTransform.Fill(122, 2, 290, 2);
                var achieved = UI.FitText(row, item.Result, Theme.FontSmall - 4, item.Met ? Theme.Success : Theme.TextMuted, TextAnchor.MiddleRight, true, "Result");
                achieved.rectTransform.Fill(0, 2, 72, 2);
                achieved.rectTransform.anchorMin = new Vector2(1, 0);
                achieved.rectTransform.offsetMin = new Vector2(-280, 2);
                if (item.Met)
                {
                    var tick = UI.Icon(row, Icon.Check, Theme.Success, 36);
                    tick.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-38, 0), new Vector2(36, 36));
                }
            }
            list.localScale = new Vector3(1f, 0f, 1f);
            Tw.Scale(list, 1f, 0.3f, Ease.OutCubic, delay + 0.1f);
        }

        /// <summary>The score broken down by source, two to a row, only the lines that scored.</summary>
        void Breakdown(float delay)
        {
            var b = result.Breakdown;
            if (b == null) return;
            var lines = new System.Collections.Generic.List<(string label, int points)>();
            void Line(string label, int points)
            {
                if (points > 0) lines.Add((label, points));
            }
            Line("Groups", b.Groups);
            Line("Special tiles", b.Specials);
            Line("Combo bonus", b.ComboBonus);
            Line("Power-ups", b.PowerUps);
            Line("Ice, chains, moss", b.Obstacles);
            Line("Stones", b.Stones);
            Line("Treasure", b.Treasure);
            Line("Specials at the end", b.Finale);
            if (lines.Count == 0) return;
            // Compact: 46-unit cells, two to a row (at most four rows for all eight lines). The details of what the
            // specials at the end scored were shown on the board when they went off; here they are one line.
            const float cellHeight = 46f, gap = 6f;
            int rows = (lines.Count + 1) / 2;
            var grid = UI.Rect("Breakdown", body);
            grid.Layout(preferredHeight: rows * cellHeight + (rows - 1) * gap);
            UI.VStack(grid, gap);
            for (int r = 0; r < rows; r++)
            {
                var row = UI.Rect("Row" + r, grid);
                row.Layout(preferredHeight: cellHeight);
                UI.HStack(row, 10);
                for (int k = 0; k < 2; k++)
                {
                    int i = r * 2 + k;
                    var cell = UI.Rect("Cell" + k, row);
                    if (i >= lines.Count) continue;
                    UI.Rounded(cell, new Color(0, 0, 0, 0.2f), 22, "Bg").rectTransform.Fill();
                    var label = UI.FitText(cell, lines[i].label, Theme.FontSmall - 8, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Label");
                    label.rectTransform.Fill(18, 0, 124, 0);
                    var value = UI.FitText(cell, NumberFormat.Thousands(lines[i].points), Theme.FontSmall - 6, Theme.Text, TextAnchor.MiddleRight, true, "Value");
                    value.rectTransform.Fill(0, 0, 16, 0);
                    value.rectTransform.anchorMin = new Vector2(1, 0);
                    value.rectTransform.offsetMin = new Vector2(-120, 0);
                }
            }
            grid.localScale = new Vector3(1f, 0f, 1f);
            Tw.Scale(grid, 1f, 0.3f, Ease.OutCubic, delay);
        }

        static string JourneyNames(MechanicSet set)
        {
            if (set.Has(MechanicSet.Rockets) || set.Has(MechanicSet.Bombs)) return "rockets and bombs";
            return Mechanics.Names(set);
        }

        void Para(string text, int fontSize = Theme.FontBody - 2)
        {
            var t = UI.Text(body, text, fontSize, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Text");
            t.Layout(preferredHeight: 110);
        }

        void Badge(string text, Color colour)
        {
            var holder = UI.Rect("Badge", body);
            holder.Layout(preferredHeight: 70);
            var chip = UI.Chip(holder, text, colour.WithAlpha(0.2f), colour, Theme.FontSmall);
            ((RectTransform)chip.transform.parent).Centre(560, 66);
            holder.localScale = Vector3.zero;
            Tw.Scale(holder, 1f, 0.4f, Ease.OutBackStrong, 0.9f);
        }

        void ScoreBlock(int score, string caption, float delay)
        {
            var block = UI.Rect("Score", body);
            block.Layout(preferredHeight: 170);
            var number = UI.Text(block, "0", 104, Theme.Text, TextAnchor.MiddleCenter, true, "Number");
            number.rectTransform.Fill(0, 50, 0, 0);
            UI.AddShadow(number, -6, 0.4f);
            Tw.Value(0, score, 0.8f, v => number.text = NumberFormat.Thousands(Mathf.RoundToInt(v)), Ease.OutCubic, delay, number);
            if (!string.IsNullOrEmpty(caption))
            {
                var c = UI.FitText(block, caption, Theme.FontSmall, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Caption");
                c.rectTransform.Fill(0, 0, 0, 120);
            }
        }

        void StatsRow(params (string label, string value)[] stats)
        {
            var row = UI.Rect("Stats", body);
            row.Layout(preferredHeight: 150);
            UI.HStack(row, 16);
            foreach (var s in stats)
            {
                var cell = UI.Rect(s.label, row);
                UI.Rounded(cell, new Color(0, 0, 0, 0.25f), 30, "Bg").rectTransform.Fill();
                var v = UI.Text(cell, s.value, Theme.FontHeading, Theme.Text, TextAnchor.MiddleCenter, true, "Value");
                v.rectTransform.Fill(0, 50, 0, 10);
                var l = UI.FitText(cell, s.label, Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Label");
                l.rectTransform.Fill(8, 12, 8, 96);
            }
        }

        /// <summary>Lists coins earned, with coins flying into a counter that counts up.</summary>
        void Rewards(float delay)
        {
            var rewards = result.Rewards;
            if (rewards == null || !rewards.Any) return;
            coins = CoinCounter.Create(Content, null, 80);
            ((RectTransform)coins.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(250, 80));
            int finalBalance = Ctx.Wallet.Balance;
            coins.Show(finalBalance - rewards.TotalCoins);
            var list = UI.Rect("Rewards", body);
            list.Layout(preferredHeight: 22 + rewards.Lines.Count * 78);
            UI.VStack(list, 8, new RectOffset(0, 0, 10, 0));
            int index = 0;
            foreach (var line in rewards.Lines)
            {
                var row = UI.Rect("Reward", list);
                row.Layout(preferredHeight: 70);
                UI.Rounded(row, Theme.Gold.WithAlpha(0.14f), 30, "Bg").rectTransform.Fill();
                var label = UI.FitText(row, line.Label, Theme.FontBody - 4, Theme.Text, TextAnchor.MiddleLeft, true, "Label");
                label.rectTransform.Fill(30, 0, 220, 0);
                var amount = UI.Text(row, "+" + line.Coins, Theme.FontBody, Theme.Gold, TextAnchor.MiddleRight, true, "Amount");
                amount.rectTransform.Fill(0, 0, 90, 0);
                var icon = UI.Sprite(row, ArtNames.Coin, 56);
                icon.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-48, 0), new Vector2(56, 56));
                row.localScale = Vector3.zero;
                float d = delay + index * 0.18f;
                Tw.Scale(row, 1f, 0.3f, Ease.OutBack, d);
                var src = icon.rectTransform;
                int count = Mathf.Clamp(line.Coins / 5, 3, 10);
                for (int k = 0; k < count; k++) FlyCoin(src, d + 0.25f + k * 0.05f);
                index++;
            }
            float end = delay + index * 0.18f + 0.25f + 0.6f;
            Tw.Delay(end, () =>
            {
                if (coins != null) coins.AnimateTo(finalBalance);
                AudioService.Sfx(SfxNames.Coin);
            }, this);
        }

        void FlyCoin(RectTransform from, float delay)
        {
            Tw.Delay(delay, () =>
            {
                if (coins == null || from == null) return;
                var overlay = App.OverlayLayer;
                var img = UI.Sprite(overlay, ArtNames.Coin, 54);
                var start = Local(overlay, from.position) + Random.insideUnitCircle * 20f;
                var end = Local(overlay, coins.CoinIcon.position);
                img.rectTransform.anchoredPosition = start;
                var mid = Vector2.Lerp(start, end, 0.5f) + new Vector2(Random.Range(-120f, 120f), 120f);
                Tw.Move(img.rectTransform, mid, 0.22f, Ease.OutQuad).OnComplete(() =>
                {
                    if (img == null) return;
                    Tw.Move(img.rectTransform, end, 0.3f, Ease.InQuad).OnComplete(() =>
                    {
                        if (img != null) Destroy(img.gameObject);
                        AudioService.Sfx(SfxNames.Tap, 0.4f, 1.6f);
                    });
                });
            }, this);
        }

        static Vector2 Local(RectTransform space, Vector3 world)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, RectTransformUtility.WorldToScreenPoint(null, world), null, out local);
            return local;
        }

        void SecondaryRow(string leftLabel, Icon leftIcon, System.Action left, string rightLabel, Icon rightIcon, System.Action right)
        {
            if (string.IsNullOrEmpty(rightLabel))
            {
                FooterButton(leftLabel, ButtonStyle.Secondary, left, leftIcon, 50, 136);
                return;
            }
            FooterButton(leftLabel, ButtonStyle.Secondary, left, leftIcon, 50, 136, 60, 480);
            FooterButton(rightLabel, ButtonStyle.Ghost, right, rightIcon, 50, 136, 480, 60);
        }
    }
}
