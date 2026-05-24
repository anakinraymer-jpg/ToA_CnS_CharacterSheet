using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CharacterSheet.Models;

public class RowData : INotifyPropertyChanged
{
    private string _equipName = "";
    private string _equipSub  = "";
    private bool   _equipUsed;
    private bool   _skillAdv;
    private string _skillName = "";
    private string _skillSub  = "";
    private string _die       = "d6";

    public string EquipName { get => _equipName; set { _equipName = value; OnPropertyChanged(); } }
    public string EquipSub  { get => _equipSub;  set { _equipSub  = value; OnPropertyChanged(); } }
    public bool   EquipUsed { get => _equipUsed; set { _equipUsed = value; OnPropertyChanged(); } }
    public bool   SkillAdv  { get => _skillAdv;  set { _skillAdv  = value; OnPropertyChanged(); } }
    public string SkillName { get => _skillName; set { _skillName = value; OnPropertyChanged(); } }
    public string SkillSub  { get => _skillSub;  set { _skillSub  = value; OnPropertyChanged(); } }
    public string Die       { get => _die;       set { _die       = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CharacterState : INotifyPropertyChanged
{
    private string _name     = "";
    private string _lineage  = "";
    private string _hometown = "";
    private string _flaws    = "";
    private string _summa    = "";
    private string _portrait = "";

    public string Name     { get => _name;     set { _name     = value; OnPropertyChanged(); } }
    public string Lineage  { get => _lineage;  set { _lineage  = value; OnPropertyChanged(); } }
    public string Hometown { get => _hometown; set { _hometown = value; OnPropertyChanged(); } }
    public string Flaws    { get => _flaws;    set { _flaws    = value; OnPropertyChanged(); } }
    public string Summa    { get => _summa;    set { _summa    = value; OnPropertyChanged(); } }
    public string Portrait { get => _portrait; set { _portrait = value; OnPropertyChanged(); } }

    public ObservableCollection<RowData> Rows   { get; set; } = [];
    public ObservableCollection<string>  Spells { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static CharacterState CreateDefault()
    {
        var s = new CharacterState();
        for (int i = 0; i < 10; i++) s.Rows.Add(new RowData());
        s.Spells.Add(""); s.Spells.Add(""); s.Spells.Add("");
        return s;
    }
}
