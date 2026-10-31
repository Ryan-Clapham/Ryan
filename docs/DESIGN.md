# Colour Cascade: Game Design

This document records how the game works and why. Every number below is a default in
`ColourCascadeConfig.asset` and can be changed without touching code.

## 1. Pillars and look

* **Instantly readable:** one rule (tap a touching group of two or more), with clear feedback.
* **Satisfying:** big groups pay off disproportionately and feel big (sound pitch rises, praise text,
  shockwave rings, screen shake for huge groups).
* **Fair and calm:** no timers except the opt-in Weekly Rush, free reshuffles, no lives, no pay-to-win.
* **Visual identity:** a deep night-violet world (`#120D33` to `#2C1B70` with soft pink and blue glows) so six
  glossy "candy" tiles glow. Coral (`#FF6A4D`) is the primary action colour and gold is reserved for rewards.
  Rounded surfaces, soft shadows and the Fredoka typeface throughout. All art is generated from vector
  shapes in code (`ArtBook.cs`), so it stays crisp at any resolution and needs no imported images.

## 2. Core rules

| Rule | Behaviour |
|---|---|
| Groups | Orthogonally connected tiles of one colour. Diagonals never connect. Minimum size 2. |
| Clearing | The whole group is removed and scores `size² × 10`. A single tile does nothing and costs nothing. |
| Gravity | Tiles fall straight down to fill gaps. |
| Barriers | **Stones** never move, block falling and block connections. **Holes** (voids) are missing cells that shape a board. Both split columns into **sections** that fall and refill independently. A section under a stone refills from just beneath the stone. |
| Refill | Every empty cell is refilled. Early levels use an "assist" chance that copies a neighbouring colour, so friendly groups form more often. |
| Dead board | After every move the engine checks for a legal move. If none exists it reshuffles the existing tiles (stones stay put): up to 12 random shuffles, then a constructive arrangement that always succeeds when success is possible. Score, moves and progress are untouched. If no arrangement can work (practically impossible with real layouts), the run ends as **Board Locked**, which is not counted as a loss. |

Coordinates are **columns × rows**, column 0 on the left, row 0 at the bottom.

### Scoring

| Group | 2 | 3 | 4 | 5 | 6 | 8 | 12 |
|---|---|---|---|---|---|---|---|
| Points | 40 | 90 | 160 | 250 | 360 | 640 | 1,440 |

Praise appears at 5 ("Nice!"), 8 ("Great!"), 12 ("Amazing!") and 16+ ("Colour Cascade!").
Tiles removed by power-ups score a flat 20 each (they are not "groups").

## 3. Campaign

* **Board sizes:** levels 1–3 are 6 × 6, levels 4–6 are 7 × 7, level 7 onwards is 8 × 9.
* **Levels 1–10 are hand-made** to teach one idea at a time:

| Level | Teaches |
|---|---|
| 1 | Fixed teaching board, three colours, hand pointer on a big group |
| 2 | Bigger groups score more; press-and-hold preview |
| 3 | A fourth colour; target, moves and stars explained |
| 4 | Bigger board (7 × 7) |
| 5 | Tip pointing at the Symbols button |
| 7 | Full-size board (8 × 9) |
| 8 | **Stone barriers** (pillars) with an explanation |
| 9 | Shelves: sections refill on their own |
| 10 | First milestone; **Blast** is introduced with 2 free uses |

* **Generated levels (11 onwards, unlimited):** each level is built from its number with a fixed seed, so it
  never changes between plays or devices. Every third level from 15 adds a fifth colour, all levels from 31
  use five, and from level 81 every fourth level uses six. Stone patterns (pillars, centre block, shelves, walls, steps, posts, scatter) appear from
  level 8 and grow over time; shaped boards with holes appear from level 25. Every fifth level is an open
  "breather" board. Some levels have a dominant or a scarce colour.
* **Targets are calibrated, not guessed (levels 1–20):** when a level is first needed, a reference bot (always
  takes the biggest group) plays it 16 times. The target is a percentage of its median score. These levels are
  unchanged by the campaign mechanics (a test checks them against the pre-mechanics build).
* **From level 21, every level is sized from simulated players** (details in 3.1).
* **Win and stars:** reaching the target **wins immediately**. Each unused move is converted into bonus
  points (worth target ÷ moves), so finishing efficiently earns more. Stars use the final score:
  ★ target, ★★ 125% of target, ★★★ 150% of target.
* **Losing:** running out of moves shows "Out of Moves" with Try Again. If Extra Moves is unlocked, the
  player may choose to use one they own, or explicitly buy one for 200 coins. Nothing is ever spent automatically.
* **Regions:** 20 levels each, cycling through six themes (Sunrise Meadow, Coral Lagoon, Crystal Caverns,
  Ember Canyon, Frost Peaks, Starlight Nebula). After six regions the names continue as "Sunrise Meadow II" etc.
* **Milestones:** every 10th level is a gem-shaped node with a coin bonus; every 20th finishes a region.

### 3.1 Campaign mechanics (from level 21)

The rules of each mechanic are in docs/MECHANICS.md. This section is about where they appear and how levels are
sized.

* **Introductions, one at a time:** ice 21, collect-a-colour goals 25, thick ice 28, rockets 31, bombs 41,
  special combos 47, prisms 51, chains 61, treasure 71, moss 101 (config: campaign → mechanics). Each has a
  hand-made level with a tutorial card and a pointer, then three practice levels focused on it, then it mixes with
  the others. Special tiles are earned from their introduction on: rockets from 31, bombs from 41, prisms from 51.
* **Bands:** Learn 1–20, Develop 21–60, Combine 61–120, Advanced 121–200, Expert 201+. Each band has a move range
  (Develop 18–30, Combine 22–35, Advanced 26–40, Expert 30–45; treasure levels may go 5 over) and a share of
  spare room (30%, 25%, 20%, 13%).
* **Ten-level rhythm:** levels ending 1–3 are generous (+10% moves), 4–5 medium, 6 a breather (+15%, or a plain
  score level), 7–9 harder (−5%), 0 a milestone, often combining two mechanics.
* **Recipes:** a generated level picks one mechanic (sometimes two, more often later on, three goals at most)
  from those taught so far, with amounts by band (fewer of each when combined). Obstacles are placed with mirror
  symmetry away from the top row; treasure only in open columns; moss away from stones and edges. A level whose
  board would need more moves than its band allows is scaled down (70%, then 50%) and tried again.
* **Moves** = what a casual player needs (median of 10 Casual-bot games with plenty of moves) × (1 + the band's
  spare room + the rhythm), within the band's range. Built-in introductions keep their hand-set moves unless
  calibration says a casual player needs more.
* **Stars** on goal levels: completing every goal is one star; two and three stars are the 35th and 75th
  percentiles of the Planner bot's final scores (spare-move bonus included), so a thoughtful player usually gets
  two and sometimes three. Score levels from 21 on take their target from Casual-bot scores at a percentile set
  by band and rhythm.
* **Same on every device:** generation and calibration use fixed seeds and integer arithmetic only. A level
  takes 2–36 ms to generate (mean 6.5 ms in the .NET harness).

**Simulation results** (docs/campaign-simulation.csv; 24 Casual and 24 Planner games per level on fresh
boards; "Casual" picks groups at random weighted by size, roughly a relaxed beginner; "Planner" is goal-aware
with one move of lookahead):

| Band | Levels | Casual win rate | Planner win rate | Average moves |
|---|---|---|---|---|
| Learn | 1–20 | 85% | 100% | 17.5 |
| Develop | 21–60 | 81% | 98% | 24.0 |
| Combine | 61–120 | 67% | 94% | 28.4 |
| Advanced | 121–200 | 60% | 90% | 34.5 |

| Main mechanic | Levels | Casual | Planner |
|---|---|---|---|
| Collect a colour | 24 | 86% | 99% |
| Score | 50 | 72% | 99% |
| Ice | 40 | 71% | 94% |
| Moss | 21 | 68% | 97% |
| Chains | 39 | 63% | 89% |
| Treasure | 26 | 52% | 86% |

**Flagged levels (30 of 200):** the report flags a level as *too hard* when the Casual win rate is below the
band's floor (Learn 75%, Develop 55%, Combine 40%, Advanced 30%, introductions 70%), *too easy* above its ceiling,
and *unfair?* when even the Planner wins under 80%. Most flags are late treasure and chain levels, where finishing
needs a colour brought next to one particular tile (a person would use power-ups or plan several moves ahead,
which neither bot does). Score levels 19, 38, 127 and 172 are hard for the Casual bot but won by the Planner
96–100% of the time. The full list is in the CSV; tune a level by adding an override in the config.

Changes made because of the simulation: specials at 6/9/12 tiles instead of 7/10/14; moss falls with its tile,
is scraped off by a neighbouring clear and never grows two moves in a row; treasure only in open interior
columns; fewer chains, and four colours on chain levels until 121; score levels from 21 calibrated against the
Casual bot; gentler introductions for treasure, chains, prisms and colour goals.

## 4. Endless

* A **fresh random board every run** (8 × 9). No timer.
* Start with **30 moves**. Each group costs one move; groups of 6+ give +1 move, 9+ give +2 and 13+ give +3.
* **Stages** at 2,000, 5,000, 10,000, 17,000, 26,000 and 38,000 points (then every 15,000). A fifth colour
  arrives at stage 3 and a sixth at stage 6.
* The run ends when moves run out. Bests are kept for score, biggest group, moves and stage.
* Coins: one-time milestone rewards the first time any run reaches 2,500 / 5,000 / 10,000 / 20,000 / 40,000
  points (20/30/50/75/100). Endless never grants campaign rewards and never unlocks levels.
* Typical runs last about 43–45 moves (3–5 minutes).

## 5. Daily Challenge

* One puzzle per **UTC calendar day**, built from the date (`daily:yyyy-MM-dd`) with a deterministic
  generator, so every player and every reopen on that date gets the same board **and** the same refills for
  the same moves.
* **20 moves, no timer, every move is played** (it is a score attack, unlike the campaign). Completing means
  reaching the target. Stars at the target, ★★ and ★★★ thresholds calibrated against a reference bot.
* First completion each day pays **30 coins**; three stars pays **+10** once. Replays are unlimited and update
  the best score but never pay twice.
* No power-ups and no shop during a run, so the challenge is identical for everyone.
* **Limitation:** the date comes from the device clock. A player who changes their clock can view other days'
  puzzles. Mitigation: rewards are refused for any date earlier than the latest one the player has opened.
  Moving the clock forward is not detected. All time reads go through `IClock`, so a server time source can
  replace it later without touching the rules.

## 6. Weekly Rush (the only timed mode)

* A **60-second** score attack on the week's board. Weeks follow **ISO-8601 in UTC** (Monday 00:00 UTC to the
  next Monday; key `yyyy-Www`, e.g. `2026-W43`).
* A 3-2-1-GO countdown, then the timer runs. Unlimited moves; play stops instantly at zero.
* **Pausing** (button, Android Back or leaving the app) stops the timer and **hides the board**, so pausing
  can't be used to plan. Animations run about 35% faster than elsewhere so the clock feels fair.
* Medal tiers: **Bronze 2,500 (+20), Silver 5,000 (+30), Gold 8,000 (+50)**, each paid once per week.
  Bots score about 5,000–6,200 in 40 moves and 7,900–9,700 in 60, so Gold rewards fast, good play.
* No power-ups, no shop during the run.

## 7. Power-ups

Bought with coins in the Shop (or from the in-game bar) and stored in an inventory. Using one never costs a
move. A use is only consumed if the effect actually happens; an invalid target costs nothing.

| Power-up | Price | Unlocks | Free trial | How it works |
|---|---|---|---|---|
| Blast | 100 | Level 10 | 2 | Tap any tile: every colour tile in the 3 × 3 area is cleared (clipped at the edges). Stones and holes are not affected. Must clear at least one tile. |
| Tile Breaker | 125 | Level 20 | 2 | Tap one tile **or one stone** to break it. The only way to remove a stone. Holes can't be broken. |
| Swap | 75 | Level 30 | 2 | Tap a tile, then a neighbouring tile of a different colour; they swap places. Nothing is cleared, so the board stays valid. |
| Colour Shift | 150 | Level 40 | 2 | Tap a tile, then pick any other colour in play from the palette. |
| Extra Moves | 200 | Level 50 | 1 | +3 moves. Campaign only (it would bypass Endless's move economy and challenges' fairness). Also offered, as an explicit choice, on the Out of Moves screen. |
| Colour Burst | 250 | Level 60 | 1 | Tap a tile: every tile of that colour is removed. |

* **Introduction:** the first time the player starts each unlock level, a "New Power-up!" card explains it,
  grants the free uses (stored as separate trial charges, spent before bought ones, granted exactly once)
  and points at its button.
* **Where they work:** Campaign and Endless only (Extra Moves: Campaign only). Daily Challenge and Weekly
  Rush block every power-up and every purchase. This is decided in one place (`ActionPolicy`) and enforced
  by the engine and services as well as the UI.
* **Cancelling:** tap the power-up again, press Cancel or use Back. Nothing is consumed until it applies.

## 8. Coins and economy

Coins are earned only by playing, and every grant has a one-time **claim key** stored in the save, so
repeated taps, reloads and replays can never pay twice.

| Source | Coins | Key |
|---|---|---|
| First clear of a campaign level | 5 (levels 1–10), +1 every 10 levels, max 15 | `c.first.N` |
| Each star the first time it's earned | 1 | `c.star.N.S` |
| Milestone (every 10th level) / region finale (every 20th) | 20 / 40 | `c.mile.N` |
| Daily completion / three stars | 15 / +5, once per date | `d.DATE.done` / `d.DATE.3star` |
| Weekly Rush tiers | 10 / 15 / 25, once per week | `w.WEEK.tN` |
| Endless milestones | 10 / 15 / 25 / 40 / 60, once ever | `e.mile.SCORE` |

**Retuned after the first playtest (reward tuning version 2).** With the original values (first clear 10–30,
2 per star, milestones 50/100, Daily 30/+10, Rush 20/30/50, Endless 20–100) the economy simulation
(`dotnet run --project Sim -c Release -- economy`) showed an active player (5 new levels a day plus the Daily,
a weekly Rush and some Endless) earning about **230 coins a day**, so every power-up cost less than a day of play
and the playtester bought three straight away. The prices stayed as specified; income was cut instead:

| Measured (30 simulated days, 5 levels a day) | Version 1 | Version 2 |
|---|---|---|
| Average income | 231 coins/day | 100 coins/day (60–70 a day after a first-day welcome of about 130) |
| Campaign coins by level 10 / 30 / 60 | 218 / 786 / 1,942 | 97 / 329 / 756 |
| Days of play per Blast (100) / Colour Burst (250) | 0.4 / 1.1 | 1.0 / 2.5 |

Levels 1–10 with two stars each now earn just under one Blast; the two free trial uses cover its introduction.
Existing config assets are moved to the new values automatically when the project opens (`EconomyTuning`);
values someone edited by hand are kept. Everything stays tunable in the config asset.

Every balance change goes through `Wallet`, the single source of truth, and is written to a 200-entry ledger
(time, amount, resulting balance, reason) for diagnosing economy bugs. Developer builds can show it in Settings.

## 9. Shop

* Reached from the home screen (Shop button and the **+** on the coin counter) and from the in-game bar.
* **Power-ups tab:** icon, name, description, owned/free count, price. Locked items show their unlock level;
  unaffordable ones explain the shortfall ("You need 35 more coins").
* **Coins tab:** balance plus every way to earn coins, with shortcuts to play. It states plainly that coin
  packs are not sold in this version: no fake or non-functional purchase buttons.
* Tabs are data (`ShopTab` subclasses), so cosmetics or real coin packs can be added later as another tab.
  Real-money purchases, if added, should use Google Play Billing (Unity IAP) with receipt validation.

## 10. Accessibility and settings

* **Symbols on tiles** (the "Symbols button", the eye icon in the game's top bar, also in Settings and Pause):
  every colour gets its own shape: red ♥ heart, yellow ★ star, blue ◎ ring, green ▲ triangle, purple ☾ moon,
  orange ✚ cross. White glyphs with a dark rim stay legible on every tile colour. The shapes were chosen by
  measurement: a test requires every pair to differ by more than 30% of their combined area (Jaccard distance),
  which rejected a circle (too close to the heart) and a square/diamond. The setting is saved, changes the
  board instantly and is shown in the Settings preview.
* Sound effects and music volume, vibration (Android, amplitude-controlled), **Reduced motion** (fewer
  particles, no screen shake, no overshoot, shorter animations, no pulsing).
* Portrait safe-area support, touch targets of at least 96 reference pixels, minimum body text about 27 px.

## 11. Onboarding and How to Play

Teaching is spread across the first levels (Section 3) with hints that never block input and appear once.
**How to Play** (home screen) has six short pages: matching (a real, playable practice board), scoring table,
gravity and stones (a playable board with stones), levels and stars, coins and power-ups, and game modes.
Tutorial hints can be shown again from Settings.

## 12. Screens and navigation

```
Home ─┬─ Play Level N ───────────────▶ Game ──▶ Result ──▶ Next Level / Map / Replay
      ├─ Map ─▶ Level card ─▶ Play ──▶ Game
      ├─ Endless / Daily / Weekly cards ─▶ Mode card ─▶ Play ─▶ Game ─▶ Result ─▶ Play Again / Home
      ├─ Shop (Power-ups | Coins)
      ├─ How to Play
      └─ Settings (gear)
```

* Back always returns to the previous screen; Home always returns home; showing a screen that's already in
  the history returns to it rather than stacking duplicates (`NavStack`, unit tested).
* Leaving a game replaces it in the history, so Back from the map after a level goes home, not into a
  finished game.
* **Android Back:** closes the top popup → pauses a game → goes back → on Home asks "Leave Colour Cascade?".

## 13. Decisions made where the brief left room

| Topic | Decision | Why |
|---|---|---|
| Original project | The repository was empty, so nothing could be inspected or reused. The game was built from scratch to the brief. | |
| Symbols button | Implemented as the colour-shape accessibility toggle the brief describes, wired end to end. | No original code existed to infer anything more specific. |
| Campaign end | Win as soon as the target is reached; unused moves become bonus points. | Avoids pointless leftover moves and rewards efficiency. |
| Daily format | Plays all 20 moves (score attack). | Distinguishes it from the campaign and suits leaderboards later. |
| Challenge resets | UTC, ISO weeks. | Everyone shares one puzzle at once; ready for server validation. |
| Power-up order | Blast → Tile Breaker → Swap → Colour Shift → Extra Moves → Colour Burst. | Simple to complex; Tile Breaker arrives once stones are common. |
| Extra Moves scope | Campaign only. | Brief: "current eligible campaign level". |
| Coin packs | None; Coins tab explains how to earn. | Brief forbids fake purchase buttons. |
| Mid-level saving | Runs are not saved mid-level; finished results save immediately. | Simple and exploit-free. A level interrupted by the OS is replayed. |
| Art and audio | Procedurally generated in code. | No asset pipeline to break; resolution independent; previewable outside Unity. |
