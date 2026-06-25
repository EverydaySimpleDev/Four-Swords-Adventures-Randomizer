using FSARandomizer.Models;

namespace FSARandomizer.ViewModels
{
    /// <summary>
    /// Wraps an <see cref="ItemLocation"/> for display in the Item Locations DataGrid.
    /// Provides editable RandomizedItemId with a ComboBox-friendly item name list.
    /// </summary>
    public class ItemLocationViewModel : ViewModelBase
    {
        private readonly ItemLocation _loc;

        public ItemLocationViewModel(ItemLocation loc) => _loc = loc;

        public ItemLocation Location => _loc;

        public string WorldName    => _loc.WorldName;
        public string SectionLabel => _loc.SectionLabel;
        public string LevelName    => _loc.LevelName;
        public string LevelId      => _loc.LevelId;
        public int    RoomIndex    => _loc.RoomIndex;
        public string ActorType    => _loc.ActorType;
        public string ActorTypeName => _loc.ActorTypeName;
        public byte   X            => _loc.X;
        public byte   Y            => _loc.Y;
        public byte   Layer        => _loc.Layer;
        public string Position     => $"({X},{Y}) L{Layer}";

        public string OriginalItemName  => _loc.OriginalItemName;
        public byte   OriginalItemId    => _loc.OriginalItemId;
        public string OriginalItemHex   => $"0x{_loc.OriginalItemId:X2}";

        public string RandomizedItemName
        {
            get => _loc.RandomizedItemName;
            set
            {
                _loc.RandomizedItemName = value;
                OnPropertyChanged();
            }
        }

        public byte RandomizedItemId
        {
            get => _loc.RandomizedItemId;
            set
            {
                _loc.RandomizedItemId = value;
                _loc.RandomizedItemName = _loc.ActorType == "TKRA"
                    ? GameData.GetChestItemName(value)
                    : GameData.GetFloorItemName(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RandomizedItemName));
                OnPropertyChanged(nameof(IsChanged));
                OnPropertyChanged(nameof(ChangedMarker));
            }
        }

        public string RandomizedItemHex => $"0x{RandomizedItemId:X2}";

        public bool   IsChanged     => _loc.IsRandomized;
        public string ChangedMarker => IsChanged ? "●" : "";

        /// <summary>Call after the underlying Location has been mutated externally (e.g. by the randomizer).</summary>
        public new void RefreshAll()
        {
            OnPropertyChanged(nameof(RandomizedItemId));
            OnPropertyChanged(nameof(RandomizedItemName));
            OnPropertyChanged(nameof(RandomizedItemHex));
            OnPropertyChanged(nameof(IsChanged));
            OnPropertyChanged(nameof(ChangedMarker));
        }
    }
}
