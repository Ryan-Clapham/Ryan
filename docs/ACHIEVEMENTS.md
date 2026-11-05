# Colour Cascade: Achievements

A fixed collection of **67 achievements** in seven categories. Each one has a difficulty tier and a reward
(coins, power-ups or both) that the player claims by hand. The list is data: the defaults live in
`AchievementCatalog.CreateDefaults()` and in the config asset, where every target and reward can be edited.
Nothing is generated, so the collection always has an end.

## 1. What the player sees

* **Home:** a trophy button next to the settings gear. A red badge shows how many rewards are waiting.
  **Settings > Your progress** also has an *Achievements N / 67* row.
* **Achievements screen:** a summary card at the top shows "*N / 67 completed*", the percentage, a progress
  bar and **Claim all** (when rewards are waiting). Below it are the seven categories, each with a header
  showing how many are done (for example *3 / 9*). Each card shows:
  * a round badge in the tier colour with the category's icon (dimmed while locked);
  * the name, the requirement, and a tier label: **Easy** (green), **Medium** (teal), **Hard** (gold) or
    **Very hard** (pink);
  * the reward ("*125 coins + Tile Breaker*", "*1,500 coins + two of each power-up*");
  * its state:
    * **Locked:** a progress bar and "*37 / 50*" (or "*Level 37 / 50*", "*Stage 4 / 6*", "*Best: Silver*");
    * **Unlocked:** a gold outline and a **Claim** button;
    * **Claimed:** a tick and "*Claimed: …*".
* **When something unlocks:**
  * The result popup of the run that unlocked it has a gold line: "*Achievement unlocked: Century*" (or
    "*3 achievements unlocked*").
  * Back on the menus, a banner slides in from the top with the name, the tier and the reward, plus a
    **Claim** button. Tapping the banner opens the achievements screen.
  * Banners come one at a time. Three or more waiting at once become one summary banner with a **View**
    button. Banners never appear during a game or over a popup, and they wait for a toast to finish.
* **Claiming:** coins go into the wallet (the ledger line reads "*Achievement reward*"), and power-ups go into
  the inventory as owned uses. It happens once: the claim is recorded with the same one-time claim keys as
  every other reward, and the save is written at once.

## 2. How it works

* **One source of truth.** Achievements keep no second copy of progress. Each one names a *stat* that is
  read from what the game already records:

| Stat | Read from |
|---|---|
| Level reached | Highest unlocked campaign level ("reach level 10" = level 10 is open, by clearing level 9) |
| Stars | Sum of each level's best stars, capped at 3 (so replays never count twice) |
| Perfect regions | Regions (20 levels each, `CampaignConfig.levelsPerRegion`) where every level has 3 stars, the maximum (`AchievementService.MaxStarsPerLevel`, the same cap `CampaignProgress.RecordWin` applies) |
| Endless best score / stage | The Endless record, which only a finished run updates |
| Biggest group, best level score, tiles cleared | Existing lifetime statistics and level records |
| Stones, ice layers, chains, moss, treasure, tiles in one tap, combination size, uses per power-up, pure three-star levels | **New** lifetime statistics, counted by `PuzzleSession` for every action and added to the save when a run ends or is left |
| Daily Challenges completed | Distinct days with a Daily completion reward claimed (kept for ever), plus the day records |
| Weekly Rush medal | Best medal reward claimed (bronze, silver, gold) |

* **When achievements unlock.** `AchievementService.Evaluate()` checks every locked achievement:
  * when the save loads, so existing players get what their save already proves;
  * at the end of every run (`GameContext.FinishRun`, listed in `RunResult.NewAchievements`);
  * when a run is left (`AbandonRun`, since board work done before leaving counts);
  * after the developer tools change progress on the test save.

  Each unlock is recorded with its time (`SaveData.Achievements.Unlocked`) and never repeats.
* **Claiming.** `AchievementService.Claim(id)` refuses unknown, locked and already-claimed achievements.
  Otherwise it adds the claim key `ach.<id>`, credits the coins, adds the power-ups (skipping any power-up the
  game no longer has), and saves. `ClaimAll()` claims every waiting reward.
* **Abandoned runs** add their board work (stones, ice, tiles and so on) to the lifetime totals, but do not
  count as a game played and never touch records that need a finished run (Endless best, level scores, stars).
  Each action is counted once: continuing after Out of Moves and leaving later adds only what happened since.

## 3. The 67 achievements

Value = coins + the power-ups at their shop prices (Blast 100, Tile Breaker 125, Swap 75, Colour Shift 150,
Colour Burst 250). "One of each power-up" is one Blast, Tile Breaker, Swap, Colour Shift and Colour Burst (700
coins of power-ups).

### Campaign (9)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `level_10` | First Steps | Reach level 10. | Easy | 25 coins | 25 |
| `level_25` | On Your Way | Reach level 25. | Easy | 50 coins | 50 |
| `level_50` | Halfway to a Hundred | Reach level 50. | Medium | 75 coins + Blast | 175 |
| `level_100` | Century | Reach level 100. | Medium | 125 coins + Tile Breaker | 250 |
| `level_150` | Seasoned Cascader | Reach level 150. | Hard | 250 coins + Colour Shift + Blast | 500 |
| `level_200` | Double Century | Reach level 200. | Hard | 400 coins + Colour Burst + Tile Breaker | 775 |
| `level_300` | Trailblazer | Reach level 300. | Very hard | 600 coins + one of each power-up | 1,300 |
| `level_400` | Unstoppable | Reach level 400. | Very hard | 900 coins + Colour Burst ×2 + Colour Shift ×2 | 1,700 |
| `level_500` | Cascade Legend | Reach level 500. | Very hard | 1,500 coins + two of each power-up | 2,900 |

### Stars (7)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `stars_30` | Starry Start | Collect 30 stars in the campaign. | Easy | 30 coins | 30 |
| `stars_100` | Star Collector | Collect 100 stars in the campaign. | Medium | 60 coins + Swap | 135 |
| `stars_250` | Shining Bright | Collect 250 stars in the campaign. | Medium | 150 coins + Blast | 250 |
| `stars_500` | Constellation | Collect 500 stars in the campaign. | Hard | 300 coins + Colour Shift + Tile Breaker | 575 |
| `stars_750` | Galaxy | Collect 750 stars in the campaign. | Hard | 450 coins + Colour Burst + Blast | 800 |
| `stars_1000` | A Thousand Stars | Collect 1,000 stars in the campaign. | Very hard | 800 coins + one of each power-up | 1,500 |
| `stars_1250` | Supernova | Collect 1,250 stars in the campaign. | Very hard | 1,200 coins + one of each power-up + Colour Burst | 2,150 |

### Perfect regions (5)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `regions_1` | Perfectionist | Earn three stars on every level of a region. | Medium | 100 coins + Swap | 175 |
| `regions_3` | Triple Perfect | Perfect 3 regions: three stars on every level. | Hard | 300 coins + Colour Shift + Swap | 525 |
| `regions_5` | Flawless Five | Perfect 5 regions: three stars on every level. | Hard | 500 coins + Colour Burst + Tile Breaker | 875 |
| `regions_10` | Perfect Ten | Perfect 10 regions: three stars on every level. | Very hard | 1,000 coins + one of each power-up | 1,700 |
| `regions_15` | Master of the Map | Perfect 15 regions: three stars on every level. | Very hard | 2,000 coins + two of each power-up | 3,400 |

### Endless (7)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `endless_5000` | Endless Explorer | Score 5,000 in one Endless run. | Easy | 40 coins | 40 |
| `endless_10000` | Going the Distance | Score 10,000 in one Endless run. | Medium | 75 coins + Blast | 175 |
| `endless_20000` | Long Haul | Score 20,000 in one Endless run. | Hard | 250 coins + Colour Shift | 400 |
| `endless_35000` | Marathon | Score 35,000 in one Endless run. | Very hard | 500 coins + Colour Burst + Tile Breaker | 875 |
| `endless_50000` | Infinite Cascade | Score 50,000 in one Endless run. | Very hard | 1,000 coins + one of each power-up | 1,700 |
| `endless_stage_6` | Rockets Away | Reach stage 6 of an Endless run. | Easy | Swap | 75 |
| `endless_stage_12` | Deep Run | Reach stage 12 of an Endless run. | Hard | 300 coins + Colour Burst | 550 |

### Skill (15)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `group_12` | Big Group | Clear a group of 12 tiles. | Easy | 30 coins | 30 |
| `group_18` | Huge Group | Clear a group of 18 tiles. | Medium | Swap + Colour Shift | 225 |
| `group_25` | Colossal Group | Clear a group of 25 tiles. | Very hard | 600 coins + Colour Shift ×2 + Swap ×2 | 1,050 |
| `move_30` | Clean Sweep | Clear 30 tiles with a single tap. | Medium | 75 coins + Blast | 175 |
| `move_45` | Avalanche | Clear 45 tiles with a single tap. | Hard | 250 coins + Colour Burst | 500 |
| `move_60` | Board Wipe | Clear 60 tiles with a single tap. | Very hard | 600 coins + Colour Burst ×2 | 1,100 |
| `combo_2` | Double Trouble | Fire two special tiles together. | Easy | 30 coins | 30 |
| `combo_3` | Triple Threat | Fire three special tiles together in one combination. | Medium | 75 coins + Tile Breaker | 200 |
| `combo_4` | Chain Reactor | Fire four special tiles together in one combination. | Hard | 300 coins + Blast ×2 | 500 |
| `combo_5` | Grand Combo | Fire five special tiles together in one combination. | Very hard | 700 coins + one of each power-up | 1,400 |
| `score_15000` | High Scorer | Finish a campaign level with 15,000 points. | Medium | 100 coins | 100 |
| `score_30000` | Score Master | Finish a campaign level with 30,000 points. | Hard | 300 coins + Colour Burst | 550 |
| `score_45000` | Score Legend | Finish a campaign level with 45,000 points. | Very hard | 700 coins + Colour Burst ×2 + Colour Shift | 1,350 |
| `pure_10` | Pure Skill | Three-star 10 different levels from level 50 on without using a power-up. | Medium | 150 coins | 150 |
| `pure_50` | Purist | Three-star 50 different levels from level 50 on without using a power-up. | Hard | 450 coins | 450 |

### Board mastery (18)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `stones_25` | Stone Breaker | Break 25 stones. | Easy | 30 coins | 30 |
| `stones_200` | Rock Crusher | Break 200 stones. | Medium | Tile Breaker ×2 | 250 |
| `stones_600` | Quarry Master | Break 600 stones. | Hard | 300 coins + Tile Breaker ×2 | 550 |
| `ice_50` | Thaw | Remove 50 layers of ice. | Easy | 30 coins | 30 |
| `ice_400` | Icebreaker | Remove 400 layers of ice. | Medium | 100 coins + Blast | 200 |
| `ice_1200` | The Big Melt | Remove 1,200 layers of ice. | Hard | 350 coins + Blast ×2 | 550 |
| `chains_25` | Unchained | Free 25 chained tiles. | Easy | 40 coins | 40 |
| `chains_150` | Lock Picker | Free 150 chained tiles. | Medium | 50 coins + Colour Shift | 200 |
| `chains_400` | Escape Artist | Free 400 chained tiles. | Hard | 350 coins + Colour Shift ×2 | 650 |
| `treasure_5` | Treasure Hunter | Collect 5 treasures. | Easy | 40 coins | 40 |
| `treasure_40` | Treasure Trove | Collect 40 treasures. | Medium | 100 coins + Tile Breaker | 225 |
| `treasure_100` | Hoarder | Collect 100 treasures. | Hard | 400 coins + Colour Burst | 650 |
| `moss_30` | Weeder | Clear 30 mossy tiles. | Easy | 40 coins | 40 |
| `moss_200` | Moss Buster | Clear 200 mossy tiles. | Medium | 150 coins + Blast | 250 |
| `tiles_10000` | Ten Thousand | Clear 10,000 tiles. | Easy | 50 coins | 50 |
| `tiles_100000` | Hundred Thousand | Clear 100,000 tiles. | Very hard | 800 coins + one of each power-up | 1,500 |
| `powerups_each_1` | Toolkit | Use each of the five power-ups at least once. | Medium | 100 coins | 100 |
| `powerups_each_10` | Power User | Use each of the five power-ups 10 times. | Hard | 300 coins + one of each power-up | 1,000 |

### Challenges (6)

| ID | Name | Requirement | Tier | Reward | Value |
|---|---|---|---|---|---:|
| `daily_1` | Daily Dabbler | Complete a Daily Challenge. | Easy | 30 coins | 30 |
| `daily_7` | Daily Habit | Complete 7 Daily Challenges. | Medium | 75 coins + Blast | 175 |
| `daily_30` | Daily Devotee | Complete 30 Daily Challenges. | Hard | 300 coins + Colour Burst | 550 |
| `daily_100` | Daily Legend | Complete 100 Daily Challenges. | Very hard | 1,000 coins + one of each power-up | 1,700 |
| `weekly_bronze` | Rush Hour | Earn a Bronze medal in the Weekly Rush. | Easy | 40 coins | 40 |
| `weekly_gold` | Golden Rush | Earn a Gold medal in the Weekly Rush. | Hard | 250 coins + Colour Shift | 400 |


**Totals:** 67 achievements; 22,265 coins and 126 power-ups (about 40,700 coins of value in all).

| Tier | Achievements | Value each | Total value |
|---|---|---|---|
| Easy | 15 | 25 – 75 | 580 |
| Medium | 18 | 100 – 250 | 3,410 |
| Hard | 19 | 400 – 1,000 | 11,350 |
| Very hard | 15 | 875 – 3,400 | 25,325 |

| Category | Achievements | Total value |
|---|---|---|
| Campaign | 9 | 7,675 |
| Stars | 7 | 5,440 |
| Perfect regions | 5 | 6,675 |
| Endless | 7 | 3,815 |
| Skill | 15 | 7,810 |
| Board mastery | 18 | 6,355 |
| Challenges | 6 | 2,895 |

## 4. How the thresholds were set

The bots played the real game. A Planner bot (goal-aware, plays well) and a Casual bot each played campaign
levels 1–500 twice per level (1,000 games each). Lifetime totals count one game per level, like a first clear.
Endless was played 200 times each by the Casual and Mixed bots and 120 times by the Planner. The measurement
program is a one-off (not in the repository); its numbers:

| Measure | Planner | Casual | Thresholds chosen |
|---|---|---|---|
| Biggest group per game | P50 9, P99 19, max 25; ≥ 12 in 13 % of games | ≥ 12 in 15 %, max 26 | 12 easy, 18 medium, 25 very hard |
| Tiles in one tap | P50 14, P99 47, max 69; ≥ 30 in 7 %, ≥ 60 in 0.4 % | ≥ 30 in 1 % | 30 medium, 45 hard, 60 very hard |
| Specials fired together | ≥ 2 in 31 %, ≥ 3 in 4.4 %, ≥ 4 in 0.6 %, ≥ 5 in 0.1 % | max 3 | 2 easy, 3 medium, 4 hard, 5 very hard |
| Final score on a level | ≥ 15,000 in 25 %, ≥ 30,000 in 1.9 %, max 46,095 | ≥ 15,000 in 3.3 % | 15,000 medium, 30,000 hard, 45,000 very hard |
| Stars by level 500 | 1,070 (2.14 a level) | 408 | 30 … 1,250 (1,500 is the most possible at level 500) |
| Three-star levels by level 500 | 235 | 32 | Regions 1, 3, 5, 10, 15 |
| Stones / ice layers / chains / moss / treasure by level 500 | 650 / 1,298 / 446 / 302 / 115 | 360 / 1,247 / 381 / 658 / 86 | See §3: easy within the first levels of each mechanic, hard near level 450–500 |
| Tiles cleared by level 500 | 96,896 | 66,683 | 10,000 easy (around level 75), 100,000 very hard |
| Endless best score | P50 17,010, P90 28,585, max 45,745 (stage 13) | P50 8,960, max 20,930 | 5k easy, 10k medium, 20k hard, 35k and 50k very hard |
| Endless stage | P50 7, max 13 | P50 5, max 8 | Stage 6 easy, stage 12 hard |

**Changes from the brief's suggestions:**

* **Endless:** the suggested top tier of 100,000 needs about stage 23. No bot run got past stage 13 or
  45,745 points, so 100,000 would be unreachable in practice. The Endless tiers are 5,000 / 10,000 /
  20,000 / 35,000 / **50,000** (the ceiling). Two stage achievements (6 and 12) were added for players who go
  far rather than score high.
* **Campaign:** 10, 25, 50, 100, 150, 200, 300, 400, 500, as suggested; level 500 is the last.
* **Perfect regions:** 1, 3, 5, 10, 15, as suggested (15 regions = levels 1–300 all at three stars, or any 15
  regions).
* **No power-up levels** count only from **level 50**. Earlier levels are three-starred without power-ups as
  a matter of course, and power-ups only unlock from level 10. Each level counts once, however often it's
  replayed. A tutorial's free power-up counts as a power-up used.

## 5. Rewards and the economy

* **No new currency:** only coins and the five existing power-ups.
* **Scaled by difficulty, and not by one multiplier:**
  * Easy pays 25–75 in coins.
  * Medium pays 100–250, usually coins plus one power-up.
  * Hard pays 400–1,000, with a strong power-up (often Colour Burst).
  * Very hard pays 875–3,400, often a full set of power-ups.
  * Within every series each step pays more than the last (tested). Level 500 (2,900) is worth over 100
    times level 10 (25).
* **Against income:** play earns roughly 60–100 coins a day; adverts pay 50 coins (up to 5 a day); coin packs
  are 500 to 6,000. Estimates from the bot data:
  * an engaged player who has reached about level 100 (around 20 days) could have unlocked about 20 achievements
    (mostly the first step of each series), worth about 1,900: roughly 1,150 coins and 750 in power-ups;
  * that is a sizeable one-time boost at the start, which shrinks as the remaining achievements get harder;
  * over half of the total value (25,325) sits in Very hard achievements that take months.
* **Not in the economy simulation:** `Sim -- economy` still models income without achievements, and the
  estimates above are arithmetic from the bot data. Watch real coin balances after release; every amount can
  be changed in the config asset.
* **Overlap:** the existing one-time **Endless milestone coins** (2,500 / 5,000 / 10,000 / 20,000 / 40,000 →
  10 / 15 / 25 / 40 / 60 coins) stay as they were. Reaching 5,000, 10,000 or 20,000 in Endless therefore pays
  the small milestone at once, and the achievement's reward when it is claimed. They are separate rewards for
  the same score; the milestones are small (150 coins in all).

## 6. Fair play

* **Replays never re-award:** an achievement unlocks once (recorded by ID) and pays once (claim key). Stars
  count each level's best only. Pure three-star levels count each level once.
* **Endless** counts only finished runs: the best score and stage are written only by `FinishRun`. A run that
  is left records no score. Finishing the same run twice is ignored (`FinishRun` is idempotent).
* **Daily** counts distinct days, so playing the same Daily again adds nothing. The days come from the
  completion rewards, which follow the existing date rules (a clock set back earns nothing). **Weekly**
  counts medals from claimed medal rewards.
* **Challenges stay fair:** achievements add nothing to Daily or Weekly boards. Power-ups stay barred there;
  power-ups won from achievements can only be used where they always could.
* **Developer tools** can't touch a player's save (§8).
* **Combination and one-tap records** come from real board actions only. Power-ups and the end-of-level
  finale are not taps, so they don't count towards "tiles in one tap" or combination size.

## 7. Existing players: what unlocks at once, and what starts from zero

When an existing save first loads with this update, `Evaluate` unlocks everything the save already proves. The
rewards wait to be claimed, and one banner says "*N achievements unlocked — earned by your progress so far*".

| Inferred from the save (unlock at once) | Not recorded before this update (start from zero) |
|---|---|
| Levels reached, stars, perfect regions | Stones broken, ice layers, chains freed, moss cleared, treasure collected |
| Endless best score and best stage | Most tiles in one tap, largest combination |
| Biggest group, best campaign level score | Uses of each power-up (Toolkit, Power User) |
| Tiles cleared (lifetime) | Pure three-star levels (no record of which attempts used power-ups) |
| Daily Challenges completed (from the completion rewards, kept for every day) | |
| Weekly Rush bronze and gold (from the medal rewards) | |

The right-hand column counts from the update onwards. Nothing in an old save can reconstruct it, so it
isn't guessed. A test loads a real pre-update save (the one in `SaveCompatibilityTests`) and checks exactly
which achievements unlock (level 10, Endless 5,000 and 10,000, group of 12, first Daily, bronze and gold
medals). It also checks that the balance is untouched until claiming and that a second load unlocks nothing
more.

**A progress reset** (Settings) erases achievements and their claims with the rest of the progress. They can
be earned again from the start, like the first-clear and milestone rewards.

## 8. Developer test save (no contamination of a player's save)

* In the Editor and development builds only, **Settings > Developer** has a **Use the test save** switch. It
  saves the current file, then loads `save-test.json` (beside the player's `save.json`) through
  `GameContext.UseStorage`. The choice is remembered for the next launch (PlayerPrefs) and **ignored in a
  release build**.
* The fast-track tools appear **only while the test save is on**: Add 500 coins, Unlock next 10 levels (which
  also checks achievements), Raise Endless level by 5, Advance clock one day. On the player's save only the
  read-only tools (coin ledger, monetisation status) are offered.
* Switching back to the player's save puts the clock offset back to zero, so a shifted date never reaches it.
  A red **TEST SAVE** label sits at the top of every screen while the test save is in use.
* A test (`ATestSaveNeverTouchesThePlayersSave`) switches to a test save, grants coins, unlocks level 500 and
  claims everything. It then checks that the player's save is byte-for-byte unchanged and loads back exactly
  as it was.

## 9. Adding or changing achievements

* **Change a target or a reward:** edit it in the config asset (`ColourCascadeConfig > Achievements`). The
  asset's list replaces the defaults when it isn't empty. A reward may list any power-ups with counts; a
  retired one is skipped when paid and isn't shown.
* **Add an achievement on an existing stat:** add an entry with a new, unique ID. Entries with no ID, a
  repeated ID or a target below 1 are skipped.
* **Add a new kind of measure:** add an `AchievementStat`, read it in `AchievementService.Current`, and record
  it in `PuzzleSession.Tally` / `GameContext.RecordStats` if it is new.
* **Never rename or reuse an ID** once released: unlocks and claims are stored by ID. A definition that is
  removed stays in old saves harmlessly and is ignored.

## 10. Known limitations

* **Unity and devices:**
  * Nothing here has been opened in Unity or run on a phone.
  * The screen, banners, Home badge and result-popup line compile against Unity's assemblies in all four
    variants and pass the lint (font glyphs included), but their layout, animation and feel are unverified.
  * The card list builds all 67 cards at once (Claim buttons only where needed); scrolling smoothness on a
    low-end phone is untested.
* **Rewards:**
  * Achievement rewards are not part of `Sim -- economy`; §5's figures are estimates.
  * The Endless milestones overlap three Endless achievements (§5).
* **Progress:**
  * The counters in §7's right-hand column start at zero for existing players.
  * Achievements are saved on the device with the rest of the progress; there is no cloud sync.
