using FSALib.Structs;
using FSARandomizer.Models;
using FSARandomizer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuroraLib.Core.Format.Identifier;

namespace FSARandomizer
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private ActorViewModel? _selectedActor;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            PopulateChestItemCombo();
            PopulateFilterCombos();
        }

        // ── Actor selection ───────────────────────────────────────────────────

        public ActorViewModel? SelectedActor => _selectedActor;

        private void ActorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedActor = ActorGrid.SelectedItem as ActorViewModel;
            UpdateActorDetailPanel(_selectedActor);
        }

        private void UpdateActorDetailPanel(ActorViewModel? actor)
        {
            if (actor == null)
            {
                ActorDetailBorder.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            ActorDetailBorder.Visibility = System.Windows.Visibility.Visible;

            // Update chest item combo to reflect V4 of selected actor
            foreach (ComboBoxItem item in ChestItemCombo.Items)
            {
                if (item.Tag is byte b && b == actor.V4)
                {
                    ChestItemCombo.SelectedItem = item;
                    break;
                }
            }
        }

        // ── Level tree selection ──────────────────────────────────────────────

        private void LevelTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            switch (e.NewValue)
            {
                case LevelViewModel level:
                    _vm.SelectedLevel = level;
                    _vm.SelectedRoom = level.Rooms.FirstOrDefault();
                    break;
                case RoomViewModel room:
                    var parentLevel = _vm.Levels.FirstOrDefault(l => l.Rooms.Contains(room));
                    if (parentLevel != null) _vm.SelectedLevel = parentLevel;
                    _vm.SelectedRoom = room;
                    break;
            }
            EmptyStateLabel.Visibility = _vm.SelectedRoom == null
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            _selectedActor = null;
            ActorDetailBorder.Visibility = System.Windows.Visibility.Collapsed;
        }

        // ── Room actions ──────────────────────────────────────────────────────

        private void SaveRoom_Click(object sender, RoutedEventArgs e)
        {
            _vm.SelectedRoom?.Save();
        }

        private void AddActor_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedRoom == null) return;

            var dialog = new AddActorDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var actor = new Actor(
                    new Identifier32(dialog.ActorId.PadRight(4).AsSpan()),
                    dialog.Layer, dialog.X, dialog.Y, dialog.Variable);
                _vm.SelectedRoom.AddActor(actor);
            }
        }

        // ── Chest item quick edit ─────────────────────────────────────────────

        private void PopulateChestItemCombo()
        {
            ChestItemCombo.Items.Clear();
            foreach (var kvp in GameData.ChestItems.OrderBy(k => k.Key))
            {
                ChestItemCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"0x{kvp.Key:X2}  {kvp.Value}",
                    Tag = kvp.Key
                });
            }
        }

        private void ChestItemCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedActor == null) return;
            if (ChestItemCombo.SelectedItem is ComboBoxItem item && item.Tag is byte b)
            {
                _selectedActor.V4 = b;
            }
        }

        // ── Item Location filters ─────────────────────────────────────────────

        private void PopulateFilterCombos()
        {
            WorldFilterCombo.Items.Clear();
            WorldFilterCombo.Items.Add("All Worlds");

            TypeFilterCombo.Items.Clear();
            TypeFilterCombo.Items.Add("All Types");
            TypeFilterCombo.Items.Add("Treasure Chest");
            TypeFilterCombo.Items.Add("Floor Item");

            WorldFilterCombo.SelectedIndex = 0;
            TypeFilterCombo.SelectedIndex = 0;
        }

        private void RefreshWorldFilter()
        {
            var current = WorldFilterCombo.SelectedItem?.ToString();
            WorldFilterCombo.Items.Clear();
            WorldFilterCombo.Items.Add("All Worlds");
            foreach (var world in _vm.ItemLocations.Select(l => l.WorldName).Distinct().OrderBy(w => w))
                WorldFilterCombo.Items.Add(world);

            WorldFilterCombo.SelectedItem = current ?? "All Worlds";
            if (WorldFilterCombo.SelectedIndex < 0) WorldFilterCombo.SelectedIndex = 0;

            FilterCountLabel.Text = $"{_vm.ItemLocations.Count} locations";
        }

        private void WorldFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void TypeFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            var view = CollectionViewSource.GetDefaultView(_vm.ItemLocations);
            if (view == null) return;

            string world = WorldFilterCombo.SelectedItem?.ToString() ?? "All Worlds";
            string type  = TypeFilterCombo.SelectedItem?.ToString()  ?? "All Types";
            string search = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";

            view.Filter = obj =>
            {
                if (obj is not ItemLocationViewModel loc) return false;
                if (world != "All Worlds" && loc.WorldName != world) return false;
                if (type  != "All Types"  && loc.ActorTypeName != type) return false;
                if (search.Length > 0)
                {
                    if (!loc.LevelName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                        !loc.OriginalItemName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                        !loc.RandomizedItemName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return true;
            };

            int count = view.Cast<object>().Count();
            FilterCountLabel.Text = $"{count} / {_vm.ItemLocations.Count} locations";
        }

        // Keep filter combos populated when game loads
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _vm.ItemLocations.CollectionChanged += (_, _) => RefreshWorldFilter();
        }
    }
}
