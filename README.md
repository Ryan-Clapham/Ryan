# Colour Cascade

A colourful, offline-first connected-group puzzle game for mobile (portrait, Android first), built in Unity.

Tap a group of two or more touching tiles of the same colour to clear it. Bigger groups score far more
(points = tiles × tiles × 10). Tiles fall, new ones drop in, and stone barriers split the board into
sections that refill on their own.

**Modes:** an effectively endless Campaign on an illustrated world map, Endless, a Daily Challenge and the
60-second Weekly Rush. **Extras:** six power-ups, a coin economy and shop, a shape-symbol accessibility mode,
reduced motion, haptics and synthesised sound and music.

---

## Open and play it in Unity (5 minutes)

1. Install **Unity 2021.3 LTS or newer** with Unity Hub (Unity 6 is fine). For phone builds, also tick
   **Android Build Support** (with OpenJDK and Android SDK & NDK) when installing the editor.
2. In Unity Hub choose **Add → Add project from disk** and select the **`ColourCascade`** folder inside this
   repository (the folder that contains `Assets` and `ProjectSettings`).
3. Open it. If Hub says the project was made with a different editor version, choose your installed version
   and accept the upgrade. The first import takes a few minutes.
4. When it finishes, the console shows a few `[Colour Cascade]` setup messages. The project configured
   itself (config asset, build scene, portrait and Android settings), so there is nothing to wire up.
5. Open **Colour Cascade → Open Main Scene** (or `Assets/ColourCascade/Scenes/Main.unity`) and press **Play**.

For a phone-shaped view, set the Game view to a portrait resolution such as **1080 × 1920** or
**1080 × 2340**, or use **Window → General → Device Simulator**.

> The game builds its entire interface in code at start-up (`GameApp.cs`). The scene only contains a
> placeholder object, so it starts the same way from any scene.

## Build and test on Android

1. **File → Build Settings → Android → Switch Platform** (first time only).
2. Enable **Developer options → USB debugging** on your phone and connect it.
3. Either use **Colour Cascade → Build → Android APK (Development)**, which writes
   `ColourCascade/Builds/Android/ColourCascade.apk`, or press **Build And Run** in Build Settings.
4. For Google Play use **Colour Cascade → Build → Android App Bundle (Release)**. You will need your own
   upload keystore: **Player Settings → Publishing Settings → Keystore Manager**.

The project uses IL2CPP and ARM64 (required by Google Play) and portrait-only orientation. The application
ID is `com.ryanclapham.colourcascade`; change it in `Assets/ColourCascade/Scripts/Editor/ColourCascadeSetup.cs`
or Player Settings before publishing.

## Tune the game without code

Select **Colour Cascade → Select Game Config**. Every number lives in one asset
(`Assets/ColourCascade/Resources/ColourCascadeConfig.asset`): scoring, board sizes by level, the difficulty
curve, hand-made levels (layouts are text: `.` tile, `#` stone, `x` hole, `R/Y/B/G/P/O` fixed colours),
Endless/Daily/Rush rules, coin rewards and power-up prices and unlock levels.
**Colour Cascade → Reset Game Config To Defaults** restores the built-in values.

## Run the tests

* **In Unity:** Window → General → Test Runner → EditMode → Run All (140 tests).
* **Without Unity (CI or a terminal with .NET 8):**
  ```
  cd tools/Harness
  dotnet test Tests/Tests.csproj            # the same 140 tests
  ./fetch-unity-refs.sh                     # once: downloads Unity reference assemblies from NuGet
  dotnet build UnityCheck/UnityCheck.csproj # compile-checks every Unity script (also -p:UnityPlatform=Android)
  dotnet build EditorCheck/EditorCheck.csproj
  dotnet run --project Sim -c Release -- balance x 60   # bots play levels 1-60, Endless, Rush and Daily
  dotnet run --project Sim -c Release -- art ./preview  # exports every generated sprite as PNG
  ```

## Useful menu items

| Menu | What it does |
|---|---|
| Colour Cascade → Open Main Scene | Opens the scene that starts the game |
| Colour Cascade → Select Game Config | Selects the tuning asset |
| Colour Cascade → Apply Mobile Player Settings | Re-applies portrait/Android settings |
| Colour Cascade → Save Data → Delete Save | Start again as a brand-new player |
| Colour Cascade → Save Data → Open Save Folder | Inspect `save.json` |

In the Editor and in development builds, **Settings** also has developer tools: add coins, unlock levels,
advance the clock a day (to test Daily/Weekly rotation) and view the coin ledger.

## Repository layout

```
ColourCascade/                 Unity project (open this folder)
  Assets/ColourCascade/
    Scripts/Core/              Game rules, no Unity dependency: board, groups, gravity, recovery,
                               sessions, power-ups, campaign, economy, challenges, saves, navigation
    Scripts/Procedural/        Art (signed-distance-field sprites, atlas) and synthesised audio
    Scripts/Runtime/           Unity layer: bootstrap, UI kit, screens, popups, board view, services
    Scripts/Editor/            Automatic project setup, build menu
    Tests/EditMode/            NUnit tests (run in Unity and in .NET)
    Resources/Fonts/           Fredoka (SIL Open Font License)
docs/                          Design, architecture and testing documents
tools/Harness/                 .NET projects: tests, Unity compile checks, balance simulator, art preview
tools/unity_meta.py            Creates .meta files for new assets
```

Read **docs/DESIGN.md** for the rules, modes, economy and design decisions, **docs/ARCHITECTURE.md** for how
the code fits together and how to extend it, and **docs/TESTING.md** for what has been verified and the
manual test checklist.

## Troubleshooting

* **"The type or namespace UnityEngine.UI could not be found"**: open **Window → Package Manager** and install
  **Unity UI** (`com.unity.ugui`). Unity normally includes it automatically.
* **No reaction to taps, with an "input module" error**: set **Player Settings → Other Settings → Active Input
  Handling** to **Both** or **Input Manager (Old)**. The game also supports the new Input System on its own,
  provided the Input System package is installed.
* **The game looks stretched in the Game view**: choose a portrait resolution; the game is portrait only.
* **Fonts look plain**: check that `Assets/ColourCascade/Resources/Fonts/Fredoka-Bold.ttf` imported (the game
  falls back to Unity's built-in font and logs a warning if it can't find them).

## Credits

Game design and code: Colour Cascade project. Typeface: [Fredoka](https://github.com/hafontia/Fredoka-One)
by Milena Brandão and Hafontia, SIL Open Font License 1.1 (see `Resources/Fonts/Fredoka-OFL.txt`).
All other art and audio is generated procedurally by the game's own code.
