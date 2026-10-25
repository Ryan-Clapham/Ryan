using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Entry point. Starts automatically when any scene loads (no scene setup needed), builds the camera,
    /// canvas and services, generates art and audio on background threads behind a splash screen, loads the
    /// save and opens the home screen. Also owns app lifecycle: saving on pause/quit and Android Back.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public GameContext Ctx { get; private set; }
        public Router Router { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        public FxLayer GlobalFx { get; private set; }
        public DebugClock Clock { get; private set; }
        public bool Ready { get; private set; }

        /// <summary>(before, after) — always refers to the current wallet, even after a progress reset.</summary>
        public event Action<int, int> CoinsChanged;
        /// <summary>Raised when the app is backgrounded (true) or resumed (false).</summary>
        public event Action<bool> AppPaused;

        RectTransform root;
        Image background;
        bool saveRequested;
        float saveAt;
        Wallet boundWallet;
#if !ENABLE_LEGACY_INPUT_MANAGER
        Func<bool> inputSystemBack;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("ColourCascade");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            Input.multiTouchEnabled = false;
            BuildCanvas();
            StartCoroutine(Startup());
        }

        // ------------------------------------------------------------------ Scene objects

        void BuildCanvas()
        {
            var camGo = new GameObject("UICamera");
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.BgDeep;
            cam.cullingMask = 0;
            cam.orthographic = true;
            cam.depth = -10;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Theme.RefWidth, Theme.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100;
            canvasGo.AddComponent<ScreenAdapter>();
            canvasGo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasGo.transform;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
                es.AddComponent<StandaloneInputModule>();
#else
                var module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (module != null) es.AddComponent(module);
                else Debug.LogError("[ColourCascade] No UI input module: enable 'Input Manager (Old)' or 'Both' in Player Settings > Active Input Handling.");
#endif
            }
#if !ENABLE_LEGACY_INPUT_MANAGER
            inputSystemBack = CreateInputSystemBackReader();
#endif
        }

        // ------------------------------------------------------------------ Startup

        IEnumerator Startup()
        {
            Fonts.Load();
            var splash = BuildSplash(out var progress);

            Clock = new DebugClock();
            var configAsset = Resources.Load<ColourCascadeConfig>(ColourCascadeConfig.ResourcePath);
            var config = configAsset != null ? configAsset.ToData() : GameConfigData.CreateDefault();
            if (configAsset == null) Debug.Log("[ColourCascade] No config asset found; using built-in defaults.");
            string saveDir = Application.persistentDataPath;
            Ctx = new GameContext(config, new FileSaveStorage(saveDir), Clock);
            Ctx.Saves.Log = msg => Debug.LogWarning("[ColourCascade] " + msg);
            var loadStatus = Ctx.Load();
            Ctx.DataReplaced += BindWallet;
            BindWallet();
            Haptics.Settings = Ctx.Settings;
            Tw.Reduced = Ctx.Settings.ReducedMotion;
            Ctx.Settings.Changed += k =>
            {
                if (k == SettingKind.ReducedMotion) Tw.Reduced = Ctx.Settings.ReducedMotion;
                RequestSave();
            };

            // Art and audio are generated off the main thread; the atlas is cached per app version.
            string cachePath = Path.Combine(Application.persistentDataPath, "art-cache.bin");
            string cacheKey = Application.version + "/" + ArtBook.Version + "/" + Application.unityVersion;
            bool useCache = !Application.isEditor;
            var artTask = Task.Run(() => LoadOrBuildAtlas(cachePath, cacheKey, useCache));
            var bgTask = Task.Run(() =>
            {
                var list = new List<ImageData>();
                for (int t = 0; t < 6; t++) list.Add(ArtBook.Background(t, 64, 256));
                list.Add(ArtBook.Background(-1, 180, 320));
                return list;
            });
            var sfxTask = Task.Run(() => SoundSynth.BuildEffects());
            var musicTask = Task.Run(() => SoundSynth.BuildMusic());
            float shown = 0;
            while (!(artTask.IsCompleted && bgTask.IsCompleted && sfxTask.IsCompleted && musicTask.IsCompleted))
            {
                int done = (artTask.IsCompleted ? 2 : 0) + (bgTask.IsCompleted ? 1 : 0) + (sfxTask.IsCompleted ? 1 : 0) + (musicTask.IsCompleted ? 1 : 0);
                shown = Mathf.MoveTowards(shown, Mathf.Max(done / 5f, shown + 0.002f), Time.unscaledDeltaTime * 1.5f);
                progress.SetFraction(Mathf.Min(shown, 0.95f), false);
                yield return null;
            }
            if (artTask.IsFaulted || bgTask.IsFaulted)
            {
                Debug.LogException(artTask.Exception ?? bgTask.Exception);
                yield break;
            }
            progress.SetFraction(1f, false);
            var backgrounds = bgTask.Result;
            ArtLibrary.Create(artTask.Result, backgrounds[6], backgrounds.GetRange(0, 6));

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            var audio = audioGo.AddComponent<AudioService>();
            if (sfxTask.IsFaulted || musicTask.IsFaulted) Debug.LogException(sfxTask.Exception ?? musicTask.Exception);
            audio.Init(Ctx.Settings, sfxTask.IsFaulted ? new List<SoundData>() : sfxTask.Result, musicTask.IsFaulted ? null : musicTask.Result);

            BuildLayers();
            Ready = true;
            Router.Show(ScreenId.Home);
            var splashGroup = splash.gameObject.AddComponent<CanvasGroup>();
            splash.SetAsLastSibling();
            Tw.Fade(splashGroup, 0f, 0.35f, Ease.OutQuad, 0.1f).OnComplete(() => Destroy(splash.gameObject));

            if (loadStatus == LoadStatus.CorruptReset)
                Tw.Delay(0.8f, () => Toast.Show("Your save couldn't be read, so a new one was started.", Icon.Info, Theme.Danger));
            else if (loadStatus == LoadStatus.RecoveredFromBackup || loadStatus == LoadStatus.RecoveredFromTemp)
                Debug.Log("[ColourCascade] Save restored from " + loadStatus);
        }

        static AtlasResult LoadOrBuildAtlas(string cachePath, string key, bool useCache)
        {
            if (useCache && File.Exists(cachePath))
            {
                try
                {
                    using (var fs = File.OpenRead(cachePath))
                    {
                        var cached = AtlasCache.Read(fs, key);
                        if (cached != null) return cached;
                    }
                }
                catch (Exception)
                {
                    // Fall through and regenerate.
                }
            }
            var atlas = AtlasPacker.Pack(ArtBook.BuildAtlasImages());
            if (useCache)
            {
                try
                {
                    string tmp = cachePath + ".tmp";
                    using (var fs = File.Create(tmp)) AtlasCache.Write(fs, atlas, key);
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                    File.Move(tmp, cachePath);
                }
                catch (Exception)
                {
                    // Cache is an optimisation only.
                }
            }
            return atlas;
        }

        RectTransform BuildSplash(out SplashBar bar)
        {
            var splash = UI.Rect("Splash", root);
            splash.Fill();
            var bg = splash.gameObject.AddComponent<Image>();
            bg.color = Theme.BgDeep;
            var title = UI.Text(splash, UI.RainbowWord("COLOUR") + "\nCASCADE", 120, Color.white, TextAnchor.MiddleCenter, true, "Logo");
            title.rectTransform.Centre(1000, 320, 0, 120);
            title.lineSpacing = 0.85f;
            var track = UI.Rect("Loading", splash);
            track.Centre(520, 22, 0, -180);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.color = new Color(1, 1, 1, 0.12f);
            var fill = UI.Rect("Fill", track);
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(0, 1);
            fill.pivot = new Vector2(0, 0.5f);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = Theme.Primary;
            bar = new SplashBar(track, fill);
            return splash;
        }

        sealed class SplashBar
        {
            readonly RectTransform track, fill;
            public SplashBar(RectTransform track, RectTransform fill) { this.track = track; this.fill = fill; }
            public void SetFraction(float f, bool animate) => fill.sizeDelta = new Vector2(track.rect.width * Mathf.Clamp01(f), 0);
        }

        void BuildLayers()
        {
            background = UI.Image(root, ArtLibrary.Instance.Background(-1), Color.white, "Background");
            background.rectTransform.Fill();
            background.transform.SetAsFirstSibling();
            var screens = UI.Rect("Screens", root);
            screens.Fill();
            var popups = UI.Rect("Popups", root);
            popups.Fill();
            OverlayLayer = UI.Rect("Overlay", root);
            OverlayLayer.Fill();
            GlobalFx = FxLayer.Create(OverlayLayer, "GlobalFx");
            Router = gameObject.AddComponent<Router>();
            Router.Init(screens, popups);
        }

        /// <summary>Rebinds to the current save data (startup and after a progress reset).</summary>
        void BindWallet()
        {
            if (boundWallet != null) boundWallet.Changed -= RelayCoins;
            boundWallet = Ctx.Wallet;
            boundWallet.Changed += RelayCoins;
            Haptics.Settings = Ctx.Settings;
            Tw.Reduced = Ctx.Settings.ReducedMotion;
            if (AudioService.Instance != null) AudioService.Instance.RefreshVolumes();
            CoinsChanged?.Invoke(boundWallet.Balance, boundWallet.Balance);
        }

        /// <summary>Saves soon, coalescing bursts of changes (e.g. dragging a volume slider) into one write.</summary>
        public void RequestSave()
        {
            if (!saveRequested) saveAt = Time.unscaledTime + 0.75f;
            saveRequested = true;
        }

        void FlushSave()
        {
            if (!saveRequested || Ctx == null) return;
            saveRequested = false;
            Ctx.Save();
        }

        void RelayCoins(int before, int after) => CoinsChanged?.Invoke(before, after);

        // ------------------------------------------------------------------ Lifecycle

        void Update()
        {
            if (!Ready) return;
            if (saveRequested && Time.unscaledTime >= saveAt) FlushSave();
            if (BackPressed()) Router.Back();
        }

        bool BackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return inputSystemBack != null && inputSystemBack();
#endif
        }

        /// <summary>Reads Keyboard.current.escapeKey (the Android Back button) via reflection, so the project
        /// compiles whether or not the Input System package is installed.</summary>
        static Func<bool> CreateInputSystemBackReader()
        {
            try
            {
                var keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                if (keyboardType == null) return null;
                var current = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                var escape = keyboardType.GetProperty("escapeKey", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo pressed = null;
                return () =>
                {
                    var kb = current?.GetValue(null);
                    if (kb == null || escape == null) return false;
                    var key = escape.GetValue(kb);
                    if (key == null) return false;
                    if (pressed == null) pressed = key.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
                    return pressed != null && (bool)pressed.GetValue(key);
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!Ready) return;
            if (paused)
            {
                saveRequested = false;
                Ctx.Save();
            }
            AppPaused?.Invoke(paused);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!Ready) return;
            if (!focused)
            {
                saveRequested = false;
                Ctx.Save();
            }
            AppPaused?.Invoke(!focused);
        }

        void OnApplicationQuit()
        {
            if (Ctx != null) Ctx.Save();
        }

        /// <summary>Changes the full-screen backdrop (main night sky or a region theme).</summary>
        public void SetBackground(int theme)
        {
            if (background != null) background.sprite = ArtLibrary.Instance.Background(theme);
        }
    }
}
