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

        protected override void Build()
        {
        }

        /// <summary>Android Back leaves the result screen the same way the "Map"/"Home" button does.</summary>
        public override void OnBack()
        {
            if (game != null) game.LeaveAfterResult();
        }

        public void Setup(RunResult r, PuzzleSession s, GameScreen screen)
        {
            result = r;
            session = s;
            game = screen;
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
            ScoreBlock(result.FinalScore, result.LeftoverBonus > 0 ? NumberFormat.Thousands(result.Score) + " + " + NumberFormat.Thousands(result.LeftoverBonus) + " bonus for spare moves" : "Target " + NumberFormat.Thousands(level.Target), t);
            if (result.NewBest) Badge("New best score!", Theme.Gold);
            Rewards(t + 0.2f);
            bool hasNext = Ctx.Campaign.IsUnlocked(level.Number + 1);
            var next = FooterButton(hasNext ? "Next Level" : "Continue", ButtonStyle.Primary, () =>
            {
                Close();
                game.NextLevel();
            }, Icon.Play, 210, 150);
            SecondaryRow("Map", Icon.Map, () => game.ExitTo(ScreenId.Map, new MapArgs { Focus = level.Number + 1, JustCompleted = level.Number }), "Replay", Icon.Restart, () =>
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
                    if (!goals.IsComplete(i)) left.Add(GoalVisuals.Describe(new GoalSpec { kind = g.kind, colour = g.colour, amount = goals.Remaining(i) }).ToLowerInvariant());
                }
                ratio = done / (float)Mathf.Max(1, total);
                Para((ratio >= 0.8f ? "So close! " : "") + "Still to do: " + string.Join(", ", left) + ".");
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

            // Offer Extra Moves only as an explicit choice.
            var def = Ctx.PowerUps.Get(PowerUpId.ExtraMoves);
            var block = Ctx.PowerUpUse.CanSelect(session, PowerUpId.ExtraMoves);
            if (def != null && (block == UseBlock.None || block == UseBlock.NoneOwned))
            {
                bool owned = block == UseBlock.None;
                var check = Ctx.Shop.Check(PowerUpId.ExtraMoves);
                string label = owned ? "Use Extra Moves (+" + def.amount + ")" : "+" + def.amount + " moves for " + NumberFormat.Thousands(def.price);
                var holder = UI.Rect("Revive", body);
                holder.Layout(preferredHeight: 150);
                var revive = UI.Button(holder, label, owned ? ButtonStyle.Success : ButtonStyle.Gold, () =>
                {
                    if (game.TryRevive()) Close();
                }, owned ? Icon.Plus : (Icon?)null, Theme.FontBody);
                ((RectTransform)revive.transform).Fill(40, 10, 40, 10);
                if (!owned && !check.Allowed)
                {
                    revive.Interactable = false;
                    revive.OnDisabledClick = () => Toast.Show(check.Message, Icon.Info, Theme.Danger);
                }
            }
            FooterButton("Try Again", ButtonStyle.Primary, () =>
            {
                Close();
                game.PlayAgain();
            }, Icon.Restart, 210, 150);
            SecondaryRow("Map", Icon.Map, () => game.ExitTo(ScreenId.Map, new MapArgs { Focus = level.Number }), "Home", Icon.Home, () => game.ExitTo(ScreenId.Home, null));
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
            for (int i = 0; i < rush.tierScores.Length; i++)
            {
                bool got = result.Score >= rush.tierScores[i] || Ctx.Rewards.IsClaimed(RewardKeys.WeeklyTier(result.PeriodKey, i));
                var cell = UI.Rect("Tier" + i, row);
                var bg = UI.Rounded(cell, got ? Theme.Gold.WithAlpha(0.25f) : new Color(0, 0, 0, 0.25f), 30, "Bg");
                bg.rectTransform.Fill();
                var name = UI.Text(cell, i < rush.tierNames.Length ? rush.tierNames[i] : "Tier " + (i + 1), Theme.FontSmall, got ? Theme.Gold : Theme.TextSecondary, TextAnchor.MiddleCenter, true, "Name");
                if (got) UI.Icon(cell, Icon.Check, Theme.Gold, 40).rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(40, 40));
                name.rectTransform.Fill(0, 70, 0, 14);
                var score = UI.Text(cell, NumberFormat.Thousands(rush.tierScores[i]), Theme.FontSmall, Theme.Text, TextAnchor.MiddleCenter, true, "Score");
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

        void Para(string text)
        {
            var t = UI.Text(body, text, Theme.FontBody - 2, Theme.TextSecondary, TextAnchor.MiddleCenter, false, "Text");
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
