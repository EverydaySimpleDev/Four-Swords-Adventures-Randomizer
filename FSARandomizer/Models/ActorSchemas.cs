using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FSARandomizer.Models
{
    /// <summary>
    /// Lightweight actor schema loader for the Randomizer — reads from assets\actors\ in the EXE directory.
    /// Bypasses FSALib.Assets to avoid static-constructor working-directory issues.
    /// </summary>
    public static class ActorSchemas
    {
        private static readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<SchemaField>> _fields = new(StringComparer.OrdinalIgnoreCase);

        static ActorSchemas()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "actors");
            if (!Directory.Exists(dir)) return;

            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var schema = JsonSerializer.Deserialize<ActorSchemaJson>(stream, opts);
                    if (schema == null) continue;

                    string id = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(schema.Name))
                        _names[id] = schema.Name;
                    if (schema.Fields?.Count > 0)
                        _fields[id] = schema.Fields;
                }
                catch { }
            }
        }

        public static bool TryGetName(string actorId, out string name)
            => _names.TryGetValue(actorId, out name!);

        public static bool TryGetFields(string actorId, out List<SchemaField> fields)
            => _fields.TryGetValue(actorId, out fields!);

        public static int LoadedCount => _names.Count;
    }

    // Minimal JSON DTOs — only what we need for display
    public class ActorSchemaJson
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public List<SchemaField> Fields { get; set; } = new();
    }

    public class SchemaField
    {
        public string Name { get; set; } = "";
        public int BitOffset { get; set; }
        public int BitSize { get; set; }
        public string ValueType { get; set; } = "Integer";
        public Dictionary<int, SchemaEnumValue>? EnumValues { get; set; }

        public int Mask => (1 << BitSize) - 1;
        public uint Read(uint variable) => (uint)((variable >> BitOffset) & Mask);
    }

    public class SchemaEnumValue
    {
        public string Name { get; set; } = "";
    }
}
