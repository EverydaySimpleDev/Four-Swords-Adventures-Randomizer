using System.Collections.Generic;
using FSALib.Structs;
using FSARandomizer.Models;

namespace FSARandomizer.ViewModels
{
    /// <summary>Wraps a single <see cref="Actor"/> for display and editing in the DataGrid.</summary>
    public class ActorViewModel : ViewModelBase
    {
        private Actor _actor;
        private readonly int _index;
        private bool _isDirty;

        public ActorViewModel(Actor actor, int index)
        {
            _actor = actor;
            _index = index;
        }

        public int Index => _index;

        public string Id
        {
            get => _actor.Name.Trim();
            set
            {
                if (_actor.Name.Trim() == value) return;
                _actor.Name = value.PadRight(4);
                _isDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeName));
                OnPropertyChanged(nameof(ItemDescription));
            }
        }

        public string TypeName
        {
            get
            {
                if (ActorSchemas.TryGetName(Id, out var name))
                    return name;
                var info = GameData.GetActorInfo(Id);
                if (info.DisplayName != Id) return info.DisplayName;
                return Id;
            }
        }

        public ActorCategory Category => GameData.GetActorInfo(Id).Category;

        public byte Layer
        {
            get => _actor.Layer;
            set { _actor.Layer = value; _isDirty = true; OnPropertyChanged(); }
        }

        public byte X
        {
            get => _actor.XCoord;
            set { _actor.XCoord = value; _isDirty = true; OnPropertyChanged(); }
        }

        public byte Y
        {
            get => _actor.YCoord;
            set { _actor.YCoord = value; _isDirty = true; OnPropertyChanged(); }
        }

        public byte V1
        {
            get => _actor.VariableByte1;
            set { _actor.VariableByte1 = value; _isDirty = true; OnPropertyChanged(); OnPropertyChanged(nameof(ItemDescription)); }
        }

        public byte V2
        {
            get => _actor.VariableByte2;
            set { _actor.VariableByte2 = value; _isDirty = true; OnPropertyChanged(); OnPropertyChanged(nameof(ItemDescription)); }
        }

        public byte V3
        {
            get => _actor.VariableByte3;
            set { _actor.VariableByte3 = value; _isDirty = true; OnPropertyChanged(); OnPropertyChanged(nameof(ItemDescription)); }
        }

        public byte V4
        {
            get => _actor.VariableByte4;
            set { _actor.VariableByte4 = value; _isDirty = true; OnPropertyChanged(); OnPropertyChanged(nameof(ItemDescription)); }
        }

        /// <summary>Friendly description derived from the actor's schema fields and variable values.</summary>
        public string ItemDescription
        {
            get
            {
                // Overrides for item actors: use our GameData names (more terse and consistent with the Item Locations tab)
                if (Id == "TKRA") return $"Chest → {GameData.GetChestItemName(V1)}";
                if (Id == "KEY0") return $"Item → {GameData.GetFloorItemName(V1)}";

                // Schema-driven: format the first 3 meaningful fields for any actor
                if (ActorSchemas.TryGetFields(Id, out var fields) && fields.Count > 0)
                {
                    uint variable = _actor.Variable;
                    var parts = new List<string>();
                    bool firstField = true;
                    foreach (var field in fields)
                    {
                        uint val = field.Read(variable);
                        if (!firstField && val == 0) { firstField = false; continue; }
                        string? fmt = FormatSchemaField(field, val);
                        if (fmt != null) parts.Add(fmt);
                        firstField = false;
                        if (parts.Count >= 3) break;
                    }
                    return string.Join(", ", parts);
                }

                return GameData.GetActorInfo(Id).Notes;
            }
        }

        private static string? FormatSchemaField(SchemaField field, uint value)
        {
            switch (field.ValueType)
            {
                case "Enum":
                    string label = field.EnumValues != null && field.EnumValues.TryGetValue((int)value, out var ev)
                        ? ev.Name : value.ToString();
                    return $"{field.Name}: {label}";
                case "Boolean":
                    return value != 0 ? field.Name : null;
                case "Flags":
                    return value != 0 ? $"{field.Name}: 0x{value:X}" : null;
                default: // Integer
                    return $"{field.Name}: {value}";
            }
        }

        public bool IsDirty => _isDirty;

        /// <summary>Get the underlying actor struct back (with any edits applied).</summary>
        public Actor GetActor() => _actor;

        public void MarkClean() => _isDirty = false;
    }
}
