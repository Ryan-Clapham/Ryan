# Colour Cascade: Architecture

## Assemblies

```
ColourCascade.Core        (no Unity references)   rules, data, services, saves
ColourCascade.Procedural  (no Unity references)   sprite + sound generation      → Core
ColourCascade.Runtime     (Unity + uGUI)          bootstrap, UI, screens, views  → Core, Procedural
ColourCascade.Editor      (Editor only)           project setup, build menu      → Runtime
ColourCascade.Setup       (Editor only)           installs missing packages,      (no references)
                                                  switches SDK integrations on
ColourCascade.Integrations.AdMob    (needs CC_ADMOB)      Google Mobile Ads + UMP  → Core, Runtime
ColourCascade.Integrations.UnityIAP (needs CC_UNITY_IAP)  Unity IAP 5             → Core, Runtime
ColourCascade.Tests.EditMode                      NUnit tests                     → Core, Procedural
```

The two integration assemblies compile only when their SDK is installed: `Setup` sets the `CC_ADMOB` /
`CC_UNITY_IAP` scripting defines when it finds the Google Mobile Ads plugin or Unity IAP 5, and the menu
**Colour Cascade > Monetisation** can switch either off. They register their implementations with
`MonetisationBackends` before the first scene loads; nothing else depends on them (docs/MONETISATION.md).

`Runtime` and `Editor` have a define constraint that is only met when Unity UI (`com.unity.ugui`) is installed,
so a project without the package has no compile errors (and no Safe Mode); `Setup`, which always compiles,
then installs the package (and the Input System, if the project accepts only that input backend).

`Core` and `Procedural` are marked `noEngineReferences`, which guarantees the game rules can't depend on
Unity. That is what lets the same tests run in Unity's Test Runner and in plain .NET
(`tools/Harness/Tests`), and lets bots simulate thousands of games for balancing.

## Core (Scripts/Core)

| Area | Key types | Responsibility |
|---|---|---|
| Board | `Board`, `Tile`, `GridPos`, `CellType`, `BoardFit` | Grid state; row 0 is the bottom. Every tile has an id so the view can track it. `BoardFit` is the cell-size arithmetic the board view uses (tested at every supported aspect ratio). |
| | `GroupFinder` | Allocation-free flood fill; `HasLegalMove`. |
| | `Gravity`, `TileSource` | Falling and refill per barrier-separated section; weighted, integer-only colour choice with optional assist. |
| | `BoardRecovery` | Dead-board reshuffle: bounded random attempts, then a guaranteed constructive arrangement. |
| | `LevelLayout`, `BoardGenerator` | Board shape (open/stone/void/fixed colour, plus an optional obstacle layer) and its text format. |
| | `BoardSilhouette` (3.3) | The panel behind a board with gaps: alpha maps (panel, outline, shadow) from an exact distance transform of the cell mask, with rounded outer corners. Pure maths; `BoardView` turns the maps into sprites and the art preview draws them. |
| Mechanics | `MechanicSet`, `Mechanics`, `EndlessMilestones`, `BoardDecorator` | The tile mechanics as one shared system for every mode: which are unlocked (campaign introductions, Endless levels), their names and tutorials, and placing obstacles and treasure on any board. |
| Challenges | `ChallengeMechanics` | Which mechanics a Daily Challenge or Weekly Rush uses: the date's plan cut to the player's unlocks, fair pairs, weekly themes. |
| Tiles | `SpecialKind`, `SpecialTier`, `SpecialRules`, `SpecialEffects` | Special tiles (rockets, bombs, prisms): which group sizes earn them, the cells each one (or a pair fired together) reaches, combination partners. |
| | `ObstacleKind`, `ObstacleDefinition`, `Obstacles`, `TileRules` | Obstacles (ice, chains, moss) as data: each kind is a definition of flags (holds in place, joins groups, hit by its own group, hit by adjacent clears, can be recoloured). `TileRules` answers every rule question about a tile; group search, gravity, recovery, power-ups and bots ask it instead of checking cell types. See docs/MECHANICS.md. |
| Goals | `GoalKind`, `GoalSpec`, `GoalTracker` | A level's one to three goals and their progress, read from each `MoveOutcome`. A campaign level is won when every goal is met; a level with no goals has a score goal equal to its target. |
| Rules | `ScoringRules`, `CampaignConfig`, `EndlessConfig`, `DailyConfig`, `RushConfig`, `EconomyConfig` | All tunables (serialised by Unity into the config asset). `EndlessTuning` (3.3) moves an older asset's Endless values (stage scores, colours, journey) to the current ones where they still equal an old default; the editor applies it to the asset and the game to its in-memory copy. |
| | `LevelSpec` | Everything needed to play one board. |
| Game | `PuzzleSession` | **The rules engine.** One run in any mode: moves, timer, power-up effects, obstacle hits, goals, win/lose. Returns a `MoveOutcome` describing exactly what happened (cleared, obstacle hits, swapped, recoloured, falls, spawns, shuffle, goal changes). |
| | `GameContext` | Composition root: owns the save and every service; starts runs (`CreateCampaignRun`…), finishes them idempotently (`FinishRun`). |
| Power-ups | `PowerUpDefinition`, `PowerUpCatalog`, `IPowerUpEffect`, `PowerUpEffects` | Data-driven definitions plus one effect class per power-up. |
| Economy | `Wallet`, `Inventory`, `RewardService`, `ShopService`, `PowerUpService`, `ActionPolicy`, `ActivityTracker`, `EconomyTuning` | Coins (single source of truth + ledger), stock and trials, one-time claim keys, validated purchases (singles and three-packs, 3.1), central eligibility rules; `EconomyTuning` moves an older config asset's untouched reward values to the current version (3 since update 3.1). |
| Monetisation (3.0) | `MonetisationServices`, `AdService`, `InterstitialPolicy`, `ContinueService`, `CoinOffers`, `PurchaseService`, `MonetisationAnalytics`, `MonetisationConfig`, `IAdNetwork`/`IConsentProvider`/`IStoreBackend` | The advertising manager (consent gate, loading, reward tickets granted exactly once, watchdogs, limits counted per player-local day, shared by placements in the same `dailyLimitGroup` since 3.1), interstitial timing, Out of Moves and shortfall offers, purchase processing (grant, save, then confirm) and the No Ads entitlement, the event log. SDKs only behind the three interfaces (docs/MONETISATION.md). |
| Achievements | `AchievementCatalog`, `AchievementDefinition`, `AchievementService`, `AchievementText`, `AchievementRewardTuning` | The fixed list as data (config asset or defaults); progress read from existing records and lifetime statistics; unlock on load, after runs and when a run is left; claim once into the wallet and inventory (claim keys `ach.<id>`), at the reward the catalog has at claim time; `AchievementRewardTuning` moves a config asset's pre-3.1 rewards to the 3.1 ones. docs/ACHIEVEMENTS.md. |
| Progression | `CampaignCatalog`, `CampaignDesign`, `MechanicSchedule`, `LayoutLibrary`, `LevelCalibrator`, `Bots`, `PlannerBot`, `LevelCodec`, `CampaignProgress`, `Regions`, `DefaultLevels` | Level generation and calibration, unlocks and stars. `CampaignDesign` holds the mechanic schedule, the built-in introduction levels, bands, rhythm and recipes that decorate generated boards. Bots: Greedy, Casual, Mixed (fire specials too) and Planner (goal-aware, one move of lookahead, integer-only so calibration is identical on every device). `LevelCodec` reads and writes levels as JSON. From level 121 (update 3.2) `CampaignProgression` plans each level (tier, intensity, board family, recipe, colours), `BoardShapes` builds and checks the board shapes, `CampaignPlacement` places obstacles and treasure fairly on any shape, and `CampaignCatalog` sizes moves from the Planner and checks each level is passed reliably. `CampaignCatalog.Get` is thread-safe (one lazy build per level) and `Prewarm` builds a level on a background thread. |
| Challenges | `ChallengeCalendar`, `ChallengeFactory` | UTC day keys, ISO week keys, deterministic daily/weekly boards. Since 3.3 (`AdvancedChallenges.cs`, a partial of the factory) players from campaign level 121 get advanced puzzles built from the campaign's shape library, recipes, placement and bot calibration; the factory caches each puzzle by key, unlocks and shapes met, and is thread-safe (one lazy build per key, so `GameContext.PrewarmChallenges` can build in the background). |
| Save | `SaveData`, `SaveCodec`, `Json`, `SaveSystem`, `ISaveStorage`, `FileSaveStorage` | Versioned JSON, atomic writes with backup, corruption recovery. |
| Navigation | `NavStack<T>` | Screen history rules (tested). |
| Util | `Rng`, `StableHash`, `IClock`, `NumberFormat` | Platform-independent determinism and time. |

### A move, end to end

```
finger down on a tile ─▶ BoardView.OnPointerDown ─▶ GameScreen.Preview ─▶ PuzzleSession.PreviewGroup
                         (highlight + "+90" bubble)
finger up on that group ─▶ BoardView.Committed ─▶ GameScreen.PlayGroup ─▶ PuzzleSession.TryPlay(cell, outcome)
    PuzzleSession: validate → remove group (chained tiles stay and lose a link) → score → moves
                   → ice next to removed tiles loses a layer (one step per obstacle per action) → obstacle points
                   → Gravity.Apply → goal progress → BoardRecovery (free tiles only) → win/lose check
    Special tiles: a tap on one fires it (and a touching partner); anything it hits is queued and fires in turn.
    Big groups leave a special where they were tapped. Treasure is collected inside Gravity.Apply; moss grows
    after move-costing actions. Power-ups run the same tail after their effect (docs/MECHANICS.md §3.1).
GameScreen ─▶ BoardView.Animate(outcome) (specials wave by wave → clear → obstacle hits → new special →
             fall → treasure collected → refill → moss growth → shuffle; input blocked until tiles land)
           ─▶ HUD, sounds, haptics, floating text
if outcome.Ended ─▶ GameContext.FinishRun(session) (progress, records, rewards, save) ─▶ ResultPopup
```

The session's board is always the truth. After every animation `BoardView.Verify()` compares the view to
the board and silently repairs any difference, so a visual glitch can never desync gameplay.

## Runtime (Scripts/Runtime)

| Area | Key types | Notes |
|---|---|---|
| App | `GameApp` | `[RuntimeInitializeOnLoadMethod]` bootstrap. Creates camera, canvas (1080 × 1920 reference), event system, splash; generates art and audio on background threads (atlas cached on device per app version); loads config and save; handles Android Back, pause/focus and saving. |
| Monetisation | `MonetisationHost`, `MonetisationBackends`, `MainThread` | Starts consent, adverts and the store after the home screen appears; runs SDK callbacks on the main thread; pauses audio and blocks input while an advert is on screen; tells the player about purchases anywhere (docs/MONETISATION.md). |
| | `ColourCascadeConfig` | ScriptableObject wrapping all Core config classes. |
| Services | `ArtLibrary`, `Fonts`, `AudioService`, `Haptics` | Sprites from one generated atlas; Fredoka fonts; pooled audio sources and music; Android vibration. |
| UI kit | `Theme`, `UI`, `CCButton`, `Tw`/`TweenRunner`, `Widgets` (SafeArea, ScreenAdapter, ToggleSwitch, SliderControl, ProgressBar, StarRow, CoinCounter, TabBar, Toast, CoachMark), `FxLayer` | Everything is built in code from these. `CCButton` debounces taps. The tween engine is pooled and replaces conflicting tweens. `FxLayer` simulates pooled particles without per-frame allocations. |
| Navigation | `Router`, `ScreenBase`, `PopupBase` | Screens are built once and re-bound on show; popups are created per use. |
| Screens | `HomeScreen`, `MapScreen`, `GameScreen`, `ShopScreen`, `SettingsScreen`, `HowToPlayScreen`, `AchievementsScreen` | The map is virtualised: only on-screen levels exist as (pooled) objects. |
| Popups | `LevelPopup`, `ModePopup`, `ResultPopup`, `PausePopup`, `ConfirmPopup`, `PowerUpIntroPopup`, `QuickBuyPopup` | |
| Board | `BoardView`, `TileView` | Rendering, press-and-hold preview, drag to explore, animation choreography. A `TileView` shows a colour tile with an obstacle overlay and strength badge, a stone, a special tile or treasure. |
| UI | `GoalsPanel`, `GoalVisuals`, `MechanicTips`, `MechanicIntroPopup` | The goals row on the game screen, goal pictures and wording, and each mechanic's tutorial card and pointer. |
| | `AchievementVisuals`, `AchievementNotifier` | Tier colours and category icons, claiming with feedback, and the unlock banners (one at a time, a summary for several, never during a game or over a popup). |

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
* Developer builds can switch to a separate `save-test.json` (`GameContext.UseStorage`, Settings > Developer),
  so fast-tracked test progress never touches `save.json` (docs/ACHIEVEMENTS.md §8).

## Extending the game

* **New power-up:** add a value to `PowerUpId`, a `PowerUpDefinition` in `PowerUpCatalog.CreateDefaults`
  (and the config asset), and a class implementing `IPowerUpEffect` registered in `PowerUpEffects`. Add its art
  in `ArtBook.PowerUpIcon`. The shop, in-game bar, inventory, trial grant and eligibility rules pick it up.
* **New shop category:** add a `ShopTab` subclass to the list in `ShopScreen.Build`.
* **New achievement:** add an entry in the config asset (or `AchievementCatalog.CreateDefaults`) with a new ID; a
  new kind of measure also needs an `AchievementStat` read in `AchievementService.Current` (docs/ACHIEVEMENTS.md §9).
* **New hand-made level:** add a `LevelOverride` in the config asset (any field left at 0 is generated).
* **New board shape (3.3):** add its rows to the config asset (campaign → shapes → extraShapes: a family and rows in
  the layout notation, '.' a cell, 'x' a gap, '#' a stone); it joins that family's variants wherever the family is
  used (campaign, advanced Daily and Weekly, Endless) if it passes `BoardShapes.IsFair`. A new **family** needs a
  `BoardFamily` value (added at the end), its level in `BoardShapeSchedule.FromLevel`, a name and introduction note
  in `BoardShapes`, rows in `BoardShapeLibrary.BuiltIn`, and, if it is slow to play, a share in
  `CampaignProgression.BoardPermille`. Nothing in the engine or the board view is per shape.
* **New obstacle on tiles:** add an `ObstacleKind`, register an `ObstacleDefinition` in `Obstacles` (its flags
  decide how it behaves everywhere), a character for the layout notation in `Board.ObstacleChar` /
  `TryParseObstacle`, a goal kind if levels should ask for it, and its art. Write the rules in
  docs/MECHANICS.md first and add tests for every interaction.
* **New fixed cell type:** add a `CellType` and answer for it in `TileRules`, add a sprite and a case in
  `TileView.Setup`.
* Obstacles and non-score goals are campaign-only: `ActionPolicy.ModeAllowsCampaignMechanics`, and a session
  in any other mode refuses to start with them.
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
