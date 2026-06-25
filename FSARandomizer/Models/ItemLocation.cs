namespace FSARandomizer.Models
{
    /// <summary>
    /// Represents a single item check (chest or floor item) in a level room.
    /// </summary>
    public class ItemLocation
    {
        /// <summary>Level ID string, e.g. "010" for boss010.</summary>
        public string LevelId { get; set; } = "";

        /// <summary>Human-readable level name.</summary>
        public string LevelName { get; set; } = "";

        /// <summary>World name (Whereabouts of the Wind, etc.).</summary>
        public string WorldName { get; set; } = "";

        /// <summary>In-game section label e.g. "1-1", "3-2". Empty for cut/connector levels.</summary>
        public string SectionLabel { get; set; } = "";

        /// <summary>Numeric map index, e.g. 10 for boss010.</summary>
        public int MapIndex { get; set; }

        /// <summary>Room index within the level.</summary>
        public int RoomIndex { get; set; }

        /// <summary>Actor index within the room's actor list.</summary>
        public int ActorIndex { get; set; }

        /// <summary>4-char actor type ID (TKRA, KEY0, etc.).</summary>
        public string ActorType { get; set; } = "";

        /// <summary>Human-readable actor type name.</summary>
        public string ActorTypeName { get; set; } = "";

        /// <summary>Grid X coordinate (in half-tiles).</summary>
        public byte X { get; set; }

        /// <summary>Grid Y coordinate (in half-tiles).</summary>
        public byte Y { get; set; }

        /// <summary>Layer the actor is on.</summary>
        public byte Layer { get; set; }

        /// <summary>Original item variable byte (before randomization).</summary>
        public byte OriginalItemId { get; set; }

        /// <summary>Randomized item variable byte.</summary>
        public byte RandomizedItemId { get; set; }

        /// <summary>Human-readable name of the original item.</summary>
        public string OriginalItemName { get; set; } = "";

        /// <summary>Human-readable name of the randomized item.</summary>
        public string RandomizedItemName { get; set; } = "";

        /// <summary>True if this location has been changed by the randomizer.</summary>
        public bool IsRandomized => OriginalItemId != RandomizedItemId;

        /// <summary>Path to the actor binary file on disk.</summary>
        public string FilePath { get; set; } = "";

        /// <summary>Unique key for stable identification across randomization runs.</summary>
        public string Key => $"{LevelId}_{RoomIndex:D2}_{ActorIndex:D3}";

        public override string ToString() =>
            $"{LevelName} R{RoomIndex} ({X},{Y}) {ActorTypeName}: {OriginalItemName}";
    }

    /// <summary>Settings controlling the randomization behaviour.</summary>
    public class RandomizerSettings
    {
        public int Seed { get; set; }

        public bool ShuffleChestItems { get; set; } = true;
        public bool ShuffleFloorKeyItems { get; set; } = false;
        public bool ShuffleKeys { get; set; } = false;
        public bool KeysInOwnLevel { get; set; } = true;
        public bool BigKeysInOwnLevel { get; set; } = true;
        public bool MoonPearlInOwnLevel { get; set; } = true;
        public bool HeartContainerInOwnLevel { get; set; } = true;
        public bool BigBombInOwnLevel { get; set; } = true;
        public bool BlueBraceletInOwnLevel { get; set; } = true;

        // Item inclusion toggles — when false the item stays in its original location
        public bool ShuffleMoonPearl { get; set; } = false;
        public bool ShuffleHeartContainers { get; set; } = false;
        public bool ShuffleBigBombs { get; set; } = false;
        public bool ShuffleBlueBracelet { get; set; } = false;
        public bool EquipmentIntoGems { get; set; } = false;
        public bool GemSanity { get; set; } = false;
        public bool ProgressiveSwords { get; set; } = false;
        public bool FormationsAsItems { get; set; } = false;

        // Game logic / beatability
        public bool EnsureBeatable      { get; set; } = false;

        // Level order (mutually exclusive — at most one should be true)
        public bool ShuffleLevelOrder   { get; set; } = false;
        public bool ShuffleStageOrder   { get; set; } = false;

        /// <summary>
        /// Explicit stage placement map: key = target slot (e.g. "boss010"),
        /// value = source content (e.g. "boss030"). When present, overrides seed-based
        /// generation. Intended for Archipelago integration where the server specifies
        /// exact placements. Partial maps are valid — unspecified slots stay as identity.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, string>? StagePlacements { get; set; }

        // Starting options
        public bool StartWithRandomItem { get; set; } = false;

        // Goal
        public GoalType Goal { get; set; } = GoalType.DefeatVaati;
        public int MaidensRequired { get; set; } = 7;
        public int GemsRequired { get; set; } = 2000;
    }

    public enum GoalType
    {
        DefeatVaati,
        DefeatGanon,
        AllDungeons,
    }
}
