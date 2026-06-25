# FSA Randomizer

A randomizer for **The Legend of Zelda: Four Swords Adventures** (GameCube).  
Shuffles items, keys, and stage order — exports a patched ISO ready to load in Dolphin.

> **Your original disc image is never modified.** The tool always writes to a new file.

---

## Requirements

| Requirement                                                                          | Version           | Notes                                                                               |
| ------------------------------------------------------------------------------------ | ----------------- | ----------------------------------------------------------------------------------- |
| **Windows**                                                                          | 10 or 11 (64-bit) | WPF app — Windows only                                                              |
| **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** | 8.0 or later      | Required to run the app. Download the **Desktop** variant (not Console or ASP.NET). |
| **Four Swords Adventures disc image**                                                | NTSC-U (USA)      | `.iso` format. Must be a legal dump of the GameCube release.                        |
| **[Dolphin Emulator](https://dolphin-emu.org/download/)**                            | 5.0+              | To actually play the randomized game.                                               |

### Installing .NET 8 Desktop Runtime

1. Go to [https://dotnet.microsoft.com/en-us/download/dotnet/8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
2. Under **.NET Desktop Runtime**, click the **x64** installer for Windows
3. Run the installer and follow the prompts
4. You only need to do this once — it stays installed for all .NET 8 apps

---

## Installation

1. Go to the [Releases](../../releases) page and download the latest `FSARandomizer-vX.X.zip`
2. Extract the zip to any folder (e.g. `C:\Tools\FSARandomizer\`)
3. Run **FSARandomizer.exe**

No installer needed — the app is fully portable.

---

## Quick Start

1. Click **Open ISO** in the toolbar and select your `.iso` file
2. Wait for the levels to load (check the **Log** tab if nothing appears)
3. Go to the **Randomizer** tab and choose your settings
4. Click **🎲 Randomize!**
5. Click **Export to New ISO…** and choose an output folder
6. Load the exported ISO in Dolphin

---

## Features

### Randomizer Tab

Configure what gets shuffled and how.

**Item Options**

| Option                  | Default | Description                                                     |
| ----------------------- | ------- | --------------------------------------------------------------- |
| Shuffle Chest Items     | ✅ On   | Randomizes all treasure chest contents                          |
| Shuffle Floor Key Items | Off     | Includes small keys found on the floor                          |
| Shuffle Keys            | Off     | Puts small keys and big keys into the item pool                 |
| Keys in Own Level       | ✅ On   | When key shuffling is on, keys stay within their original level |
| Big Keys in Own Level   | ✅ On   | Same restriction for big keys                                   |
| Moon Pearl in Own Level | ✅ On   | Moon Pearl stays within its original level                      |

**Level Order**  
Controls whether the 32 playable stages are rearranged when exporting. The two modes are mutually exclusive.

| Option                  | Description                                                                                                                                                         |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Shuffle World Order** | Moves all eight worlds as groups. All four stages of a world travel together — you might play World 5's stages in World 1's slot, but the world itself is coherent. |
| **Shuffle Stage Order** | Shuffles all 32 stages individually. Any stage can end up in any slot across any world. The more chaotic option.                                                    |

**Seed**  
The seed controls both item placement and level order. Change it manually or click **New Seed**. Enable **Randomize seed each time** to get a fresh seed automatically on every Randomize click.

---

### Stage Order Tab

Manually assign which stage's content goes into each slot — useful when you want a specific layout without relying on a seed.

1. Use the dropdowns to pick which source stage plays in each slot
2. Slots are grouped by world; hub/overworld stages appear at the bottom of each group
3. A green dot (●) marks any slot that differs from the original layout
4. Click **✔ Apply to Export** to activate your layout
5. Click **↺ Reset All** to go back to the default order

> Changes here set the same explicit placement used by JSON import — clicking Randomize will discard them.

---

### JSON Export / Import

Export your current settings and item layout to a JSON file so you can share seeds or reproduce a run exactly.

**Exporting**

- Click **Export JSON** to save the current seed, settings, and item locations
- The exported file embeds the stage order explicitly, so re-importing always reproduces the same layout even if the shuffle algorithm changes in a future version

**Importing**

- Click **Import JSON** to load a previously exported file
- All settings are restored automatically
- The Stage Order tab updates to show the imported stage layout
- If the file contains explicit stage placements, a gold banner appears above the Randomize button:

> ⚠ **Explicit stage placements active.** Skip Randomize! — go straight to Export to New ISO.

---

### JSON File Format

You can write a JSON file by hand (or generate one from a script) and import it directly. Only the fields you include are applied — everything else is left at the default.

**Minimal example — item locations only:**

```json
[
  { "Key": "010_00_001", "NewItemId": 17 },
  { "Key": "011_02_004", "NewItemId": 16 }
]
```

**Full wrapped format with settings and stage placements:**

```json
{
  "Settings": {
    "Seed": 12345,
    "ShuffleChestItems": true,
    "ShuffleFloorKeyItems": false,
    "ShuffleKeys": false,
    "KeysInOwnLevel": true,
    "BigKeysInOwnLevel": true,
    "MoonPearlInOwnLevel": true,
    "HeartContainerInOwnLevel": true,
    "BigBombInOwnLevel": true,
    "BlueBraceletInOwnLevel": true,
    "EnsureBeatable": false,
    "ShuffleLevelOrder": false,
    "ShuffleStageOrder": false,
    "StagePlacements": {
      "boss010": "boss050",
      "boss011": "boss020"
    }
  },
  "Locations": [
    {
      "Key": "010_00_001",
      "LevelId": "010",
      "LevelName": "Lake Hylia",
      "WorldName": "Whereabouts of the Wind",
      "Room": 0,
      "ActorIndex": 1,
      "Type": "TKRA",
      "Position": "(12,8)",
      "OriginalItemId": 16,
      "OriginalItem": "Small Key",
      "NewItemId": 17,
      "NewItem": "Big Key"
    }
  ]
}
```

**Field reference**

| Field                        | Required | Notes                                                                                  |
| ---------------------------- | -------- | -------------------------------------------------------------------------------------- |
| `Key`                        | Yes      | `{LevelId}_{RoomIndex:D2}_{ActorIndex:D3}` — e.g. `"010_00_001"`. Must match exactly.  |
| `NewItemId`                  | Yes      | The item byte to write into this location (decimal). Everything else is informational. |
| `LevelId`, `LevelName`, etc. | No       | Informational only — ignored on import.                                                |
| `Settings` block             | No       | Omit the whole block to import only item locations.                                    |
| `StagePlacements`            | No       | Keys and values are both `"boss{stem}"`.                                               |

---

### Spoiler Log

After randomizing, click **👁 Show Spoiler** on the Randomizer tab to reveal the full item placement list. Export it to a file via **Export Spoiler Log** for reference during a race or async.

---

### Log Tab

Shows a full log of every operation — useful if levels fail to load or an export encounters an error. Check here first when something goes wrong.

---

## Notes

- **Source ISO safety** — the app enforces this at two levels: a confirmation dialog and a hard check in the export service. There is no way to accidentally overwrite your original disc image.
- **NTSC-U only** — only the North American GameCube release has been tested. PAL and JP versions are not supported.
- **File size** — exported ISOs are the same size as the original (~1.35 GB). The randomizer patches content in-place; it does not grow the disc image.
- **Dolphin settings** — no special Dolphin configuration is needed. Load the exported ISO the same way you would load any other GameCube game.

---

## Credits

Special thanks to:

- **Jaytheham** – for the majority of the original reverse-engineering work and creation of the first FSA editor.
- **Venomalia** – for creating [FSALib](https://github.com/Venomalia/EFSAdvent) which made making this possible.
- **nbouteme** – for [documenting](https://nbouteme.github.io/fsasobdoc/) the sprite format in detail.
- **PinkSwitch** – for suppling great notes and ideas on getting this started [Github](https://github.com/PinkSwitch).

This project is not affiliated with or endorsed by Nintendo.
