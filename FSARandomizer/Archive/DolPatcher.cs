using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace FSARandomizer.Archive
{
    /// <summary>
    /// A 4-byte code patch to main.dol, given by its RAM address (as used in Gecko/kmWrite32 codes).
    /// <paramref name="Expected"/> is the original instruction; the patch is only applied when it
    /// matches, so a different game version is never patched blindly.
    /// </summary>
    public record DolPatch(string Name, uint Address, uint Expected, uint Value);

    /// <summary>Applies <see cref="DolPatch"/>es to main.dol inside a GameCube disc image, in place.</summary>
    public static class DolPatcher
    {
        /// <summary>Skips the intro sequence: a new file goes straight to the world map (USA).</summary>
        public static readonly DolPatch SkipIntro = new("Skip intro", 0x803EC450, 0x41820020, 0x60000000); // beq +0x20 → nop

        /// <summary>
        /// Patch main.dol in <paramref name="isoPath"/>. Returns one message per patch
        /// saying whether it was applied, already present, or skipped.
        /// </summary>
        public static List<string> Apply(string isoPath, IEnumerable<DolPatch> patches)
        {
            var log = new List<string>();
            using var fs = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite);
            uint dolOffset = ReadU32(fs, 0x420);
            var header = new byte[0x100];
            fs.Position = dolOffset;
            fs.ReadExactly(header);

            foreach (var patch in patches)
            {
                long? fileOffset = FindFileOffset(header, patch.Address);
                if (fileOffset == null)
                {
                    log.Add($"  Warning: {patch.Name}: address 0x{patch.Address:X8} is not in main.dol – skipped.");
                    continue;
                }

                long discOffset = dolOffset + fileOffset.Value;
                uint current = ReadU32(fs, discOffset);
                if (current == patch.Value)
                    log.Add($"{patch.Name}: already applied.");
                else if (current != patch.Expected)
                    log.Add($"  Warning: {patch.Name}: found 0x{current:X8} at 0x{patch.Address:X8}, expected 0x{patch.Expected:X8} – skipped (different game version?).");
                else
                {
                    WriteU32(fs, discOffset, patch.Value);
                    log.Add($"{patch.Name}: applied.");
                }
            }
            return log;
        }

        /// <summary>Map a RAM address to its offset in main.dol using the 7 text + 11 data section table.</summary>
        private static long? FindFileOffset(byte[] dolHeader, uint address)
        {
            for (int s = 0; s < 18; s++)
            {
                uint offset = BinaryPrimitives.ReadUInt32BigEndian(dolHeader.AsSpan(s * 4));
                uint start  = BinaryPrimitives.ReadUInt32BigEndian(dolHeader.AsSpan(0x48 + s * 4));
                uint size   = BinaryPrimitives.ReadUInt32BigEndian(dolHeader.AsSpan(0x90 + s * 4));
                if (size != 0 && address >= start && address + 4 <= start + size)
                    return offset + (address - start);
            }
            return null;
        }

        private static uint ReadU32(Stream s, long position)
        {
            Span<byte> b = stackalloc byte[4];
            s.Position = position;
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt32BigEndian(b);
        }

        private static void WriteU32(Stream s, long position, uint value)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(b, value);
            s.Position = position;
            s.Write(b);
        }
    }
}
