using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FSARandomizer
{
    public partial class AddActorDialog : Window
    {
        public string ActorId { get; private set; } = "TKRA";
        public byte Layer { get; private set; }
        public byte X { get; private set; } = 16;
        public byte Y { get; private set; } = 16;
        public uint Variable { get; private set; } = 0x10;

        public AddActorDialog()
        {
            InitializeComponent();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadValues()) return;
            DialogResult = true;
        }

        private bool TryReadValues()
        {
            ActorId = IdBox.Text.PadRight(4).Substring(0, 4);
            if (!byte.TryParse(LayerBox.Text, out var l)) { ShowError("Layer must be 0–255."); return false; }
            if (!byte.TryParse(XBox.Text, out var x))     { ShowError("X must be 0–255."); return false; }
            if (!byte.TryParse(YBox.Text, out var y))     { ShowError("Y must be 0–255."); return false; }
            Layer = l; X = x; Y = y;

            string varStr = VarBox.Text.Replace("0x", "").Replace("0X", "").Trim();
            if (!uint.TryParse(varStr, NumberStyles.HexNumber, null, out var v))
            { ShowError("Variable must be a 4-byte hex value like 0x00000010."); return false; }
            Variable = v;
            return true;
        }

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetCombo.SelectedItem is not ComboBoxItem item) return;
            var parts = item.Tag?.ToString()?.Split('|');
            if (parts?.Length != 5) return;
            IdBox.Text    = parts[0];
            LayerBox.Text = parts[1];
            XBox.Text     = parts[2];
            YBox.Text     = parts[3];
            VarBox.Text   = parts[4];
        }

        private void ShowError(string msg) =>
            MessageBox.Show(msg, "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
