using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum PowerUpId
    {
        Blast = 0,
        Swap = 1,
        ColourShift = 2,
        TileBreaker = 3,
        /// <summary>
        /// Retired in 3.0.1: Out of Moves offers better ways to continue (an advert for 10 moves, 100 coins for 5). The
        /// value stays so saves and config assets that mention it still load; see RetiredPowerUps.
        /// </summary>
        ExtraMoves = 4,
        ColourBurst = 5,
    }

    /// <summary>
    /// Power-ups taken out of the game. They are left out of the catalog even if an older config asset still lists
    /// them, so they appear nowhere (bar, shop, How to Play, introductions). Uses a player still owned are paid back
    /// once, in coins, at their last price (GameContext.Load); free trial uses simply lapse.
    /// </summary>
    public static class RetiredPowerUps
    {
        public static readonly PowerUpId[] Ids = { PowerUpId.ExtraMoves };

        /// <summary>Coins paid back per owned use when no config entry gives a price (Extra Moves' last price).</summary>
        public const int DefaultRefundPerUse = 200;

        public static bool IsRetired(PowerUpId id) => Array.IndexOf(Ids, id) >= 0;

        public static string ClaimKey(PowerUpId id) => "pu.retired." + (int)id;

        public struct Refund
        {
            public PowerUpId Id;
            public string Name;
            public int Uses;
            public int Coins;
        }

        /// <summary>
        /// Pays back owned uses of retired power-ups, once per power-up (claim key), and clears their stock. Safe to
        /// call on every load. priceOf gives the refund per use (null = DefaultRefundPerUse).
        /// </summary>
        public static List<Refund> Settle(SaveData data, Wallet wallet, Func<PowerUpId, int> priceOf = null)
        {
            var refunds = new List<Refund>();
            if (data == null) return refunds;
            foreach (var id in Ids)
            {
                InventoryEntry entry = null;
                foreach (var e in data.Inventory)
                    if (e != null && e.Id == (int)id) entry = e;
                if (entry == null || (entry.Owned <= 0 && entry.Trials <= 0)) continue;
                string key = ClaimKey(id);
                if (entry.Owned > 0 && !data.Claims.Contains(key))
                {
                    int price = priceOf != null ? priceOf(id) : DefaultRefundPerUse;
                    int coins = Math.Max(0, price) * entry.Owned;
                    if (coins > 0 && wallet != null) wallet.Credit(coins, "refund." + id + ".x" + entry.Owned);
                    refunds.Add(new Refund { Id = id, Name = id == PowerUpId.ExtraMoves ? "Extra Moves" : id.ToString(), Uses = entry.Owned, Coins = coins });
                }
                data.Claims.Add(key);
                entry.Owned = 0;
                entry.Trials = 0;
            }
            return refunds;
        }
    }

    public enum PowerUpTargeting
    {
        /// <summary>Takes effect immediately, no board target.</summary>
        Instant = 0,
        /// <summary>One board cell.</summary>
        Cell = 1,
        /// <summary>Two orthogonally adjacent cells.</summary>
        AdjacentPair = 2,
        /// <summary>One cell plus a colour choice.</summary>
        CellAndColour = 3,
    }

    /// <summary>Data describing a power-up. The shop and the in-game bar are both built from these.</summary>
    [Serializable]
    public class PowerUpDefinition
    {
        public PowerUpId id;
        public string displayName = "";
        public string description = "";
        /// <summary>Instruction shown while targeting, e.g. "Tap a tile to blast the 3×3 area around it".</summary>
        public string targetingHint = "";
        public int price = 100;
        /// <summary>
        /// Price of a three-pack in the shop (update 3.1: about 10 % off three bought one at a time). 0 = the built-in
        /// default for this power-up (also what older config assets get); negative = no three-pack.
        /// </summary>
        public int packPrice;
        public PowerUpTargeting targeting;
        /// <summary>Unlocked once the player reaches this campaign level. 0 = always unlocked.</summary>
        public int unlockLevel;
        /// <summary>Free uses granted once when the power-up is introduced.</summary>
        public int trialUses = 2;
        /// <summary>Effect strength where an effect uses one (none of the current six does).</summary>
        public int amount;
        public ModeMask allowedModes = ModeMask.Campaign | ModeMask.Endless;
        public string accentHex = "#FFFFFF";
    }

    public sealed class PowerUpCatalog
    {
        readonly List<PowerUpDefinition> ordered = new List<PowerUpDefinition>();
        readonly Dictionary<PowerUpId, PowerUpDefinition> byId = new Dictionary<PowerUpId, PowerUpDefinition>();

        public PowerUpCatalog(IEnumerable<PowerUpDefinition> definitions)
        {
            foreach (var d in definitions)
            {
                if (d == null || byId.ContainsKey(d.id) || RetiredPowerUps.IsRetired(d.id)) continue;
                byId[d.id] = d;
                ordered.Add(d);
            }
            ordered.Sort((a, b) => a.unlockLevel != b.unlockLevel ? a.unlockLevel.CompareTo(b.unlockLevel) : a.id.CompareTo(b.id));
        }

        /// <summary>Definitions in introduction order.</summary>
        public IReadOnlyList<PowerUpDefinition> All => ordered;

        public PowerUpDefinition Get(PowerUpId id)
        {
            PowerUpDefinition d;
            return byId.TryGetValue(id, out d) ? d : null;
        }

        /// <summary>The power-up introduced at exactly this campaign level, if any.</summary>
        public PowerUpDefinition IntroducedAt(int level)
        {
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i].unlockLevel == level) return ordered[i];
            return null;
        }

        public static PowerUpDefinition[] CreateDefaults()
        {
            return new[]
            {
                new PowerUpDefinition
                {
                    id = PowerUpId.Blast, displayName = "Blast", price = 100, packPrice = 270, unlockLevel = 10, trialUses = 2,
                    targeting = PowerUpTargeting.Cell, accentHex = "#FF6B4A",
                    description = "Clears every tile in a 3×3 area. Stones are not affected.",
                    targetingHint = "Tap a tile to blast the 3×3 area around it",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.TileBreaker, displayName = "Tile Breaker", price = 125, packPrice = 340, unlockLevel = 20, trialUses = 2,
                    targeting = PowerUpTargeting.Cell, accentHex = "#B0B8D8",
                    description = "Smashes one tile or one stone barrier.",
                    targetingHint = "Tap a tile or a stone to break it",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.Swap, displayName = "Swap", price = 75, packPrice = 200, unlockLevel = 30, trialUses = 2,
                    targeting = PowerUpTargeting.AdjacentPair, accentHex = "#3DD6C4",
                    description = "Swaps two neighbouring tiles of different colours.",
                    targetingHint = "Tap a tile, then a neighbouring tile to swap them",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.ColourShift, displayName = "Colour Shift", price = 150, packPrice = 400, unlockLevel = 40, trialUses = 2,
                    targeting = PowerUpTargeting.CellAndColour, accentHex = "#B57BFF",
                    description = "Repaints one tile in a colour of your choice.",
                    targetingHint = "Tap a tile, then pick its new colour",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.ColourBurst, displayName = "Colour Burst", price = 250, packPrice = 675, unlockLevel = 60, trialUses = 1,
                    targeting = PowerUpTargeting.Cell, accentHex = "#FFC93C",
                    description = "Removes every tile of the colour you choose.",
                    targetingHint = "Tap a tile to remove every tile of its colour",
                },
            };
        }
    }
}
