using FSARandomizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;

namespace FSARandomizer.Services
{
    /// <summary>
    /// Shuffles item locations according to <see cref="RandomizerSettings"/> and produces a spoiler log.
    /// </summary>
    public class RandomizerService
    {
        // ── Randomize ─────────────────────────────────────────────────────────

        /// <summary>
        /// In-place: reassign <see cref="ItemLocation.RandomizedItemId"/> on every location.
        /// Locations are grouped into pools and shuffled within constraints.
        /// </summary>
        public void Randomize(List<ItemLocation> locations, RandomizerSettings settings)
        {
            var rng = new Random(settings.Seed);

            // Items excluded from shuffling — they stay at their original location
            var excludedChestIds = BuildExcludedChestIds(settings);
            var excludedFloorIds = BuildExcludedFloorIds(settings);

            // Build item pool
            var chestLocations = locations
                .Where(l => l.ActorType == "TKRA" && settings.ShuffleChestItems
                            && !excludedChestIds.Contains(l.OriginalItemId))
                .ToList();

            var floorLocations = locations
                .Where(l => l.ActorType == "KEY0" && settings.ShuffleFloorKeyItems
                            && !excludedFloorIds.Contains(l.OriginalItemId))
                .ToList();

            // ── Chest pool ──
            if (chestLocations.Count > 0)
            {
                ShufflePool(chestLocations, rng, settings);
            }

            // ── Floor item pool ──
            if (floorLocations.Count > 0)
            {
                ShuffleFloorItems(floorLocations, rng, settings);
            }

            // Update display names after shuffling
            foreach (var loc in locations)
            {
                loc.RandomizedItemName = loc.ActorType == "TKRA"
                    ? GameData.GetChestItemName(loc.RandomizedItemId)
                    : GameData.GetFloorItemName(loc.RandomizedItemId);
            }
        }

        private static HashSet<byte> BuildExcludedChestIds(RandomizerSettings s)
        {
            var set = new HashSet<byte>();
            if (!s.ShuffleMoonPearl)       set.Add(0x16);
            if (!s.ShuffleHeartContainers) set.Add(0x14);
            if (!s.ShuffleBigBombs)        set.Add(0x2E);
            if (!s.ShuffleBlueBracelet)    set.Add(0x15);
            return set;
        }

        private static HashSet<byte> BuildExcludedFloorIds(RandomizerSettings s)
        {
            var set = new HashSet<byte>();
            if (!s.ShuffleMoonPearl)       set.Add(0x06);
            if (!s.ShuffleHeartContainers) set.Add(0x04);
            if (!s.ShuffleBlueBracelet)    set.Add(0x05);
            // Big Bomb has no floor-item equivalent
            // EnsureBeatable: floor keys must never leave their original level — boss-door
            // mechanisms (KEY0 big keys) would become permanently locked if moved.
            // KEY0 floor item IDs: 0x00=small key, 0x01=big key (different from TKRA chest IDs 0x10/0x11).
            if (s.EnsureBeatable)          { set.Add(0x00); set.Add(0x01); }
            return set;
        }

        private static void ShufflePool(
            List<ItemLocation> chestLocs,
            Random rng,
            RandomizerSettings settings)
        {
            var bigKeys         = chestLocs.Where(l => l.OriginalItemId == 0x11).ToList();
            var keys            = chestLocs.Where(l => l.OriginalItemId == 0x10).ToList();
            var moonPearls      = chestLocs.Where(l => l.OriginalItemId == 0x16).ToList();
            var heartContainers = chestLocs.Where(l => l.OriginalItemId == 0x14).ToList();
            var bigBombs        = chestLocs.Where(l => l.OriginalItemId == 0x2E).ToList();
            var blueBracelets   = chestLocs.Where(l => l.OriginalItemId == 0x15).ToList();
            var others          = chestLocs.Where(l =>
                l.OriginalItemId != 0x11 && l.OriginalItemId != 0x10 &&
                l.OriginalItemId != 0x16 && l.OriginalItemId != 0x14 &&
                l.OriginalItemId != 0x2E && l.OriginalItemId != 0x15).ToList();

            var globalPool = new List<ItemLocation>(others);

            if (settings.ShuffleKeys)
            {
                if (bigKeys.Count > 0)
                {
                    if (settings.BigKeysInOwnLevel || settings.EnsureBeatable)
                        ShuffleWithinLevels(bigKeys, rng);
                    else
                        globalPool.AddRange(bigKeys);
                }

                // Small keys: level constraint or global pool
                if (keys.Count > 0)
                {
                    if (settings.KeysInOwnLevel || settings.EnsureBeatable)
                        ShuffleWithinLevels(keys, rng);
                    else
                        globalPool.AddRange(keys);
                }
            }
            // When ShuffleKeys = false, keys are left out of globalPool and retain OriginalItemId

            ApplyLevelConstraints(new List<(List<ItemLocation>, bool)>
            {
                (moonPearls,      settings.MoonPearlInOwnLevel      || settings.EnsureBeatable),
                (heartContainers, settings.HeartContainerInOwnLevel),
                (bigBombs,        settings.BigBombInOwnLevel),
                (blueBracelets,   settings.BlueBraceletInOwnLevel),
            }, globalPool, rng);

            ShuffleItemIds(globalPool, rng);
        }

        private static void ShuffleItemIds(List<ItemLocation> pool, Random rng)
        {
            if (pool.Count == 0) return;
            var ids = pool.Select(l => l.OriginalItemId).ToArray();
            FisherYates(ids, rng);
            for (int i = 0; i < pool.Count; i++)
                pool[i].RandomizedItemId = ids[i];
        }

        private static void ShuffleWithinLevels(List<ItemLocation> pool, Random rng)
        {
            foreach (var levelGroup in pool.GroupBy(l => l.LevelId))
                ShuffleItemIds(levelGroup.ToList(), rng);
        }

        private static void ShuffleFloorItems(
            List<ItemLocation> floorLocs,
            Random rng,
            RandomizerSettings settings)
        {
            // Floor item IDs: Moon Pearl=0x06, Heart Container=0x04, Blue Bracelet=0x05
            // Big Bomb has no floor-item equivalent
            var moonPearls      = floorLocs.Where(l => l.OriginalItemId == 0x06).ToList();
            var heartContainers = floorLocs.Where(l => l.OriginalItemId == 0x04).ToList();
            var blueBracelets   = floorLocs.Where(l => l.OriginalItemId == 0x05).ToList();
            var globalPool      = floorLocs.Where(l =>
                l.OriginalItemId != 0x06 &&
                l.OriginalItemId != 0x04 &&
                l.OriginalItemId != 0x05).ToList();

            ApplyLevelConstraints(new List<(List<ItemLocation>, bool)>
            {
                (moonPearls,      settings.MoonPearlInOwnLevel || settings.EnsureBeatable),
                (heartContainers, settings.HeartContainerInOwnLevel),
                (blueBracelets,   settings.BlueBraceletInOwnLevel),
            }, globalPool, rng);

            ShuffleItemIds(globalPool, rng);
        }

        // Gathers all constrained item types together before doing the level-local shuffle,
        // so multiple constrained types in the same level share one pool rather than each
        // draining it sequentially and leaving subsequent types with nothing to swap with.
        private static void ApplyLevelConstraints(
            List<(List<ItemLocation> Items, bool Constrained)> groups,
            List<ItemLocation> globalPool,
            Random rng)
        {
            var allConstrained = new List<ItemLocation>();
            foreach (var (items, constrained) in groups)
            {
                if (items.Count == 0) continue;
                if (constrained) allConstrained.AddRange(items);
                else             globalPool.AddRange(items);
            }

            foreach (var levelGroup in allConstrained.GroupBy(l => l.LevelId))
            {
                var levelConstrained = levelGroup.ToList();
                var levelGlobal = globalPool.Where(l => l.LevelId == levelGroup.Key).ToList();
                foreach (var loc in levelGlobal) globalPool.Remove(loc);
                ShuffleItemIds(levelConstrained.Concat(levelGlobal).ToList(), rng);
            }
        }

        private static void FisherYates<T>(T[] array, Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        // ── Spoiler log ───────────────────────────────────────────────────────

        public SpoilerLog BuildSpoilerLog(
            List<ItemLocation> locations,
            RandomizerSettings settings)
        {
            var excludedChestIds = BuildExcludedChestIds(settings);
            var excludedFloorIds = BuildExcludedFloorIds(settings);

            return new SpoilerLog
            {
                Seed = settings.Seed,
                Settings = settings,
                Locations = locations
                    .Where(l =>
                        (l.ActorType == "TKRA" && settings.ShuffleChestItems
                            && !excludedChestIds.Contains(l.OriginalItemId)) ||
                        (l.ActorType == "KEY0" && settings.ShuffleFloorKeyItems
                            && !excludedFloorIds.Contains(l.OriginalItemId)))
                    .OrderBy(l => l.WorldName)
                    .ThenBy(l => l.LevelName)
                    .ThenBy(l => l.RoomIndex)
                    .Select(l => new SpoilerEntry
                    {
                        World = l.WorldName,
                        Level = l.LevelName,
                        Room = l.RoomIndex,
                        Position = $"({l.X},{l.Y}) L{l.Layer}",
                        Type = l.ActorTypeName,
                        OriginalItem = l.OriginalItemName,
                        NewItem = l.RandomizedItemName,
                        Changed = l.IsRandomized,
                    })
                    .ToList()
            };
        }

        public void ExportSpoilerLog(SpoilerLog log, string path)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(log, options);
            File.WriteAllText(path, json);
        }

        // ── JSON export / import ─────────────────────────────────────────────

        public void ExportJson(List<ItemLocation> locations, RandomizerSettings settings, string path)
        {
            var entries = locations.Select(l => new ItemLocationEntry
            {
                Key              = l.Key,
                LevelId          = l.LevelId,
                LevelName        = l.LevelName,
                WorldName        = l.WorldName,
                Room             = l.RoomIndex,
                ActorIndex       = l.ActorIndex,
                Type             = l.ActorType,
                Position         = $"({l.X},{l.Y}) L{l.Layer}",
                OriginalItemId   = l.OriginalItemId,
                OriginalItem     = l.OriginalItemName,
                NewItemId        = l.RandomizedItemId,
                NewItem          = l.RandomizedItemName,
            }).ToList();

            var file = new RandomizerExportFile { Settings = settings, Locations = entries };
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(file, options));
        }

        /// <summary>
        /// Applies item locations from a JSON file. Returns the saved settings if present
        /// (new format), or null for old flat-array files (backward compatible).
        /// </summary>
        public RandomizerSettings? ImportJson(string path, List<ItemLocation> locations)
        {
            var json = File.ReadAllText(path);

            List<ItemLocationEntry>? entries = null;
            RandomizerSettings? settings = null;

            // Try new wrapped format first
            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var file = JsonSerializer.Deserialize<RandomizerExportFile>(json);
                    entries  = file?.Locations;
                    settings = file?.Settings;
                }
            }

            // Fall back to old flat-array format
            if (entries == null)
                entries = JsonSerializer.Deserialize<List<ItemLocationEntry>>(json);

            if (entries == null) return null;

            var byKey = locations.ToDictionary(l => l.Key);
            foreach (var entry in entries)
            {
                if (!byKey.TryGetValue(entry.Key, out var match)) continue;
                match.RandomizedItemId   = entry.NewItemId;
                match.RandomizedItemName = match.ActorType == "TKRA"
                    ? GameData.GetChestItemName(entry.NewItemId)
                    : GameData.GetFloorItemName(entry.NewItemId);
            }

            return settings;
        }

        // ── CSV export / import ───────────────────────────────────────────────

        public void ExportCsv(List<ItemLocation> locations, string path)
        {
            using var w = new StreamWriter(path, append: false, System.Text.Encoding.UTF8);
            w.WriteLine("LevelId,LevelName,World,Room,X,Y,Layer,ActorType,OriginalItemId,OriginalItem,RandomizedItemId,RandomizedItem");
            foreach (var loc in locations)
            {
                w.WriteLine(
                    $"{loc.LevelId},{CsvEsc(loc.LevelName)},{CsvEsc(loc.WorldName)},{loc.RoomIndex},{loc.X},{loc.Y},{loc.Layer}," +
                    $"{loc.ActorType},0x{loc.OriginalItemId:X2},{CsvEsc(loc.OriginalItemName)},0x{loc.RandomizedItemId:X2},{CsvEsc(loc.RandomizedItemName)}");
            }
        }

        public void ImportCsv(string path, List<ItemLocation> locations)
        {
            using var r = new StreamReader(path);
            string? header = r.ReadLine(); // skip header
            while (!r.EndOfStream)
            {
                string? line = r.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 12) continue;

                string levelId = parts[0].Trim();
                if (!int.TryParse(parts[3].Trim(), out int room)) continue;
                if (!TryParseHex(parts[8].Trim(), out byte origId)) continue;
                if (!TryParseHex(parts[10].Trim(), out byte randId)) continue;

                // Find matching location
                var match = locations.FirstOrDefault(l =>
                    l.LevelId == levelId &&
                    l.RoomIndex == room &&
                    l.OriginalItemId == origId);

                if (match != null)
                {
                    match.RandomizedItemId = randId;
                    match.RandomizedItemName = GameData.GetChestItemName(randId);
                }
            }
        }

        private static string CsvEsc(string s) =>
            s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        private static bool TryParseHex(string s, out byte value)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            return byte.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        // ── Key balance check ─────────────────────────────────────────────────

        /// <summary>
        /// Compares original vs current small-key and big-key counts per level.
        /// A level with fewer keys than it started with may be impossible to complete
        /// (locked doors without enough keys to open them).
        /// </summary>
        public KeyBalanceReport CheckKeyBalance(List<ItemLocation> locations)
        {
            // TKRA chest small key = 0x10, big key = 0x11
            // KEY0 floor small key = 0x00, big key = 0x01  (different ID spaces — must check ActorType)
            var origSmall = locations
                .Where(l => (l.ActorType == "TKRA" && l.OriginalItemId == 0x10) ||
                            (l.ActorType == "KEY0" && l.OriginalItemId == 0x00))
                .GroupBy(l => l.LevelId)
                .ToDictionary(g => g.Key, g => g.Count());

            var origBig = locations
                .Where(l => (l.ActorType == "TKRA" && l.OriginalItemId == 0x11) ||
                            (l.ActorType == "KEY0" && l.OriginalItemId == 0x01))
                .GroupBy(l => l.LevelId)
                .ToDictionary(g => g.Key, g => g.Count());

            var curSmall = locations
                .Where(l => (l.ActorType == "TKRA" && l.RandomizedItemId == 0x10) ||
                            (l.ActorType == "KEY0" && l.RandomizedItemId == 0x00))
                .GroupBy(l => l.LevelId)
                .ToDictionary(g => g.Key, g => g.Count());

            var curBig = locations
                .Where(l => (l.ActorType == "TKRA" && l.RandomizedItemId == 0x11) ||
                            (l.ActorType == "KEY0" && l.RandomizedItemId == 0x01))
                .GroupBy(l => l.LevelId)
                .ToDictionary(g => g.Key, g => g.Count());

            var warnings = new List<string>();

            foreach (var (levelId, need) in origSmall)
            {
                curSmall.TryGetValue(levelId, out int have);
                if (have < need)
                    warnings.Add($"{GameData.GetLevelName(levelId)}: {have}/{need} small keys");
            }

            foreach (var (levelId, need) in origBig)
            {
                curBig.TryGetValue(levelId, out int have);
                if (have < need)
                    warnings.Add($"{GameData.GetLevelName(levelId)}: {have}/{need} big keys");
            }

            return new KeyBalanceReport(warnings, origSmall, curSmall, origBig, curBig);
        }
    }

    public class SpoilerLog
    {
        public int Seed { get; set; }
        public RandomizerSettings? Settings { get; set; }
        public List<SpoilerEntry> Locations { get; set; } = new();
    }

    public class RandomizerExportFile
    {
        public RandomizerSettings? Settings  { get; set; }
        public List<ItemLocationEntry>? Locations { get; set; }
    }

    public class ItemLocationEntry
    {
        public string Key          { get; set; } = "";
        public string LevelId      { get; set; } = "";
        public string LevelName    { get; set; } = "";
        public string WorldName    { get; set; } = "";
        public int    Room         { get; set; }
        public int    ActorIndex   { get; set; }
        public string Type         { get; set; } = "";
        public string Position     { get; set; } = "";
        public byte   OriginalItemId { get; set; }
        public string OriginalItem { get; set; } = "";
        public byte   NewItemId    { get; set; }
        public string NewItem      { get; set; } = "";
    }

    public class SpoilerEntry
    {
        public string World { get; set; } = "";
        public string Level { get; set; } = "";
        public int Room { get; set; }
        public string Position { get; set; } = "";
        public string Type { get; set; } = "";
        public string OriginalItem { get; set; } = "";
        public string NewItem { get; set; } = "";
        public bool Changed { get; set; } = true;
    }

    /// <summary>
    /// Result of <see cref="RandomizerService.CheckKeyBalance"/>.
    /// Warnings lists every level whose key count fell below the original.
    /// The dictionaries give full original vs. current counts for all levels.
    /// </summary>
    public class KeyBalanceReport
    {
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyDictionary<string, int> OriginalSmallKeys { get; }
        public IReadOnlyDictionary<string, int> CurrentSmallKeys  { get; }
        public IReadOnlyDictionary<string, int> OriginalBigKeys   { get; }
        public IReadOnlyDictionary<string, int> CurrentBigKeys    { get; }

        public bool IsBalanced => Warnings.Count == 0;

        public KeyBalanceReport(
            List<string> warnings,
            Dictionary<string, int> origSmall,
            Dictionary<string, int> curSmall,
            Dictionary<string, int> origBig,
            Dictionary<string, int> curBig)
        {
            Warnings          = warnings;
            OriginalSmallKeys = origSmall;
            CurrentSmallKeys  = curSmall;
            OriginalBigKeys   = origBig;
            CurrentBigKeys    = curBig;
        }
    }
}
