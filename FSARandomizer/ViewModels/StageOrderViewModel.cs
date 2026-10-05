using FSARandomizer.Models;
using FSARandomizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace FSARandomizer.ViewModels
{
    public class StageOrderViewModel : ViewModelBase
    {
        private readonly RandomizerViewModel _randVm;

        public ObservableCollection<WorldGroupViewModel> WorldGroups { get; } = new();
        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }

        // True when any slot differs from identity (drives the per-slot green dot via IsChanged).
        private bool _hasChanges;
        public bool HasChanges
        {
            get => _hasChanges;
            private set => Set(ref _hasChanges, value);
        }

        // True when the user has made edits that have NOT yet been applied via Apply().
        private bool _hasUnappliedChanges;
        public bool HasUnappliedChanges
        {
            get => _hasUnappliedChanges;
            private set => Set(ref _hasUnappliedChanges, value);
        }

        // Suppresses the "unapplied" flag during bulk load/reset operations.
        private bool _suppressPendingFlag;

        // The 24 story-stage options shared across every slot dropdown — built once from WorldStems.
        // Tingle's Tower mini-game slots are not shuffled, so they get neither a slot nor an option.
        internal static readonly List<StageOption> AllOptions;
        private static readonly Dictionary<int, StageOption> s_optionBySlot;

        static StageOrderViewModel()
        {
            var stems = DolPatcherService.WorldStems;
            AllOptions = DolPatcherService.StorySlots
                .Select(i => new StageOption(i, stems[i], BuildStemLabel(stems[i])))
                .ToList();
            s_optionBySlot = AllOptions.ToDictionary(o => o.SlotIndex);
        }

        public StageOrderViewModel(RandomizerViewModel randVm)
        {
            _randVm = randVm;
            ApplyCommand = new RelayCommand(Apply);
            ResetCommand = new RelayCommand(Reset);
            BuildWorldGroups();
        }

        private void BuildWorldGroups()
        {
            _suppressPendingFlag = true;
            WorldGroups.Clear();
            var stems = DolPatcherService.WorldStems;
            for (int w = 0; w < 8; w++)
            {
                int worldNum = w + 1;
                string worldName = GameData.Levels.TryGetValue(stems[w * 4], out var info)
                    ? info.WorldName : $"World {worldNum}";
                var group = new WorldGroupViewModel($"World {worldNum} — {worldName}");

                // The world's 3 story stages, in map order.
                var indices = Enumerable.Range(w * 4, 4).Where(idx => !DolPatcherService.IsMiniGameSlot(idx));

                foreach (int idx in indices)
                {
                    var identity = s_optionBySlot[idx];
                    var slot = new StageSlotViewModel(idx, stems[idx], identity.DisplayLabel, AllOptions, identity);
                    slot.PropertyChanged += (_, _) => OnSlotChanged();
                    group.Stages.Add(slot);
                }
                WorldGroups.Add(group);
            }
            _suppressPendingFlag = false;
            HasChanges = false;
            HasUnappliedChanges = false;
        }

        /// <summary>Populate all slot dropdowns from an explicit placement map.</summary>
        public void LoadFromPlacements(Dictionary<string, string>? placements)
        {
            _suppressPendingFlag = true;
            var stems = DolPatcherService.WorldStems;
            foreach (var group in WorldGroups)
                foreach (var slot in group.Stages)
                {
                    string targetKey = $"boss{slot.TargetStem}";
                    if (placements != null && placements.TryGetValue(targetKey, out var srcValue))
                    {
                        string srcStem = srcValue.StartsWith("boss", StringComparison.OrdinalIgnoreCase)
                            ? srcValue[4..] : srcValue;
                        int srcIdx = Array.IndexOf(stems, srcStem);
                        slot.SelectedOption = s_optionBySlot.TryGetValue(srcIdx, out var src)
                            ? src : s_optionBySlot[slot.SlotIndex];
                    }
                    else
                    {
                        slot.SelectedOption = s_optionBySlot[slot.SlotIndex]; // identity
                    }
                }
            _suppressPendingFlag = false;
            // Placements are already active on RandomizerVm — nothing is "unapplied".
            HasUnappliedChanges = false;
        }

        private void Apply()
        {
            var dict = new Dictionary<string, string>(32);
            bool anyChanged = false;
            foreach (var group in WorldGroups)
                foreach (var slot in group.Stages)
                {
                    dict[$"boss{slot.TargetStem}"] = $"boss{slot.SelectedOption.Stem}";
                    if (slot.IsChanged) anyChanged = true;
                }
            _randVm.StagePlacements = anyChanged ? dict : null;
            HasUnappliedChanges = false;
        }

        private void Reset()
        {
            _suppressPendingFlag = true;
            foreach (var group in WorldGroups)
                foreach (var slot in group.Stages)
                    slot.SelectedOption = s_optionBySlot[slot.SlotIndex];
            _suppressPendingFlag = false;
            _randVm.StagePlacements = null;
            HasChanges = false;
            HasUnappliedChanges = false;
        }

        private void OnSlotChanged()
        {
            RefreshHasChanges();
            if (!_suppressPendingFlag)
                HasUnappliedChanges = true;
        }

        private void RefreshHasChanges() =>
            HasChanges = WorldGroups.Any(g => g.Stages.Any(s => s.IsChanged));

        internal static string BuildStemLabel(string stem) =>
            GameData.Levels.TryGetValue(stem, out var info)
                ? $"{info.SectionLabel} — {info.Name}  (boss{stem})"
                : $"boss{stem}";
    }

    public class WorldGroupViewModel
    {
        public string WorldLabel { get; }
        public ObservableCollection<StageSlotViewModel> Stages { get; } = new();
        public WorldGroupViewModel(string label) => WorldLabel = label;
    }

    public class StageSlotViewModel : ViewModelBase
    {
        /// <summary>Position in the WorldStems array (0–31); identifies the slot and its identity option.</summary>
        public int SlotIndex { get; }
        public string TargetStem { get; }
        public string TargetLabel { get; }
        public List<StageOption> AllOptions { get; }

        private StageOption _selectedOption;
        public StageOption SelectedOption
        {
            get => _selectedOption;
            set { Set(ref _selectedOption, value); OnPropertyChanged(nameof(IsChanged)); }
        }

        public bool IsChanged => _selectedOption.Stem != TargetStem;

        public StageSlotViewModel(int slotIndex, string targetStem, string targetLabel, List<StageOption> options, StageOption initial)
        {
            SlotIndex = slotIndex;
            TargetStem = targetStem;
            TargetLabel = targetLabel;
            AllOptions = options;
            _selectedOption = initial;
        }
    }

    public class StageOption
    {
        public int SlotIndex { get; }
        public string Stem { get; }
        public string DisplayLabel { get; }

        public StageOption(int slotIndex, string stem, string displayLabel)
        {
            SlotIndex = slotIndex;
            Stem = stem;
            DisplayLabel = displayLabel;
        }

        public override string ToString() => DisplayLabel;
    }
}
