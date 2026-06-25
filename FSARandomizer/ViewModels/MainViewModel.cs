using FSALib.Structs;
using FSARandomizer.Models;
using FSARandomizer.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FSARandomizer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly LevelService _levelService = new LevelService();
        private readonly RandomizerService _randService = new RandomizerService();

        private LoadedGame? _game;
        private LevelViewModel? _selectedLevel;
        private RoomViewModel? _selectedRoom;
        private string _statusText = "Open a Boss/ folder or an extracted levels folder to begin.";
        private bool _isBusy;
        private string? _lastExportPath;

        public MainViewModel()
        {
            RandomizerVm = new RandomizerViewModel(_randService);
            StageOrderVm = new StageOrderViewModel(RandomizerVm);

            OpenFolderCommand = new RelayCommand(OpenFolder);
            OpenIsoCommand = new RelayCommand(OpenIso);
            ExportIsoCommand     = new RelayCommand(ExportIso,     () => _game != null && File.Exists(_game.SourceDirectory));
            ExportNewIsoCommand  = new RelayCommand(ExportIso,     () => _game != null && File.Exists(_game.SourceDirectory));
            ReexportIsoCommand   = new RelayCommand(ReexportIso,   () => _lastExportPath != null && _game != null && File.Exists(_game.SourceDirectory));
            SaveAllCommand       = new RelayCommand(SaveAll,        () => _game?.IsModified == true);
            ApplyAndSaveCommand  = new RelayCommand(SaveAll,        () => _game != null && ItemLocations.Count > 0);
            RepackAllCommand = new RelayCommand(RepackAll, () => _game?.Levels.Any(l => l.SourceArcPath != null) == true);
            ExportCsvCommand  = new RelayCommand(ExportCsv,  () => ItemLocations.Count > 0);
            ImportCsvCommand  = new RelayCommand(ImportCsv,  () => _game != null);
            ExportJsonCommand = new RelayCommand(ExportJson, () => ItemLocations.Count > 0);
            ImportJsonCommand = new RelayCommand(ImportJson, () => _game != null);
            ExportSpoilerCommand = new RelayCommand(ExportSpoiler, () => RandomizerVm.SpoilerEntries.Count > 0);
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand OpenFolderCommand { get; }
        public ICommand OpenIsoCommand { get; }
        public ICommand ExportIsoCommand { get; }
        public ICommand ExportNewIsoCommand { get; }
        public ICommand ReexportIsoCommand { get; }
        public ICommand SaveAllCommand { get; }
        public ICommand ApplyAndSaveCommand { get; }
        public ICommand RepackAllCommand { get; }
        public ICommand ExportCsvCommand  { get; }
        public ICommand ImportCsvCommand  { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand ImportJsonCommand { get; }
        public ICommand ExportSpoilerCommand { get; }

        // ── Child VMs ─────────────────────────────────────────────────────────

        public RandomizerViewModel RandomizerVm { get; }
        public StageOrderViewModel StageOrderVm { get; }

        // ── Collections ───────────────────────────────────────────────────────

        public ObservableCollection<LevelViewModel> Levels { get; } = new();
        public ObservableCollection<ItemLocationViewModel> ItemLocations { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

        // ── Selection ─────────────────────────────────────────────────────────

        public LevelViewModel? SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                Set(ref _selectedLevel, value);
                SelectedRoom = value?.Rooms.FirstOrDefault();
            }
        }

        public RoomViewModel? SelectedRoom
        {
            get => _selectedRoom;
            set => Set(ref _selectedRoom, value);
        }

        // ── Status ────────────────────────────────────────────────────────────

        public string StatusText
        {
            get => _statusText;
            private set => Set(ref _statusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => Set(ref _isBusy, value);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private async void OpenFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Boss/ folder (with .arc files) or an extracted levels folder"
            };
            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            StatusText = "Loading…";
            LogLines.Clear();
            Levels.Clear();
            ItemLocations.Clear();
            RandomizerVm.ItemLocations.Clear();
            RandomizerVm.Game = null;

            try
            {
                var progress = new Progress<string>(msg => { StatusText = msg; LogLines.Add(msg); });
                _game = await Task.Run(() => _levelService.OpenDirectory(dialog.FolderName, progress));

                foreach (var level in _game.Levels.OrderBy(l => l.Id))
                    Levels.Add(new LevelViewModel(level, _levelService));

                SelectedLevel = Levels.FirstOrDefault();

                // Build item locations list
                var locations = _levelService.FindItemLocations(_game);
                foreach (var loc in locations)
                {
                    var vm = new ItemLocationViewModel(loc);
                    ItemLocations.Add(vm);
                    RandomizerVm.ItemLocations.Add(vm);
                }
                RandomizerVm.Game = _game;

                var summary = $"Loaded {Levels.Count} levels, {ItemLocations.Count} item checks found.";
                StatusText = summary;
                LogLines.Add(summary);
                if (Levels.Count == 0)
                {
                    LogLines.Add("No levels found. Check the Log tab for warnings.");
                    MessageBox.Show(
                        "No levels were loaded.\n\nCheck the Log tab for details — it shows what happened during extraction.",
                        "Nothing Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error: {ex.Message}";
                StatusText = msg;
                LogLines.Add(msg);
                LogLines.Add(ex.StackTrace ?? "");
                MessageBox.Show(ex.Message, "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OpenIso()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select GameCube disc image",
                Filter = "GameCube images (*.iso;*.gcm)|*.iso;*.gcm|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            StatusText = "Loading from ISO…";
            LogLines.Clear();
            Levels.Clear();
            ItemLocations.Clear();
            RandomizerVm.ItemLocations.Clear();
            RandomizerVm.Game = null;

            try
            {
                var progress = new Progress<string>(msg => { StatusText = msg; LogLines.Add(msg); });
                _game = await Task.Run(() => _levelService.OpenIso(dialog.FileName, progress));

                foreach (var level in _game.Levels.OrderBy(l => l.Id))
                    Levels.Add(new LevelViewModel(level, _levelService));

                SelectedLevel = Levels.FirstOrDefault();

                var locations = _levelService.FindItemLocations(_game);
                foreach (var loc in locations)
                {
                    var vm = new ItemLocationViewModel(loc);
                    ItemLocations.Add(vm);
                    RandomizerVm.ItemLocations.Add(vm);
                }
                RandomizerVm.Game = _game;

                var summary = $"Loaded {Levels.Count} levels, {ItemLocations.Count} item checks found.";
                StatusText = summary;
                LogLines.Add(summary);
                if (Levels.Count == 0)
                {
                    LogLines.Add("No levels found. Check the Log tab for warnings.");
                    MessageBox.Show(
                        "No levels were loaded from the ISO.\n\nCheck the Log tab for details.",
                        "Nothing Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error: {ex.Message}";
                StatusText = msg;
                LogLines.Add(msg);
                LogLines.Add(ex.StackTrace ?? "");
                MessageBox.Show(ex.Message, "Open ISO Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExportIso()
        {
            if (_game == null) return;

            var folderDlg = new OpenFolderDialog
            {
                Title = "Choose output folder for randomized ISO"
            };
            if (folderDlg.ShowDialog() != true) return;

            string seed = RandomizerVm.Seed.ToString();
            string outputPath = Path.Combine(folderDlg.FolderName, $"GC4Sword_randomized_seed{seed}.iso");

            // Hard safety check — never overwrite the source disc image
            string? sourcePath = _game.SourceDirectory;
            if (string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(sourcePath ?? ""),
                              StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "The output path matches the source ISO. Choose a different folder — the original disc image will never be modified.",
                    "Safety Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusText = "Building ISO…";
            LogLines.Clear();
            LogLines.Add($"Output: {outputPath}");

            try
            {
                var progress = new Progress<string>(msg => { StatusText = msg; LogLines.Add(msg); });
                // Capture locations on UI thread before switching to background thread
                var locations      = ItemLocations.Select(vm => vm.Location).ToList();
                bool shuffleWorlds  = RandomizerVm.ShuffleLevelOrder;
                bool shuffleStages  = RandomizerVm.ShuffleStageOrder;
                int  dolSeed        = RandomizerVm.Seed;
                var  placements     = RandomizerVm.StagePlacements;
                int[]? levelPerm    = placements     != null ? DolPatcherService.PlacementsToPermutation(placements)
                                    : shuffleWorlds  ? DolPatcherService.BuildWorldPermutation(dolSeed)
                                    : shuffleStages  ? DolPatcherService.BuildStagePermutation(dolSeed)
                                    : null;
                await Task.Run(() =>
                {
                    _levelService.ApplyItemLocations(_game, locations);
                    _levelService.ExportIso(_game, outputPath, levelPerm, progress);
                });
                _lastExportPath = outputPath;
                StatusText = $"ISO exported: {Path.GetFileName(outputPath)}";
                LogLines.Add(StatusText);
            }
            catch (Exception ex)
            {
                StatusText = $"Export error: {ex.Message}";
                LogLines.Add(StatusText);
                LogLines.Add(ex.StackTrace ?? "");
                MessageBox.Show(ex.Message, "Export ISO Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Clean up partial output file if it exists
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ReexportIso()
        {
            if (_game == null || _lastExportPath == null) return;

            var confirm = MessageBox.Show(
                $"This will overwrite the previous export:\n{_lastExportPath}\n\nContinue?",
                "Re-export ISO", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            StatusText = "Re-building ISO…";
            LogLines.Clear();
            LogLines.Add($"Output: {_lastExportPath}");

            try
            {
                var progress = new Progress<string>(msg => { StatusText = msg; LogLines.Add(msg); });
                var locations      = ItemLocations.Select(vm => vm.Location).ToList();
                bool shuffleWorlds  = RandomizerVm.ShuffleLevelOrder;
                bool shuffleStages  = RandomizerVm.ShuffleStageOrder;
                int  dolSeed        = RandomizerVm.Seed;
                var  placements     = RandomizerVm.StagePlacements;
                int[]? levelPerm    = placements     != null ? DolPatcherService.PlacementsToPermutation(placements)
                                    : shuffleWorlds  ? DolPatcherService.BuildWorldPermutation(dolSeed)
                                    : shuffleStages  ? DolPatcherService.BuildStagePermutation(dolSeed)
                                    : null;
                await Task.Run(() =>
                {
                    _levelService.ApplyItemLocations(_game, locations);
                    _levelService.ExportIso(_game, _lastExportPath, levelPerm, progress);
                });
                StatusText = $"ISO re-exported: {Path.GetFileName(_lastExportPath)}";
                LogLines.Add(StatusText);
            }
            catch (Exception ex)
            {
                StatusText = $"Export error: {ex.Message}";
                LogLines.Add(StatusText);
                LogLines.Add(ex.StackTrace ?? "");
                MessageBox.Show(ex.Message, "Export ISO Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SaveAll()
        {
            if (_game == null) return;
            try
            {
                _levelService.ApplyItemLocations(
                    _game,
                    ItemLocations.Select(vm => vm.Location));
                StatusText = "All changes saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RepackAll()
        {
            if (_game == null) return;
            int packed = 0;
            foreach (var level in _game.Levels.Where(l => l.SourceArcPath != null))
            {
                try
                {
                    // Save any in-memory edits first
                    var lvm = Levels.FirstOrDefault(vm => vm.Id == level.Id);
                    lvm?.SaveAll();
                    _levelService.RepackLevel(level);
                    packed++;
                }
                catch (Exception ex)
                {
                    StatusText = $"Repack error on boss{level.Id}: {ex.Message}";
                }
            }
            StatusText = $"Repacked {packed} .arc files.";
        }

        private void ExportCsv()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Item Locations",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "item_locations.csv"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _randService.ExportCsv(ItemLocations.Select(v => v.Location).ToList(), dlg.FileName);
                StatusText = $"Exported {ItemLocations.Count} locations to {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCsv()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Item Locations",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _randService.ImportCsv(dlg.FileName, ItemLocations.Select(v => v.Location).ToList());
                foreach (var vm in ItemLocations)
                    vm.RefreshAll();
                StatusText = $"Imported from {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportJson()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Item Locations",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "item_locations.json"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var settings = RandomizerVm.BuildSettings();
                // Embed explicit stage placements so the JSON is self-contained:
                // re-importing produces identical results regardless of seed algorithm changes.
                if (settings.StagePlacements == null)
                {
                    int[]? perm = settings.ShuffleLevelOrder ? DolPatcherService.BuildWorldPermutation(settings.Seed)
                                : settings.ShuffleStageOrder ? DolPatcherService.BuildStagePermutation(settings.Seed)
                                : null;
                    if (perm != null)
                        settings.StagePlacements = DolPatcherService.PermutationToPlacements(perm);
                }
                _randService.ExportJson(ItemLocations.Select(v => v.Location).ToList(), settings, dlg.FileName);
                StatusText = $"Exported {ItemLocations.Count} locations to {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportJson()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Item Locations",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var importedSettings = _randService.ImportJson(dlg.FileName, ItemLocations.Select(v => v.Location).ToList());
                foreach (var vm in ItemLocations)
                    vm.RefreshAll();
                if (importedSettings != null)
                {
                    RandomizerVm.ApplySettings(importedSettings);
                    StageOrderVm.LoadFromPlacements(importedSettings.StagePlacements);
                }
                StatusText = $"Imported from {Path.GetFileName(dlg.FileName)}."
                           + (importedSettings != null ? " Settings restored." : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSpoiler()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Spoiler Log",
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"spoiler_{RandomizerVm.Seed}.json"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var log = _randService.BuildSpoilerLog(
                    ItemLocations.Select(v => v.Location).ToList(),
                    new RandomizerSettings { Seed = RandomizerVm.Seed });
                _randService.ExportSpoilerLog(log, dlg.FileName);
                StatusText = $"Spoiler log exported to {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
