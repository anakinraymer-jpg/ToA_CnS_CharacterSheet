using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CharacterSheet.Models;

public class CharacterState : INotifyPropertyChanged
{
    private string _name        = "";
    private string _lineage     = "";
    private string _hometown    = "";
    private string _flaw1       = "";
    private string _flaw2       = "";
    private string _flaw3       = "";
    private string _flaw4       = "";
    private string _coreAbility = "";
    private string _summary     = "";
    private string _portrait    = "";

    public string Name        { get => _name;        set { _name        = value; OnPropertyChanged(); } }
    public string Lineage     { get => _lineage;     set { _lineage     = value; OnPropertyChanged(); } }
    public string Hometown    { get => _hometown;    set { _hometown    = value; OnPropertyChanged(); } }
    public string Flaw1       { get => _flaw1;       set { _flaw1       = value; OnPropertyChanged(); } }
    public string Flaw2       { get => _flaw2;       set { _flaw2       = value; OnPropertyChanged(); } }
    public string Flaw3       { get => _flaw3;       set { _flaw3       = value; OnPropertyChanged(); } }
    public string Flaw4       { get => _flaw4;       set { _flaw4       = value; OnPropertyChanged(); } }
    public string CoreAbility { get => _coreAbility; set { _coreAbility = value; OnPropertyChanged(); } }
    public string Summary     { get => _summary;     set { _summary     = value; OnPropertyChanged(); } }
    public string Portrait    { get => _portrait;    set { _portrait    = value; OnPropertyChanged(); } }

    public ObservableCollection<EquipmentItem> Equipment { get; set; } = [];
    public ObservableCollection<SkillItem>     Skills    { get; set; } = [];
    public ObservableCollection<string>        Spells    { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static CharacterState CreateDefault()
    {
        var s = new CharacterState();
        s.Spells.Add(""); s.Spells.Add(""); s.Spells.Add("");
        return s;
    }
}
