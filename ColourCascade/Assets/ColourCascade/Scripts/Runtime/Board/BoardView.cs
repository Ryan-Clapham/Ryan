using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>One tile on screen. The root is cell-sized; the body is scaled for highlight and animation.</summary>
    public sealed class TileView : MonoBehaviour
    {
        public RectTransform Rt;
        public Image Body;
        public Image Symbol;
        public Image Glow;
        public int Id;
        [System.NonSerialized] public GridPos Cell; // runtime state only, never saved with the object
        public int Colour = -1;
        public bool IsStone;

        public static TileView Create(Transform parent)
        {
            var rt = UI.Rect("Tile", parent);
            var v = rt.gameObject.AddComponent<TileView>();
            v.Rt = rt;
            v.Glow = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.TileGlow), new Color(1, 1, 1, 0), "Glow");
            v.Body = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.Tile(0)), Color.white, "Body");
            v.Symbol = UI.Image(v.Body.rectTransform, ArtLibrary.Instance.Get(ArtNames.Symbol(0)), Color.white, "Symbol");
            return v;
        }

        public void Setup(Tile t, GridPos cell, float size, bool symbols)
        {
            Id = t.Id;
            Cell = cell;
            IsStone = t.Type == CellType.Stone;
            Rt.sizeDelta = new Vector2(size, size);
            Rt.localScale = Vector3.one;
            Rt.localEulerAngles = Vector3.zero;
            Body.rectTransform.Centre(size * 0.94f, size * 0.94f);
            Body.rectTransform.localScale = Vector3.one;
            Glow.rectTransform.Centre(size * 1.4f, size * 1.4f);
            Glow.color = new Color(1, 1, 1, 0);
            Symbol.rectTransform.Centre(size * 0.54f, size * 0.54f, 0, size * 0.015f);
            Body.color = Color.white;
            if (IsStone)
            {
                Colour = -1;
                Body.sprite = ArtLibrary.Instance.Get(ArtNames.Stone);
                Symbol.gameObject.SetActive(false);
            }
            else
            {
                SetColour(t.Colour, symbols);
            }
            gameObject.SetActive(true);
        }

        public void SetColour(int colour, bool symbols)
        {
            Colour = colour;
            Body.sprite = ArtLibrary.Instance.Tile(colour);
            Symbol.sprite = ArtLibrary.Instance.Symbol(colour);
            Symbol.gameObject.SetActive(symbols);
        }

        public void SetSymbols(bool on, bool animate)
        {
            if (IsStone) return;
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
                    if (t.Type == CellType.Colour || t.Type == CellType.Stone) CreateView(t, new GridPos(c, r));
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
                v.Setup(new Tile { Id = v.Id, Colour = v.Colour, Type = v.IsStone ? CellType.Stone : CellType.Colour }, v.Cell, cell, symbols);
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

        /// <summary>Animates an outcome. Returns its duration; input is blocked until it finishes.</summary>
        public float Animate(MoveOutcome o)
        {
            ClearMarks();
            float end = 0f;

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

            // Clears, staggered outwards from the tapped tile.
            float pace = 1f / Mathf.Max(0.5f, Speed);
            float maxStagger = 0f;
            int shardCount = Mathf.Clamp(48 / Mathf.Max(1, o.Cleared.Count), 2, 7);
            foreach (var cl in o.Cleared)
            {
                TileView v;
                if (!views.TryGetValue(cl.TileId, out v)) continue;
                views.Remove(cl.TileId);
                float dist = Mathf.Abs(cl.Pos.Col - o.Origin.Col) + Mathf.Abs(cl.Pos.Row - o.Origin.Row);
                float stagger = Mathf.Min(o.Kind == OutcomeKind.Group ? 0.13f : 0.2f, dist * (o.Kind == OutcomeKind.Group ? 0.016f : 0.025f)) * pace;
                maxStagger = Mathf.Max(maxStagger, stagger);
                var view = v;
                var colour = cl.WasStone ? Theme.C(Palette.Stone) : Theme.Tile(cl.Colour);
                var fxPos = CellInFx(cl.Pos);
                view.transform.SetAsLastSibling();
                Tw.Scale(view.Body.transform, 1.2f, 0.06f, Ease.OutQuad, stagger).OnComplete(() =>
                {
                    if (view == null) return;
                    Fx.Burst(fxPos, colour, cl.WasStone ? 9 : shardCount, cell * 5.2f, cl.WasStone ? ArtNames.ParticleShard : ArtNames.ParticleShard, cell * 0.2f);
                    Tw.Scale(view.Body.transform, 0f, 0.12f, Ease.InQuad).OnComplete(() => Recycle(view));
                });
            }
            float clearEnd = o.Cleared.Count > 0 ? maxStagger + 0.2f : 0f;
            end = Mathf.Max(end, clearEnd);

            // Falls and refills.
            float fallStart = o.Cleared.Count > 0 ? 0.07f + maxStagger * 0.6f : 0f;
            foreach (var f in o.Falls)
            {
                TileView v;
                if (!views.TryGetValue(f.TileId, out v)) continue;
                v.Cell = f.To;
                float d = FallDuration(f.From.Row - f.To.Row);
                float delay = fallStart + f.To.Col * 0.012f;
                Drop(v, CellLocal(f.To), d, delay);
                end = Mathf.Max(end, delay + d + 0.12f);
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
                    if (t.Type != CellType.Colour && t.Type != CellType.Stone) continue;
                    seen.Add(t.Id);
                    var p = new GridPos(c, r);
                    TileView v;
                    if (!views.TryGetValue(t.Id, out v))
                    {
                        CreateView(t, p);
                        repaired = true;
                        continue;
                    }
                    if (v.Cell != p || (t.Type == CellType.Colour && v.Colour != t.Colour))
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
