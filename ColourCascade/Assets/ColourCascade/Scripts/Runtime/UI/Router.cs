using System;
using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    public enum ScreenId
    {
        Home,
        Map,
        Game,
        Shop,
        Settings,
        HowToPlay,
    }

    /// <summary>Base class for full screens. Built once from code, then shown/hidden; OnShow re-binds data.</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public CanvasGroup Group { get; private set; }
        /// <summary>Content area inside the device safe area.</summary>
        protected RectTransform Safe { get; private set; }
        protected GameApp App => GameApp.Instance;
        protected GameContext Ctx => GameApp.Instance.Ctx;
        protected Router Nav => GameApp.Instance.Router;

        public abstract ScreenId Id { get; }

        internal void Init()
        {
            Root = (RectTransform)transform;
            Root.Fill();
            Group = gameObject.AddComponent<CanvasGroup>();
            var safe = UI.Rect("Safe", Root);
            safe.gameObject.AddComponent<SafeArea>();
            Safe = safe;
            Build();
        }

        protected abstract void Build();

        public virtual void OnShow(object args) { }
        public virtual void OnHide() { }

        /// <summary>Android back / Escape. Return true if handled here.</summary>
        public virtual bool OnBack() => false;

        /// <summary>Standard top bar: back button on the left, title in the centre. Returns the bar.</summary>
        protected RectTransform TopBar(string title, Action onBack, Icon backIcon = Icon.Back)
        {
            var bar = UI.Rect("TopBar", Safe);
            bar.TopStrip(Theme.TopBarHeight, 18, Theme.Margin, Theme.Margin);
            var back = UI.IconButton(bar, backIcon, ButtonStyle.Secondary, onBack, Theme.IconButtonSize, "Back");
            ((RectTransform)back.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(Theme.IconButtonSize, Theme.IconButtonSize));
            var t = UI.FitText(bar, title, Theme.FontHeading, Theme.Text, TextAnchor.MiddleCenter, true, "Title");
            t.rectTransform.Fill(Theme.IconButtonSize + 20, 0, Theme.IconButtonSize + 20, 0);
            UI.AddShadow(t, -4, 0.35f);
            return bar;
        }
    }

    /// <summary>Base class for modal popups. Created fresh on open and destroyed on close.</summary>
    public abstract class PopupBase : MonoBehaviour
    {
        protected RectTransform Panel { get; private set; }
        protected RectTransform Content { get; private set; }
        protected GameApp App => GameApp.Instance;
        protected GameContext Ctx => GameApp.Instance.Ctx;
        protected Router Nav => GameApp.Instance.Router;
        CanvasGroup group;
        Image dim;
        bool closing;

        /// <summary>Whether Back / tapping outside closes it.</summary>
        public virtual bool Cancellable => true;
        public bool IsClosing => closing;
        public event Action Closed;

        protected virtual float PanelWidth => 940f;
        protected virtual float PanelHeight => 1100f;

        internal void Init()
        {
            var rt = (RectTransform)transform;
            rt.Fill();
            group = gameObject.AddComponent<CanvasGroup>();
            dim = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.Round), Theme.Dim, "Dim");
            dim.rectTransform.Fill(-40, -40, -40, -40);
            dim.raycastTarget = true;
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() =>
            {
                if (Cancellable && !closing) Close();
            });
            var safe = UI.Rect("Safe", rt);
            safe.gameObject.AddComponent<SafeArea>();
            Panel = UI.Rect("Panel", safe);
            Panel.Centre(PanelWidth, PanelHeight);
            var blocker = Panel.gameObject.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0);
            blocker.raycastTarget = true;
            Content = UI.Card(Panel, "Card", Theme.Surface, 56);
            Content.parent.GetComponent<RectTransform>().Fill();
            Build();
            group.alpha = 0;
            Tw.Fade(group, 1f, 0.16f);
            Panel.localScale = Vector3.one * 0.86f;
            Tw.Scale(Panel, 1f, 0.32f, Ease.OutBack);
        }

        protected abstract void Build();

        public virtual void OnBack()
        {
            if (Cancellable) Close();
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            group.blocksRaycasts = false;
            Tw.Scale(Panel, 0.92f, 0.14f, Ease.InQuad);
            Tw.Fade(group, 0f, 0.14f).OnComplete(() =>
            {
                if (this != null) Destroy(gameObject);
            });
            Nav.NotifyPopupClosed(this);
            try
            {
                Closed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Popup title with an optional close button.</summary>
        protected Text Title(string text, bool closeButton = true, float top = 40)
        {
            var t = UI.FitText(Content, text, Theme.FontTitle, Theme.Text, TextAnchor.MiddleCenter, true, "Title");
            t.rectTransform.TopStrip(100, top, 120, 120);
            UI.AddShadow(t, -5, 0.4f);
            if (closeButton && Cancellable)
            {
                var close = UI.IconButton(Content, Icon.Close, ButtonStyle.Ghost, Close, 96, "Close");
                ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -28), new Vector2(96, 96));
            }
            return t;
        }

        protected CCButton FooterButton(string label, ButtonStyle style, Action onClick, Icon? icon, float fromBottom, float height = 150, float left = 60, float right = 60)
        {
            var b = UI.Button(Content, label, style, onClick, icon, Theme.FontLarge);
            ((RectTransform)b.transform).BottomStrip(height, fromBottom, left, right);
            return b;
        }
    }

    /// <summary>
    /// Owns screens and popups. Uses NavStack for history, so Back always returns to the previous screen,
    /// Home always returns to the home screen, and repeated taps cannot stack duplicate screens.
    /// </summary>
    public sealed class Router : MonoBehaviour
    {
        readonly NavStack<ScreenId> history = new NavStack<ScreenId>(ScreenId.Home);
        readonly Dictionary<ScreenId, ScreenBase> screens = new Dictionary<ScreenId, ScreenBase>();
        readonly List<PopupBase> popups = new List<PopupBase>();
        RectTransform screenLayer;
        RectTransform popupLayer;
        ScreenBase visible;
        float transitionUntil;

        public ScreenId Current => history.Current;
        public bool AnyPopup => popups.Count > 0;
        public event Action<ScreenId> ScreenChanged;

        public void Init(RectTransform screenLayer, RectTransform popupLayer)
        {
            this.screenLayer = screenLayer;
            this.popupLayer = popupLayer;
        }

        ScreenBase GetScreen(ScreenId id)
        {
            ScreenBase s;
            if (screens.TryGetValue(id, out s)) return s;
            var rt = UI.Rect(id + "Screen", screenLayer);
            switch (id)
            {
                case ScreenId.Home: s = rt.gameObject.AddComponent<HomeScreen>(); break;
                case ScreenId.Map: s = rt.gameObject.AddComponent<MapScreen>(); break;
                case ScreenId.Game: s = rt.gameObject.AddComponent<GameScreen>(); break;
                case ScreenId.Shop: s = rt.gameObject.AddComponent<ShopScreen>(); break;
                case ScreenId.Settings: s = rt.gameObject.AddComponent<SettingsScreen>(); break;
                default: s = rt.gameObject.AddComponent<HowToPlayScreen>(); break;
            }
            s.Init();
            s.gameObject.SetActive(false);
            screens[id] = s;
            return s;
        }

        /// <summary>Shows a screen (returning to it if it is already in the history).</summary>
        public void Show(ScreenId id, object args = null)
        {
            history.Show(id);
            SwitchTo(GetScreen(id), args);
        }

        /// <summary>Leaves the current screen for another without keeping it in the history (used when leaving a game).</summary>
        public void Replace(ScreenId id, object args = null)
        {
            history.Replace(id);
            SwitchTo(GetScreen(history.Current), args);
        }

        public void Home()
        {
            CloseAllPopups();
            history.Home();
            SwitchTo(GetScreen(ScreenId.Home), null);
        }

        /// <summary>Back: closes the top popup, else lets the screen handle it, else goes to the previous screen.</summary>
        public void Back()
        {
            if (Time.unscaledTime < transitionUntil) return;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                if (popups[i] == null || popups[i].IsClosing) continue;
                popups[i].OnBack();
                return;
            }
            if (visible != null && visible.OnBack()) return;
            if (history.Back())
            {
                SwitchTo(GetScreen(history.Current), null);
                return;
            }
            if (history.Current == ScreenId.Home)
            {
                Open<ConfirmPopup>().Setup("Leave Colour Cascade?", "Your progress is saved.", "Quit", () => Application.Quit(), ButtonStyle.Secondary);
            }
        }

        void SwitchTo(ScreenBase next, object args)
        {
            if (visible == next)
            {
                next.OnShow(args);
                return;
            }
            var previous = visible;
            visible = next;
            transitionUntil = Time.unscaledTime + 0.2f;
            if (previous != null)
            {
                previous.OnHide();
                previous.Group.blocksRaycasts = false;
                var p = previous;
                Tw.Fade(p.Group, 0f, 0.14f).OnComplete(() =>
                {
                    if (p != null && p != visible) p.gameObject.SetActive(false);
                });
            }
            next.gameObject.SetActive(true);
            next.transform.SetAsLastSibling();
            next.Group.alpha = 0f;
            next.Group.blocksRaycasts = true;
            Tw.Fade(next.Group, 1f, 0.2f, Ease.OutQuad, previous != null ? 0.05f : 0f);
            next.OnShow(args);
            ScreenChanged?.Invoke(next.Id);
        }

        public T Open<T>() where T : PopupBase
        {
            var rt = UI.Rect(typeof(T).Name, popupLayer);
            var p = rt.gameObject.AddComponent<T>();
            popups.Add(p);
            p.Init();
            return p;
        }

        internal void NotifyPopupClosed(PopupBase p)
        {
            popups.Remove(p);
        }

        public bool IsOpen<T>() where T : PopupBase
        {
            foreach (var p in popups)
                if (p is T && !p.IsClosing) return true;
            return false;
        }

        public void CloseAllPopups()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
                if (popups[i] != null) popups[i].Close();
            popups.Clear();
        }
    }
}
