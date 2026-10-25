using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>Keeps a RectTransform inside the device safe area (notches, rounded corners, gesture bar).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        RectTransform rt;
        Rect applied;
        Vector2Int screen;

        void Awake()
        {
            rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != screen.x || Screen.height != screen.y) Apply();
        }

        void Apply()
        {
            var safe = Screen.safeArea;
            applied = safe;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            var max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Phones scale the canvas by width; wider screens (tablets) scale by height so nothing overflows.</summary>
    public sealed class ScreenAdapter : MonoBehaviour
    {
        CanvasScaler scaler;
        Vector2Int last;

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != last.x || Screen.height != last.y) Apply();
        }

        void Apply()
        {
            last = new Vector2Int(Screen.width, Screen.height);
            if (scaler == null || Screen.height <= 0) return;
            float aspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = aspect > 0.62f ? 1f : 0f;
        }
    }

    /// <summary>Two-state switch.</summary>
    public sealed class ToggleSwitch : MonoBehaviour, IPointerClickHandler
    {
        Image track;
        RectTransform knob;
        bool value;
        Action<bool> changed;
        float lastClick = -1;

        public bool Value => value;

        public static ToggleSwitch Create(Transform parent, bool initial, Action<bool> onChanged)
        {
            var rt = UI.Rect("Toggle", parent);
            rt.sizeDelta = new Vector2(150, 84);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var t = rt.gameObject.AddComponent<ToggleSwitch>();
            t.track = UI.Rounded(rt, Theme.SurfaceLow, 42, "Track");
            t.track.rectTransform.Fill(0, 6, 0, 6);
            var k = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Knob");
            t.knob = k.rectTransform;
            t.knob.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62, 62));
            var gloss = UI.Image(t.knob, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.5f), "Gloss");
            gloss.rectTransform.Fill();
            t.changed = onChanged;
            t.Set(initial, false);
            return t;
        }

        public void Set(bool on, bool animate)
        {
            value = on;
            var target = new Vector2(on ? 33 : -33, 0);
            var colour = on ? Theme.Success : Theme.SurfaceLow;
            if (animate)
            {
                Tw.Move(knob, target, 0.18f, Ease.OutBack);
                Tw.Colour(track, colour, 0.15f);
            }
            else
            {
                knob.anchoredPosition = target;
                track.color = colour;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime - lastClick < 0.25f) return;
            lastClick = Time.unscaledTime;
            Set(!value, true);
            AudioService.Sfx(SfxNames.Toggle);
            Haptics.Light();
            changed?.Invoke(value);
        }
    }

    /// <summary>Horizontal 0..1 slider.</summary>
    public sealed class SliderControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform area;
        RectTransform fill;
        RectTransform knob;
        float value;
        Action<float> changed;
        Action released;

        public static SliderControl Create(Transform parent, float initial, Action<float> onChanged, Action onReleased = null)
        {
            var rt = UI.Rect("Slider", parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var s = rt.gameObject.AddComponent<SliderControl>();
            s.area = UI.Rect("Area", rt);
            s.area.Fill(30, 0, 30, 0);
            var track = UI.Rounded(s.area, Theme.SurfaceLow, 16, "Track");
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 28);
            var f = UI.Rounded(s.area, Theme.Teal, 14, "Fill");
            s.fill = f.rectTransform;
            s.fill.anchorMin = new Vector2(0, 0.5f);
            s.fill.anchorMax = new Vector2(0, 0.5f);
            s.fill.pivot = new Vector2(0, 0.5f);
            var k = UI.Image(s.area, ArtLibrary.Instance.Get(ArtNames.Circle), Color.white, "Knob");
            s.knob = k.rectTransform;
            s.knob.sizeDelta = new Vector2(64, 64);
            s.knob.anchorMin = s.knob.anchorMax = new Vector2(0, 0.5f);
            UI.Image(s.knob, ArtLibrary.Instance.Get(ArtNames.CircleGloss), new Color(1, 1, 1, 0.5f), "Gloss").rectTransform.Fill();
            s.changed = onChanged;
            s.released = onReleased;
            s.Set(initial);
            return s;
        }

        void OnRectTransformDimensionsChange()
        {
            if (area != null) Set(value);
        }

        public void Set(float v)
        {
            value = Mathf.Clamp01(v);
            float w = area.rect.width;
            fill.sizeDelta = new Vector2(Mathf.Max(28, w * value), 28);
            knob.anchoredPosition = new Vector2(w * value, 0);
        }

        void Drag(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out local)) return;
            float w = area.rect.width;
            if (w <= 0) return;
            Set((local.x - area.rect.xMin) / w);
            changed?.Invoke(value);
        }

        public void OnPointerDown(PointerEventData e) => Drag(e);
        public void OnDrag(PointerEventData e) => Drag(e);

        public void OnPointerUp(PointerEventData e)
        {
            released?.Invoke();
        }
    }

    /// <summary>Score progress bar with optional star markers at fractions of the bar.</summary>
    public sealed class ProgressBar : MonoBehaviour
    {
        RectTransform fillArea;
        RectTransform fill;
        Image fillImage;
        readonly List<Image> markers = new List<Image>();
        float shown;
        float target;

        public static ProgressBar Create(Transform parent, Color fillColour, float height = 34)
        {
            var rt = UI.Rect("Progress", parent);
            var bar = rt.gameObject.AddComponent<ProgressBar>();
            var track = UI.Rounded(rt, new Color(0, 0, 0, 0.35f), height / 2, "Track");
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, height);
            bar.fillArea = UI.Rect("FillArea", rt);
            bar.fillArea.anchorMin = new Vector2(0, 0.5f);
            bar.fillArea.anchorMax = new Vector2(1, 0.5f);
            bar.fillArea.sizeDelta = new Vector2(-8, height - 8);
            bar.fillImage = UI.Rounded(bar.fillArea, fillColour, (height - 8) / 2, "Fill");
            bar.fill = bar.fillImage.rectTransform;
            bar.fill.anchorMin = new Vector2(0, 0);
            bar.fill.anchorMax = new Vector2(0, 1);
            bar.fill.pivot = new Vector2(0, 0.5f);
            bar.fill.sizeDelta = new Vector2(0, 0);
            var gloss = UI.Rounded(bar.fill, new Color(1, 1, 1, 0.8f), (height - 8) / 2, "Gloss", ArtNames.Gloss);
            gloss.rectTransform.Fill();
            return bar;
        }

        /// <summary>Star markers at the given fractions (0..1) of the bar.</summary>
        public void SetMarkers(params float[] fractions)
        {
            foreach (var m in markers) UI.Destroy(m);
            markers.Clear();
            foreach (var f in fractions)
            {
                if (f <= 0 || f > 1.001f) continue;
                var m = UI.Image(transform, ArtLibrary.Instance.Get(ArtNames.StarEmpty), Color.white, "Marker");
                m.rectTransform.Place(new Vector2(Mathf.Clamp01(f), 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58));
                markers.Add(m);
            }
        }

        public void SetMarkerLit(int index, bool lit)
        {
            if (index < 0 || index >= markers.Count) return;
            var m = markers[index];
            var sprite = ArtLibrary.Instance.Get(lit ? ArtNames.StarGold : ArtNames.StarEmpty);
            if (m.sprite == sprite) return;
            m.sprite = sprite;
            if (lit)
            {
                m.transform.localScale = Vector3.one * 1.6f;
                Tw.Scale(m.transform, 1f, 0.35f, Ease.OutBackStrong);
            }
        }

        public void SetColour(Color c) => fillImage.color = c;

        public void SetFraction(float f, bool animate = true)
        {
            target = Mathf.Clamp01(f);
            if (!animate)
            {
                shown = target;
                Apply();
                return;
            }
            Tw.Value(shown, target, 0.35f, v => { shown = v; Apply(); }, Ease.OutCubic, 0f, this);
        }

        void OnRectTransformDimensionsChange()
        {
            if (fill != null) Apply();
        }

        void Apply()
        {
            float w = fillArea.rect.width;
            float h = fillArea.rect.height;
            fill.sizeDelta = new Vector2(shown <= 0.0001f ? 0 : Mathf.Max(h, w * shown), 0);
            fill.gameObject.SetActive(shown > 0.0001f);
        }
    }

    /// <summary>Row of three stars (earned ones gold).</summary>
    public sealed class StarRow : MonoBehaviour
    {
        readonly Image[] stars = new Image[3];

        public static StarRow Create(Transform parent, float size, float spacing, bool arc = false)
        {
            var rt = UI.Rect("Stars", parent);
            rt.sizeDelta = new Vector2(size * 3 + spacing * 2, size * (arc ? 1.3f : 1f));
            var row = rt.gameObject.AddComponent<StarRow>();
            for (int i = 0; i < 3; i++)
            {
                var img = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.StarEmpty), Color.white, "Star" + i);
                float x = (i - 1) * (size + spacing);
                float y = arc && i == 1 ? size * 0.22f : 0;
                float s = arc && i == 1 ? size * 1.18f : size;
                img.rectTransform.Centre(s, s, x, y);
                if (arc) img.rectTransform.localEulerAngles = new Vector3(0, 0, (1 - i) * 12f);
                row.stars[i] = img;
            }
            return row;
        }

        public void Set(int count)
        {
            for (int i = 0; i < 3; i++)
            {
                stars[i].sprite = ArtLibrary.Instance.Get(i < count ? ArtNames.StarGold : ArtNames.StarEmpty);
                stars[i].transform.localScale = Vector3.one;
            }
        }

        /// <summary>Reveals stars one at a time with a pop and rising chime. Returns total duration.</summary>
        public float Reveal(int count, float startDelay, FxLayer fx = null)
        {
            Set(0);
            for (int i = 0; i < count && i < 3; i++)
            {
                int idx = i;
                float d = startDelay + i * 0.32f;
                Tw.Delay(d, () =>
                {
                    var img = stars[idx];
                    img.sprite = ArtLibrary.Instance.Get(ArtNames.StarGold);
                    img.transform.localScale = Vector3.one * 2.2f;
                    Tw.Scale(img.transform, 1f, 0.35f, Ease.OutBackStrong);
                    AudioService.Sfx(SfxNames.Star(idx + 1));
                    Haptics.Light();
                    if (fx != null) fx.BurstAt(img.rectTransform, Theme.Gold, 14, 520f);
                }, this);
            }
            return startDelay + count * 0.32f + 0.35f;
        }
    }

    /// <summary>Coin balance pill. Follows every wallet change (including after a progress reset) and counts up/down.</summary>
    public sealed class CoinCounter : MonoBehaviour
    {
        Text label;
        RectTransform coin;
        int shown;
        bool subscribed;

        public RectTransform CoinIcon => coin;

        public static CoinCounter Create(Transform parent, Action onTap = null, float height = 92)
        {
            var rt = UI.Rect("Coins", parent);
            rt.sizeDelta = new Vector2(280, height);
            var bg = UI.Rounded(rt, new Color(0.03f, 0.02f, 0.12f, 0.55f), height / 2, "Bg");
            bg.rectTransform.Fill();
            bg.raycastTarget = onTap != null;
            var c = rt.gameObject.AddComponent<CoinCounter>();
            var icon = UI.Sprite(rt, ArtNames.Coin, height * 0.86f);
            icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(height * 0.42f, 0), new Vector2(height * 0.86f, height * 0.86f));
            c.coin = icon.rectTransform;
            c.label = UI.Text(rt, "0", (int)(height * 0.48f), Theme.Text, TextAnchor.MiddleCenter, true, "Amount");
            c.label.rectTransform.Fill(height * 0.86f, 0, onTap != null ? height * 0.78f : 22, 0);
            if (onTap != null)
            {
                var plus = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.Circle), Theme.Success, "Plus");
                plus.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(height * 0.62f, height * 0.62f));
                UI.Icon(plus.transform, Procedural.Icon.Plus, Color.white, height * 0.36f).rectTransform.Centre(height * 0.36f, height * 0.36f);
                var btn = rt.gameObject.AddComponent<CCButton>();
                btn.OnClick = onTap;
                btn.Configure(ButtonStyle.Ghost, 0);
            }
            c.Subscribe();
            return c;
        }

        void OnEnable()
        {
            Subscribe();
        }

        // OnEnable also runs inside AddComponent, before Create has built the label, so binding waits for it.
        void Subscribe()
        {
            if (label == null || subscribed || GameApp.Instance == null) return;
            GameApp.Instance.CoinsChanged += OnCoinsChanged;
            subscribed = true;
            Show(GameApp.Instance.Ctx.Wallet.Balance);
        }

        void OnDisable()
        {
            if (subscribed && GameApp.Instance != null) GameApp.Instance.CoinsChanged -= OnCoinsChanged;
            subscribed = false;
        }

        /// <summary>Displays a value immediately (used before a reward count-up).</summary>
        public void Show(int value)
        {
            Tw.Kill(this);
            shown = value;
            label.text = NumberFormat.Thousands(value);
        }

        public void AnimateTo(int value)
        {
            OnCoinsChanged(shown, value);
        }

        void OnCoinsChanged(int before, int after)
        {
            int from = shown;
            Tw.Value(from, after, 0.6f, v =>
            {
                shown = Mathf.RoundToInt(v);
                label.text = NumberFormat.Thousands(shown);
            }, Ease.OutCubic, 0f, this);
            coin.localScale = Vector3.one * 1.25f;
            Tw.Scale(coin, 1f, 0.3f, Ease.OutBack);
        }
    }

    /// <summary>Segmented tab control. Tabs are data, so new shop categories are one line each.</summary>
    public sealed class TabBar : MonoBehaviour
    {
        readonly List<CCButton> buttons = new List<CCButton>();
        Action<int> selected;
        public int Current { get; private set; } = -1;

        public static TabBar Create(Transform parent, IList<string> titles, IList<Icon> icons, Action<int> onSelect)
        {
            var rt = UI.Rect("Tabs", parent);
            var bg = UI.Rounded(rt, new Color(0.03f, 0.02f, 0.12f, 0.45f), 48, "Bg");
            bg.rectTransform.Fill();
            var row = UI.Rect("Row", rt);
            row.Fill(10, 10, 10, 10);
            UI.HStack(row, 10);
            var bar = rt.gameObject.AddComponent<TabBar>();
            bar.selected = onSelect;
            for (int i = 0; i < titles.Count; i++)
            {
                int idx = i;
                var b = UI.Button(row, titles[i], ButtonStyle.Ghost, () => bar.Select(idx), icons != null && i < icons.Count ? icons[i] : (Icon?)null, Theme.FontBody, 38, 6);
                b.ClickSound = SfxNames.Toggle;
                bar.buttons.Add(b);
            }
            return bar;
        }

        public void Select(int index, bool force = false)
        {
            if (index == Current && !force) return;
            Current = index;
            for (int i = 0; i < buttons.Count; i++) buttons[i].SetStyle(i == index ? ButtonStyle.Secondary : ButtonStyle.Ghost);
            selected?.Invoke(index);
        }
    }

    /// <summary>Short non-blocking messages at the top of the screen.</summary>
    public static class Toast
    {
        static RectTransform current;

        public static void Show(string message, Icon? icon = null, Color? accent = null)
        {
            var app = GameApp.Instance;
            if (app == null) return;
            if (current != null) UnityEngine.Object.Destroy(current.gameObject);
            var rt = UI.Rect("Toast", app.OverlayLayer);
            rt.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(940, 128));
            current = rt;
            var card = UI.Card(rt, "Card", Theme.SurfaceHigh, 40);
            card.parent.GetComponent<RectTransform>().Fill();
            var text = UI.FitText(card, message, Theme.FontBody, Theme.Text, TextAnchor.MiddleCenter, true, "Message");
            text.rectTransform.Fill(icon.HasValue ? 120 : 36, 8, 36, 8);
            if (icon.HasValue)
            {
                var i = UI.Icon(card, icon.Value, accent ?? Theme.Gold, 68);
                i.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72, 0), new Vector2(68, 68));
            }
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0;
            rt.anchoredPosition = new Vector2(0, -110);
            Tw.Fade(group, 1f, 0.18f);
            Tw.Move(rt, new Vector2(0, -170), 0.3f, Ease.OutBack);
            Tw.Fade(group, 0f, 0.3f, Ease.InQuad, 2.2f).OnComplete(() =>
            {
                if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
            });
        }
    }

    /// <summary>
    /// Tutorial hint: a speech bubble with an optional animated hand pointing at a spot. Never blocks input,
    /// so a hint can never leave the game unplayable; it disappears when dismissed or replaced.
    /// </summary>
    public sealed class CoachMark : MonoBehaviour
    {
        static CoachMark current;
        RectTransform hand;
        CanvasGroup group;

        public static void Hide()
        {
            if (current == null) return;
            var c = current;
            current = null;
            Tw.Kill(c.group);
            Tw.Fade(c.group, 0f, 0.15f).OnComplete(() =>
            {
                if (c != null) Destroy(c.gameObject);
            });
        }

        public static bool Visible => current != null;

        /// <summary>Shows a hint. pointAt is a world position (null for a bubble only). bubbleY is the bubble centre in overlay space.</summary>
        public static CoachMark Show(string text, Vector3? pointAt, float bubbleY, float autoHideSeconds = 0f)
        {
            if (current != null) Destroy(current.gameObject);
            var app = GameApp.Instance;
            var rt = UI.Rect("CoachMark", app.OverlayLayer);
            rt.Fill();
            var cm = rt.gameObject.AddComponent<CoachMark>();
            cm.group = rt.gameObject.AddComponent<CanvasGroup>();
            cm.group.blocksRaycasts = false;
            cm.group.interactable = false;
            current = cm;

            var bubbleRt = UI.Rect("Bubble", rt);
            bubbleRt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, bubbleY), new Vector2(900, 170));
            var content = UI.Card(bubbleRt, "Card", Theme.Hex("#FFF6E9"), 44);
            content.parent.GetComponent<RectTransform>().Fill();
            var label = UI.FitText(content, text, Theme.FontBody, Theme.TextDark, TextAnchor.MiddleCenter, true, "Text");
            label.rectTransform.Fill(36, 12, 36, 12);

            if (pointAt.HasValue)
            {
                var handImg = UI.Sprite(rt, ArtNames.Hand, 170);
                cm.hand = handImg.rectTransform;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, RectTransformUtility.WorldToScreenPoint(null, pointAt.Value), null, out local);
                cm.hand.anchoredPosition = local + new Vector2(30, -60);
                cm.StartCoroutine(cm.Bob(local + new Vector2(30, -60)));
            }
            cm.group.alpha = 0;
            Tw.Fade(cm.group, 1f, 0.25f);
            bubbleRt.localScale = Vector3.one * 0.85f;
            Tw.Scale(bubbleRt, 1f, 0.3f, Ease.OutBack);
            if (autoHideSeconds > 0) Tw.Delay(autoHideSeconds, () => { if (current == cm) Hide(); }, cm);
            return cm;
        }

        System.Collections.IEnumerator Bob(Vector2 basePos)
        {
            float t = 0;
            while (hand != null)
            {
                t += Time.unscaledDeltaTime;
                float press = Mathf.Abs(Mathf.Sin(t * 3.2f));
                hand.anchoredPosition = basePos + new Vector2(0, press * 28f);
                hand.localScale = Vector3.one * (1f - (1f - press) * 0.08f);
                yield return null;
            }
        }
    }
}
