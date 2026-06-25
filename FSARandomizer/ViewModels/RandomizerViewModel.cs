using FSARandomizer.Models;
using FSARandomizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace FSARandomizer.ViewModels
{
    public class RandomizerViewModel : ViewModelBase
    {
        private readonly RandomizerService   _service;
        private readonly LevelGraphService   _graphService = new LevelGraphService();
        private string _statusText = "Ready.";
        private int _seed;

        private bool _isSpoilerVisible;

        public bool IsSpoilerVisible
        {
            get => _isSpoilerVisible;
            private set => Set(ref _isSpoilerVisible, value);
        }

        // Set by MainViewModel after a game is loaded; used by the graph reachability check.
        public LoadedGame? Game { get; set; }

        public RandomizerViewModel(RandomizerService service)
        {
            _service = service;
            _seed = new Random().Next(1, 999999);
            RandomizeCommand    = new RelayCommand(DoRandomize, () => ItemLocations.Count > 0);
            NewSeedCommand      = new RelayCommand(() => Seed = new Random().Next(1, 999999));
            ToggleSpoilerCommand = new RelayCommand(
                () => IsSpoilerVisible = !IsSpoilerVisible);
        }

        // ── Settings ──────────────────────────────────────────────────────────

        public int Seed
        {
            get => _seed;
            set => Set(ref _seed, value);
        }

        public bool ShuffleChestItems { get; set; } = true;
        public bool ShuffleFloorKeyItems { get; set; } = false;
        public bool ShuffleKeys { get; set; } = false;
        public bool KeysInOwnLevel { get; set; } = true;
        public bool BigKeysInOwnLevel { get; set; } = true;
        public bool MoonPearlInOwnLevel { get; set; } = true;
        public bool HeartContainerInOwnLevel { get; set; } = true;
        public bool BigBombInOwnLevel { get; set; } = true;
        public bool BlueBraceletInOwnLevel { get; set; } = true;
        public bool EnsureBeatable      { get; set; } = false;
        public bool AutoRandomizeSeed   { get; set; } = true;

        /// <summary>
        /// Explicit placement map imported from JSON (e.g. from Archipelago).
        /// When non-null, overrides seed-based generation at export time.
        /// Cleared when the user re-randomizes or toggles shuffle mode.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, string>? StagePlacements
        {
            get => _stagePlacements;
            set { _stagePlacements = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImportedPlacements)); }
        }
        private System.Collections.Generic.Dictionary<string, string>? _stagePlacements;

        /// <summary>True when a JSON with explicit AP stage placements is loaded.</summary>
        public bool HasImportedPlacements => _stagePlacements != null;

        private bool _shuffleLevelOrder;
        public bool ShuffleLevelOrder
        {
            get => _shuffleLevelOrder;
            set
            {
                Set(ref _shuffleLevelOrder, value);
                if (value && _shuffleStageOrder) { _shuffleStageOrder = false; OnPropertyChanged(nameof(ShuffleStageOrder)); }
            }
        }

        private bool _shuffleStageOrder;
        public bool ShuffleStageOrder
        {
            get => _shuffleStageOrder;
            set
            {
                Set(ref _shuffleStageOrder, value);
                if (value && _shuffleLevelOrder) { _shuffleLevelOrder = false; OnPropertyChanged(nameof(ShuffleLevelOrder)); }
            }
        }
        public bool ProgressiveSwords { get; set; } = false;
        public bool FormationsAsItems { get; set; } = false;

        // Item inclusion toggles
        public bool ShuffleMoonPearl { get; set; } = false;
        public bool ShuffleHeartContainers { get; set; } = false;
        public bool ShuffleBigBombs { get; set; } = false;
        public bool ShuffleBlueBracelet { get; set; } = false;

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand RandomizeCommand { get; }
        public ICommand NewSeedCommand { get; }
        public ICommand ToggleSpoilerCommand { get; }

        // ── Bound data from MainViewModel ─────────────────────────────────────

        public ObservableCollection<ItemLocationViewModel> ItemLocations { get; } = new();

        public string StatusText
        {
            get => _statusText;
            private set => Set(ref _statusText, value);
        }

        // ── Spoiler ───────────────────────────────────────────────────────────

        public ObservableCollection<SpoilerEntry> SpoilerEntries { get; } = new();

        // ── Key balance ───────────────────────────────────────────────────────

        private string _keyWarningText = "";
        public string KeyWarningText
        {
            get => _keyWarningText;
            private set => Set(ref _keyWarningText, value);
        }

        public bool HasKeyWarnings => !string.IsNullOrEmpty(KeyWarningText);

        // ── Actions ───────────────────────────────────────────────────────────

        private void DoRandomize()
        {
            StagePlacements = null; // seed-based run supersedes any imported AP placements
            if (AutoRandomizeSeed)
                Seed = new Random().Next(1, 999999);

            var settings = BuildSettings();
            var rawLocations = ItemLocations.Select(vm => vm.Location).ToList();

            _service.Randomize(rawLocations, settings);

            if (EnsureBeatable && ShuffleKeys && Game != null)
            {
                // Establish which issues exist in the unrandomized original layout.
                // These are structural false positives — rooms reachable via tile passages
                // that the DOOR-actor graph cannot see.  We only retry and warn about issues
                // that are *new* compared to the original layout.
                var baselineReports = _graphService.CheckAllLevels(Game, rawLocations, useOriginalIds: true);
                var baselineIssues  = baselineReports
                    .ToDictionary(r => r.LevelId, r => new System.Collections.Generic.HashSet<string>(r.Issues));

                List<ReachabilityReport> NewIssuesOnly(List<ReachabilityReport> current)
                {
                    var result = new System.Collections.Generic.List<ReachabilityReport>();
                    foreach (var r in current)
                    {
                        baselineIssues.TryGetValue(r.LevelId, out var bl);
                        var fresh = r.Issues.Where(i => bl == null || !bl.Contains(i)).ToList();
                        if (fresh.Count > 0)
                            result.Add(new ReachabilityReport
                            {
                                LevelId = r.LevelId, LevelName = r.LevelName,
                                IsFullyReachable = false,
                                TotalRooms = r.TotalRooms, ReachableRooms = r.ReachableRooms,
                                Issues = fresh,
                            });
                    }
                    return result;
                }

                const int MaxRetries = 20;
                var rng     = new Random();
                var reports = NewIssuesOnly(_graphService.CheckAllLevels(Game, rawLocations));

                for (int attempt = 1; attempt < MaxRetries && reports.Count > 0; attempt++)
                {
                    foreach (var loc in rawLocations)
                        loc.RandomizedItemId = loc.OriginalItemId;
                    settings.Seed = rng.Next(1, 999999);
                    _service.Randomize(rawLocations, settings);
                    reports = NewIssuesOnly(_graphService.CheckAllLevels(Game, rawLocations));
                }

                Seed = settings.Seed;

                KeyWarningText = reports.Count == 0 ? ""
                    : $"⚠ Key cycle remains after {MaxRetries} attempts:\n"
                      + string.Join("\n", reports.SelectMany(r =>
                          r.Issues.Select(issue => $"  • {r.LevelName}: {issue}")));
            }
            else
            {
                // Simple count check — warns when fewer keys ended up in a level than it started with
                var balance = _service.CheckKeyBalance(rawLocations);
                KeyWarningText = balance.IsBalanced ? ""
                    : "⚠ Key shortfalls (levels may have locked doors with no key):\n"
                      + string.Join("\n", balance.Warnings.Select(w => "  • " + w));
            }

            // Refresh VMs and spoiler log after the final successful (or last) shuffle
            foreach (var vm in ItemLocations)
                vm.RefreshAll();

            var log = _service.BuildSpoilerLog(rawLocations, settings);
            SpoilerEntries.Clear();
            foreach (var entry in log.Locations)
                SpoilerEntries.Add(entry);
            IsSpoilerVisible = false;

            StatusText = $"Seed {Seed}: {log.Locations.Count} locations shuffled.";
            OnPropertyChanged(nameof(HasKeyWarnings));
        }

        /// <summary>
        /// Restore all settings from an imported JSON file and refresh all UI bindings.
        /// </summary>
        public void ApplySettings(RandomizerSettings s)
        {
            _seed = s.Seed;
            ShuffleChestItems        = s.ShuffleChestItems;
            ShuffleFloorKeyItems     = s.ShuffleFloorKeyItems;
            ShuffleKeys              = s.ShuffleKeys;
            KeysInOwnLevel           = s.KeysInOwnLevel;
            BigKeysInOwnLevel        = s.BigKeysInOwnLevel;
            MoonPearlInOwnLevel      = s.MoonPearlInOwnLevel;
            HeartContainerInOwnLevel = s.HeartContainerInOwnLevel;
            BigBombInOwnLevel        = s.BigBombInOwnLevel;
            BlueBraceletInOwnLevel   = s.BlueBraceletInOwnLevel;
            EnsureBeatable           = s.EnsureBeatable;
            _shuffleLevelOrder       = s.ShuffleLevelOrder;
            _shuffleStageOrder       = s.ShuffleStageOrder;
            StagePlacements          = s.StagePlacements;
            ShuffleMoonPearl         = s.ShuffleMoonPearl;
            ShuffleHeartContainers   = s.ShuffleHeartContainers;
            ShuffleBigBombs          = s.ShuffleBigBombs;
            ShuffleBlueBracelet      = s.ShuffleBlueBracelet;
            ProgressiveSwords        = s.ProgressiveSwords;
            FormationsAsItems        = s.FormationsAsItems;
            RefreshAll();
        }

        public RandomizerSettings BuildSettings() => new RandomizerSettings
        {
            Seed             = Seed,
            EnsureBeatable   = EnsureBeatable,
            ShuffleLevelOrder = ShuffleLevelOrder,
            ShuffleStageOrder = ShuffleStageOrder,
            StagePlacements   = StagePlacements,
            ShuffleChestItems = ShuffleChestItems,
            ShuffleFloorKeyItems = ShuffleFloorKeyItems,
            ShuffleKeys = ShuffleKeys,
            KeysInOwnLevel = KeysInOwnLevel,
            BigKeysInOwnLevel = BigKeysInOwnLevel,
            MoonPearlInOwnLevel = MoonPearlInOwnLevel,
            HeartContainerInOwnLevel = HeartContainerInOwnLevel,
            BigBombInOwnLevel = BigBombInOwnLevel,
            BlueBraceletInOwnLevel = BlueBraceletInOwnLevel,
            ProgressiveSwords = ProgressiveSwords,
            FormationsAsItems = FormationsAsItems,
            ShuffleMoonPearl = ShuffleMoonPearl,
            ShuffleHeartContainers = ShuffleHeartContainers,
            ShuffleBigBombs = ShuffleBigBombs,
            ShuffleBlueBracelet = ShuffleBlueBracelet,
        };
    }

    /// <summary>Minimal ICommand implementation.</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? _) => _canExecute?.Invoke() ?? true;
        public void Execute(object? _) => _execute();
        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
