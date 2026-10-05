using System.Collections.Generic;

namespace FSARandomizer.Models
{
    /// <summary>
    /// Central lookup tables for FSA game data: level names, item IDs, actor types.
    /// Data sourced from hex/Dolphin RAM analysis and actor templates.
    /// </summary>
    public static class GameData
    {
        // ── Level names keyed by numeric ID string (e.g. "010") ──────────────

        public static readonly IReadOnlyDictionary<string, LevelInfo> Levels =
            new Dictionary<string, LevelInfo>
            {
                // Each world has three playable stages: bossXX0, bossXX1 and bossXX3.
                // bossXX2 is a cut development stage in every world, so it has no section label.
                // Names cross-checked against EFSAdvent's FSALib/assets/stages.json.
                // World 1 – Whereabouts of the Wind
                ["010"] = new("Lake Hylia",                    0, "Whereabouts of the Wind", "1-1"),
                ["011"] = new("Cave of No Return",             0, "Whereabouts of the Wind", "1-2"),
                ["012"] = new("River Flow (cut)",              0, "Whereabouts of the Wind", ""),
                ["013"] = new("Hyrule Castle",                 0, "Whereabouts of the Wind", "1-3"),
                // World 2 – Eastern Hyrule
                ["020"] = new("The Coast",                     1, "Eastern Hyrule",           "2-1"),
                ["021"] = new("Village of the Blue Maiden",    1, "Eastern Hyrule",           "2-2"),
                ["022"] = new("Rainy Forest (cut)",            1, "Eastern Hyrule",           ""),
                ["023"] = new("Eastern Temple",                1, "Eastern Hyrule",           "2-3"),
                // World 3 – Death Mountain
                ["030"] = new("Death Mountain Foothills",      2, "Death Mountain",           "3-1"),
                ["031"] = new("The Mountain Path",             2, "Death Mountain",           "3-2"),
                ["032"] = new("Mountain Road (cut)",           2, "Death Mountain",           ""),
                ["033"] = new("Tower of Flames",               2, "Death Mountain",           "3-3"),
                // World 4 – Near the Fields
                ["040"] = new("The Field",                     3, "Near the Fields",          "4-1"),
                ["041"] = new("The Swamp",                     3, "Near the Fields",          "4-2"),
                ["042"] = new("Graveyard (cut)",               3, "Near the Fields",          ""),
                ["043"] = new("Infiltration of Hyrule Castle", 3, "Near the Fields",          "4-3"),
                // World 5 – The Dark World
                ["050"] = new("Lost Woods",                    4, "The Dark World",           "5-1"),
                ["051"] = new("Kakariko Village",              4, "The Dark World",           "5-2"),
                ["052"] = new("Four Descents into the Darkness (cut)", 4, "The Dark World",   ""),
                ["053"] = new("Temple of Darkness",            4, "The Dark World",           "5-3"),
                // World 6 – The Desert of Doubt
                ["060"] = new("Desert of Doubt",               5, "The Desert of Doubt",     "6-1"),
                ["061"] = new("Desert Temple",                 5, "The Desert of Doubt",     "6-2"),
                ["062"] = new("Oasis (cut)",                   5, "The Desert of Doubt",     ""),
                ["063"] = new("Pyramid",                       5, "The Desert of Doubt",     "6-3"),
                // World 7 – Frozen Hyrule
                ["070"] = new("Frozen Hyrule",                 6, "Frozen Hyrule",           "7-1"),
                ["071"] = new("Temple of Ice",                 6, "Frozen Hyrule",           "7-2"),
                ["072"] = new("Through the Blizzard (cut)",    6, "Frozen Hyrule",           ""),
                ["073"] = new("Tower of Winds",                6, "Frozen Hyrule",           "7-3"),
                // World 8 – Realm of the Heavens
                ["080"] = new("Realm of the Heavens",          7, "Realm of the Heavens",    "8-1"),
                ["081"] = new("The Dark Cloud",                7, "Realm of the Heavens",    "8-2"),
                ["082"] = new("Fortress of Winds (cut)",       7, "Realm of the Heavens",    ""),
                ["083"] = new("Palace of Winds",               7, "Realm of the Heavens",    "8-3"),
                // boss000–009 are development test / demo stages, not part of the final game
                // (boss003 does not exist in the ISO). Boss fights live inside each world's stage 3.
                ["000"] = new("Test-1",                          -1, "Test / Demo Stages", ""),
                ["001"] = new("Test-2",                          -1, "Test / Demo Stages", ""),
                ["002"] = new("Test-3",                          -1, "Test / Demo Stages", ""),
                ["004"] = new("ETCS-1",                          -1, "Test / Demo Stages", ""),
                ["005"] = new("ETCS-2",                          -1, "Test / Demo Stages", ""),
                ["006"] = new("ETCS-3",                          -1, "Test / Demo Stages", ""),
                ["007"] = new("E3-1",                            -1, "Test / Demo Stages", ""),
                ["008"] = new("E3-2",                            -1, "Test / Demo Stages", ""),
                ["009"] = new("Tingle's room / E3-3",            -1, "Test / Demo Stages", ""),
                ["500"] = new("Shadow Battle", -1, "Shadow Battle", ""),
            };

        public static string GetLevelName(string id) =>
            Levels.TryGetValue(id, out var info) ? info.Name : $"Level {id}";

        // ── Chest item IDs (TKRA VariableByte4) ──────────────────────────────
        // Source: analysis of the table at $802eb78c (Dolphin RAM)

        public static readonly IReadOnlyDictionary<byte, string> ChestItems =
            new Dictionary<byte, string>
            {
                [0x00] = "1 Force Gem",
                [0x01] = "5 Force Gems",
                [0x02] = "20 Force Gems",
                [0x03] = "50 Force Gems",
                [0x04] = "100 Force Gems",
                [0x05] = "150 Force Gems",
                [0x06] = "200 Force Gems",
                [0x07] = "300 Force Gems",
                [0x08] = "1000 Force Gems",
                [0x09] = "1500 Force Gems",
                [0x0A] = "2000 Force Gems",
                [0x0B] = "3000 Force Gems",
                [0x10] = "Small Key",
                [0x11] = "Big Key",
                [0x12] = "Letter",
                [0x13] = "Power Bracelet",
                [0x14] = "Heart Container",
                [0x15] = "Blue Bracelet",
                [0x16] = "Moon Pearl",
                [0x17] = "Bombos Medallion",
                [0x18] = "Quake Medallion",
                [0x19] = "Spellbook",
                [0x20] = "Crystal Ball",
                [0x2A] = "Blue Royal Jewel",
                [0x2B] = "Green Royal Jewel",
                [0x2C] = "Red Royal Jewel",
                [0x2D] = "Purple Royal Jewel",
                [0x2E] = "Big Bomb",
                [0x2F] = "Nothing",
                [0x30] = "Pegasus Boots",
                [0x31] = "Lamp",
                [0x32] = "Boomerang",
                [0x33] = "Bow",
                [0x34] = "Magic Hammer",
                [0x35] = "Fire Rod",
                [0x36] = "Roc's Feather",
                [0x37] = "Bombs",
                [0x38] = "Shovel",
                [0x39] = "Slingshot",
                [0x3A] = "Sword (Unused)",
                [0x40] = "Lv2 Pegasus Boots",
                [0x41] = "Lv2 Lamp",
                [0x42] = "Lv2 Boomerang",
                [0x43] = "Lv2 Bow",
                [0x44] = "Lv2 Magic Hammer",
                [0x45] = "Lv2 Fire Rod",
                [0x46] = "Lv2 Roc's Feather",
                [0x47] = "Lv2 Bombs",
                [0x48] = "Lv2 Shovel",
                [0x49] = "Lv2 Slingshot",
                [0x4A] = "Lv2 Sword (Unused)",
            };

        public static string GetChestItemName(byte id) =>
            ChestItems.TryGetValue(id, out var name) ? name : $"Item 0x{id:X2}";

        // ── Floor/pickup item IDs (KEY0 VariableByte4) ───────────────────────

        public static readonly IReadOnlyDictionary<byte, string> FloorItems =
            new Dictionary<byte, string>
            {
                [0x00] = "Small Key",
                [0x01] = "Big Key",
                [0x02] = "Letter",
                [0x03] = "Power Bracelet",
                [0x04] = "Heart Container",
                [0x05] = "Blue Bracelet",
                [0x06] = "Moon Pearl",
                [0x07] = "Bombos Medallion",
                [0x08] = "Quake Medallion",
                [0x09] = "Book",
                [0x0A] = "Small Key (alt)",
            };

        public static string GetFloorItemName(byte id)
        {
            if (FloorItems.TryGetValue(id, out var name)) return name;
            // Bit 7 = "Activated by Variable" flag (KEY0.json bitOffset 7).
            // Strip it and look up the base item type; conditional items spawn only when a room variable is set.
            if ((id & 0x80) != 0)
            {
                byte itemType = (byte)(id & 0x7F);
                if (FloorItems.TryGetValue(itemType, out var baseName))
                    return $"{baseName} [cond.]";
            }
            return $"Floor Item 0x{id:X2}";
        }

        // ── Equipment inventory item IDs (0x00-0x0A, seen in current item slots) ──

        public static readonly IReadOnlyDictionary<byte, string> InventoryItems =
            new Dictionary<byte, string>
            {
                [0x00] = "Nothing",
                [0x01] = "Pegasus Boots",
                [0x02] = "Lamp",
                [0x03] = "Boomerang",
                [0x04] = "Bow",
                [0x05] = "Hammer",
                [0x06] = "Fire Rod",
                [0x07] = "Roc's Feather",
                [0x08] = "Bombs",
                [0x09] = "Shovel",
                [0x0A] = "Slingshot",
            };

        // ── Formation item IDs ───────────────────────────────────────────────

        public static readonly IReadOnlyDictionary<byte, string> Formations =
            new Dictionary<byte, string>
            {
                [0x00] = "Line Formation",
                [0x01] = "Column Formation",
                [0x02] = "Box Formation",
                [0x03] = "Cross Formation",
                [0x04] = "Solo Formation",
                [0x05] = "Normal Formation",
            };

        // ── Actor type descriptions ───────────────────────────────────────────

        public static readonly IReadOnlyDictionary<string, ActorTypeInfo> ActorTypes =
            new Dictionary<string, ActorTypeInfo>
            {
                ["TKRA"] = new("Treasure Chest",   ActorCategory.Item,   "VariableByte4 = chest item ID"),
                ["KEY0"] = new("Floor Item",        ActorCategory.Item,   "VariableByte4 = item type (0=Key, 1=BigKey, 3=PowerBracelet...)"),
                ["RUPY"] = new("Force Gem",         ActorCategory.Item,   "VariableByte3/4 = gem type"),
                ["BRRY"] = new("Buried Force Gem",  ActorCategory.Item,   ""),
                ["ESRY"] = new("Escaping Gem",      ActorCategory.Item,   ""),
                ["HNRY"] = new("Hovering Gem",      ActorCategory.Item,   ""),
                ["WDRY"] = new("Wooden Force Gem",  ActorCategory.Item,   "VariableByte2=1 for ice"),
                ["FARY"] = new("Force Fairy",       ActorCategory.Item,   ""),
                ["PONT"] = new("Force Luck Hole",   ActorCategory.Item,   ""),
                ["GOLD"] = new("Force Stone",       ActorCategory.Item,   ""),
                ["NNJN"] = new("Carrot",            ActorCategory.Item,   ""),
                ["DOOR"] = new("Door",              ActorCategory.Object, ""),
                ["SWTH"] = new("Switch",            ActorCategory.Object, ""),
                ["MEDM"] = new("Eyeball Switch",    ActorCategory.Object, ""),
                ["PSWK"] = new("Chain Pull Switch", ActorCategory.Object, ""),
                ["ZNSW"] = new("Enemy Annihilation Switch", ActorCategory.Object, ""),
                ["FOCS"] = new("Focus Switch",      ActorCategory.Object, ""),
                ["GLBS"] = new("Global Switch",     ActorCategory.Object, ""),
                ["LCLS"] = new("Local Switch",      ActorCategory.Object, ""),
            };

        public static ActorTypeInfo GetActorInfo(string id) =>
            ActorTypes.TryGetValue(id, out var info) ? info : new ActorTypeInfo(id, ActorCategory.Unknown, "");

        // ── Randomizable item categories ─────────────────────────────────────

        public static readonly byte[] KeyItems = { 0x10, 0x11, 0x16 };                          // Small Key, Big Key, Moon Pearl
        public static readonly byte[] QuestItems = { 0x12, 0x13, 0x15, 0x17, 0x18, 0x19 };      // Letter, Bracelet, Medallions, Spellbook
        public static readonly byte[] EquipmentItems = { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39 };
        public static readonly byte[] GemItems = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
    }

    /// <param name="SectionLabel">In-game section number e.g. "1-1", "2-3". Empty for cut/connector levels.</param>
    public record LevelInfo(string Name, int WorldId, string WorldName, string SectionLabel = "");

    public record ActorTypeInfo(string DisplayName, ActorCategory Category, string Notes);

    public enum ActorCategory { Unknown, Item, Enemy, Object, NPC, Switch, Warp }
}
