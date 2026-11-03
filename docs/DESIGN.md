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
* **Passing and stars:** reaching the target (or meeting every goal) **passes** the level and earns ★, but play
  goes on: the moves left raise the score towards ★★ and ★★★, and the stars light up on the game screen as they
  are earned. The player can tap **Finish** at any time after passing; each unused move then adds a small bonus.
  Out of moves after passing = won with the stars of the final score; out of moves before = failed, whatever the
  score (docs/MECHANICS.md §4.3). Levels 1–20: ★★ 125% and ★★★ 150% of the target, finishing early pays target ÷
  moves per unused move. From 21: see 3.2.
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
  Introductions and power-up introductions show their lesson on **every** attempt, replays included.
* **Bands:** Learn 1–20, Develop 21–60, Combine 61–120, Advanced 121–200, Expert 201+. Each band has a move range
  (Develop 16–30, Combine 20–35, Advanced 24–40, Expert 28–45; treasure levels may go 5 over) and a share of
  spare room (20%, 18%, 15%, 11%; lowered from 30/25/20/13% after a player found the 40s easy).
* **Ten-level rhythm:** levels ending 1–3 are generous (+10% moves), 4–5 medium, 6 a breather (+15%, or a plain
  score level), 7–9 harder (−5%), 0 a milestone, often combining two mechanics.
* **Recipes:** a generated level picks one mechanic from those taught so far, and often a second ("free 3 frozen
  tiles and collect 10 blue"): about 45% of levels 29–30, 55% of 31–40, 65% to 120, 70% to 200, nearly every
  milestone; from level 51 some levels have three goals. Partners include special-tile goals from 31 ("make 2
  rockets", "set off 3 specials", "fire a combination") and a score to reach from 35 (MECHANICS.md §4.2). The
  practice levels after each special-tile introduction ask the player to make or set off that special next to a
  goal they know (bombs, prisms and combinations on four-colour boards; some start with one on the board). Amounts
  are set by band (fewer of each when combined). Obstacles are placed with mirror symmetry away from the top row;
  treasure only in open columns; moss in separate patches away from stones and edges. A level whose board would
  need more moves than its band allows is scaled down (80%, 65%, then 50%, never below 4 ice, 3 chains, 3 moss or
  10 tiles to collect) and tried again. Goal levels use at most five colours (four with chains, bombs, prisms or
  combinations).
* **Moves** = what a casual player needs (median of 10 Casual-bot games with plenty of moves) × (1 + the band's
  spare room + the rhythm), within the band's range. Levels with a special-tile goal are sized from the Planner
  bot instead (its 75th-percentile game, +30%), since a casual player never aims for big groups. Built-in
  introductions keep their hand-set moves unless calibration says more are needed.
* **Stars:** see 3.2. Score levels from 21 on take their target from Casual-bot scores at a percentile set by band
  and rhythm.
* **Same on every device:** generation and calibration use fixed seeds and integer arithmetic only (generation
  time: 3.2).

**Simulation results** (docs/campaign-simulation.csv; 12 Casual and 12 Planner games per level on fresh
boards, every move played; "Casual" picks groups at random weighted by size, roughly a relaxed beginner who never
aims for specials; "Planner" is goal-aware with one move of lookahead):

| Band | Levels | Casual pass | Planner pass | Planner ★★ / ★★★ | Casual ★★ / ★★★ | Average moves |
|---|---|---|---|---|---|---|
| Learn | 1–20 | 83% | 100% | 100% / 99% | 68% / 52% | 17.5 |
| Develop | 21–60 | 62% | 94% | 79% / 41% | 16% / 3% | 23.6 |
| Combine | 61–120 | 60% | 94% | 80% / 44% | 14% / 5% | 28.6 |
| Advanced | 121–200 | 56% | 92% | 78% / 40% | 13% / 2% | 32.1 |

Before this update (goals ended the level, stars came from early finishes) the pass rates were Develop 76% / 96%,
Combine 66% / 95%, Advanced 57% / 89%: levels 21–60 are now harder for a casual player (more combined goals,
special-tile goals) while a thoughtful player passes about as often.

Levels 21–200: 61 with one goal, 81 with two, 10 with three, 28 score levels; 32 have a special-tile goal and 11 a
score to reach as well as another goal.

| Main mechanic | Levels | Casual | Planner |
|---|---|---|---|
| Score | 28 | 68% | 98% |
| Moss | 20 | 60% | 97% |
| Collect a colour | 14 | 61% | 96% |
| Chains | 28 | 60% | 94% |
| Ice | 59 | 55% | 90% |
| Treasure | 30 | 54% | 89% |

**Flagged levels (57 of 200):** the report flags a level as *too hard* when the Casual pass rate is below the
band's floor (Learn 75%, Develop 55%, Combine 40%, Advanced 30%, introductions 70%), *too easy* above its ceiling,
*unfair?* when even the Planner passes under 80%, *goals trivial* when the Planner meets them by move 2, and
*3 stars rare* when it never gets three. "Too hard" is expected on special-tile levels (the Casual bot never builds
specials). 22 levels are *unfair?*, half of them at exactly 9 of 12 Planner games; the clearly hard ones are
77, 79, 112, 137, 145, 150, 151, 160 and 190 (chains with ice, combinations with ice or chains, treasure),
where the Planner passes 50–67%. Level 128's moss goal is trivial (its board only fits two patches). The full list
is in the CSV; tune a level by adding an override in the config.

Changes made because of the simulation: specials at 6/9/12 tiles instead of 7/10/14; moss falls with its tile,
is scraped off by a neighbouring clear and never grows two moves in a row; treasure only in open interior
columns; fewer chains, and four colours on chain levels until 121; score levels from 21 calibrated against the
Casual bot; gentler introductions for treasure, chains, prisms and colour goals.

### 3.2 Stars and difficulty (play on after passing)

* **What stars measure:** ★ passing (every goal, the target included); ★★ and ★★★ the final score, with play going
  on after the pass. A player who passes in one move with 17 to spare keeps playing those 17 moves for stars. (This
  is what went wrong on level 47: the level used to end at the pass, 1,160 points + 17 × 70 spare-move bonus =
  2,350, under its two-star 2,500, so one star.)
* **Calibration (every level from 21):** bots play the level to its last move, as a player chasing stars would.
  ★★ = halfway between a casual player's median final score (Casual bot, 16 games) and the 35th percentile of a
  thoughtful player's (Planner bot, 8 games); ★★★ = the Planner's 60th percentile, never more than 1.45 × ★★.
  Medians, not extremes, so one lucky calibration game can't put three stars out of reach. Goal levels show a
  target of ¾ of ★★; levels with a score goal show that score.
* **Spare-move bonus:** finishing early pays each unused move half of what a thoughtful player's average move
  scores (levels 1–20: target ÷ moves), so it rewards stopping once three stars are safe but never beats playing.
* **"...and reach a score":** the 20th percentile of the final scores of Casual games that meet the other goals.
* **No one-move levels:** the combinations introduction (47) asks for a combination and 14 frozen tiles, 4 of them
  out of the starting rockets' reach; thick-ice practice always has thick ice; moss comes in separate patches;
  scaled-down goals keep a working minimum. A test checks that the Planner needs more than two moves to pass every
  level 21–60, and that its best of a few games reaches ★★★ on most of them and gets within 80% on all.
* **Level generation time** rose from about 6 ms to about 17 ms on average (max about 100 ms) in the .NET harness,
  because calibration now plays whole games. Levels are built when first needed (level card, game start) and then
  cached; on a phone this may show as a short pause the first time a level card opens. Not measured on a device.

## 4. Endless

* A **fresh random board every run** (8 × 9). No timer.
* Start with **30 moves**. Each group costs one move; groups of 6+ give +1 move, 9+ give +2 and 13+ give +3.
* **Stages** at 2,000, 5,000, 10,000, 17,000, 26,000 and 38,000 points (then every 15,000). A fifth colour
  arrives at stage 3 and a sixth at stage 6.
* The run ends when moves run out. Bests are kept for score, biggest group, moves and stage.
* Coins: one-time milestone rewards the first time any run reaches 2,500 / 5,000 / 10,000 / 20,000 / 40,000
  points (20/30/50/75/100). Endless never grants campaign rewards and never unlocks levels.
* Typical runs last about 43–45 moves (3–5 minutes).
* **Tile mechanics (Endless level):** the player's Endless level starts at 1 and goes up by one for every stage
  reached in any run (saved). Mechanics unlock at Endless levels 5 (frozen tiles), 10 (rockets), 12 (thick ice),
  15 (bombs), 20 (chains), 25 (prisms), 30 (treasure) and 40 (moss), set in the config. A run starts with the
  original tiles; each new stage adds a wave of unlocked mechanics: one kind on stages 2–3, two on 4–5, three from 6.
  Treasure brought down gives 3 bonus moves. The mode card shows the Endless level and the next unlock; the run
  result shows levels gained and anything newly unlocked.
* Simulation (Planner bot, 20 runs per level): median scores stay around 17,500–21,000 and median stage 5 at every
  Endless level, with about 20–25 obstacles and treasures added per run once mechanics are unlocked: complexity
  goes up without the targets moving.

## 5. Daily Challenge

* One puzzle per **UTC calendar day**, built from the date (`daily:yyyy-MM-dd`) with a deterministic
  generator, so every player and every reopen on that date gets the same board **and** the same refills for
  the same moves.
* **20 moves, no timer, every move is played** (it is a score attack, unlike the campaign). Completing means
  reaching the target. Stars at the target, ★★ and ★★★ thresholds calibrated against a reference bot.
* First completion each day pays **30 coins**; three stars pays **+10** once. Replays are unlimited and update
  the best score but never pay twice.
* No power-ups and no shop during a run, so the challenge is identical for everyone.
* **Tile mechanics:** each date has a plan of one mechanic, or a pair known to play fairly (chains only alone). A
  player gets the parts of it they have completed in the campaign, so everyone with the same progress plays the same
  puzzle, and a new player plays the original daily. Obstacles add goals (shown in the goals panel, with the target
  as the first goal); the day is complete when the target and every goal are met. Days with goals have a target 12%
  lower. Simulation (60 days): Casual-bot success 47% on plain days, 43–54% with mechanics; Planner 88–100%.
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
* **Tile mechanics:** each week has a theme (frozen, rocket, chain, bomb, treasure, prism or moss week) and up to two
  more of the player's campaign-unlocked mechanics, so weekly boards combine more than daily ones. Special-tile weeks
  start with a few of that special on the board. The medal scores are scaled by how much the mechanics change a
  45-move bot score, and rewards use the medal scores of the board played. Simulation (16 weeks): with everything
  unlocked the medal scores were 1.19× the plain ones on average, and the bot earned bronze, silver and gold at the
  same rates as on the plain boards (100% / 98% / 43% against 100% / 98% / 42%).

## 7. Power-ups

Bought with coins in the Shop (or from the in-game bar) and stored in an inventory. Using one never costs a
move. A use is only consumed if the effect actually happens; an invalid target costs nothing.

| Power-up | Price | Unlocks | Free uses on its level | How it works |
|---|---|---|---|---|
| Blast | 100 | Level 10 | 2 | Tap any tile: every colour tile in the 3 × 3 area is cleared (clipped at the edges). Stones and holes are not affected. Must clear at least one tile. |
| Tile Breaker | 125 | Level 20 | 2 | Tap one tile **or one stone** to break it. The only way to remove a stone. Holes can't be broken. |
| Swap | 75 | Level 30 | 2 | Tap a tile, then a neighbouring tile of a different colour; they swap places. Nothing is cleared, so the board stays valid. |
| Colour Shift | 150 | Level 40 | 2 | Tap a tile, then pick any other colour in play from the palette. |
| Extra Moves | 200 | Level 50 | 1 | +3 moves. Campaign only (it would bypass Endless's move economy and challenges' fairness). Also offered, as an explicit choice, on the Out of Moves screen. |
| Colour Burst | 250 | Level 60 | 1 | Tap a tile: every tile of that colour is removed. |

* **Introduction:** every attempt at a power-up's unlock level (first play, retry or replay, also after the
  game is closed and reopened) shows its card ("New Power-up!", or "Power-up practice" on a replay), points at its
  button and **lends** its free uses for that attempt only: they are spent before the player's own, are never
  saved or added to the inventory, and any left over are taken back when the run ends, so replaying can't collect
  power-ups. Free trial uses granted by older versions stay in the inventory. Future power-ups with an unlock
  level get the same treatment automatically.
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
