using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowThumbWall;

internal sealed class AutoAddAppEntry : INotifyPropertyChanged
{
    private string _displayName = string.Empty;

    public required string ProcessName { get; init; }

    public required string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal))
                return;

            _displayName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
