using FSALib.Structs;
using FSARandomizer.Models;
using System.Collections.Generic;
using System.Linq;

namespace FSARandomizer.Services
{
    /// <summary>
    /// Builds a room-connectivity graph from FSA level data and runs a key-logic
    /// reachability check: can the player collect all keys before every locked door
    /// they need to pass through?
    /// </summary>
    public class LevelGraphService
    {
        // Cardinal directions with their (dx, dy) map-grid deltas
        private static readonly (int dx, int dy, DoorDirection dir)[] Directions =
        {
            ( 0, -1, DoorDirection.North),
            ( 0,  1, DoorDirection.South),
            (-1,  0, DoorDirection.West),
            ( 1,  0, DoorDirection.East),
        };

        // ── DOOR actor bit decoding ───────────────────────────────────────────
        // Variable bytes (first byte on disk = VariableByte1 = schema bitOffset 0-7):
        //   Door Type  : bitOffset  0, bitSize 7  →  VariableByte1 & 0x7F
        //   Direction  : bitOffset  7, bitSize 5  →  bit7(VB1) | bits0-3(VB2) << 1
        //   OpenedByVar: bitOffset 27, bitSize 5  →  (VariableByte4 >> 3) & 0x1F

        private static int GetDoorType(Actor a)
            => a.VariableByte1 & 0x7F;

        private static DoorDirection GetDoorDirection(Actor a)
            => (DoorDirection)(((a.VariableByte1 >> 7) & 1) | ((a.VariableByte2 & 0x0F) << 1));

        // Small key doors: type 1 (Key door), 3 (Key door House), 5 (1x1 Key block)
        private static bool IsSmallKeyDoor(int t) => t == 1 || t == 3 || t == 5;
        // Big key door: type 7 (Locked Big chest)
        private static bool IsBigKeyDoor(int t)   => t == 7;
        // Doors that need a key of any kind
        private static bool IsKeyDoor(int t)       => IsSmallKeyDoor(t) || IsBigKeyDoor(t);

        // ── Graph building ────────────────────────────────────────────────────

        public LevelGraph BuildGraph(LoadedLevel level, List<ItemLocation> levelItems)
        {
            var roomByCoord = level.Rooms.ToDictionary(r => (r.MapX, r.MapY));

            var graph = new LevelGraph
            {
                LevelId  = level.Id,
                LevelName = level.Name,
                AllRooms = level.Rooms.Select(r => r.RoomIndex).ToList(),
            };

            // Start room from the map header's StartX/StartY
            if (roomByCoord.TryGetValue((level.Map.StartX, level.Map.StartY), out var sr))
                graph.StartRoom = sr.RoomIndex;
            else if (graph.AllRooms.Count > 0)
                graph.StartRoom = graph.AllRooms[0]; // fallback: first room

            foreach (var r in level.Rooms)
            {
                graph.Edges[r.RoomIndex]       = new List<RoomEdge>();
                graph.ItemsByRoom[r.RoomIndex]  = new List<ItemLocation>();
                graph.RoomCoords[r.RoomIndex]   = (r.MapX, r.MapY);
            }

            // Assign items to their rooms
            foreach (var item in levelItems)
                if (graph.ItemsByRoom.ContainsKey(item.RoomIndex))
                    graph.ItemsByRoom[item.RoomIndex].Add(item);

            // Build directed edges ONLY where a DOOR actor explicitly exists.
            // FSA rooms are also connected via open tile passages (no actor), which we cannot model
            // from actor data alone.  We only track DOOR-actor transitions so we can detect
            // key-cycle blockages; rooms linked purely by tile passages are left out of the graph
            // and will never appear as "blocked by a locked door," which is the correct outcome.
            foreach (var room in level.Rooms)
            {
                foreach (var (dx, dy, dir) in Directions)
                {
                    int nx = room.MapX + dx, ny = room.MapY + dy;
                    if (!roomByCoord.TryGetValue((nx, ny), out var neighbor)) continue;

                    // Find the first DOOR actor in this room facing this direction
                    bool foundDoor = false;
                    DoorRequirement req = DoorRequirement.None;
                    for (int i = 0; i < room.Actors.Count; i++)
                    {
                        var actor = room.Actors[i];
                        if (actor.Name.Trim() != "DOOR") continue;

                        var actorDir = GetDoorDirection(actor);
                        if (actorDir != dir) continue;

                        foundDoor = true;
                        int doorType = GetDoorType(actor);
                        if (IsSmallKeyDoor(doorType))    req = DoorRequirement.SmallKey;
                        else if (IsBigKeyDoor(doorType)) req = DoorRequirement.BigKey;
                        // Closed/bombable/vine doors treated as free for key-logic purposes
                        break;
                    }

                    // Only model this edge if an explicit DOOR actor was found here.
                    // Tile-based open passages have no actor and are always accessible — skipping
                    // them avoids false "unreachable" reports for rooms connected that way.
                    if (!foundDoor) continue;

                    graph.Edges[room.RoomIndex].Add(new RoomEdge
                    {
                        FromRoom    = room.RoomIndex,
                        ToRoom      = neighbor.RoomIndex,
                        Requirement = req,
                        Direction   = dir,
                    });
                }
            }

            return graph;
        }

        // ── Reachability check ────────────────────────────────────────────────

        /// <summary>
        /// Forward-fill from the starting room.  Collects keys in reachable rooms,
        /// uses them to unlock doors, and expands reachability until no further
        /// progress can be made.  Any rooms still unreachable after convergence
        /// indicate a key-cycle or key-shortage.
        /// </summary>
        /// <param name="useOriginalIds">
        /// When true the BFS uses each item's OriginalItemId instead of RandomizedItemId.
        /// Pass true to establish a baseline of structural issues that exist even in the
        /// unrandomized game (e.g. rooms reachable via tile passages the graph can't see).
        /// </param>
        public ReachabilityReport CheckReachability(LevelGraph graph, bool useOriginalIds = false)
        {
            if (graph.StartRoom < 0 || graph.AllRooms.Count == 0)
                return new ReachabilityReport
                {
                    LevelId = graph.LevelId, LevelName = graph.LevelName,
                    IsFullyReachable = true, TotalRooms = 0, ReachableRooms = 0,
                };

            var reachable = new HashSet<int> { graph.StartRoom };
            var collected = new HashSet<int>(); // rooms whose items have been picked up
            int smallKeys = 0, bigKeys = 0;

            bool changed = true;
            while (changed)
            {
                changed = false;

                // Phase 1 — collect items from newly reachable rooms
                foreach (int room in reachable.Where(r => !collected.Contains(r)).ToList())
                {
                    collected.Add(room);
                    foreach (var item in graph.ItemsByRoom[room])
                    {
                        byte id = useOriginalIds ? item.OriginalItemId : item.RandomizedItemId;
                        bool isSmall = (item.ActorType == "TKRA" && id == 0x10)
                                    || (item.ActorType == "KEY0" && id == 0x00);
                        bool isBig   = (item.ActorType == "TKRA" && id == 0x11)
                                    || (item.ActorType == "KEY0" && id == 0x01);
                        if (isSmall) { smallKeys++; changed = true; }
                        if (isBig)   { bigKeys++;   changed = true; }
                    }
                }

                // Phase 2 — expand through free passages first (collect more items before spending keys)
                foreach (int room in reachable.ToList())
                    foreach (var edge in graph.Edges[room])
                        if (!reachable.Contains(edge.ToRoom) && edge.Requirement == DoorRequirement.None)
                        { reachable.Add(edge.ToRoom); changed = true; }

                // Phase 3 — use available keys to open locked doors
                foreach (int room in reachable.ToList())
                    foreach (var edge in graph.Edges[room])
                    {
                        if (reachable.Contains(edge.ToRoom)) continue;
                        if (edge.Requirement == DoorRequirement.SmallKey && smallKeys > 0)
                        { smallKeys--; reachable.Add(edge.ToRoom); changed = true; }
                        else if (edge.Requirement == DoorRequirement.BigKey && bigKeys > 0)
                        { reachable.Add(edge.ToRoom); changed = true; }
                    }
            }

            // Count how many keys of each type exist anywhere in this level (all rooms, including unreachable).
            // If a level has zero big keys, its big-key doors are opened by keys from a prior level
            // (e.g. boss stages get their big key from the preceding world stage).  These are not cycles.
            int totalSmallKeysInLevel = graph.AllRooms
                .SelectMany(r => graph.ItemsByRoom[r])
                .Count(item => { byte id = useOriginalIds ? item.OriginalItemId : item.RandomizedItemId;
                                 return (item.ActorType == "TKRA" && id == 0x10)
                                     || (item.ActorType == "KEY0" && id == 0x00); });
            int totalBigKeysInLevel = graph.AllRooms
                .SelectMany(r => graph.ItemsByRoom[r])
                .Count(item => { byte id = useOriginalIds ? item.OriginalItemId : item.RandomizedItemId;
                                 return (item.ActorType == "TKRA" && id == 0x11)
                                     || (item.ActorType == "KEY0" && id == 0x01); });

            var issues = new List<string>();
            foreach (int roomIdx in graph.AllRooms.Where(r => !reachable.Contains(r)))
            {
                // Find a key-locked edge from a reachable room into this one.
                // If none exists the room is either isolated in the grid (connected via tile passages
                // the actor data can't see) or not adjacent to anything reachable — not a key cycle.
                var blocker = graph.AllRooms
                    .Where(reachable.Contains)
                    .SelectMany(r => graph.Edges[r])
                    .FirstOrDefault(e => e.ToRoom == roomIdx && e.Requirement != DoorRequirement.None);

                if (blocker == null) continue; // Not a key-cycle — skip

                // If no keys of this type exist anywhere in the level, the door is meant to be
                // opened with a key carried in from a previous level — not a cycle.
                if (blocker.Requirement == DoorRequirement.BigKey   && totalBigKeysInLevel   == 0) continue;
                if (blocker.Requirement == DoorRequirement.SmallKey && totalSmallKeysInLevel == 0) continue;

                string pos = graph.RoomCoords.TryGetValue(roomIdx, out var c)
                    ? $"({c.x},{c.y})" : $"#{roomIdx}";
                string reqName = blocker.Requirement == DoorRequirement.BigKey ? "big key" : "small key";
                issues.Add($"Room {pos}: {reqName} door leads here but key is locked away");
            }

            return new ReachabilityReport
            {
                LevelId          = graph.LevelId,
                LevelName        = graph.LevelName,
                IsFullyReachable = issues.Count == 0,
                TotalRooms       = graph.AllRooms.Count,
                ReachableRooms   = reachable.Count,
                Issues           = issues,
            };
        }

        // ── Batch check ───────────────────────────────────────────────────────

        /// <summary>
        /// Build and check graphs for all levels in the game.
        /// Only returns reports for levels with reachability issues.
        /// </summary>
        public List<ReachabilityReport> CheckAllLevels(
            LoadedGame game, List<ItemLocation> items, bool useOriginalIds = false)
        {
            var reports = new List<ReachabilityReport>();
            foreach (var level in game.Levels)
            {
                var levelItems = items.Where(i => i.LevelId == level.Id).ToList();
                var report = CheckReachability(BuildGraph(level, levelItems), useOriginalIds);
                if (!report.IsFullyReachable)
                    reports.Add(report);
            }
            return reports;
        }
    }
}
