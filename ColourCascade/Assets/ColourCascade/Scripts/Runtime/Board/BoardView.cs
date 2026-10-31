using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// One tile on screen. The root is cell-sized; the body is scaled for highlight and animation. A colour tile can
    /// carry an obstacle overlay (ice, chain, moss) with a small badge for its strength; special tiles and treasure
    /// show their own art instead of a colour.
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        public RectTransform Rt;
        public Image Body;
        public Image Overlay;
        public Image Symbol;
        public Image Glow;
        public RectTransform Badge;
        public Text BadgeText;
        public int Id;
        [System.NonSerialized] public GridPos Cell; // runtime state only, never saved with the object
        [System.NonSerialized] public Tile Data;
        public int Colour = -1;
        public bool IsStone;
        bool symbolsOn;

        public static TileView Create(Transform parent)
        {
            var rt = UI.Rect("Tile", parent);
            var v = rt.gameObject.AddComponent<TileView>();
            v.Rt = rt;
            v.Glow = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.TileGlow), new Color(1, 1, 1, 0), "Glow");
            v.Body = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.Tile(0)), Color.white, "Body");
            v.Overlay = UI.Image(v.Body.rectTransform, ArtLibrary.Instance.Get(ArtNames.Ice1), Color.white, "Overlay");
            v.Overlay.gameObject.SetActive(false);
            v.Symbol = UI.Image(v.Body.rectTransform, ArtLibrary.Instance.Get(ArtNames.Symbol(0)), Color.white, "Symbol");
            v.Badge = UI.Rect("Badge", v.Body.rectTransform);
            var badgeBg = UI.Image(v.Badge, ArtLibrary.Instance.Get(ArtNames.Circle), new Color(0.07f, 0.05f, 0.2f, 0.9f), "Bg");
            badgeBg.rectTransform.Fill();
            v.BadgeText = UI.Text(v.Badge, "2", 30, Color.white, TextAnchor.MiddleCenter, true, "Count");
            v.BadgeText.rectTransform.Fill();
            v.Badge.gameObject.SetActive(false);
            return v;
        }

        public bool IsPiece => Data.Type == CellType.Special || Data.Type == CellType.Treasure;

        public void Setup(Tile t, GridPos cell, float size, bool symbols)
        {
            Data = t;
            Id = t.Id;
            Cell = cell;
            IsStone = t.Type == CellType.Stone;
            symbolsOn = symbols;
            Rt.sizeDelta = new Vector2(size, size);
            Rt.localScale = Vector3.one;
            Rt.localEulerAngles = Vector3.zero;
            Body.rectTransform.Centre(size * 0.94f, size * 0.94f);
            Body.rectTransform.localScale = Vector3.one;
            Body.rectTransform.localEulerAngles = Vector3.zero;
            Overlay.rectTransform.Fill();
            Overlay.rectTransform.localScale = Vector3.one;
            Overlay.color = Color.white;
            Glow.rectTransform.Centre(size * 1.4f, size * 1.4f);
            Glow.color = new Color(1, 1, 1, 0);
            Symbol.rectTransform.Centre(size * 0.54f, size * 0.54f, 0, size * 0.015f);
            float badge = Mathf.Max(26f, size * 0.3f);
            Badge.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-badge * 0.42f, -badge * 0.42f), new Vector2(badge, badge));
            BadgeText.fontSize = Mathf.RoundToInt(badge * 0.62f);
            Body.color = Color.white;
            switch (t.Type)
            {
                case CellType.Stone:
                    Colour = -1;
                    Body.sprite = ArtLibrary.Instance.Get(ArtNames.Stone);
                    Symbol.gameObject.SetActive(false);
                    SetObstacle(ObstacleKind.None, 0, false);
                    break;
                case CellType.Special:
                    Colour = -1;
                    Body.sprite = ArtLibrary.Instance.Get(ArtNames.Special(t.Special));
                    Symbol.gameObject.SetActive(false);
                    SetObstacle(ObstacleKind.None, 0, false);
                    break;
                case CellType.Treasure:
                    Colour = -1;
                    Body.sprite = ArtLibrary.Instance.Get(ArtNames.Treasure);
                    Symbol.gameObject.SetActive(false);
                    SetObstacle(ObstacleKind.None, 0, false);
                    break;
                default:
                    SetColour(t.Colour, symbols);
                    SetObstacle(t.Obstacle, t.Strength, false);
                    break;
            }
            gameObject.SetActive(true);
        }

        public void SetColour(int colour, bool symbols)
        {
            Colour = colour;
            Data.Colour = colour;
            Body.sprite = ArtLibrary.Instance.Tile(colour);
            Symbol.sprite = ArtLibrary.Instance.Symbol(colour);
            Symbol.gameObject.SetActive(symbols);
        }

        /// <summary>Shows an obstacle (or none) and its strength. Ice and chains show a number from two up.</summary>
        public void SetObstacle(ObstacleKind kind, int strength, bool animate)
        {
            Data.Obstacle = kind;
            Data.Strength = kind == ObstacleKind.None ? 0 : strength;
            string sprite = ArtNames.Obstacle(kind, strength);
            Overlay.gameObject.SetActive(sprite != null);
            if (sprite != null) Overlay.sprite = ArtLibrary.Instance.Get(sprite);
            bool count = (kind == ObstacleKind.Ice || kind == ObstacleKind.Chain) && strength >= 2;
            Badge.gameObject.SetActive(count);
            if (count) BadgeText.text = strength.ToString();
            if (animate && sprite != null)
            {
                Overlay.rectTransform.localScale = Vector3.one * 1.18f;
                Tw.Scale(Overlay.rectTransform, 1f, 0.22f, Ease.OutBack);
            }
        }

        public void SetSymbols(bool on, bool animate)
        {
            symbolsOn = on;
            if (Data.Type != CellType.Colour) return;
            bool was = Symbol.gameObject.activeSelf;
            Symbol.gameObject.SetActive(on);
            if (on && !was && animate)
            {
                Symbol.transform.localScale = Vector3.zero;
                Tw.Scale(Symbol.transform, 1f, 0.25f, Ease.OutBack);
            }
        }

        public void SetHighlight(bool lifted, bool dimmed, Color glow)
        {
            Tw.Scale(Body.transform, lifted ? 1.09f : 1f, 0.1f, Ease.OutQuad);
            Glow.color = lifted ? glow : new Color(1, 1, 1, 0);
            Body.color = dimmed ? new Color(0.55f, 0.55f, 0.66f, 1f) : Color.white;
        }
    }

    /// <summary>
    /// Draws a PuzzleSession's board and animates each MoveOutcome. Input: press a tile to preview what it
    /// would do, drag to explore other tiles, release to commit, or slide off the board to cancel.
    /// The view never changes game state; GameScreen applies moves to the session and passes the outcome here.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const float FramePadding = 18f;

        /// <summary>Fill the list with the cells this press would affect; return 0 if nothing would happen.</summary>
        public Func<GridPos, List<GridPos>, int> PreviewProvider;
        public Action<GridPos, List<GridPos>, int> PreviewShown;
        public Action PreviewHidden;
        public Action<GridPos> Committed;
        public Action<GridPos> InvalidPress;
        /// <summary>Raised when a dead-board shuffle animation starts.</summary>
        public Action ShuffleStarted;

        RectTransform frame;
        Image frameImage;
        RectTransform slotsLayer;
        RectTransform tilesLayer;
        public FxLayer Fx { get; private set; }

        PuzzleSession session;
        readonly Dictionary<int, TileView> views = new Dictionary<int, TileView>();
        readonly Stack<TileView> pool = new Stack<TileView>();
        readonly List<Image> slots = new List<Image>();
        readonly List<GridPos> highlight = new List<GridPos>();
        readonly List<GridPos> marked = new List<GridPos>();
        Color markGlow;
        bool highlightShown;
        bool[] highlightMask = new bool[0];
        Color highlightGlow = new Color(1, 1, 1, 0.85f);
        int width, height;
        float cell = 100f;
        int activePointer = int.MinValue;
        GridPos previewCell;
        bool previewActive;
        GridPos pressCell;
        bool pressValid;
        float busyUntil;
        bool symbols;

        public bool InputEnabled = true;
        /// <summary>Animation speed multiplier (Weekly Rush plays faster so the clock feels fair).</summary>
        public float Speed = 1f;
        public float CellSize => cell;
        public bool Busy => Time.unscaledTime < busyUntil;
        public RectTransform Frame => frame;

        public static BoardView Create(Transform parent)
        {
            var rt = UI.Rect("Board", parent);
            rt.Fill();
            var bv = rt.gameObject.AddComponent<BoardView>();
            bv.frame = UI.Rect("Frame", rt);
            bv.frame.Centre(800, 900);
            var shadow = UI.Rounded(bv.frame, new Color(0.02f, 0.01f, 0.08f, 0.55f), 52, "Shadow", ArtNames.RoundShadow);
            shadow.rectTransform.Fill(-24, -36, -24, -14);
            bv.frameImage = UI.Rounded(bv.frame, Theme.BoardBack.WithAlpha(0.82f), 44, "Back");
            bv.frameImage.rectTransform.Fill();
            bv.frameImage.raycastTarget = true;
            var stroke = UI.Rounded(bv.frame, new Color(1, 1, 1, 0.08f), 44, "Stroke", ArtNames.RoundStroke);
            stroke.rectTransform.Fill();
            bv.slotsLayer = UI.Rect("Slots", bv.frame);
            bv.slotsLayer.Centre(100, 100);
            var clip = UI.Rect("Clip", bv.frame);
            clip.Fill(4, 4, 4, 4);
            clip.gameObject.AddComponent<RectMask2D>();
            bv.tilesLayer = UI.Rect("Tiles", clip);
            bv.tilesLayer.Centre(100, 100);
            bv.Fx = FxLayer.Create(rt, "BoardFx");
            return bv;
        }

        // ------------------------------------------------------------------ Binding and layout

        public void Bind(PuzzleSession s, bool showSymbols)
        {
            session = s;
            symbols = showSymbols;
            width = s.Board.Width;
            height = s.Board.Height;
            highlightMask = new bool[width * height];
            foreach (var v in views.Values) Recycle(v);
            views.Clear();
            foreach (var slot in slots) UI.Destroy(slot);
            slots.Clear();
            Fx.Clear();
            highlight.Clear();
            marked.Clear();
            highlightShown = false;
            previewActive = false;
            activePointer = int.MinValue;
            busyUntil = 0;
            var b = s.Board;
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    var t = b[c, r];
                    if (t.Type == CellType.Void) continue;
                    var slot = UI.Rounded(slotsLayer, Theme.Slot.WithAlpha(0.6f), 18, "Slot");
                    slot.name = "Slot" + c + "_" + r;
                    slots.Add(slot);
                    if (t.Type != CellType.Empty) CreateView(t, new GridPos(c, r));
                }
            }
            Relayout();
        }

        /// <summary>Fits the board into the given area (reference pixels).</summary>
        public void FitInto(Vector2 area)
        {
            if (width == 0) return;
            float maxCell = 172f;
            cell = Mathf.Floor(Mathf.Min((area.x - FramePadding * 2) / width, (area.y - FramePadding * 2) / height, maxCell));
            cell = Mathf.Max(40f, cell);
            Relayout();
        }

        void Relayout()
        {
            if (width == 0) return;
            var size = new Vector2(width * cell, height * cell);
            frame.sizeDelta = size + new Vector2(FramePadding * 2, FramePadding * 2);
            slotsLayer.sizeDelta = size;
            tilesLayer.sizeDelta = size;
            int i = 0;
            var b = session.Board;
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                {
                    if (b[c, r].Type == CellType.Void) continue;
                    if (i >= slots.Count) break;
                    var slot = slots[i++];
                    slot.rectTransform.anchoredPosition = CellLocal(new GridPos(c, r));
                    slot.rectTransform.sizeDelta = new Vector2(cell * 0.9f, cell * 0.9f);
                    UI.SetRadius(slot, cell * 0.2f);
                }
            foreach (var v in views.Values)
            {
                Tw.Kill(v.Rt);
                v.Setup(v.Data, v.Cell, cell, symbols);
                v.Rt.anchoredPosition = CellLocal(v.Cell);
            }
        }

        public Vector2 CellLocal(GridPos p)
        {
            return new Vector2((p.Col + 0.5f) * cell - width * cell / 2f, (p.Row + 0.5f) * cell - height * cell / 2f);
        }

        /// <summary>World position of a cell centre (for effects and tutorial pointers).</summary>
        public Vector3 CellWorld(GridPos p)
        {
            return tilesLayer.TransformPoint(CellLocal(p));
        }

        public Vector2 CellInFx(GridPos p) => Fx.ToLocal(CellWorld(p));

        TileView CreateView(Tile t, GridPos cellPos)
        {
            var v = pool.Count > 0 ? pool.Pop() : TileView.Create(tilesLayer);
            v.transform.SetAsLastSibling();
            v.Setup(t, cellPos, cell, symbols);
            v.Rt.anchoredPosition = CellLocal(cellPos);
            views[t.Id] = v;
            return v;
        }

        void Recycle(TileView v)
        {
            if (v == null) return;
            Tw.Kill(v.Rt);
            Tw.Kill(v.Body.transform);
            Tw.Kill(v.Body);
            Tw.Kill(v.Overlay.rectTransform);
            Tw.Kill(v.Overlay);
            v.gameObject.SetActive(false);
            pool.Push(v);
        }

        public void SetSymbols(bool on)
        {
            symbols = on;
            foreach (var v in views.Values) v.SetSymbols(on, true);
        }

        // ------------------------------------------------------------------ Input

        bool Locate(PointerEventData e, out GridPos p)
        {
            p = default;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(tilesLayer, e.position, e.pressEventCamera, out local)) return false;
            int c = Mathf.FloorToInt((local.x + width * cell / 2f) / cell);
            int r = Mathf.FloorToInt((local.y + height * cell / 2f) / cell);
            if (c < 0 || r < 0 || c >= width || r >= height) return false;
            p = new GridPos(c, r);
            return true;
        }

        bool CanInteract => InputEnabled && session != null && !Busy;

        public void OnPointerDown(PointerEventData e)
        {
            if (activePointer != int.MinValue || !CanInteract) return;
            activePointer = e.pointerId;
            pressValid = Locate(e, out pressCell);
            if (pressValid) UpdatePreview(pressCell);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            if (!CanInteract)
            {
                ClearHighlight();
                return;
            }
            GridPos p;
            if (!Locate(e, out p))
            {
                ClearHighlight();
                return;
            }
            if (!previewActive || p != previewCell)
            {
                if (previewActive && InHighlight(p)) return;
                UpdatePreview(p);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            activePointer = int.MinValue;
            GridPos p;
            bool onBoard = Locate(e, out p);
            bool commit = previewActive && onBoard && InHighlight(p) && CanInteract;
            var target = previewCell;
            ClearHighlight();
            if (commit) Committed?.Invoke(target);
            else if (onBoard && pressValid && p == pressCell && CanInteract) InvalidPress?.Invoke(p);
        }

        bool InHighlight(GridPos p) => p.Col >= 0 && p.Row >= 0 && p.Col < width && p.Row < height && highlightMask[p.Row * width + p.Col];

        void UpdatePreview(GridPos p)
        {
            ClearHighlight();
            if (PreviewProvider == null) return;
            int n = PreviewProvider(p, highlight);
            if (n <= 0 || highlight.Count == 0)
            {
                highlight.Clear();
                return;
            }
            previewActive = true;
            previewCell = p;
            ShowHighlight(highlight, highlightGlow);
            PreviewShown?.Invoke(p, highlight, n);
            AudioService.Sfx(SfxNames.Select, 0.45f, 0.9f + Mathf.Min(n, 12) * 0.03f);
        }

        public void ShowHighlight(List<GridPos> cells, Color glow)
        {
            highlightShown = true;
            for (int i = 0; i < highlightMask.Length; i++) highlightMask[i] = false;
            foreach (var c in cells) if (c.Col >= 0 && c.Row >= 0 && c.Col < width && c.Row < height) highlightMask[c.Row * width + c.Col] = true;
            foreach (var v in views.Values)
            {
                bool lit = InHighlight(v.Cell);
                v.SetHighlight(lit, !lit, glow);
            }
        }

        /// <summary>Removes the press preview. Persistent marks (e.g. a Swap's first tile) stay visible.</summary>
        public void ClearHighlight()
        {
            bool had = previewActive;
            previewActive = false;
            if (marked.Count > 0)
            {
                ShowHighlight(marked, markGlow);
            }
            else if (highlightShown)
            {
                highlightShown = false;
                for (int i = 0; i < highlightMask.Length; i++) highlightMask[i] = false;
                foreach (var v in views.Values) v.SetHighlight(false, false, highlightGlow);
            }
            if (had) PreviewHidden?.Invoke();
        }

        /// <summary>Removes persistent marks and any highlight.</summary>
        public void ClearMarks()
        {
            marked.Clear();
            ClearHighlight();
        }

        /// <summary>Cancels any press in progress (e.g. when a popup opens).</summary>
        public void CancelPress()
        {
            activePointer = int.MinValue;
            ClearHighlight();
        }

        public void SetGlowColour(Color c) => highlightGlow = c;

        /// <summary>Marks cells as selected without an active press (e.g. first tile of a Swap). The mark
        /// stays until ClearMarks, surviving later presses that preview nothing.</summary>
        public void Mark(List<GridPos> cells, Color glow)
        {
            marked.Clear();
            marked.AddRange(cells);
            markGlow = glow;
            ShowHighlight(marked, glow);
        }

        public void Shake(GridPos p)
        {
            var v = ViewAt(p);
            if (v == null) return;
            var body = v.Body.rectTransform;
            Tw.Kill(body);
            body.anchoredPosition = Vector2.zero;
            float amp = cell * 0.08f;
            Tw.Move(body, new Vector2(amp, 0), 0.05f, Ease.OutQuad).OnComplete(() =>
                Tw.Move(body, new Vector2(-amp, 0), 0.08f, Ease.InOutQuad).OnComplete(() =>
                    Tw.Move(body, Vector2.zero, 0.08f, Ease.OutQuad)));
        }

        public TileView ViewAt(GridPos p)
        {
            if (session == null || !session.Board.InBounds(p)) return null;
            TileView v;
            return views.TryGetValue(session.Board[p].Id, out v) ? v : null;
        }

        // ------------------------------------------------------------------ Animation

        float FallDuration(int rows) => Mathf.Min(0.42f, 0.12f + 0.045f * Mathf.Max(1, rows)) / Mathf.Max(0.5f, Speed);

        /// <summary>Seconds between one wave of a chain reaction and the next.</summary>
        float WaveGap => 0.2f / Mathf.Max(0.5f, Speed);

        static Color ObstacleColour(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Ice: return new Color(0.82f, 0.95f, 1f, 1f);
                case ObstacleKind.Chain: return new Color(0.78f, 0.8f, 0.9f, 1f);
                case ObstacleKind.Moss: return new Color(0.45f, 0.86f, 0.5f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>Animates an outcome. Returns its duration; input is blocked until it finishes.</summary>
        public float Animate(MoveOutcome o)
        {
            ClearMarks();
            float end = 0f;
            float pace = 1f / Mathf.Max(0.5f, Speed);

            // Swaps.
            foreach (var mv in o.SwapMoves)
            {
                TileView v;
                if (!views.TryGetValue(mv.TileId, out v)) continue;
                v.Cell = mv.To;
                v.transform.SetAsLastSibling();
                Tw.Move(v.Rt, CellLocal(mv.To), 0.26f, Ease.InOutCubic);
                v.Rt.localScale = Vector3.one * 1.12f;
                Tw.Scale(v.Rt, 1f, 0.3f, Ease.OutBack, 0.05f);
                end = Mathf.Max(end, 0.32f);
            }

            // Recolours (Colour Shift): flip the tile and change its colour halfway.
            foreach (var rc in o.Recolours)
            {
                TileView v;
                if (!views.TryGetValue(rc.TileId, out v)) continue;
                var view = v;
                int to = rc.To;
                Tw.Scale(view.Rt, new Vector3(0f, 1.1f, 1f), 0.12f, Ease.InQuad).OnComplete(() =>
                {
                    if (view == null) return;
                    view.SetColour(to, symbols);
                    Tw.Scale(view.Rt, 1f, 0.22f, Ease.OutBack);
                });
                Fx.Sparkle(CellInFx(rc.Pos), Theme.Tile(to), 10, cell * 0.4f);
                end = Mathf.Max(end, 0.38f);
            }

            // Special tiles firing, wave by wave (docs/MECHANICS.md §2.3). Each wave's clears ripple out from it.
            var waveOrigin = new Dictionary<int, GridPos>();
            waveOrigin[0] = o.Origin;
            bool rocketSound = false, bombSound = false, prismSound = false;
            foreach (var sf in o.SpecialsFired)
            {
                float at = sf.Wave * WaveGap;
                waveOrigin[sf.Wave] = sf.Pos;
                RemoveSpecialView(sf.TileId, at);
                if (sf.ComboWith != SpecialKind.None) RemoveSpecialView(sf.ComboTileId, at);
                PlaySpecialEffect(sf, at);
                end = Mathf.Max(end, at + 0.45f);
                bool rocket = sf.Kind == SpecialKind.RocketRow || sf.Kind == SpecialKind.RocketColumn;
                if (rocket && !rocketSound) { rocketSound = true; SfxLater(SfxNames.Rocket, at, 0.9f); }
                if (sf.Kind == SpecialKind.Bomb && !bombSound) { bombSound = true; SfxLater(SfxNames.Bomb, at, 0.9f); }
                if ((sf.Kind == SpecialKind.Prism || sf.ComboWith == SpecialKind.Prism) && !prismSound) { prismSound = true; SfxLater(SfxNames.Prism, at, 0.9f); }
            }

            // Clears, staggered outwards from the tapped tile (or from the special of their wave).
            float maxStagger = 0f;
            int shardCount = Mathf.Clamp(48 / Mathf.Max(1, o.Cleared.Count), 2, 7);
            foreach (var cl in o.Cleared)
            {
                TileView v;
                if (!views.TryGetValue(cl.TileId, out v)) continue;
                views.Remove(cl.TileId);
                GridPos from;
                if (!waveOrigin.TryGetValue(cl.Wave, out from)) from = o.Origin;
                float dist = Mathf.Abs(cl.Pos.Col - from.Col) + Mathf.Abs(cl.Pos.Row - from.Row);
                float stagger = cl.Wave * WaveGap + Mathf.Min(o.Kind == OutcomeKind.Group ? 0.13f : 0.2f, dist * (o.Kind == OutcomeKind.Group ? 0.016f : 0.022f)) * pace;
                maxStagger = Mathf.Max(maxStagger, stagger);
                var view = v;
                var colour = cl.WasStone ? Theme.C(Palette.Stone) : Theme.Tile(cl.Colour);
                var fxPos = CellInFx(cl.Pos);
                bool mossy = view.Data.Obstacle == ObstacleKind.Moss;
                view.transform.SetAsLastSibling();
                Tw.Scale(view.Body.transform, 1.2f, 0.06f, Ease.OutQuad, stagger).OnComplete(() =>
                {
                    if (view == null) return;
                    Fx.Burst(fxPos, colour, cl.WasStone ? 9 : shardCount, cell * 5.2f, ArtNames.ParticleShard, cell * 0.2f);
                    if (mossy) Fx.Burst(fxPos, ObstacleColour(ObstacleKind.Moss), 6, cell * 3.5f, ArtNames.ParticleSoft, cell * 0.22f);
                    Tw.Scale(view.Body.transform, 0f, 0.12f, Ease.InQuad).OnComplete(() => Recycle(view));
                });
            }
            float clearEnd = o.Cleared.Count > 0 ? maxStagger + 0.2f : 0f;
            end = Mathf.Max(end, clearEnd);

            // Obstacles that lost strength: ice cracks, links snap, moss is scraped off.
            bool iceSound = false, chainSound = false, mossSound = false;
            foreach (var h in o.ObstacleHits)
            {
                TileView v;
                if (!views.TryGetValue(h.TileId, out v)) continue; // removed with its tile (moss cleared in a group)
                float at = h.Wave * WaveGap + 0.08f * pace;
                var view = v;
                var kind = h.Kind;
                int after = h.StrengthAfter;
                var fxPos = CellInFx(h.Pos);
                Tw.Delay(at, () =>
                {
                    if (view == null) return;
                    Fx.Burst(fxPos, ObstacleColour(kind), after <= 0 ? 10 : 5, cell * 4f, kind == ObstacleKind.Moss ? ArtNames.ParticleSoft : ArtNames.ParticleShard, cell * 0.18f);
                    if (after <= 0)
                    {
                        var overlay = view.Overlay;
                        Tw.Scale(overlay.rectTransform, 1.3f, 0.14f, Ease.OutQuad);
                        Tw.Fade(overlay, 0f, 0.16f, Ease.InQuad).OnComplete(() =>
                        {
                            if (view == null) return;
                            view.SetObstacle(ObstacleKind.None, 0, false);
                            overlay.color = Color.white;
                            overlay.rectTransform.localScale = Vector3.one;
                        });
                    }
                    else
                    {
                        view.SetObstacle(kind, after, true);
                    }
                }, this);
                if (kind == ObstacleKind.Ice && !iceSound) { iceSound = true; SfxLater(SfxNames.IceCrack, at, 0.8f); }
                if (kind == ObstacleKind.Chain && !chainSound) { chainSound = true; SfxLater(SfxNames.ChainBreak, at, 0.8f); }
                if (kind == ObstacleKind.Moss && !mossSound) { mossSound = true; SfxLater(SfxNames.MossClear, at, 0.8f); }
                end = Mathf.Max(end, at + 0.25f);
            }

            // A big group earned a special tile: it appears where the group was tapped, before tiles fall.
            if (o.HasCreated)
            {
                var t = new Tile { Type = CellType.Special, Special = o.Created.Kind, Id = o.Created.TileId, Colour = -1 };
                var v = CreateView(t, o.Created.Pos);
                v.Rt.localScale = Vector3.zero;
                float at = Mathf.Max(0.05f, maxStagger * 0.5f + 0.08f);
                Tw.Scale(v.Rt, 1f, 0.32f, Ease.OutBackStrong, at);
                var fxPos = CellInFx(o.Created.Pos);
                Tw.Delay(at, () => Fx.Sparkle(fxPos, Theme.Gold, 12, cell * 0.5f), this);
                SfxLater(SfxNames.SpecialMade, at, 0.85f);
                end = Mathf.Max(end, at + 0.35f);
            }

            // Falls and refills.
            float fallStart = o.Cleared.Count > 0 ? 0.07f + maxStagger * 0.6f : 0f;
            if (o.HasCreated) fallStart = Mathf.Max(fallStart, maxStagger * 0.5f + 0.3f);
            var fallEnds = new Dictionary<int, float>();
            foreach (var f in o.Falls)
            {
                TileView v;
                if (!views.TryGetValue(f.TileId, out v)) continue;
                v.Cell = f.To;
                float d = FallDuration(f.From.Row - f.To.Row);
                float delay = fallStart + f.To.Col * 0.012f;
                Drop(v, CellLocal(f.To), d, delay);
                fallEnds[f.TileId] = delay + d;
                end = Mathf.Max(end, delay + d + 0.12f);
            }

            // Treasure that reached the floor is collected once it lands.
            foreach (var tr in o.Treasures)
            {
                TileView v;
                if (!views.TryGetValue(tr.TileId, out v)) continue;
                views.Remove(tr.TileId);
                float landed;
                if (!fallEnds.TryGetValue(tr.TileId, out landed)) landed = fallStart;
                var view = v;
                var fxPos = CellInFx(tr.At);
                view.transform.SetAsLastSibling();
                Tw.Scale(view.Rt, 1.35f, 0.18f, Ease.OutBack, landed + 0.02f).OnComplete(() =>
                {
                    if (view == null) return;
                    Fx.Sparkle(fxPos, Theme.Gold, 16, cell * 0.6f);
                    Fx.Ring(fxPos, Theme.Gold, cell * 3f);
                    Tw.Scale(view.Rt, 0f, 0.2f, Ease.InQuad).OnComplete(() => Recycle(view));
                });
                SfxLater(SfxNames.Treasure, landed + 0.05f, 0.9f);
                end = Mathf.Max(end, landed + 0.45f);
            }

            foreach (var sp in o.Spawns)
            {
                var t = new Tile { Id = sp.TileId, Colour = sp.Colour, Type = CellType.Colour };
                var v = CreateView(t, sp.At);
                float delay = fallStart + sp.At.Col * 0.012f + 0.03f;
                if (sp.FromBoardTop)
                {
                    int startRow = height + sp.Order;
                    v.Rt.anchoredPosition = CellLocal(new GridPos(sp.At.Col, startRow)) + new Vector2(0, cell * 0.15f);
                    float d = FallDuration(startRow - sp.At.Row);
                    Drop(v, CellLocal(sp.At), d, delay);
                    end = Mathf.Max(end, delay + d + 0.12f);
                }
                else
                {
                    v.Rt.anchoredPosition = CellLocal(sp.SectionTop);
                    v.Rt.localScale = Vector3.one * 0.2f;
                    float d = FallDuration(sp.SectionTop.Row - sp.At.Row + 1);
                    float extra = sp.Order * 0.05f;
                    Tw.Scale(v.Rt, 1f, 0.18f, Ease.OutBack, delay + extra);
                    Drop(v, CellLocal(sp.At), d, delay + extra);
                    end = Mathf.Max(end, delay + extra + d + 0.12f);
                }
            }

            // Moss grows onto a neighbour after everything has landed.
            foreach (var g in o.MossGrowth)
            {
                TileView v;
                if (!views.TryGetValue(g.TileId, out v)) continue;
                var view = v;
                var fxPos = CellInFx(g.Pos);
                float at = end + 0.05f;
                Tw.Delay(at, () =>
                {
                    if (view == null) return;
                    view.SetObstacle(ObstacleKind.Moss, 1, true);
                    Fx.Burst(fxPos, ObstacleColour(ObstacleKind.Moss), 6, cell * 2f, ArtNames.ParticleSoft, cell * 0.18f);
                }, this);
                SfxLater(SfxNames.MossGrow, at, 0.8f);
                end = at + 0.3f;
            }

            // Dead-board recovery: shuffle after everything has landed.
            if (o.Shuffled && o.ShuffleMoves.Count > 0)
            {
                float start = end + 0.5f;
                Tw.Delay(end + 0.05f, () => ShuffleStarted?.Invoke(), this);
                foreach (var mv in o.ShuffleMoves)
                {
                    TileView v;
                    if (!views.TryGetValue(mv.TileId, out v)) continue;
                    v.Cell = mv.To;
                    float jitter = UnityEngine.Random.Range(0f, 0.12f);
                    Tw.Move(v.Rt, CellLocal(mv.To), 0.5f, Ease.InOutCubic, start + jitter);
                    v.Rt.localScale = Vector3.one;
                    Tw.Scale(v.Rt, 0.75f, 0.2f, Ease.OutQuad, start + jitter).OnComplete(() =>
                    {
                        if (v != null) Tw.Scale(v.Rt, 1f, 0.3f, Ease.OutBack);
                    });
                }
                end = start + 0.75f;
            }

            // Input reopens as tiles land; the final landing squash is cosmetic and need not block play.
            busyUntil = Time.unscaledTime + Mathf.Max(0.05f, end - 0.1f);
            Tw.Delay(end + 0.02f, Verify, this);
            return end;
        }

        void SfxLater(string name, float delay, float volume)
        {
            if (delay <= 0.01f) AudioService.Sfx(name, volume);
            else Tw.Delay(delay, () => AudioService.Sfx(name, volume), this);
        }

        /// <summary>A fired special tile flashes and disappears.</summary>
        void RemoveSpecialView(int tileId, float at)
        {
            TileView v;
            if (tileId == 0 || !views.TryGetValue(tileId, out v)) return;
            views.Remove(tileId);
            var view = v;
            view.transform.SetAsLastSibling();
            Tw.Scale(view.Rt, 1.3f, 0.1f, Ease.OutQuad, at).OnComplete(() =>
            {
                if (view == null) return;
                Tw.Scale(view.Rt, 0f, 0.14f, Ease.InQuad).OnComplete(() => Recycle(view));
            });
        }

        /// <summary>The visual of a special firing: beams for rockets, a shockwave for bombs, a rainbow pulse for prisms.</summary>
        void PlaySpecialEffect(SpecialFired sf, float at)
        {
            var origin = CellInFx(sf.Pos);
            bool combo = sf.ComboWith != SpecialKind.None;
            var gold = Theme.Gold;
            void Row(int r, float thick) => Fx.Beam(CellInFx(new GridPos(0, r)) - new Vector2(cell * 0.6f, 0), CellInFx(new GridPos(width - 1, r)) + new Vector2(cell * 0.6f, 0), Color.white, thick, at);
            void Column(int c, float thick) => Fx.Beam(CellInFx(new GridPos(c, 0)) - new Vector2(0, cell * 0.6f), CellInFx(new GridPos(c, height - 1)) + new Vector2(0, cell * 0.6f), Color.white, thick, at);

            var kinds = new[] { sf.Kind, sf.ComboWith };
            bool rocketRow = Array.IndexOf(kinds, SpecialKind.RocketRow) >= 0;
            bool rocketCol = Array.IndexOf(kinds, SpecialKind.RocketColumn) >= 0;
            bool bomb = Array.IndexOf(kinds, SpecialKind.Bomb) >= 0;
            bool prism = Array.IndexOf(kinds, SpecialKind.Prism) >= 0;
            Tw.Delay(at, () =>
            {
                if (this == null) return;
                if (prism)
                {
                    var colour = sf.Colour >= 0 ? Theme.Tile(sf.Colour) : Color.white;
                    Fx.Ring(origin, colour, cell * (combo ? 10f : 7f));
                    Fx.Sparkle(origin, colour, 24, cell * 1.2f);
                }
                if (bomb)
                {
                    bool doubleBomb = sf.Kind == SpecialKind.Bomb && sf.ComboWith == SpecialKind.Bomb;
                    Fx.Ring(origin, new Color(1f, 0.55f, 0.35f, 1f), cell * (doubleBomb ? 6.5f : 4f));
                    Fx.Burst(origin, gold, 14, cell * 6f, ArtNames.ParticleSpark, cell * 0.25f);
                }
            }, this);
            // Rockets fire along their line; two rockets cross; a rocket with a bomb clears three lines; a prism fires
            // its partner from where the partner sat.
            bool bothRockets = combo && (sf.Kind == SpecialKind.RocketRow || sf.Kind == SpecialKind.RocketColumn) &&
                               (sf.ComboWith == SpecialKind.RocketRow || sf.ComboWith == SpecialKind.RocketColumn);
            if (prism && combo)
            {
                var p = sf.Kind == SpecialKind.Prism ? sf.ComboPos : sf.Pos;
                if (rocketRow) Row(p.Row, cell * 0.9f);
                if (rocketCol) Column(p.Col, cell * 0.9f);
            }
            else if (bothRockets)
            {
                Row(sf.Pos.Row, cell * 0.9f);
                Column(sf.Pos.Col, cell * 0.9f);
            }
            else if (combo && bomb && (rocketRow || rocketCol))
            {
                for (int k = -1; k <= 1; k++)
                {
                    if (rocketRow && sf.Pos.Row + k >= 0 && sf.Pos.Row + k < height) Row(sf.Pos.Row + k, cell * 0.8f);
                    if (rocketCol && sf.Pos.Col + k >= 0 && sf.Pos.Col + k < width) Column(sf.Pos.Col + k, cell * 0.8f);
                }
            }
            else if (sf.Kind == SpecialKind.RocketRow) Row(sf.Pos.Row, cell * 0.9f);
            else if (sf.Kind == SpecialKind.RocketColumn) Column(sf.Pos.Col, cell * 0.9f);
        }

        void Drop(TileView v, Vector2 to, float duration, float delay)
        {
            var view = v;
            Tw.Move(view.Rt, to, duration, Ease.InQuad, delay).OnComplete(() =>
            {
                if (view == null || Tw.Reduced) return;
                view.Body.transform.localScale = new Vector3(1.07f, 0.9f, 1f);
                Tw.Scale(view.Body.transform, 1f, 0.16f, Ease.OutBack);
            });
        }

        /// <summary>Makes sure the view exactly matches the board (self-healing safety net).</summary>
        public void Verify()
        {
            if (session == null) return;
            var b = session.Board;
            var seen = new HashSet<int>();
            bool repaired = false;
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                {
                    var t = b[c, r];
                    if (t.Type == CellType.Empty || t.Type == CellType.Void) continue;
                    seen.Add(t.Id);
                    var p = new GridPos(c, r);
                    TileView v;
                    if (!views.TryGetValue(t.Id, out v))
                    {
                        CreateView(t, p);
                        repaired = true;
                        continue;
                    }
                    if (v.Cell != p || !v.Data.Equals(t))
                    {
                        Tw.Kill(v.Rt);
                        v.Setup(t, p, cell, symbols);
                        v.Rt.anchoredPosition = CellLocal(p);
                        repaired = true;
                    }
                }
            var stale = new List<int>();
            foreach (var kv in views) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale)
            {
                Recycle(views[id]);
                views.Remove(id);
                repaired = true;
            }
            if (repaired) Debug.LogWarning("[ColourCascade] Board view re-synchronised with the game state.");
        }

        public void SetBoardVisible(bool visible)
        {
            tilesLayer.gameObject.SetActive(visible);
            slotsLayer.gameObject.SetActive(visible);
        }
    }
}
