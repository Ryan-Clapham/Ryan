# Colour Cascade: Architecture

## Assemblies

```
ColourCascade.Core        (no Unity references)   rules, data, services, saves
ColourCascade.Procedural  (no Unity references)   sprite + sound generation      → Core
ColourCascade.Runtime     (Unity + uGUI)          bootstrap, UI, screens, views  → Core, Procedural
ColourCascade.Editor      (Editor only)           project setup, build menu      → Runtime
ColourCascade.Setup       (Editor only)           installs missing packages       (no references)
ColourCascade.Tests.EditMode                      NUnit tests                     → Core, Procedural
```

`Runtime` and `Editor` have a define constraint that is only met when Unity UI (`com.unity.ugui`) is installed,
so a project without the package has no compile errors (and no Safe Mode); `Setup`, which always compiles,
then installs the package (and the Input System, if the project accepts only that input backend).

`Core` and `Procedural` are marked `noEngineReferences`, which guarantees the game rules can't depend on
Unity. That is what lets the same tests run in Unity's Test Runner and in plain .NET
(`tools/Harness/Tests`), and lets bots simulate thousands of games for balancing.

## Core (Scripts/Core)

| Area | Key types | Responsibility |
|---|---|---|
| Board | `Board`, `Tile`, `GridPos`, `CellType` | Grid state; row 0 is the bottom. Every tile has an id so the view can track it. |
| | `GroupFinder` | Allocation-free flood fill; `HasLegalMove`. |
| | `Gravity`, `TileSource` | Falling and refill per barrier-separated section; weighted, integer-only colour choice with optional assist. |
| | `BoardRecovery` | Dead-board reshuffle: bounded random attempts, then a guaranteed constructive arrangement. |
| | `LevelLayout`, `BoardGenerator` | Board shape (open/stone/void/fixed colour) and its text format. |
| Rules | `ScoringRules`, `CampaignConfig`, `EndlessConfig`, `DailyConfig`, `RushConfig`, `EconomyConfig` | All tunables (serialised by Unity into the config asset). |
| | `LevelSpec` | Everything needed to play one board. |
| Game | `PuzzleSession` | **The rules engine.** One run in any mode: moves, timer, power-up effects, win/lose. Returns a `MoveOutcome` describing exactly what happened (cleared, swapped, recoloured, falls, spawns, shuffle). |
| | `GameContext` | Composition root: owns the save and every service; starts runs (`CreateCampaignRun`…), finishes them idempotently (`FinishRun`). |
| Power-ups | `PowerUpDefinition`, `PowerUpCatalog`, `IPowerUpEffect`, `PowerUpEffects` | Data-driven definitions plus one effect class per power-up. |
| Economy | `Wallet`, `Inventory`, `RewardService`, `ShopService`, `PowerUpService`, `ActionPolicy`, `ActivityTracker` | Coins (single source of truth + ledger), stock and trials, one-time claim keys, validated purchases, central eligibility rules. |
| Progression | `CampaignCatalog`, `LayoutLibrary`, `LevelCalibrator`, `Bots`, `CampaignProgress`, `Regions`, `DefaultLevels` | Level generation and calibration, unlocks and stars. |
| Challenges | `ChallengeCalendar`, `ChallengeFactory` | UTC day keys, ISO week keys, deterministic daily/weekly boards. |
| Save | `SaveData`, `SaveCodec`, `Json`, `SaveSystem`, `ISaveStorage`, `FileSaveStorage` | Versioned JSON, atomic writes with backup, corruption recovery. |
| Navigation | `NavStack<T>` | Screen history rules (tested). |
| Util | `Rng`, `StableHash`, `IClock`, `NumberFormat` | Platform-independent determinism and time. |

### A move, end to end

```
finger down on a tile ─▶ BoardView.OnPointerDown ─▶ GameScreen.Preview ─▶ PuzzleSession.PreviewGroup
                         (highlight + "+90" bubble)
finger up on that group ─▶ BoardView.Committed ─▶ GameScreen.PlayGroup ─▶ PuzzleSession.TryPlay(cell, outcome)
    PuzzleSession: validate → remove group → score → moves → Gravity.Apply → BoardRecovery → win/lose check
GameScreen ─▶ BoardView.Animate(outcome) (clear → fall → refill → shuffle; input blocked until tiles land)
           ─▶ HUD, sounds, haptics, floating text
if outcome.Ended ─▶ GameContext.FinishRun(session) (progress, records, rewards, save) ─▶ ResultPopup
```

The session's board is always the truth. After every animation `BoardView.Verify()` compares the view to
the board and silently repairs any difference, so a visual glitch can never desync gameplay.

## Runtime (Scripts/Runtime)

| Area | Key types | Notes |
|---|---|---|
| App | `GameApp` | `[RuntimeInitializeOnLoadMethod]` bootstrap. Creates camera, canvas (1080 × 1920 reference), event system, splash; generates art and audio on background threads (atlas cached on device per app version); loads config and save; handles Android Back, pause/focus and saving. |
| | `ColourCascadeConfig` | ScriptableObject wrapping all Core config classes. |
| Services | `ArtLibrary`, `Fonts`, `AudioService`, `Haptics` | Sprites from one generated atlas; Fredoka fonts; pooled audio sources and music; Android vibration. |
| UI kit | `Theme`, `UI`, `CCButton`, `Tw`/`TweenRunner`, `Widgets` (SafeArea, ScreenAdapter, ToggleSwitch, SliderControl, ProgressBar, StarRow, CoinCounter, TabBar, Toast, CoachMark), `FxLayer` | Everything is built in code from these. `CCButton` debounces taps. The tween engine is pooled and replaces conflicting tweens. `FxLayer` simulates pooled particles without per-frame allocations. |
| Navigation | `Router`, `ScreenBase`, `PopupBase` | Screens are built once and re-bound on show; popups are created per use. |
| Screens | `HomeScreen`, `MapScreen`, `GameScreen`, `ShopScreen`, `SettingsScreen`, `HowToPlayScreen` | The map is virtualised: only on-screen levels exist as (pooled) objects. |
| Popups | `LevelPopup`, `ModePopup`, `ResultPopup`, `PausePopup`, `ConfirmPopup`, `PowerUpIntroPopup`, `QuickBuyPopup` | |
| Board | `BoardView`, `TileView` | Rendering, press-and-hold preview, drag to explore, animation choreography. |

## Determinism

`Rng` (xoshiro128**) and `StableHash` (FNV-1a) use integer maths only, so a seed gives the same sequence on
every device and runtime (IL2CPP, Mono, .NET). Daily and weekly puzzles, campaign layouts and level targets all
derive from stable seeds. `System.Random` and floating point are never used for game state.

## Saving

* `save.json` in `Application.persistentDataPath`. Writes go to `save.json.tmp` (flushed), then the old file
  becomes `save.json.bak` and the temp file is renamed into place. Loading tries main, then temp, then backup;
  if everything is unreadable the player starts fresh and is told.
* Saves happen immediately after anything earned (run results, purchases, power-up use, trial grants),
  when the app is paused or quit, and (debounced) after settings changes.
* `SaveData.Version` plus `SaveCodec.Migrate` handle future format changes. Unknown fields are ignored and
  missing ones take defaults; `Normalize()` repairs impossible values (negative coins, a completed level
  beyond the unlocked one, duplicate records).
* Temporary run state (the board in play) is deliberately not saved.

## Extending the game

* **New power-up:** add a value to `PowerUpId`, a `PowerUpDefinition` in `PowerUpCatalog.CreateDefaults`
  (and the config asset), and a class implementing `IPowerUpEffect` registered in `PowerUpEffects`. Add its art
  in `ArtBook.PowerUpIcon`. The shop, in-game bar, inventory, trial grant and eligibility rules pick it up.
* **New shop category:** add a `ShopTab` subclass to the list in `ShopScreen.Build`.
* **New hand-made level:** add a `LevelOverride` in the config asset (any field left at 0 is generated).
* **New board obstacle:** add a `CellType`, teach `Gravity`/`GroupFinder` how it behaves, add a sprite and a
  case in `TileView.Setup`.
* **New mode:** add a `GameMode`, a `Create…Run` method in `GameContext`, end rules in
  `PuzzleSession.EvaluateEnd`, result handling in `FinishRun`, and a card on the home screen.
* **Online features later:** replace `IClock` with server time; challenge seeds are already shared by key,
  so leaderboards can compare like with like.

## Performance notes

* One 2048 × 1024 atlas holds every sprite, so the UI batches into very few draw calls.
* Art (~0.4 s on a desktop CPU) and audio (~0.3 s) are generated on worker threads behind the splash; the
  atlas is cached to disk after the first launch of each app version.
* Group search, gravity and recovery reuse buffers; particles and floating text are pooled; target calibration
  takes a few milliseconds per level and is cached.
* 60 fps target, vSync off, portrait only.
