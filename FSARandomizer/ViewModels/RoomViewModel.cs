using FSALib.Structs;
using FSARandomizer.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace FSARandomizer.ViewModels
{
    public class RoomViewModel : ViewModelBase
    {
        private readonly LoadedRoom _room;
        private readonly LevelService _levelService;

        public RoomViewModel(LoadedRoom room, LevelService levelService)
        {
            _room = room;
            _levelService = levelService;
            RefreshActors();
        }

        public int RoomIndex => _room.RoomIndex;
        public int MapX => _room.MapX;
        public int MapY => _room.MapY;

        public string DisplayName => $"Room {RoomIndex:D2}  ({MapX},{MapY})";

        public ObservableCollection<ActorViewModel> Actors { get; } = new();

        public int ItemCheckCount => Actors.Count(a => a.Id == "TKRA" || a.Id == "KEY0");

        public bool IsDirty => _room.IsDirty || Actors.Any(a => a.IsDirty);

        private void RefreshActors()
        {
            Actors.Clear();
            for (int i = 0; i < _room.Actors.Count; i++)
                Actors.Add(new ActorViewModel(_room.Actors[i], i));
        }

        public void Save()
        {
            // Commit actor VM changes back to the ActorList
            for (int i = 0; i < Actors.Count; i++)
            {
                if (Actors[i].IsDirty)
                {
                    Actor updated = Actors[i].GetActor();
                    _room.Actors[i] = updated;
                    Actors[i].MarkClean();
                }
            }
            _levelService.SaveRoom(_room);
            _room.IsDirty = false;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void AddActor(Actor actor)
        {
            _room.Actors.Add(actor);
            _room.IsDirty = true;
            RefreshActors();
            OnPropertyChanged(nameof(ItemCheckCount));
        }

        public void RemoveActor(ActorViewModel vm)
        {
            int idx = vm.Index;
            if (idx >= 0 && idx < _room.Actors.Count)
            {
                var actor = _room.Actors[idx];
                _room.Actors.Remove(actor);
                _room.IsDirty = true;
                RefreshActors();
                OnPropertyChanged(nameof(ItemCheckCount));
            }
        }
    }
}
