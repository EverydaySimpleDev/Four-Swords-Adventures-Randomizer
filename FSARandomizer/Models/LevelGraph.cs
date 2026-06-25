using System.Collections.Generic;

namespace FSARandomizer.Models
{
    public enum DoorRequirement { None, SmallKey, BigKey }
    public enum DoorDirection   { South = 0, North = 1, West = 2, East = 3 }

    public class RoomEdge
    {
        public int             FromRoom    { get; set; }
        public int             ToRoom      { get; set; }
        public DoorRequirement Requirement { get; set; }
        public DoorDirection   Direction   { get; set; }
    }

    public class LevelGraph
    {
        public string                              LevelId     { get; set; } = "";
        public string                              LevelName   { get; set; } = "";
        public int                                 StartRoom   { get; set; } = -1;
        // roomIndex → outgoing edges to adjacent rooms
        public Dictionary<int, List<RoomEdge>>     Edges       { get; set; } = new();
        // roomIndex → items available in that room (after randomization)
        public Dictionary<int, List<ItemLocation>> ItemsByRoom { get; set; } = new();
        // roomIndex → (mapX, mapY) for human-readable diagnostics
        public Dictionary<int, (int x, int y)>    RoomCoords  { get; set; } = new();
        public List<int>                           AllRooms    { get; set; } = new();
    }

    public class ReachabilityReport
    {
        public string       LevelId          { get; set; } = "";
        public string       LevelName        { get; set; } = "";
        public bool         IsFullyReachable { get; set; }
        public int          TotalRooms       { get; set; }
        public int          ReachableRooms   { get; set; }
        // Each entry is a user-readable description of a blocked room
        public List<string> Issues           { get; set; } = new();
    }
}
