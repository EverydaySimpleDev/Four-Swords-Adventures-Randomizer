using System;
using System.Collections.Generic;
using System.Linq;

namespace FSARandomizer.Services
{
    /// <summary>
    /// Provides level-order permutations for the world and stage shuffle features.
    ///
    /// All permutations are 32-element arrays (8 worlds × 4 slots), where
    /// result[i] = which original slot-index's content goes in target slot i.
    /// Tingle's Tower mini-game slots (boss200–207) always map to themselves.
    /// The physical file content swap and RARC entry rename is handled by LevelService.ExportIso.
    /// </summary>
    public class DolPatcherService
    {
        private const int ShuffleableWorlds = 8;
        private const int StagesPerWorld    = 4;
        private const int TotalStages       = ShuffleableWorlds * StagesPerWorld; // 32
        private const int MiniGameSlot      = 2; // Tingle's Tower, third point on each world map

        /// <summary>
        /// Boss archive stem for each of the 32 world-map slots, in index order.
        /// Index i corresponds to <c>boss{WorldStems[i]}.arc</c>.
        /// </summary>
        public static readonly string[] WorldStems =
        {
            "010","011","200","013",   // World 1
            "020","021","201","023",   // World 2
            "030","031","202","033",   // World 3
            "040","041","203","043",   // World 4
            "050","051","204","053",   // World 5
            "060","061","205","063",   // World 6
            "070","071","206","073",   // World 7
            "080","081","207","083",   // World 8
        };

        /// <summary>
        /// True for the Tingle's Tower mini-game slots. They are not part of the story,
        /// so neither shuffle mode moves them.
        /// </summary>
        public static bool IsMiniGameSlot(int slot) => slot % StagesPerWorld == MiniGameSlot;

        /// <summary>Slot indices of the 24 story stages (3 per world) that the shuffles move.</summary>
        public static readonly int[] StorySlots =
            Enumerable.Range(0, TotalStages).Where(i => !IsMiniGameSlot(i)).ToArray();

        /// <summary>
        /// Shuffle entire worlds as groups: the 3 story stages of a world move together,
        /// while each world's Tingle's Tower stays in place.
        /// Returns a 32-element stage permutation expanded from an 8-element world permutation.
        /// </summary>
        public static int[] BuildWorldPermutation(int seed)
        {
            var worldPerm = new int[ShuffleableWorlds];
            for (int i = 0; i < ShuffleableWorlds; i++) worldPerm[i] = i;

            var rng = new System.Random(seed);
            for (int i = ShuffleableWorlds - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (worldPerm[i], worldPerm[j]) = (worldPerm[j], worldPerm[i]);
            }

            var stagePerm = new int[TotalStages];
            for (int w = 0; w < ShuffleableWorlds; w++)
                for (int s = 0; s < StagesPerWorld; s++)
                {
                    int slot = w * StagesPerWorld + s;
                    stagePerm[slot] = IsMiniGameSlot(slot) ? slot : worldPerm[w] * StagesPerWorld + s;
                }

            return stagePerm;
        }

        /// <summary>
        /// Shuffle the 24 story stages independently: any story stage can end up in any story slot.
        /// Returns a 32-element stage permutation.
        /// </summary>
        public static int[] BuildStagePermutation(int seed)
        {
            var perm = new int[TotalStages];
            for (int i = 0; i < TotalStages; i++) perm[i] = i;

            var shuffled = (int[])StorySlots.Clone();
            var rng = new Random(seed);
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            for (int k = 0; k < StorySlots.Length; k++)
                perm[StorySlots[k]] = shuffled[k];
            return perm;
        }

        /// <summary>
        /// Convert a 32-element permutation to a human-readable placement dictionary.
        /// Key = target slot (e.g. "boss010"), value = source content (e.g. "boss030").
        /// Only story stages are listed. Suitable for JSON export and Archipelago integration.
        /// </summary>
        public static Dictionary<string, string> PermutationToPlacements(int[] perm)
        {
            var result = new Dictionary<string, string>(StorySlots.Length);
            foreach (int i in StorySlots)
                if (i < perm.Length)
                    result[$"boss{WorldStems[i]}"] = $"boss{WorldStems[perm[i]]}";
            return result;
        }

        /// <summary>
        /// Convert a placement dictionary back to a 32-element permutation.
        /// Accepts partial dictionaries — unspecified slots default to identity (no swap).
        /// Entries that target or name a Tingle's Tower slot are ignored.
        /// Values may be "boss010" or just the stem "010".
        /// </summary>
        public static int[] PlacementsToPermutation(Dictionary<string, string> placements)
        {
            var perm = new int[TotalStages];
            for (int i = 0; i < TotalStages; i++) perm[i] = i;

            foreach (int i in StorySlots)
            {
                string targetKey = $"boss{WorldStems[i]}";
                if (!placements.TryGetValue(targetKey, out var srcValue)) continue;
                string srcStem = srcValue.StartsWith("boss", StringComparison.OrdinalIgnoreCase)
                    ? srcValue.Substring(4) : srcValue;
                int srcIdx = Array.IndexOf(WorldStems, srcStem);
                if (srcIdx >= 0 && !IsMiniGameSlot(srcIdx)) perm[i] = srcIdx;
            }
            return perm;
        }
    }
}
