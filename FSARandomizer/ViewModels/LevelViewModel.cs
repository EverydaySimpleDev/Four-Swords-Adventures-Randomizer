using FSARandomizer.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace FSARandomizer.ViewModels
{
    public class LevelViewModel : ViewModelBase
    {
        private readonly LoadedLevel _level;
        private readonly LevelService _levelService;
        private RoomViewModel? _selectedRoom;

        public LevelViewModel(LoadedLevel level, LevelService levelService)
        {
            _level = level;
            _levelService = levelService;

            foreach (var room in level.Rooms.OrderBy(r => r.RoomIndex))
                Rooms.Add(new RoomViewModel(room, levelService));
        }

        public string Id           => _level.Id;
        public string Name         => _level.Name;
        public string WorldName    => _level.WorldName;
        public int    WorldId      => _level.WorldId;
        public string SectionLabel => _level.SectionLabel;

        public string DisplayName =>
            SectionLabel.Length > 0
                ? $"{SectionLabel}  {Name}"
                : $"boss{Id}  –  {Name}";
        public string WorldLabel => $"[{WorldName}]";

        public ObservableCollection<RoomViewModel> Rooms { get; } = new();

        public int TotalItemChecks => Rooms.Sum(r => r.ItemCheckCount);

        public RoomViewModel? SelectedRoom
        {
            get => _selectedRoom;
            set => Set(ref _selectedRoom, value);
        }

        public bool IsDirty => Rooms.Any(r => r.IsDirty);

        public void SaveAll()
        {
            foreach (var room in Rooms.Where(r => r.IsDirty))
                room.Save();
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>Re-pack the level directory back into its source .arc file.</summary>
        public void Repack()
        {
            SaveAll();
            _levelService.RepackLevel(_level);
        }

        public bool HasSourceArc => _level.SourceArcPath != null;
    }
}
