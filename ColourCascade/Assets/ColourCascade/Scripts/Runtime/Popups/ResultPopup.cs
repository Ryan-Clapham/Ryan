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
            FitHeight(body, BodyTop, BodyBottom, 860f);
        }

        // ------------------------------------------------------------------ Variants

        void CampaignWon()
        {
            var level = result.Level;
            Title(level.IsRegionFinale ? "Region Complete!" : "Level Complete!", false, 46);
            var stars = StarRow.Create(body, 150, 18, true);
            stars.Layout(preferredHeight: 200);
            float t = stars.Reveal(result.Stars, 0.35f, App.GlobalFx);
            ScoreBlock(result.FinalScore, "Final score", t);
            // Where the points came from, line by line; the lines add up to the final score (docs/MECHANICS.md §5).
            Breakdown(t);
            if (result.NewBest) Badge("New best score!", Theme.Gold);
            if (result.KeptPowerUps > 0 && result.KeptPowerUp != null)
                Badge("Kept " + result.KeptPowerUps + " free " + result.KeptPowerUp.displayName + (result.KeptPowerUps == 1 ? "" : "s"), Theme.Teal);
            // Every star's requirement, ticked, with the real result against it, and why the next star was missed.
            StarList(t);
            string why = StarChecklist.WhyNotNext(level, result.FinalScore, result.Stars, result.EndReason == EndReason.FinishedEarly, result.MovesLeft);
            if (why != null) Para(why, Theme.FontSmall);
            Rewards(t + 0.2f);
            bool hasNext = Ctx.Campaign.IsUnlocked(level.Number + 1);
            var next = FooterButton(hasNext ? "Next Level" : "Continue", ButtonStyle.Primary, () => AfterWin(AdTransition.LevelWonNext, () =>
            {
                Close();
                game.NextLevel();
            }), Icon.Play, 210, 150);
            SecondaryRow("Map", Icon.Map, () => AfterWin(AdTransition.LevelWonExit, () => game.ExitTo(ScreenId.Map, new MapArgs { Focus = level.Number + 1, JustCompleted = level.Number })), "Replay", Icon.Restart, () =>
            {
                Close();
                game.PlayAgain();
            });
            next.transform.localScale = Vector3.zero;
            Tw.Scale(next.transform, 1f, 0.35f, Ease.OutBack, t);
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
            var def = Ctx.PowerUps.Get(PowerUpId.ExtraMoves);
            bool ownsExtraMoves = def != null && Ctx.PowerUpUse.CanSelect(session, PowerUpId.ExtraMoves) == UseBlock.None;
            bool adOffer = options.AdMoves > 0 && options.Ad.Status != OfferStatus.Disabled;
            bool anyOffer = adOffer || options.CoinMoves > 0 || ownsExtraMoves;
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
                // An Extra Moves power-up the player already owns can still be used here, as before.
                if (ownsExtraMoves)
                    OfferButton("Use your Extra Moves (+" + def.amount + ")", ButtonStyle.Teal, Icon.Plus, () =>
                    {
                        if (game.TryRevive()) Close();
                    });
                Ctx.Monetisation.Ads.Changed -= RefreshOffers;
                Ctx.Monetisation.Ads.Changed += RefreshOffers;
                RefreshOffers();
                FooterButton("Give Up", ButtonStyle.Ghost, GiveUp, null, 60, 150);
                return;
            }
            if (!options.Available && ownsExtraMoves)
            {
                var holder = UI.Rect("Revive", body);
                holder.Layout(preferredHeight: 150);
                var revive = UI.Button(holder, "Use Extra Moves (+" + def.amount + ")", ButtonStyle.Success, () =>
                {
                    if (game.TryRevive()) Close();
                }, Icon.Plus, Theme.FontBody);
                ((RectTransform)revive.transform).Fill(40, 10, 40, 10);
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

        /// <summary>What each star needs, ticked when earned, with the result against it, and a tip for scoring more.</summary>
        void StarList(float delay)
        {
            var items = StarChecklist.For(result.Level, session != null ? session.Goals : null, result.Passed, result.FinalScore, result.Stars);
            var list = UI.Rect("StarChecklist", body);
            // The line under the list says what the next star needed; the tip only shows when it has nothing to say.
            bool tip = result.Stars < 3 && StarChecklist.WhyNotNext(result.Level, result.FinalScore, result.Stars, false, 0) == null;
            list.Layout(preferredHeight: items.Count * 64 + 6 + (tip ? 64 : 0));
            UI.VStack(list, 4);
            foreach (var item in items)
            {
                var row = UI.Rect("Row" + item.Stars, list);
                row.Layout(preferredHeight: 60);
                var bg = UI.Rounded(row, new Color(0, 0, 0, item.Met ? 0.12f : 0.24f), 26, "Bg");
                bg.rectTransform.Fill();
                for (int k = 0; k < 3; k++)
                {
                    bool show = k < item.Stars;
                    var star = UI.Image(row, ArtLibrary.Instance.Get(show && item.Met ? ArtNames.StarGold : ArtNames.StarEmpty), Color.white, "Star" + k);
                    star.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(36 + k * 34, 0), new Vector2(34, 34));
                    star.gameObject.SetActive(show);
                }
                var text = UI.FitText(row, item.Text, Theme.FontSmall, item.Met ? Theme.TextSecondary : Theme.Text, TextAnchor.MiddleLeft, true, "Text");
                text.rectTransform.Fill(140, 0, 300, 0);
                var achieved = UI.FitText(row, item.Result, Theme.FontSmall - 2, item.Met ? Theme.Success : Theme.TextMuted, TextAnchor.MiddleRight, true, "Result");
                achieved.rectTransform.Fill(0, 0, 80, 0);
                achieved.rectTransform.anchorMin = new Vector2(1, 0);
                achieved.rectTransform.offsetMin = new Vector2(-290, 0);
                if (item.Met)
                {
                    var tick = UI.Icon(row, Icon.Check, Theme.Success, 40);
                    tick.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-42, 0), new Vector2(40, 40));
                }
            }
            if (tip)
            {
                var hint = UI.FitText(list, StarChecklist.Tip(result.Level), Theme.FontSmall - 4, Theme.TextMuted, TextAnchor.MiddleCenter, false, "Tip");
                hint.Layout(preferredHeight: 60);
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
            int rows = (lines.Count + 1) / 2;
            var grid = UI.Rect("Breakdown", body);
            grid.Layout(preferredHeight: rows * 52 + (rows - 1) * 6);
            UI.VStack(grid, 6);
            for (int r = 0; r < rows; r++)
            {
                var row = UI.Rect("Row" + r, grid);
                row.Layout(preferredHeight: 52);
                UI.HStack(row, 10);
                for (int k = 0; k < 2; k++)
                {
                    int i = r * 2 + k;
                    var cell = UI.Rect("Cell" + k, row);
                    if (i >= lines.Count) continue;
                    UI.Rounded(cell, new Color(0, 0, 0, 0.2f), 24, "Bg").rectTransform.Fill();
                    var label = UI.FitText(cell, lines[i].label, Theme.FontSmall - 6, Theme.TextSecondary, TextAnchor.MiddleLeft, true, "Label");
                    label.rectTransform.Fill(20, 0, 130, 0);
                    var value = UI.FitText(cell, NumberFormat.Thousands(lines[i].points), Theme.FontSmall - 4, Theme.Text, TextAnchor.MiddleRight, true, "Value");
                    value.rectTransform.Fill(0, 0, 18, 0);
                    value.rectTransform.anchorMin = new Vector2(1, 0);
                    value.rectTransform.offsetMin = new Vector2(-130, 0);
                }
            }
            grid.localScale = new Vector3(1f, 0f, 1f);
            Tw.Scale(grid, 1f, 0.3f, Ease.OutCubic, delay);
            // What the specials left on the board scored at the end, the same figures as the in-game card.
            var f = result.FinaleBreakdown;
            if (b.Finale > 0 && f != null)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (f.Specials > 0) parts.Add("tiles " + NumberFormat.Thousands(f.Specials));
                if (f.ComboBonus > 0) parts.Add("combo bonus " + NumberFormat.Thousands(f.ComboBonus));
                if (f.Obstacles > 0) parts.Add("ice, chains, moss " + NumberFormat.Thousands(f.Obstacles));
                if (f.Stones > 0) parts.Add("stones " + NumberFormat.Thousands(f.Stones));
                if (f.Treasure > 0) parts.Add("treasure " + NumberFormat.Thousands(f.Treasure));
                var note = UI.FitText(body, "Specials at the end: " + string.Join(" · ", parts), Theme.FontSmall - 6, Theme.TextMuted, TextAnchor.MiddleCenter, false, "FinaleNote");
                note.Layout(preferredHeight: 50);
            }
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
