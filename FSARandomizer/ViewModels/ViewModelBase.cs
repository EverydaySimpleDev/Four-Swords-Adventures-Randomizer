using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FSARandomizer.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected void RefreshAll()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
