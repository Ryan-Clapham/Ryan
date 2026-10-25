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
        ExtraMoves = 4,
        ColourBurst = 5,
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
        public PowerUpTargeting targeting;
        /// <summary>Unlocked once the player reaches this campaign level. 0 = always unlocked.</summary>
        public int unlockLevel;
        /// <summary>Free uses granted once when the power-up is introduced.</summary>
        public int trialUses = 2;
        /// <summary>Effect strength where relevant (Extra Moves: moves added).</summary>
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
                if (d == null || byId.ContainsKey(d.id)) continue;
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
                    id = PowerUpId.Blast, displayName = "Blast", price = 100, unlockLevel = 10, trialUses = 2,
                    targeting = PowerUpTargeting.Cell, accentHex = "#FF6B4A",
                    description = "Clears every tile in a 3×3 area. Stones are not affected.",
                    targetingHint = "Tap a tile to blast the 3×3 area around it",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.TileBreaker, displayName = "Tile Breaker", price = 125, unlockLevel = 20, trialUses = 2,
                    targeting = PowerUpTargeting.Cell, accentHex = "#B0B8D8",
                    description = "Smashes one tile or one stone barrier.",
                    targetingHint = "Tap a tile or a stone to break it",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.Swap, displayName = "Swap", price = 75, unlockLevel = 30, trialUses = 2,
                    targeting = PowerUpTargeting.AdjacentPair, accentHex = "#3DD6C4",
                    description = "Swaps two neighbouring tiles of different colours.",
                    targetingHint = "Tap a tile, then a neighbouring tile to swap them",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.ColourShift, displayName = "Colour Shift", price = 150, unlockLevel = 40, trialUses = 2,
                    targeting = PowerUpTargeting.CellAndColour, accentHex = "#B57BFF",
                    description = "Repaints one tile in a colour of your choice.",
                    targetingHint = "Tap a tile, then pick its new colour",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.ExtraMoves, displayName = "Extra Moves", price = 200, unlockLevel = 50, trialUses = 1,
                    amount = 3, targeting = PowerUpTargeting.Instant, accentHex = "#4BD47A",
                    allowedModes = ModeMask.Campaign,
                    description = "Adds 3 moves to the current campaign level.",
                    targetingHint = "",
                },
                new PowerUpDefinition
                {
                    id = PowerUpId.ColourBurst, displayName = "Colour Burst", price = 250, unlockLevel = 60, trialUses = 1,
                    targeting = PowerUpTargeting.Cell, accentHex = "#FFC93C",
                    description = "Removes every tile of the colour you choose.",
                    targetingHint = "Tap a tile to remove every tile of its colour",
                },
            };
        }
    }
}
