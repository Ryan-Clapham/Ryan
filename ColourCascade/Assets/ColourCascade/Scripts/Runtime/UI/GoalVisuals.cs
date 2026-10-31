using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>Pictures and words for level goals and campaign mechanics, shared by the HUD, popups and tutorials.</summary>
    public static class GoalVisuals
    {
        /// <summary>A small picture of a goal: the tile, obstacle or piece it is about.</summary>
        public static RectTransform Icon(Transform parent, GoalSpec g, float size, bool symbols)
        {
            var root = UI.Rect("GoalIcon", parent);
            root.sizeDelta = new Vector2(size, size);
            switch (g.kind)
            {
                case GoalKind.Score:
                    UI.Image(root, ArtLibrary.Instance.Get(ArtNames.StarGold), Color.white, "Star").rectTransform.Fill();
                    break;
                case GoalKind.CollectColour:
                    TileWith(root, g.colour, null, symbols);
                    break;
                case GoalKind.FreeIce:
                    TileWith(root, TileColours.Blue, ArtNames.Ice1, false);
                    break;
                case GoalKind.BreakChains:
                    TileWith(root, TileColours.Purple, ArtNames.Chain1, false);
                    break;
                case GoalKind.CollectTreasure:
                    UI.Image(root, ArtLibrary.Instance.Get(ArtNames.Treasure), Color.white, "Treasure").rectTransform.Fill();
                    break;
                case GoalKind.ClearMoss:
                    TileWith(root, TileColours.Yellow, ArtNames.Moss, false);
                    break;
            }
            return root;
        }

        static void TileWith(RectTransform root, int colour, string overlay, bool symbols)
        {
            var tile = UI.Image(root, ArtLibrary.Instance.Tile(Mathf.Max(0, colour)), Color.white, "Tile");
            tile.rectTransform.Fill();
            if (overlay != null) UI.Image(tile.rectTransform, ArtLibrary.Instance.Get(overlay), Color.white, "Overlay").rectTransform.Fill();
            if (symbols && colour >= 0) UI.Image(tile.rectTransform, ArtLibrary.Instance.Symbol(colour), Color.white, "Symbol").rectTransform.Centre(root.sizeDelta.x * 0.55f, root.sizeDelta.y * 0.55f);
        }

        /// <summary>The goal as a sentence: "Free 6 frozen tiles".</summary>
        public static string Describe(GoalSpec g)
        {
            switch (g.kind)
            {
                case GoalKind.Score: return "Score " + NumberFormat.Thousands(g.amount) + " points";
                case GoalKind.CollectColour: return "Collect " + g.amount + " " + ColourName(g.colour) + " tiles";
                case GoalKind.FreeIce: return "Free " + g.amount + (g.amount == 1 ? " frozen tile" : " frozen tiles");
                case GoalKind.BreakChains: return "Free " + g.amount + (g.amount == 1 ? " chained tile" : " chained tiles");
                case GoalKind.CollectTreasure: return "Bring down " + g.amount + (g.amount == 1 ? " treasure" : " treasures");
                case GoalKind.ClearMoss: return "Clear all the moss";
                default: return "";
            }
        }

        public static string ColourName(int colour) => colour >= 0 && colour < TileColours.Names.Length ? TileColours.Names[colour].ToLowerInvariant() : "";

        /// <summary>The banner shown when a level starts.</summary>
        public static string IntroLine(LevelSpec level)
        {
            var goals = level.EffectiveGoals();
            if (goals.Length == 1) return Describe(goals[0]) + " in " + level.Moves + " moves";
            return "Complete " + goals.Length + " goals in " + level.Moves + " moves";
        }
    }

    /// <summary>The goals row on the game screen: a picture of each goal with what is still needed, ticked when done.</summary>
    public sealed class GoalsPanel : MonoBehaviour
    {
        sealed class Chip
        {
            public RectTransform Root;
            public Text Count;
            public Image Tick;
            public int Shown = -1;
            public bool Done;
        }

        readonly List<Chip> chips = new List<Chip>();
        RectTransform row;

        public static GoalsPanel Create(Transform parent)
        {
            var rt = UI.Rect("Goals", parent);
            var p = rt.gameObject.AddComponent<GoalsPanel>();
            p.row = UI.Rect("Row", rt);
            p.row.Fill();
            var h = UI.HStack(p.row, 14, null, TextAnchor.MiddleCenter, false);
            h.childControlWidth = false;
            h.childControlHeight = false;
            return p;
        }

        public void Configure(GoalTracker goals, bool symbols)
        {
            UI.ClearChildren(row);
            chips.Clear();
            if (goals == null) return;
            float size = goals.Count >= 3 ? 70f : 82f;
            for (int i = 0; i < goals.Count; i++)
            {
                var chip = new Chip { Root = UI.Rect("Goal" + i, row) };
                chip.Root.sizeDelta = new Vector2(goals.Count >= 3 ? 128 : 150, 170);
                var icon = GoalVisuals.Icon(chip.Root, goals.Goal(i), size, symbols);
                icon.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(size, size));
                chip.Count = UI.FitText(chip.Root, "", Theme.FontBody, Theme.Text, TextAnchor.MiddleCenter, true, "Count");
                chip.Count.rectTransform.Fill(0, 0, 0, size + 6);
                UI.AddShadow(chip.Count, -3, 0.35f);
                chip.Tick = UI.Icon(icon, Procedural.Icon.Check, Theme.Success, size * 0.5f);
                chip.Tick.rectTransform.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-size * 0.12f, size * 0.12f), new Vector2(size * 0.5f, size * 0.5f));
                chip.Tick.gameObject.SetActive(false);
                chips.Add(chip);
            }
        }

        /// <summary>Updates the counts. Returns the screen position of any goal just completed (for effects), else null.</summary>
        public Vector3? Refresh(GoalTracker goals, bool animate)
        {
            Vector3? completed = null;
            if (goals == null) return null;
            for (int i = 0; i < chips.Count && i < goals.Count; i++)
            {
                var c = chips[i];
                var g = goals.Goal(i);
                int remaining = goals.Remaining(i);
                bool done = goals.IsComplete(i);
                if (g.kind == GoalKind.Score) c.Count.text = done ? "Done" : NumberFormat.Thousands(remaining);
                else c.Count.text = done ? "Done" : remaining.ToString();
                c.Count.color = done ? Theme.Success : Theme.Text;
                c.Tick.gameObject.SetActive(done);
                if (animate && c.Shown >= 0 && (remaining != c.Shown || done != c.Done))
                {
                    c.Root.localScale = Vector3.one * (done && !c.Done ? 1.3f : 1.15f);
                    Tw.Scale(c.Root, 1f, 0.35f, Ease.OutBack);
                    if (done && !c.Done) completed = c.Root.position;
                }
                c.Shown = remaining;
                c.Done = done;
            }
            return completed;
        }

        /// <summary>World position of the goal of this kind (for tutorial pointers), or null.</summary>
        public Vector3? PositionOf(GoalTracker goals, GoalKind kind)
        {
            if (goals == null) return null;
            for (int i = 0; i < chips.Count && i < goals.Count; i++)
                if (goals.Goal(i).kind == kind) return chips[i].Root.position;
            return null;
        }
    }

    /// <summary>What each mechanic's introduction says, and which tile it points at.</summary>
    public static class MechanicTips
    {
        public sealed class Tip
        {
            public string Title = "";
            public string Body = "";
            public string Pointer = "";
            public string ArtTile;
            public string ArtOverlay;
            public int ArtColour = TileColours.Blue;
        }

        public static Tip For(string key, ScoringRules scoring)
        {
            var sp = scoring != null ? scoring.specials : new SpecialRules();
            switch (key)
            {
                case TutorialKeys.Ice:
                    return new Tip
                    {
                        Title = "Frozen tiles", ArtOverlay = ArtNames.Ice1,
                        Body = "Frozen tiles can't be tapped and don't fall.\nClear tiles right next to one to crack its ice. Once free, it falls and matches like any other tile.",
                        Pointer = "Clear a group next to the ice to crack it",
                    };
                case TutorialKeys.ThickIce:
                    return new Tip
                    {
                        Title = "Thick ice", ArtOverlay = ArtNames.Ice2, ArtColour = TileColours.Red,
                        Body = "Thick ice needs two cracks.\nThe number in the corner shows how many are left.",
                        Pointer = "Thick ice: crack it twice",
                    };
                case TutorialKeys.ColourGoal:
                    return new Tip
                    {
                        Title = "Collect a colour", ArtColour = TileColours.Blue,
                        Body = "This level asks for tiles of one colour.\nEvery tile of that colour you clear counts. The goal at the top shows how many are left.",
                        Pointer = "Your goal: clear tiles of this colour",
                    };
                case TutorialKeys.Rockets:
                    return new Tip
                    {
                        Title = "Rockets", ArtTile = ArtNames.RocketRow,
                        Body = "Clear a group of " + sp.rocketGroupSize + " or more tiles to make a rocket.\nTap a rocket to clear its whole row or column: the arrows show which. Firing it uses a move.",
                        Pointer = "Tap the rocket to fire it",
                    };
                case TutorialKeys.Bombs:
                    return new Tip
                    {
                        Title = "Bombs", ArtTile = ArtNames.Bomb,
                        Body = "Clear a group of " + sp.bombGroupSize + " or more to make a bomb.\nTap it to clear every tile around it.",
                        Pointer = "Tap the bomb to set it off",
                    };
                case TutorialKeys.Combos:
                    return new Tip
                    {
                        Title = "Special combos", ArtTile = ArtNames.RocketColumn,
                        Body = "Tap a special tile that touches another one to fire both together for a much bigger effect.\nTwo rockets clear a row and a column; two bombs clear a huge square.",
                        Pointer = "Tap one of these rockets to fire them together",
                    };
                case TutorialKeys.Prisms:
                    return new Tip
                    {
                        Title = "Prisms", ArtTile = ArtNames.Prism,
                        Body = "Clear a group of " + sp.prismGroupSize + " or more to make a prism.\nTap it and pick a colour: every tile of that colour is cleared. Save it for the colour you need!",
                        Pointer = "Tap the prism, then choose a colour",
                    };
                case TutorialKeys.Chains:
                    return new Tip
                    {
                        Title = "Chained tiles", ArtOverlay = ArtNames.Chain1, ArtColour = TileColours.Purple,
                        Body = "Chained tiles can't move, but they still match their own colour.\nClear a group that includes one to break a link. The number shows how many links are left.",
                        Pointer = "Clear this tile's group to break its chain",
                    };
                case TutorialKeys.Treasure:
                    return new Tip
                    {
                        Title = "Treasure", ArtTile = ArtNames.Treasure,
                        Body = "Treasure falls as you clear the tiles beneath it.\nBring it all the way down to the bottom of the board (or onto a stone) to collect it.",
                        Pointer = "Clear the tiles under the treasure",
                    };
                case TutorialKeys.Moss:
                    return new Tip
                    {
                        Title = "Moss", ArtOverlay = ArtNames.Moss, ArtColour = TileColours.Yellow,
                        Body = "Moss covers tiles. Clear a mossy tile, or a group right next to it, to remove the moss.\nIf a move clears no moss, it grows onto a neighbouring tile. Clear it all to win!",
                        Pointer = "Clear the moss before it spreads",
                    };
                default:
                    return null;
            }
        }
    }
}
