using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    public sealed class MapArgs
    {
        /// <summary>Level to centre on (0 = current level).</summary>
        public int Focus;
        /// <summary>Level just completed (plays the unlock animation for the next one).</summary>
        public int JustCompleted;
    }

    /// <summary>
    /// The campaign world: an illustrated winding path through themed regions, generated from level data.
    /// Only the levels near the viewport exist as objects (pooled), so the map scales to any number of levels.
    /// </summary>
    public sealed class MapScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Map;

        const float Spacing = 236f;
        const float BottomPad = 420f;
        const float TopPad = 620f;
        const float Amplitude = 250f;
        const float RegionGap = 260f;
        const int DotsPerSegment = 6;

        static readonly Decor[][] DecorSets =
        {
            new[] { Decor.Tree, Decor.Flower, Decor.Hill, Decor.Cloud },
            new[] { Decor.Wave, Decor.Bubble, Decor.Coral, Decor.Cloud },
            new[] { Decor.Crystal, Decor.Sparkle, Decor.Bubble, Decor.Crystal },
            new[] { Decor.Mesa, Decor.Sparkle, Decor.Cloud, Decor.Mesa },
            new[] { Decor.Mountain, Decor.Snowflake, Decor.Cloud, Decor.Snowflake },
            new[] { Decor.Planet, Decor.Sparkle, Decor.Sparkle, Decor.Planet },
        };

        ScrollRect scroll;
        RectTransform viewport;
        RectTransform content;
        RectTransform backdrop;
        RectTransform pathLayer;
        RectTransform nodeLayer;
        Text regionTitle;
        Text starsText;
        CCButton jumpButton;

        int maxLevel;
        int highest;
        int animateUnlockFor;
        readonly Dictionary<int, Slot> live = new Dictionary<int, Slot>();
        readonly Stack<Slot> pool = new Stack<Slot>();
        readonly List<int> scratch = new List<int>();
        int firstVisible = -1, lastVisible = -1;

        sealed class Slot
        {
            public int Level;
            public RectTransform Node;
            public Image Shadow;
            public Image Face;
            public Image Gem;
            public Image Gloss;
            public Image Glow;
            public Text Number;
            public Image Lock;
            public RectTransform Stars;
            public Image[] StarImages = new Image[3];
            public Text Tag;
            public readonly List<Image> Dots = new List<Image>();
            public Image Decor;
            public CCButton Button;
        }

        protected override void Build()
        {
            viewport = UI.Rect("Viewport", Root);
            viewport.Fill();
            // Draw the map behind the header and buttons (which live in the Safe container).
            viewport.SetAsFirstSibling();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            content = UI.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(1, 0);
            content.pivot = new Vector2(0.5f, 0);
            backdrop = UI.Rect("Regions", content);
            backdrop.Fill();
            pathLayer = UI.Rect("Path", content);
            pathLayer.Fill();
            nodeLayer = UI.Rect("Nodes", content);
            nodeLayer.Fill();

            scroll = Root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            scroll.onValueChanged.AddListener(_ => UpdateVisible());

            // Header drawn above the map.
            var header = UI.Rect("Header", Safe);
            header.TopStrip(200, 0, 0, 0);
            var shade = UI.Image(header, ArtLibrary.Instance.Get(ArtNames.Gradient), new Color(0.05f, 0.03f, 0.15f, 0.85f), "Shade");
            shade.rectTransform.Fill(0, -60, 0, -200);
            var bar = UI.Rect("Bar", header);
            bar.TopStrip(Theme.TopBarHeight, 18, Theme.Margin, Theme.Margin);
            var back = UI.IconButton(bar, Icon.Back, ButtonStyle.Secondary, () => Nav.Back(), Theme.IconButtonSize, "Back");
            ((RectTransform)back.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(Theme.IconButtonSize, Theme.IconButtonSize));
            regionTitle = UI.FitText(bar, "", Theme.FontLarge, Theme.Text, TextAnchor.MiddleCenter, true, "Region");
            regionTitle.rectTransform.Fill(140, 0, 260, 0);
            UI.AddShadow(regionTitle, -4, 0.4f);
            var starsChip = UI.Rect("Stars", bar);
            starsChip.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(220, 92));
            UI.Rounded(starsChip, new Color(0.03f, 0.02f, 0.12f, 0.55f), 46, "Bg").rectTransform.Fill();
            var starIcon = UI.Sprite(starsChip, ArtNames.StarGold, 70);
            starIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 2), new Vector2(70, 70));
            starsText = UI.Text(starsChip, "0", Theme.FontBody, Theme.Text, TextAnchor.MiddleCenter, true, "Count");
            starsText.rectTransform.Fill(80, 0, 16, 0);

            jumpButton = UI.Button(Safe, "My level", ButtonStyle.Primary, () => ScrollTo(highest, true), Icon.Target, Theme.FontBody, 44, 8, "Jump");
            ((RectTransform)jumpButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-Theme.Margin, 40), new Vector2(320, 120));
        }

        public override void OnShow(object args)
        {
            App.SetBackground(-1);
            var a = args as MapArgs;
            highest = Ctx.Campaign.HighestUnlocked;
            int lastRegionShown = Ctx.Catalog.RegionForLevel(highest + 10).LastLevel;
            maxLevel = Mathf.Max(lastRegionShown, highest);
            animateUnlockFor = a != null && a.JustCompleted > 0 && a.JustCompleted + 1 == highest ? highest : 0;
            starsText.text = Ctx.Campaign.TotalStars.ToString();
            BuildBackdrop();
            foreach (var kv in live) Recycle(kv.Value);
            live.Clear();
            firstVisible = lastVisible = -1;
            Canvas.ForceUpdateCanvases();
            ScrollTo(a != null && a.Focus > 0 ? Mathf.Min(a.Focus, maxLevel) : highest, false);
            UpdateVisible();
            if (animateUnlockFor > 0) Tw.Delay(0.45f, () => PlayUnlock(animateUnlockFor), this);
        }

        public override void OnHide()
        {
            Tw.Kill(this);
        }

        // ------------------------------------------------------------------ Geometry

        int PerRegion => Ctx.Catalog.LevelsPerRegion;

        static float NodeX(float level) => Mathf.Sin((level - 1f) * 0.78f) * Amplitude;

        /// <summary>Vertical position of a level; regions are separated by a gap that holds their banner.</summary>
        float NodeY(int level) => BottomPad + (level - 1) * Spacing + ((level - 1) / PerRegion) * RegionGap;

        float PathY(float t)
        {
            int a = Mathf.FloorToInt(t);
            return Mathf.Lerp(NodeY(a), NodeY(a + 1), t - a);
        }

        /// <summary>Inverse of NodeY (the level whose slot contains y).</summary>
        int LevelAt(float y)
        {
            float block = PerRegion * Spacing + RegionGap;
            float rel = Mathf.Max(0f, y - BottomPad + Spacing * 0.5f);
            int region = Mathf.FloorToInt(rel / block);
            float within = Mathf.Min(rel - region * block, PerRegion * Spacing - 1f);
            return Mathf.Clamp(region * PerRegion + Mathf.FloorToInt(within / Spacing) + 1, 1, Mathf.Max(1, maxLevel));
        }

        float ContentHeight => NodeY(maxLevel) + TopPad;

        void ScrollTo(int level, bool smooth)
        {
            float viewH = viewport.rect.height;
            content.sizeDelta = new Vector2(0, ContentHeight);
            float y = -(NodeY(level) - viewH * 0.42f);
            y = Mathf.Clamp(y, -(ContentHeight - viewH), 0f);
            scroll.StopMovement();
            if (smooth)
            {
                Tw.Value(content.anchoredPosition.y, y, 0.5f, v =>
                {
                    content.anchoredPosition = new Vector2(0, v);
                    UpdateVisible();
                }, Ease.InOutCubic, 0f, this);
            }
            else
            {
                content.anchoredPosition = new Vector2(0, y);
            }
        }

        // ------------------------------------------------------------------ Regions

        void BuildBackdrop()
        {
            UI.ClearChildren(backdrop);
            int regions = Ctx.Catalog.RegionForLevel(maxLevel).Index + 1;
            for (int i = 0; i < regions; i++)
            {
                var info = Ctx.Catalog.Region(i);
                float seamBelow = i == 0 ? 0 : (NodeY(info.FirstLevel - 1) + NodeY(info.FirstLevel)) * 0.5f;
                float seamAbove = i == regions - 1 ? ContentHeight : (NodeY(info.LastLevel) + NodeY(info.LastLevel + 1)) * 0.5f;
                float bottom = seamBelow;
                float top = seamAbove;
                var band = UI.Image(backdrop, ArtLibrary.Instance.Background(info.Theme), Color.white, "Region" + i);
                band.rectTransform.anchorMin = new Vector2(0, 0);
                band.rectTransform.anchorMax = new Vector2(1, 0);
                band.rectTransform.pivot = new Vector2(0.5f, 0);
                band.rectTransform.anchoredPosition = new Vector2(0, bottom);
                band.rectTransform.sizeDelta = new Vector2(0, top - bottom);
                if (i > 0)
                {
                    // Blend the seam between regions.
                    var prev = Ctx.Catalog.Region(i - 1);
                    var blend = UI.Image(backdrop, ArtLibrary.Instance.Get(ArtNames.Gradient), Theme.Region(prev.Theme, 0), "Blend" + i);
                    blend.rectTransform.anchorMin = new Vector2(0, 0);
                    blend.rectTransform.anchorMax = new Vector2(1, 0);
                    blend.rectTransform.pivot = new Vector2(0.5f, 1);
                    blend.rectTransform.localScale = new Vector3(1, -1, 1);
                    blend.rectTransform.anchoredPosition = new Vector2(0, bottom);
                    blend.rectTransform.sizeDelta = new Vector2(0, 420);
                }
                RegionBanner(info, i == 0 ? BottomPad - 305f : bottom - 75f);
            }
            var more = UI.Rect("ComingUp", backdrop);
            more.anchorMin = more.anchorMax = new Vector2(0.5f, 0);
            more.pivot = new Vector2(0.5f, 0);
            more.anchoredPosition = new Vector2(0, NodeY(maxLevel) + 190f);
            more.sizeDelta = new Vector2(760, 150);
            var card = UI.Card(more, "Card", new Color(0.05f, 0.03f, 0.15f, 0.55f), 50, false);
            var t = UI.FitText(card, "More levels appear as you progress", Theme.FontBody, Theme.Text, TextAnchor.MiddleCenter, true, "Text");
            t.rectTransform.Fill(30, 10, 30, 10);
        }

        void RegionBanner(RegionInfo info, float y)
        {
            var holder = UI.Rect("Banner" + info.Index, backdrop);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0);
            holder.pivot = new Vector2(0.5f, 0);
            holder.anchoredPosition = new Vector2(0, y);
            holder.sizeDelta = new Vector2(820, 150);
            var ribbon = UI.Image(holder, ArtLibrary.Instance.Get(ArtNames.Ribbon), Theme.Region(info.Theme, 2), "Ribbon");
            ribbon.type = Image.Type.Sliced;
            ribbon.rectTransform.Fill();
            var label = UI.FitText(holder, "REGION " + (info.Index + 1) + "  ·  " + info.Name.ToUpperInvariant(), Theme.FontSmall, Theme.TextDark, TextAnchor.MiddleCenter, true, "Name");
            label.rectTransform.Fill(110, 40, 110, 30);
        }

        // ------------------------------------------------------------------ Virtualised levels

        void UpdateVisible()
        {
            if (content == null || maxLevel <= 0) return;
            float viewH = viewport.rect.height;
            float yMin = -content.anchoredPosition.y - 300f;
            float yMax = yMin + viewH + 600f;
            int first = Mathf.Max(1, LevelAt(yMin) - 1);
            int last = Mathf.Min(maxLevel, LevelAt(yMax) + 1);
            if (first != firstVisible || last != lastVisible)
            {
                scratch.Clear();
                foreach (var kv in live) if (kv.Key < first || kv.Key > last) scratch.Add(kv.Key);
                foreach (var n in scratch)
                {
                    Recycle(live[n]);
                    live.Remove(n);
                }
                for (int n = first; n <= last; n++)
                    if (!live.ContainsKey(n)) live[n] = Bind(n);
                firstVisible = first;
                lastVisible = last;
            }

            float centreY = -content.anchoredPosition.y + viewH * 0.5f;
            int centreLevel = LevelAt(centreY);
            var region = Ctx.Catalog.RegionForLevel(centreLevel);
            regionTitle.text = region.Name;
            float currentY = NodeY(highest) + content.anchoredPosition.y;
            bool currentVisible = currentY > 120f && currentY < viewH - 260f;
            if (jumpButton.gameObject.activeSelf == currentVisible) jumpButton.gameObject.SetActive(!currentVisible);
        }

        Slot Bind(int level)
        {
            var s = pool.Count > 0 ? pool.Pop() : CreateSlot();
            s.Level = level;
            var region = Ctx.Catalog.RegionForLevel(level);
            bool milestone = Ctx.Catalog.IsMilestone(level);
            bool unlocked = level <= highest && !(animateUnlockFor == level);
            bool current = level == highest && unlocked;
            bool done = Ctx.Campaign.IsCompleted(level);
            int stars = Ctx.Campaign.StarsFor(level);

            float x = NodeX(level), y = NodeY(level);
            float size = current ? 168f : milestone ? 160f : 136f;
            s.Node.anchoredPosition = new Vector2(x, y);
            s.Node.gameObject.SetActive(true);
            s.Node.localScale = Vector3.one;
            s.Face.rectTransform.sizeDelta = new Vector2(size, size);
            s.Gloss.rectTransform.sizeDelta = new Vector2(size, size);
            s.Shadow.rectTransform.sizeDelta = new Vector2(size * 1.35f, size * 1.35f);
            s.Gem.rectTransform.sizeDelta = new Vector2(size * 1.12f, size * 1.12f);
            s.Glow.rectTransform.sizeDelta = new Vector2(size * 2.1f, size * 2.1f);
            s.Face.gameObject.SetActive(!milestone);
            s.Gloss.gameObject.SetActive(!milestone);
            s.Gem.gameObject.SetActive(milestone);

            Color face;
            Color text;
            if (current) { face = Theme.Primary; text = Theme.Text; }
            else if (done) { face = milestone ? Theme.Gold : Color.white; text = Theme.TextDark; }
            else if (unlocked) { face = Theme.Secondary; text = Theme.Text; }
            else { face = new Color(0.1f, 0.07f, 0.25f, 0.82f); text = new Color(1, 1, 1, 0.5f); }
            s.Face.color = face;
            s.Gem.color = milestone && !unlocked ? new Color(0.35f, 0.32f, 0.55f, 0.9f) : face;
            s.Number.text = level.ToString();
            s.Number.color = text;
            s.Number.fontSize = level >= 100 ? 50 : 60;
            s.Number.gameObject.SetActive(unlocked || milestone);
            s.Lock.gameObject.SetActive(!unlocked);
            s.Lock.rectTransform.anchoredPosition = milestone ? new Vector2(0, -size * 0.55f) : Vector2.zero;
            s.Glow.gameObject.SetActive(current);
            if (current && !Tw.Reduced) StartPulse(s);
            else Tw.Kill(s.Glow);
            s.Stars.gameObject.SetActive(done);
            for (int i = 0; i < 3; i++) s.StarImages[i].sprite = ArtLibrary.Instance.Get(i < stars ? ArtNames.StarGold : ArtNames.StarEmpty);
            s.Stars.anchoredPosition = new Vector2(0, size * 0.5f + 34f);
            s.Tag.gameObject.SetActive(current || (milestone && !done));
            s.Tag.text = current ? "PLAY" : "+" + (Ctx.Catalog.IsRegionFinale(level) ? Ctx.Config.Economy.regionFinaleReward : Ctx.Config.Economy.milestoneReward);
            s.Tag.color = current ? Theme.Text : Theme.Gold;
            s.Tag.rectTransform.anchoredPosition = new Vector2(0, -size * 0.5f - 40f);

            // Path dots from this level to the next.
            bool segmentDone = level + 1 <= highest && animateUnlockFor != level + 1;
            for (int d = 0; d < DotsPerSegment; d++)
            {
                var dot = s.Dots[d];
                bool show = level < maxLevel;
                dot.gameObject.SetActive(show);
                if (!show) continue;
                float t = level + (d + 1f) / (DotsPerSegment + 1f);
                dot.rectTransform.anchoredPosition = new Vector2(NodeX(t), PathY(t));
                dot.color = segmentDone ? new Color(1, 1, 1, 0.95f) : new Color(0.06f, 0.04f, 0.18f, 0.45f);
                dot.rectTransform.sizeDelta = segmentDone ? new Vector2(30, 30) : new Vector2(24, 24);
            }

            // Decoration on the open side of the path.
            var set = DecorSets[region.Theme % DecorSets.Length];
            uint h = (uint)(level * 2654435761u);
            var decor = set[(int)(h % (uint)set.Length)];
            s.Decor.sprite = ArtLibrary.Instance.Decor(decor);
            float side = x > 0 ? -1f : 1f;
            float dx = side * (330f + (h >> 8) % 90);
            float dy = ((int)((h >> 16) % 120)) - 60f;
            float dsize = 130f + (h >> 4) % 90;
            s.Decor.rectTransform.anchoredPosition = new Vector2(dx, y + dy);
            s.Decor.rectTransform.sizeDelta = new Vector2(dsize, dsize);
            s.Decor.color = Color.Lerp(Theme.Region(region.Theme, 3), Color.white, 0.4f).WithAlpha(0.8f);
            s.Decor.gameObject.SetActive(true);

            int lvl = level;
            s.Button.OnClick = () => OnNodeTapped(lvl);
            return s;
        }

        Slot CreateSlot()
        {
            var s = new Slot();
            var decor = UI.Image(pathLayer, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Decor");
            decor.rectTransform.anchorMin = decor.rectTransform.anchorMax = new Vector2(0.5f, 0);
            decor.preserveAspect = true;
            s.Decor = decor;
            for (int d = 0; d < DotsPerSegment; d++)
            {
                var dot = UI.Image(pathLayer, ArtLibrary.Instance.Get(ArtNames.Dot), Color.white, "Dot");
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0);
                s.Dots.Add(dot);
            }
            var node = UI.Rect("Node", nodeLayer);
            node.anchorMin = node.anchorMax = new Vector2(0.5f, 0);
            node.sizeDelta = new Vector2(200, 200);
            s.Node = node;
            s.Glow = UI.Image(node, ArtLibrary.Instance.Get(ArtNames.Glow), Theme.Primary.WithAlpha(0.7f), "Glow");
            s.Shadow = UI.Image(node, ArtLibrary.Instance.Get(ArtNames.CircleShadow), new Color(0, 0, 0, 0.55f), "Shadow");
            s.Shadow.rectTransform.anchoredPosition = new Vector2(0, -12);
            var hit = UI.Image(node, ArtLibrary.Instance.Get(ArtNames.Circle), new Color(0, 0, 0, 0), "Hit");
            hit.rectTransform.Centre(200, 200);
            hit.raycastTarget = true;
            s.Face = UI.Image(node, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Face");
            s.Gem = UI.Image(node, ArtLibrary.Instance.Get(ArtNames.Gem), Color.white, "Gem");
            s.Gloss = UI.Image(node, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.55f), "Gloss");
            s.Number = UI.Text(node, "1", 60, Theme.Text, TextAnchor.MiddleCenter, true, "Number");
            s.Number.rectTransform.Centre(200, 100, 0, 2);
            UI.AddShadow(s.Number, -3, 0.25f);
            s.Lock = UI.Icon(node, Icon.Lock, new Color(1, 1, 1, 0.75f), 60);
            s.Stars = UI.Rect("Stars", node);
            s.Stars.sizeDelta = new Vector2(180, 60);
            for (int i = 0; i < 3; i++)
            {
                var star = UI.Image(s.Stars, ArtLibrary.Instance.Get(ArtNames.StarEmpty), Color.white, "Star" + i);
                star.rectTransform.Centre(i == 1 ? 58 : 48, i == 1 ? 58 : 48, (i - 1) * 52f, i == 1 ? 8 : 0);
                star.rectTransform.localEulerAngles = new Vector3(0, 0, (1 - i) * 14f);
                s.StarImages[i] = star;
            }
            s.Tag = UI.Text(node, "PLAY", Theme.FontSmall, Theme.Text, TextAnchor.MiddleCenter, true, "Tag");
            s.Tag.rectTransform.sizeDelta = new Vector2(220, 50);
            UI.AddShadow(s.Tag, -3, 0.5f);
            s.Button = node.gameObject.AddComponent<CCButton>();
            s.Button.Configure(ButtonStyle.Ghost, 0);
            return s;
        }

        void Recycle(Slot s)
        {
            Tw.Kill(s.Glow);
            Tw.Kill(s.Node);
            s.Node.gameObject.SetActive(false);
            s.Decor.gameObject.SetActive(false);
            foreach (var d in s.Dots) d.gameObject.SetActive(false);
            pool.Push(s);
        }

        void StartPulse(Slot s)
        {
            var glow = s.Glow;
            glow.color = Theme.Primary.WithAlpha(0.7f);
            System.Action loop = null;
            loop = () =>
            {
                if (glow == null || !glow.gameObject.activeInHierarchy) return;
                Tw.Fade(glow, 0.25f, 0.8f, Ease.InOutQuad).OnComplete(() =>
                {
                    if (glow == null || !glow.gameObject.activeInHierarchy) return;
                    Tw.Fade(glow, 0.75f, 0.8f, Ease.InOutQuad).OnComplete(loop);
                });
            };
            loop();
        }

        void OnNodeTapped(int level)
        {
            if (!Ctx.Campaign.IsUnlocked(level))
            {
                Toast.Show("Complete level " + (level - 1) + " to unlock this one", Icon.Lock);
                return;
            }
            Nav.Open<LevelPopup>().Setup(level);
        }

        /// <summary>After a win: light up the path to the new level and pop its node open.</summary>
        void PlayUnlock(int level)
        {
            Slot prev;
            if (live.TryGetValue(level - 1, out prev))
            {
                for (int d = 0; d < prev.Dots.Count; d++)
                {
                    var dot = prev.Dots[d];
                    Tw.Delay(d * 0.07f, () =>
                    {
                        if (dot == null) return;
                        dot.color = Color.white;
                        dot.rectTransform.sizeDelta = new Vector2(30, 30);
                        dot.transform.localScale = Vector3.one * 1.8f;
                        Tw.Scale(dot.transform, 1f, 0.2f, Ease.OutBack);
                    }, this);
                }
            }
            Tw.Delay(DotsPerSegment * 0.07f + 0.1f, () =>
            {
                animateUnlockFor = 0;
                Slot s;
                if (!live.TryGetValue(level, out s)) return;
                Recycle(s);
                live[level] = Bind(level);
                var node = live[level].Node;
                node.localScale = Vector3.one * 0.3f;
                Tw.Scale(node, 1f, 0.5f, Ease.OutBackStrong);
                AudioService.Sfx(SfxNames.Unlock);
                Haptics.Medium();
                App.GlobalFx.Sparkle(App.GlobalFx.ToLocal(node.position), Theme.Gold, 22, 90f);
                App.GlobalFx.Ring(App.GlobalFx.ToLocal(node.position), Color.white, 420f);
            }, this);
        }
    }
}
