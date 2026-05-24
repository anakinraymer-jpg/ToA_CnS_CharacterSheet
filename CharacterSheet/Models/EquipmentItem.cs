using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CharacterSheet.Models;

public class EquipmentItem : INotifyPropertyChanged
{
    private string _name        = "";
    private string _description = "";
    private bool   _used;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(Tooltip)); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); OnPropertyChanged(nameof(Tooltip)); }
    }

    public bool Used
    {
        get => _used;
        set { _used = value; OnPropertyChanged(); }
    }

    /// <summary>Returns null (no tooltip) when Description is blank.</summary>
    public string? Tooltip => string.IsNullOrWhiteSpace(_description) ? null : _description;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
