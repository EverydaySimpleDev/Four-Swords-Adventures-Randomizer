using FSALib.Structs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FSARandomizer.Services
{
    /// <summary>
    /// Swaps ordinary enemies in the story stages for other ordinary enemies.
    ///
    /// Only "plain" placements take part: enemy types that can be beaten with the sword alone,
    /// with a variable value that has no trigger, rail, squad link, or special state. Everything
    /// else (bosses, mini-bosses, traps, spawners, water/sand/wall enemies, scripted soldiers)
    /// is left untouched. Each eligible placement keeps its position and layer and is replaced
    /// one-for-one, so "defeat all enemies" rooms keep the same number of enemies.
    /// The replacement variants are taken from plain placements seen in the original game.
    /// </summary>
    public class EnemyRandomizerService
    {
        /// <summary>
        /// Enemy types that may be swapped, with the rule that decides which variable values
        /// count as a plain placement. Field layouts come from FSALib's actor definitions.
        /// </summary>
        private static readonly Dictionary<string, Func<uint, bool>> s_plainVariants = new()
        {
            ["OKTA"] = v => v <= 4,                                       // Octorok, any colour
            ["KETH"] = v => v == 0 || v == 3,                             // Keese: default / back-and-forth
            ["SLR1"] = v => v is 0 or 1 or 2 or 4,                        // Sword Soldier, unlinked: green/red/blue/black
            ["SLR2"] = v => v == 0 || v == 2,                             // Bow / bomb soldier, unlinked
            ["SLR3"] = v => v <= 3,                                       // Ball and Chain Soldier, no trigger
            ["STAL"] = v => v <= 2,                                       // Stalfos, not big
            ["POOH"] = v => v == 0,                                       // Ghini, normal size
            ["TKTA"] = v => v == 0 || v == 1,                             // Tektite, not hidden in a bush
            ["ROPE"] = v => v == 0,                                       // Rope
            ["NZMI"] = v => v <= 1,                                       // Rat
            ["Poon"] = v => v == 0,                                       // Hardhat Beetle
            ["PEAC"] = v => v == 0,                                       // Crab
            ["BZBR"] = v => v == 0 || v == 1,                             // Buzz Blob
            ["MOZO"] = v => v == 0,                                       // Ropa
            ["TNDR"] = v => v <= 0xFF,                                    // Vulture (low byte is activation distance)
            ["WIZR"] = v => v is 0 or 3 or 4 or 5,                        // Wizrobe, not a summoner, no trigger
            ["MIRA"] = v => v <= 1,                                       // Gibdo
            ["ZASL"] = v => v == 0,                                       // Zol, not under a rock
            ["DGT2"] = v => v == 0,                                       // Mini-Moldorm
        };

        /// <summary>Original actors of every room this service has changed, for restoring.</summary>
        private readonly Dictionary<LoadedRoom, Actor[]> _originals = new();

        public static bool IsPlain(Actor actor) =>
            s_plainVariants.TryGetValue(actor.ID.ToString(), out var rule) && rule(actor.Variable);

        /// <summary>
        /// Put every room back to its original enemies. Safe to call when nothing was changed.
        /// </summary>
        public void Restore()
        {
            foreach (var (room, actors) in _originals)
                ReplaceActors(room, actors);
            _originals.Clear();
        }

        /// <summary>
        /// Restore the original enemies, then replace every plain enemy in the story stages with
        /// a random plain enemy. Returns the number of placements that changed type or variant.
        /// </summary>
        public int Randomize(LoadedGame game, int seed)
        {
            Restore();

            var storyStems = DolPatcherService.StorySlots.Select(i => DolPatcherService.WorldStems[i]).ToHashSet();
            var storyRooms = game.Levels
                .Where(l => storyStems.Contains(l.Id))
                .SelectMany(l => l.Rooms)
                .ToList();

            // Pool of plain variants seen in the original game, grouped by enemy type so each
            // type is equally likely regardless of how common it is.
            var pool = storyRooms
                .SelectMany(r => r.Actors)
                .Where(IsPlain)
                .GroupBy(a => a.ID.ToString())
                .Select(g => g.Select(a => a.Variable).Distinct().Select(v => (g.First().ID, v)).ToArray())
                .ToArray();
            if (pool.Length == 0) return 0;

            var rng = new Random(seed);
            int changed = 0;
            foreach (var room in storyRooms)
            {
                // Work on a copy: ActorList keeps itself sorted by layer, position and ID,
                // so editing it in place can move actors to other indexes.
                var original = room.Actors.ToArray();
                var updated = (Actor[])original.Clone();
                int roomChanges = 0;
                for (int i = 0; i < updated.Length; i++)
                {
                    if (!IsPlain(updated[i])) continue;

                    var variants = pool[rng.Next(pool.Length)];
                    var (id, variable) = variants[rng.Next(variants.Length)];
                    if (updated[i].ID.Equals(id) && updated[i].Variable == variable) continue;

                    updated[i].ID = id;
                    updated[i].Variable = variable;
                    roomChanges++;
                }

                if (roomChanges == 0) continue;
                _originals[room] = original;
                ReplaceActors(room, updated);
                changed += roomChanges;
            }
            return changed;
        }

        private static void ReplaceActors(LoadedRoom room, Actor[] actors)
        {
            room.Actors.Clear();
            foreach (var a in actors)
                room.Actors.Add(a); // ActorList inserts in sorted order
            room.IsDirty = true;
        }
    }
}
