using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>Converts SaveData to and from JSON, including version migration.</summary>
    public static class SaveCodec
    {
        public const string FormatTag = "colour-cascade-save";

        public static string Encode(SaveData d)
        {
            var root = new Dictionary<string, object>
            {
                ["format"] = FormatTag,
                ["version"] = SaveData.CurrentVersion,
                ["created"] = d.CreatedUtcTicks,
                ["saved"] = d.LastSavedUtcTicks,
                ["saveCount"] = d.SaveCount,
                ["coins"] = d.Coins,
                ["highestUnlocked"] = d.HighestUnlocked,
                ["latestDay"] = d.LatestDayKey ?? "",
            };

            var ledger = new List<object>(d.Ledger.Count);
            foreach (var e in d.Ledger)
                ledger.Add(new Dictionary<string, object> { ["t"] = e.Ticks, ["d"] = e.Delta, ["b"] = e.Balance, ["r"] = e.Reason ?? "" });
            root["ledger"] = ledger;

            var inv = new List<object>(d.Inventory.Count);
            foreach (var e in d.Inventory)
                inv.Add(new Dictionary<string, object> { ["id"] = e.Id, ["owned"] = e.Owned, ["trials"] = e.Trials });
            root["inventory"] = inv;

            var levels = new List<object>(d.Levels.Count);
            foreach (var r in d.Levels)
                levels.Add(new Dictionary<string, object>
                {
                    ["n"] = r.Level, ["best"] = r.BestScore, ["stars"] = r.Stars, ["done"] = r.Completed, ["plays"] = r.Plays, ["wins"] = r.Wins,
                });
            root["levels"] = levels;

            var claims = new List<string>(d.Claims);
            claims.Sort(string.CompareOrdinal);
            root["claims"] = claims;

            var daily = new List<object>(d.Daily.Count);
            foreach (var r in d.Daily)
                daily.Add(new Dictionary<string, object> { ["day"] = r.Day, ["best"] = r.BestScore, ["stars"] = r.Stars, ["done"] = r.Completed, ["plays"] = r.Plays });
            root["daily"] = daily;

            var weekly = new List<object>(d.Weekly.Count);
            foreach (var r in d.Weekly)
                weekly.Add(new Dictionary<string, object> { ["week"] = r.Week, ["best"] = r.BestScore, ["runs"] = r.Runs });
            root["weekly"] = weekly;

            root["endless"] = new Dictionary<string, object>
            {
                ["best"] = d.Endless.BestScore, ["bestGroup"] = d.Endless.BestGroup, ["bestMoves"] = d.Endless.BestMoves,
                ["bestStage"] = d.Endless.BestStage, ["runs"] = d.Endless.Runs, ["level"] = d.Endless.Level,
            };

            var tutorials = new List<string>(d.Tutorials);
            tutorials.Sort(string.CompareOrdinal);
            root["tutorials"] = tutorials;

            root["settings"] = new Dictionary<string, object>
            {
                ["sfx"] = d.Settings.SfxVolume, ["music"] = d.Settings.MusicVolume, ["haptics"] = d.Settings.Haptics,
                ["sfxWhenOn"] = d.Settings.SfxVolumeWhenOn, ["musicWhenOn"] = d.Settings.MusicVolumeWhenOn,
                ["symbols"] = d.Settings.Symbols, ["reducedMotion"] = d.Settings.ReducedMotion,
            };

            var uses = new Dictionary<string, object>();
            foreach (var kv in d.Stats.PowerUpUses) uses[kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = kv.Value;
            root["stats"] = new Dictionary<string, object>
            {
                ["tiles"] = d.Stats.TilesCleared, ["groups"] = d.Stats.GroupsCleared, ["powerUps"] = d.Stats.PowerUpsUsed,
                ["games"] = d.Stats.GamesPlayed, ["biggestGroup"] = d.Stats.BiggestGroup, ["shuffles"] = d.Stats.Shuffles,
                ["stones"] = d.Stats.StonesBroken, ["ice"] = d.Stats.IceLayersRemoved, ["chains"] = d.Stats.ChainsReleased,
                ["moss"] = d.Stats.MossRemoved, ["treasures"] = d.Stats.TreasuresCollected, ["mostInMove"] = d.Stats.MostTilesInOneMove,
                ["biggestCombo"] = d.Stats.BiggestCombo, ["powerUpUses"] = uses, ["pureLevels"] = new List<int>(d.Stats.PureThreeStarLevels),
            };

            var unlocked = new Dictionary<string, object>();
            foreach (var kv in d.Achievements.Unlocked) unlocked[kv.Key] = kv.Value;
            root["achievements"] = new Dictionary<string, object> { ["unlocked"] = unlocked };

            root["monetisation"] = EncodeMonetisation(d.Monetisation);

            return Json.Write(root);
        }

        static Dictionary<string, object> EncodeMonetisation(MonetisationRecord m)
        {
            var today = new Dictionary<string, object>();
            foreach (var kv in m.AdsToday) today[kv.Key] = kv.Value;
            var last = new Dictionary<string, object>();
            foreach (var kv in m.LastAdTicks) last[kv.Key] = kv.Value;
            var events = new Dictionary<string, object>();
            foreach (var kv in m.EventCounts) events[kv.Key] = kv.Value;
            return new Dictionary<string, object>
            {
                ["completions"] = m.CompletionsSinceInterstitial,
                ["lastInterstitial"] = m.LastInterstitialTicks,
                ["lastQuiet"] = m.LastQuietTicks,
                ["adDay"] = m.AdDay ?? "",
                ["adsToday"] = today,
                ["lastAd"] = last,
                ["transactions"] = new List<string>(m.Transactions),
                ["noAds"] = m.NoAds,
                ["noAdsTransaction"] = m.NoAdsTransaction ?? "",
                ["noAdsChecked"] = m.NoAdsCheckedTicks,
                ["noAdsMissing"] = m.NoAdsMissingChecks,
                ["events"] = events,
            };
        }

        static MonetisationRecord DecodeMonetisation(Dictionary<string, object> o)
        {
            var m = new MonetisationRecord();
            if (o == null) return m;
            m.CompletionsSinceInterstitial = JsonRead.Int(o, "completions");
            m.LastInterstitialTicks = JsonRead.Long(o, "lastInterstitial");
            m.LastQuietTicks = JsonRead.Long(o, "lastQuiet");
            m.AdDay = JsonRead.Str(o, "adDay");
            var today = JsonRead.Obj(o, "adsToday");
            if (today != null) foreach (var kv in today) m.AdsToday[kv.Key] = (int)JsonRead.Long(today, kv.Key);
            var last = JsonRead.Obj(o, "lastAd");
            if (last != null) foreach (var kv in last) m.LastAdTicks[kv.Key] = JsonRead.Long(last, kv.Key);
            var tx = JsonRead.Arr(o, "transactions");
            if (tx != null) foreach (var t in tx) if (t is string s && s.Length > 0) m.AddTransaction(s);
            m.NoAds = JsonRead.Bool(o, "noAds");
            m.NoAdsTransaction = JsonRead.Str(o, "noAdsTransaction");
            m.NoAdsCheckedTicks = JsonRead.Long(o, "noAdsChecked");
            m.NoAdsMissingChecks = JsonRead.Int(o, "noAdsMissing");
            var events = JsonRead.Obj(o, "events");
            if (events != null) foreach (var kv in events) m.EventCounts[kv.Key] = (int)JsonRead.Long(events, kv.Key);
            return m;
        }

        /// <summary>Parses a save. Throws FormatException if the text is not a Colour Cascade save.</summary>
        public static SaveData Decode(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Save root is not an object");
            if (JsonRead.Str(root, "format") != FormatTag) throw new FormatException("Not a Colour Cascade save");

            int version = JsonRead.Int(root, "version", 1);
            Migrate(root, version);

            var d = new SaveData
            {
                Version = SaveData.CurrentVersion,
                CreatedUtcTicks = JsonRead.Long(root, "created"),
                LastSavedUtcTicks = JsonRead.Long(root, "saved"),
                SaveCount = JsonRead.Int(root, "saveCount"),
                Coins = JsonRead.Int(root, "coins"),
                HighestUnlocked = JsonRead.Int(root, "highestUnlocked", 1),
                LatestDayKey = JsonRead.Str(root, "latestDay"),
            };

            foreach (var o in Items(root, "ledger"))
                d.Ledger.Add(new LedgerEntry { Ticks = JsonRead.Long(o, "t"), Delta = JsonRead.Int(o, "d"), Balance = JsonRead.Int(o, "b"), Reason = JsonRead.Str(o, "r") });

            foreach (var o in Items(root, "inventory"))
                d.Inventory.Add(new InventoryEntry { Id = JsonRead.Int(o, "id"), Owned = JsonRead.Int(o, "owned"), Trials = JsonRead.Int(o, "trials") });

            foreach (var o in Items(root, "levels"))
                d.Levels.Add(new LevelRecord
                {
                    Level = JsonRead.Int(o, "n"), BestScore = JsonRead.Int(o, "best"), Stars = JsonRead.Int(o, "stars"),
                    Completed = JsonRead.Bool(o, "done"), Plays = JsonRead.Int(o, "plays"), Wins = JsonRead.Int(o, "wins"),
                });

            var claims = JsonRead.Arr(root, "claims");
            if (claims != null) foreach (var c in claims) if (c is string s) d.Claims.Add(s);

            foreach (var o in Items(root, "daily"))
                d.Daily.Add(new DailyRecord
                {
                    Day = JsonRead.Str(o, "day"), BestScore = JsonRead.Int(o, "best"), Stars = JsonRead.Int(o, "stars"),
                    Completed = JsonRead.Bool(o, "done"), Plays = JsonRead.Int(o, "plays"),
                });

            foreach (var o in Items(root, "weekly"))
                d.Weekly.Add(new WeeklyRecord { Week = JsonRead.Str(o, "week"), BestScore = JsonRead.Int(o, "best"), Runs = JsonRead.Int(o, "runs") });

            var endless = JsonRead.Obj(root, "endless");
            d.Endless = new EndlessRecord
            {
                BestScore = JsonRead.Int(endless, "best"), BestGroup = JsonRead.Int(endless, "bestGroup"), BestMoves = JsonRead.Int(endless, "bestMoves"),
                BestStage = JsonRead.Int(endless, "bestStage"), Runs = JsonRead.Int(endless, "runs"),
            };
            // Saves from before Endless levels existed start from the best stage reached (so the level is never lost).
            d.Endless.Level = Math.Max(1, JsonRead.Int(endless, "level", Math.Max(1, d.Endless.BestStage)));

            var tutorials = JsonRead.Arr(root, "tutorials");
            if (tutorials != null) foreach (var t in tutorials) if (t is string s) d.Tutorials.Add(s);

            var settings = JsonRead.Obj(root, "settings");
            var defaults = new SettingsData();
            float sfx = JsonRead.Float(settings, "sfx", defaults.SfxVolume), music = JsonRead.Float(settings, "music", defaults.MusicVolume);
            d.Settings = new SettingsData
            {
                SfxVolume = sfx,
                MusicVolume = music,
                // Saves from before these fields existed: the saved volume, if audible, is the one to come back to.
                SfxVolumeWhenOn = JsonRead.Float(settings, "sfxWhenOn", sfx > GameSettings.Silent ? sfx : defaults.SfxVolume),
                MusicVolumeWhenOn = JsonRead.Float(settings, "musicWhenOn", music > GameSettings.Silent ? music : defaults.MusicVolume),
                Haptics = JsonRead.Bool(settings, "haptics", defaults.Haptics),
                Symbols = JsonRead.Bool(settings, "symbols", defaults.Symbols),
                ReducedMotion = JsonRead.Bool(settings, "reducedMotion", defaults.ReducedMotion),
            };

            var stats = JsonRead.Obj(root, "stats");
            d.Stats = new StatsData
            {
                TilesCleared = JsonRead.Long(stats, "tiles"), GroupsCleared = JsonRead.Int(stats, "groups"), PowerUpsUsed = JsonRead.Int(stats, "powerUps"),
                GamesPlayed = JsonRead.Int(stats, "games"), BiggestGroup = JsonRead.Int(stats, "biggestGroup"), Shuffles = JsonRead.Int(stats, "shuffles"),
                // Counters added with achievements: older saves have none (they start at zero).
                StonesBroken = JsonRead.Long(stats, "stones"), IceLayersRemoved = JsonRead.Long(stats, "ice"), ChainsReleased = JsonRead.Long(stats, "chains"),
                MossRemoved = JsonRead.Long(stats, "moss"), TreasuresCollected = JsonRead.Long(stats, "treasures"),
                MostTilesInOneMove = JsonRead.Int(stats, "mostInMove"), BiggestCombo = JsonRead.Int(stats, "biggestCombo"),
            };
            var uses = stats != null ? JsonRead.Obj(stats, "powerUpUses") : null;
            if (uses != null)
                foreach (var kv in uses)
                {
                    int id;
                    if (int.TryParse(kv.Key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id))
                        d.Stats.PowerUpUses[id] = (int)JsonRead.Long(uses, kv.Key);
                }
            var pure = stats != null ? JsonRead.Arr(stats, "pureLevels") : null;
            if (pure != null)
                foreach (var v in pure)
                    if (v is long n && n > 0 && n <= int.MaxValue && !d.Stats.PureThreeStarLevels.Contains((int)n)) d.Stats.PureThreeStarLevels.Add((int)n);

            var ach = JsonRead.Obj(root, "achievements");
            var unlocked = ach != null ? JsonRead.Obj(ach, "unlocked") : null;
            if (unlocked != null)
                foreach (var kv in unlocked)
                    if (!string.IsNullOrEmpty(kv.Key)) d.Achievements.Unlocked[kv.Key] = JsonRead.Long(unlocked, kv.Key);

            // Saves from before update 3.0 have no monetisation record: everything starts at zero.
            d.Monetisation = DecodeMonetisation(JsonRead.Obj(root, "monetisation"));

            d.Normalize();
            return d;
        }

        /// <summary>Upgrades older save layouts in place. Add a step here whenever CurrentVersion increases.</summary>
        static void Migrate(Dictionary<string, object> root, int fromVersion)
        {
            // Version 1 is the first released format; no migrations yet.
            // Example for the future:
            // if (fromVersion < 2) { root["newField"] = defaultValue; }
            // Saves written by a NEWER game version are read leniently: unknown fields are ignored.
        }

        static IEnumerable<Dictionary<string, object>> Items(Dictionary<string, object> root, string key)
        {
            var arr = JsonRead.Arr(root, key);
            if (arr == null) yield break;
            foreach (var item in arr)
                if (item is Dictionary<string, object> o) yield return o;
        }
    }
}
