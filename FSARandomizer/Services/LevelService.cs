using FSALib;
using FSALib.Structs;
using FSARandomizer.Archive;
using FSARandomizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FSARandomizer.Services
{
    /// <summary>
    /// Discovers and loads FSA level data from:
    ///   - A folder of extracted level directories (boss010/, boss011/, …)
    ///   - A Boss/ folder containing boss*.arc archives
    /// </summary>
    public class LevelService
    {
        // ── Open ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Detect what kind of folder the user opened and return LoadedGame.
        /// </summary>
        public LoadedGame OpenDirectory(string path, IProgress<string>? progress = null)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException(path);

            var game = new LoadedGame(path);

            // Check for .arc files → Boss/ directory
            var arcFiles = Directory.GetFiles(path, "boss*.arc");
            if (arcFiles.Length > 0)
            {
                progress?.Report("Found .arc files – extracting to temp directory…");
                string tempDir = Path.Combine(Path.GetTempPath(), "FSARandomizer", "extracted");
                Directory.CreateDirectory(tempDir);
                game.TempExtractDirectory = tempDir;

                foreach (var arc in arcFiles.OrderBy(f => f))
                {
                    string id = GetLevelId(Path.GetFileNameWithoutExtension(arc));
                    if (id == "") continue;
                    string outDir = Path.Combine(tempDir, $"boss{id}");

                    // Always wipe and re-extract so stale temp dirs never block fresh data
                    if (Directory.Exists(outDir))
                        Directory.Delete(outDir, recursive: true);

                    progress?.Report($"Extracting boss{id}.arc…");
                    try
                    {
                        var archive = RarcArchive.Load(arc);
                        archive.ExtractTo(outDir);
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"  Warning: boss{id}.arc – {ex.Message}");
                        continue;
                    }

                    var level = LoadExtractedLevel(outDir, id, arc);
                    if (level != null) game.Levels.Add(level);
                    else progress?.Report($"  Warning: boss{id} extracted but map{id}.csv not found in map/ – skipped.");
                }
            }
            else
            {
                // Assume pre-extracted directories
                foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
                {
                    string dirName = Path.GetFileName(dir);
                    // Accept directories named "boss010", "(Level 1-1) Lake Hylia", etc.
                    string id = ExtractIdFromDirName(dirName);
                    if (id == "") continue;
                    progress?.Report($"Loading boss{id}…");
                    var level = LoadExtractedLevel(dir, id, null);
                    if (level != null) game.Levels.Add(level);
                }
            }

            return game;
        }

        // ── Load single extracted level ───────────────────────────────────────

        public LoadedLevel? LoadExtractedLevel(string dir, string id, string? arcPath)
        {
            string mapDir = Path.Combine(dir, "map");
            if (!Directory.Exists(mapDir)) return null;

            if (!int.TryParse(id, out int mapIndex)) return null;

            // Try multiplayer map first, fall back to singleplayer
            string mapFile = Map.GetFilePath(dir, mapIndex, singleplayer: false);
            if (!File.Exists(mapFile))
                mapFile = Map.GetFilePath(dir, mapIndex, singleplayer: true);
            if (!File.Exists(mapFile)) return null;

            Map map;
            using (var fs = File.OpenRead(mapFile))
                map = new Map(fs);

            var info = GameData.Levels.TryGetValue(id, out var li) ? li
                       : new LevelInfo($"Level {id}", -1, "Unknown");

            var level = new LoadedLevel
            {
                Id = id,
                MapIndex = mapIndex,
                Name = info.Name,
                WorldName = info.WorldName,
                WorldId = info.WorldId,
                SectionLabel = info.SectionLabel,
                BaseDirectory = dir,
                SourceArcPath = arcPath,
                Map = map,
            };

            // Load all rooms that exist on the map
            for (int y = 0; y < map.YDimension; y++)
            {
                for (int x = 0; x < map.XDimension; x++)
                {
                    int roomIdx = map[x, y];
                    if (roomIdx == Map.EMPTY_ROOM_VALUE) continue;
                    if (level.Rooms.Any(r => r.RoomIndex == roomIdx)) continue;

                    string binPath = ActorList.GetFilePath(dir, level.MapIndex, roomIdx);
                    if (!File.Exists(binPath)) continue;

                    var actors = new ActorList();
                    using (var fs = File.OpenRead(binPath))
                        actors.BinaryDeserialize(fs);

                    level.Rooms.Add(new LoadedRoom
                    {
                        RoomIndex = roomIdx,
                        MapX = x,
                        MapY = y,
                        Actors = actors,
                        ActorFilePath = binPath,
                    });
                }
            }

            return level;
        }

        // ── Open ISO ─────────────────────────────────────────────────────────

        /// <summary>
        /// Load levels directly from a GameCube disc image (.iso / .gcm).
        /// The Boss/ folder inside the disc is extracted to a temp directory.
        /// </summary>
        public LoadedGame OpenIso(string isoPath, IProgress<string>? progress = null)
        {
            var game = new LoadedGame(isoPath);

            progress?.Report("Opening GameCube disc image…");
            using var gcm = GcmReader.Open(isoPath);

            // Boss arc files live at GC4Sword_usa/Boss/boss*.arc
            const string bossDir = "GC4Sword_usa/Boss";
            var arcEntries = gcm.ListFiles(bossDir)
                .Where(e => e.Name.StartsWith("boss", StringComparison.OrdinalIgnoreCase)
                         && e.Name.EndsWith(".arc", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Name)
                .ToList();

            if (arcEntries.Count == 0)
                throw new InvalidDataException($"No boss*.arc files found at '{bossDir}' inside the disc image.");

            progress?.Report($"Found {arcEntries.Count} boss*.arc files in disc image.");

            string tempDir = Path.Combine(Path.GetTempPath(), "FSARandomizer", "iso_extracted",
                                          Path.GetFileNameWithoutExtension(isoPath));
            game.TempExtractDirectory = tempDir;

            foreach (var entry in arcEntries)
            {
                string id = GetLevelId(Path.GetFileNameWithoutExtension(entry.Name));
                if (id == "") continue;

                string outDir = Path.Combine(tempDir, $"boss{id}");
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, recursive: true);

                progress?.Report($"Extracting boss{id}.arc from ISO…");
                try
                {
                    using var arcStream = gcm.OpenFile(entry);
                    var archive = RarcArchive.Load(arcStream);
                    archive.ExtractTo(outDir);
                }
                catch (Exception ex)
                {
                    progress?.Report($"  Warning: boss{id}.arc – {ex.Message}");
                    continue;
                }

                var level = LoadExtractedLevel(outDir, id, arcPath: null);
                if (level != null) game.Levels.Add(level);
                else progress?.Report($"  Warning: boss{id} extracted but map{id}.csv not found – skipped.");
            }

            return game;
        }

        // ── Save ─────────────────────────────────────────────────────────────

        /// <summary>Save modified actor lists back to the extracted level directory.</summary>
        public void SaveRoom(LoadedRoom room)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(room.ActorFilePath)!);
            using var fs = File.Create(room.ActorFilePath);
            room.Actors.BinarySerialize(fs);
        }

        /// <summary>
        /// Re-pack an extracted level directory back into its .arc source file.
        /// Only works when the level was originally opened from a .arc archive.
        /// </summary>
        public void RepackLevel(LoadedLevel level)
        {
            if (level.SourceArcPath == null)
                throw new InvalidOperationException("No source .arc path recorded – level was opened from an extracted directory.");

            var archive = RarcArchive.FromDirectory(level.BaseDirectory);
            archive.Save(level.SourceArcPath, compress: true);
        }

        // ── Export ISO ───────────────────────────────────────────────────────

        private static string[] s_worldStems => DolPatcherService.WorldStems;
        private const int ShuffleableWorlds = 8;
        private const int StagesPerWorld    = 4;

        /// <summary>
        /// Build a new .iso with all modified levels repacked as .arc and injected back.
        /// Only valid when the game was originally opened from an .iso.
        /// </summary>
        /// <param name="stagePerm">
        /// Optional 32-element stage permutation (8 worlds × 4 stages): stagePerm[i] = which
        /// original stage-index's content goes in target slot i. Both world-shuffle and
        /// stage-shuffle use this format. File content is physically swapped and internal RARC
        /// entries are renamed so the game's path lookups remain consistent.
        /// </param>
        public void ExportIso(LoadedGame game, string outputIsoPath,
                              int[]? stagePerm = null,
                              IProgress<string>? progress = null)
        {
            // Determine source ISO path
            string sourcePath = game.SourceDirectory;
            if (!File.Exists(sourcePath))
                throw new InvalidOperationException(
                    "Cannot export ISO: the game was not opened from a disc image.");

            // Never overwrite the source
            if (string.Equals(Path.GetFullPath(outputIsoPath), Path.GetFullPath(sourcePath),
                              StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Output path is the same as the source ISO. Choose a different location.");

            // Repack all modified levels into temp .arc bytes
            var replacements = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            const string bossDir = "GC4Sword_usa/Boss";

            foreach (var level in game.Levels.Where(l => l.BaseDirectory.Length > 0))
            {
                var archive = RarcArchive.FromDirectory(level.BaseDirectory);
                using var ms = new MemoryStream();
                archive.Save(ms, compress: true);
                replacements[$"{bossDir}/boss{level.Id}.arc"] = ms.ToArray();
            }

            // Apply level-order shuffle: physically swap file content between stage slots and
            // rename internal RARC entries (bin/b{src}/, map/map{src}.csv, etc.) so the game's
            // path lookups (derived from the target filename) still match.
            int totalStages = ShuffleableWorlds * StagesPerWorld;
            if (stagePerm != null && stagePerm.Length == totalStages)
            {
                progress?.Report("Reading stage files for level order shuffle…");

                // Ensure every shuffleable file's bytes are in the replacements map.
                // Item-randomized ones are already there; others come from the source ISO.
                using var reader = GcmReader.Open(sourcePath);
                for (int i = 0; i < totalStages; i++)
                {
                    string path = $"{bossDir}/boss{s_worldStems[i]}.arc";
                    if (!replacements.ContainsKey(path))
                    {
                        var entry = reader.FindFile(path);
                        if (entry != null)
                            replacements[path] = reader.ReadFile(entry);
                    }
                }

                // Build shuffled map: target slot i gets content from source slot stagePerm[i].
                var shuffled = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < totalStages; i++)
                {
                    string targetStem = s_worldStems[i];
                    string srcStem    = s_worldStems[stagePerm[i]];
                    string targetPath = $"{bossDir}/boss{targetStem}.arc";
                    string srcPath2   = $"{bossDir}/boss{srcStem}.arc";
                    if (replacements.TryGetValue(srcPath2, out var content))
                        shuffled[targetPath] = RenameArchiveEntries(content, srcStem, targetStem);
                }

                foreach (var kv in shuffled)
                    replacements[kv.Key] = kv.Value;

                // Log each changed slot
                for (int i = 0; i < totalStages; i++)
                {
                    if (stagePerm[i] != i)
                        progress?.Report($"  boss{s_worldStems[i]} <- boss{s_worldStems[stagePerm[i]]}");
                }
            }

            progress?.Report($"Injecting {replacements.Count} modified level(s) into new ISO…");
            GcmWriter.ReplaceFiles(sourcePath, outputIsoPath, replacements, progress);
        }

        // ── RARC rename helpers ───────────────────────────────────────────────

        private static byte[] RenameArchiveEntries(byte[] arcBytes, string fromStem, string toStem)
        {
            if (fromStem == toStem) return arcBytes;
            var archive = RarcArchive.Load(new MemoryStream(arcBytes));
            RenameDirectory(archive.Root, fromStem, toStem);
            using var ms = new MemoryStream();
            archive.Save(ms, compress: true);
            return ms.ToArray();
        }

        private static void RenameDirectory(RarcDirectory dir, string from, string to)
        {
            if (dir.Name.Contains(from, StringComparison.Ordinal))
                dir.Name = dir.Name.Replace(from, to, StringComparison.Ordinal);
            foreach (var subdir in dir.Directories)
                RenameDirectory(subdir, from, to);
            foreach (var file in dir.Files)
                if (file.Name.Contains(from, StringComparison.Ordinal))
                    file.Name = file.Name.Replace(from, to, StringComparison.Ordinal);
        }

        // ── Item location scanning ────────────────────────────────────────────

        /// <summary>
        /// Scan all loaded levels and return a flat list of item locations
        /// (chests = TKRA, floor items = KEY0).
        /// </summary>
        public List<ItemLocation> FindItemLocations(LoadedGame game)
        {
            var locations = new List<ItemLocation>();

            foreach (var level in game.Levels)
            {
                foreach (var room in level.Rooms)
                {
                    for (int i = 0; i < room.Actors.Count; i++)
                    {
                        var actor = room.Actors[i];
                        string actorId = actor.Name.Trim();

                        if (actorId == "TKRA") // Treasure chest
                        {
                            byte itemId = actor.VariableByte1;
                            locations.Add(new ItemLocation
                            {
                                LevelId = level.Id,
                                LevelName = level.Name,
                                WorldName = level.WorldName,
                                SectionLabel = level.SectionLabel,
                                MapIndex = level.MapIndex,
                                RoomIndex = room.RoomIndex,
                                ActorIndex = i,
                                ActorType = actorId,
                                ActorTypeName = "Treasure Chest",
                                X = actor.XCoord,
                                Y = actor.YCoord,
                                Layer = actor.Layer,
                                OriginalItemId = itemId,
                                RandomizedItemId = itemId,
                                OriginalItemName = GameData.GetChestItemName(itemId),
                                RandomizedItemName = GameData.GetChestItemName(itemId),
                                FilePath = room.ActorFilePath,
                            });
                        }
                        else if (actorId == "KEY0") // Floor item pickup
                        {
                            byte itemId = actor.VariableByte1;
                            locations.Add(new ItemLocation
                            {
                                LevelId = level.Id,
                                LevelName = level.Name,
                                WorldName = level.WorldName,
                                SectionLabel = level.SectionLabel,
                                MapIndex = level.MapIndex,
                                RoomIndex = room.RoomIndex,
                                ActorIndex = i,
                                ActorType = actorId,
                                ActorTypeName = "Floor Item",
                                X = actor.XCoord,
                                Y = actor.YCoord,
                                Layer = actor.Layer,
                                OriginalItemId = itemId,
                                RandomizedItemId = itemId,
                                OriginalItemName = GameData.GetFloorItemName(itemId),
                                RandomizedItemName = GameData.GetFloorItemName(itemId),
                                FilePath = room.ActorFilePath,
                            });
                        }
                    }
                }
            }

            return locations;
        }

        /// <summary>
        /// Write randomized item IDs back to the actor binary files.
        /// </summary>
        public void ApplyItemLocations(LoadedGame game, IEnumerable<ItemLocation> locations)
        {
            foreach (var loc in locations)
            {
                var level = game.Levels.FirstOrDefault(l => l.Id == loc.LevelId);
                if (level == null) continue;

                var room = level.Rooms.FirstOrDefault(r => r.RoomIndex == loc.RoomIndex);
                if (room == null) continue;

                if (loc.ActorIndex >= room.Actors.Count) continue;

                var actor = room.Actors[loc.ActorIndex];
                actor.VariableByte1 = loc.RandomizedItemId;
                room.Actors[loc.ActorIndex] = actor;
                room.IsDirty = true;
            }

            // Save all dirty rooms
            foreach (var level in game.Levels)
            {
                foreach (var room in level.Rooms)
                {
                    if (room.IsDirty)
                    {
                        SaveRoom(room);
                        room.IsDirty = false;
                    }
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetLevelId(string filename)
        {
            // "boss010" → "010"
            if (filename.StartsWith("boss", StringComparison.OrdinalIgnoreCase))
                return filename.Substring(4);
            return "";
        }

        private static string ExtractIdFromDirName(string name)
        {
            // "boss010" → "010"
            if (name.StartsWith("boss", StringComparison.OrdinalIgnoreCase) && name.Length >= 7)
                return name.Substring(4, 3);

            // "(Level 1-1) Lake Hylia" – match by known level names
            foreach (var (id, info) in GameData.Levels)
                if (name.Contains(info.Name, StringComparison.OrdinalIgnoreCase))
                    return id;

            return "";
        }
    }

    // ── Data containers ───────────────────────────────────────────────────────

    public class LoadedGame
    {
        public string SourceDirectory { get; }
        public string? TempExtractDirectory { get; set; }
        public List<LoadedLevel> Levels { get; } = new();
        public bool IsModified => Levels.Any(l => l.IsModified);

        public LoadedGame(string sourceDir) => SourceDirectory = sourceDir;
    }

    public class LoadedLevel
    {
        public string Id { get; set; } = "";
        public int MapIndex { get; set; }
        public string Name { get; set; } = "";
        public string WorldName { get; set; } = "";
        public int WorldId { get; set; }
        public string SectionLabel { get; set; } = "";
        public string BaseDirectory { get; set; } = "";
        public string? SourceArcPath { get; set; }
        public Map Map { get; set; } = new Map();
        public List<LoadedRoom> Rooms { get; } = new();
        public bool IsModified => Rooms.Any(r => r.IsDirty);
    }

    public class LoadedRoom
    {
        public int RoomIndex { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        public ActorList Actors { get; set; } = new ActorList();
        public string ActorFilePath { get; set; } = "";
        public bool IsDirty { get; set; }
    }
}
