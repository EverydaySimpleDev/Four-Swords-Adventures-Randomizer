using System;
using System.Collections.Generic;

namespace FSARandomizer.Services
{
    /// <summary>
    /// Provides level-order permutations for the world and stage shuffle features.
    ///
    /// All permutations are 32-element arrays (8 worlds × 4 stages), where
    /// result[i] = which original stage-index's content goes in target slot i.
    /// The physical file content swap and RARC entry rename is handled by LevelService.ExportIso.
    /// </summary>
    public class DolPatcherService
    {
        private const int ShuffleableWorlds = 8;
        private const int StagesPerWorld    = 4;
        private const int TotalStages       = ShuffleableWorlds * StagesPerWorld; // 32

        /// <summary>
        /// Boss archive stem for each of the 32 shuffleable stage slots, in index order.
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
            "080","207","081","083",   // World 8
        };

        /// <summary>
        /// Shuffle entire worlds as groups: all 4 stages of a world move together.
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
                    stagePerm[w * StagesPerWorld + s] = worldPerm[w] * StagesPerWorld + s;

            return stagePerm;
        }

        /// <summary>
        /// Shuffle all 32 stages independently: any stage can end up in any slot.
        /// Returns a 32-element stage permutation.
        /// </summary>
        public static int[] BuildStagePermutation(int seed)
        {
            var perm = new int[TotalStages];
            for (int i = 0; i < TotalStages; i++) perm[i] = i;

            var rng = new Random(seed);
            for (int i = TotalStages - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }
            return perm;
        }

        /// <summary>
        /// Convert a 32-element permutation to a human-readable placement dictionary.
        /// Key = target slot (e.g. "boss010"), value = source content (e.g. "boss030").
        /// Suitable for JSON export and Archipelago integration.
        /// </summary>
        public static Dictionary<string, string> PermutationToPlacements(int[] perm)
        {
            var result = new Dictionary<string, string>(TotalStages);
            for (int i = 0; i < TotalStages && i < perm.Length; i++)
                result[$"boss{WorldStems[i]}"] = $"boss{WorldStems[perm[i]]}";
            return result;
        }

        /// <summary>
        /// Convert a placement dictionary back to a 32-element permutation.
        /// Accepts partial dictionaries — unspecified slots default to identity (no swap).
        /// Values may be "boss010" or just the stem "010".
        /// </summary>
        public static int[] PlacementsToPermutation(Dictionary<string, string> placements)
        {
            var perm = new int[TotalStages];
            for (int i = 0; i < TotalStages; i++) perm[i] = i;

            for (int i = 0; i < TotalStages; i++)
            {
                string targetKey = $"boss{WorldStems[i]}";
                if (!placements.TryGetValue(targetKey, out var srcValue)) continue;
                string srcStem = srcValue.StartsWith("boss", StringComparison.OrdinalIgnoreCase)
                    ? srcValue.Substring(4) : srcValue;
                int srcIdx = Array.IndexOf(WorldStems, srcStem);
                if (srcIdx >= 0) perm[i] = srcIdx;
            }
            return perm;
        }
    }
}
