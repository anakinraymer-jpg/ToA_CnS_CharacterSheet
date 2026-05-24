using System.ComponentModel;
using System.Runtime.CompilerServices;
using CharacterSheet.Data;

namespace CharacterSheet.Models;

public class SkillItem : INotifyPropertyChanged
{
    private string _name        = "";
    private string _description = "";
    private bool   _adv;
    private int    _rating = 0;

    public string Name
    {
        get => _name;
        set
        {
            bool hadSkill = HasSkill;
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSkill));
            OnPropertyChanged(nameof(Tooltip));
            if (!hadSkill && HasSkill && _rating == 0)
                Rating = 3;
            else if (hadSkill && !HasSkill)
                Rating = 0;
        }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); OnPropertyChanged(nameof(Tooltip)); }
    }

    public bool Adv
    {
        get => _adv;
        set { _adv = value; OnPropertyChanged(); }
    }

    public bool HasSkill => !string.IsNullOrWhiteSpace(_name);

    public int Rating
    {
        get => _rating;
        set
        {
            _rating = HasSkill ? Math.Clamp(value, 3, 18) : 0;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Custom description first; falls back to the built-in SkillList description.
    /// Returns null when there is nothing to show.
    /// </summary>
    public string? Tooltip =>
        !string.IsNullOrWhiteSpace(_description) ? _description :
        SkillList.GetDescription(_name);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
