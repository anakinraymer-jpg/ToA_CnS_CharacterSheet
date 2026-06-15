using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace CharacterSheet.Map;

/// <summary>
/// The full map tab: canvas on the left, control panel on the right.
/// Ports MainWindow (main_window.py) into a WPF UserControl.
/// </summary>
public partial class MapView : UserControl
{
    // ── Chult default grid ─────────────────────────────────────────────
    private static readonly (double Size, (double X, double Y) Origin,
        string Orientation, int Cols, int Rows) ChultGrid
        = (87.0, (150.0, 138.0), "flat", 33, 39);

    private static readonly string[] WeatherConditions =
    [
        "Misty", "Heavy Mist", "Dry and Sunny", "Sunny with Rain Showers",
        "Rainy", "Heavy Rain", "Tropical Storm", "Extremely Warm",
        "Extremely Warm and Rainy", "Extremely Warm and Dry", "Monsoon Shift",
    ];

    // ── Map state ──────────────────────────────────────────────────────
    private readonly HexGrid            _grid;
    private readonly MapEntityManager   _em;
    private readonly MapLocationManager _lm;
    private readonly MapTerrainMap      _tm;
    private readonly HexMapCanvas       _canvas;

    private int    _day                      = 1;
    private string _currentWeather           = "";
    private string _bonusMoveEntityId        = "";    // entity ID with unused bonus move, or ""
    private bool   _advanceDayAfterBonusMove = false; // advance the day once the pending bonus move is used
    private bool   _suppressWeatherEvent;

    /// <summary>Fires whenever the current weather condition changes (empty string = no weather).</summary>
    public event Action<string>? WeatherChanged;

    /// <summary>The current weather condition string, or empty when none is set.</summary>
    public string CurrentWeather => _currentWeather;

    // The character sheet PC name (for auto-marking IsPlayer on load/add)
    public string PlayerName { get; set; } = "";

    public HexMapCanvas Canvas => _canvas;

    // ── Constructor ────────────────────────────────────────────────────

    public MapView()
    {
        InitializeComponent();

        _grid = new HexGrid(ChultGrid.Size, ChultGrid.Origin,
                            ChultGrid.Orientation, ChultGrid.Cols, ChultGrid.Rows);
        _em  = new MapEntityManager();
        _lm  = new MapLocationManager();
        _tm  = new MapTerrainMap();

        _canvas = new HexMapCanvas(_grid, _em, _lm, _tm);
        _canvas.FogEnabled = true;
        CanvasHost.Children.Add(_canvas);

        // Wire canvas events
        _canvas.EntitySelected      += OnEntitySelected;
        _canvas.MoveRequested       += OnMoveRequested;
        _canvas.OriginClicked       += OnOriginClicked;
        _canvas.RightClickedEntity  += OnRightClickEntity;
        _canvas.RightClickedHex     += OnCanvasRightClickHex;

        // Populate weather combo
        WeatherCombo.Items.Add("— None —");
        foreach (var w in WeatherConditions)
            WeatherCombo.Items.Add(w);
        WeatherCombo.SelectedIndex = 0;

        // Populate terrain paint combo
        TerrainPaintCombo.Items.Add("— Clear —");
        foreach (var t in MapTerrain.All)
            TerrainPaintCombo.Items.Add(t);
        TerrainPaintCombo.SelectedIndex = 0;

        UpdateDayUi();
        SetStatus($"Chult grid loaded  |  {_grid.MaxNumber} hex points " +
                  $"({_grid.Cols}×{_grid.Rows}, size {(int)_grid.Size} px)");

        Loaded += OnLoaded;
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;   // fire once
        var prefs = MapPrefs.Load();

        if (!string.IsNullOrEmpty(prefs.LastMapImage) && File.Exists(prefs.LastMapImage))
        {
            _canvas.LoadImage(prefs.LastMapImage);
            SetStatus($"Map image restored: {System.IO.Path.GetFileName(prefs.LastMapImage)}");
        }

        if (!string.IsNullOrEmpty(prefs.LastMapSave) && File.Exists(prefs.LastMapSave))
            LoadState(prefs.LastMapSave, PlayerName);
    }

    // ── Day panel ──────────────────────────────────────────────────────

    private void UpdateDayUi()
    {
        var entities = _em.TopLevel;
        int n = CountMoved();
        DayLabel.Text         = $"Day {_day}";
        DayProgressLabel.Text = $"{n} / {entities.Count} acted";
    }

    private int CountMoved()
    {
        // We don't have direct access to _canvas._movedIds — we use the
        // FogRevealed set as a proxy for now. Actually expose via a method.
        return _canvas.CountMoved(_em.TopLevel.Select(e => e.Id));
    }

    private string ProgressStr()
    {
        int n = CountMoved(), total = _em.TopLevel.Count;
        return $"{n}/{total} acted";
    }

    private void OnWaitClick(object s, RoutedEventArgs e)
    {
        // Canvas exposes selected entity via public property
        var entity = _canvas.SelectedEntity;
        if (entity is null || _canvas.HasMoved(entity.Id)) return;
        bool wasAdvanceBonus = _advanceDayAfterBonusMove && entity.Id == _bonusMoveEntityId;
        _bonusMoveEntityId = "";
        _canvas.MarkMoved(entity.Id);
        _canvas.SetSelected(entity);
        if (wasAdvanceBonus)
        {
            SetStatus($"'{entity.Name}' passed on extra move.");
            AdvanceDay();
        }
        else
        {
            SetStatus($"'{entity.Name}' is waiting.  [{ProgressStr()}]");
            RefreshEntityList();
            UpdateDayUi();
            CheckDayEnd();
        }
    }

    private void OnAdvanceDayClick(object s, RoutedEventArgs e)
        => ShowAdvanceDayDialog(allActed: false);

    private void OnEditDayClick(object s, RoutedEventArgs e)
    {
        var dlg = new MapInputDialog("Edit Day", "Set current day:", _day.ToString(), Window.GetWindow(this));
        if (dlg.ShowDialog() == true && int.TryParse(dlg.Value, out int v) && v >= 1)
        {
            _day = v;
            UpdateDayUi();
        }
    }

    private void AdvanceDay()
    {
        _advanceDayAfterBonusMove = false;
        _day++;
        _canvas.ClearMoved();
        _bonusMoveEntityId = "";
        _canvas.SetSelected(null);
        RefreshEntityList();
        UpdateDayUi();
        SetStatus($"Day {_day} has begun!");
    }

    private void CheckDayEnd()
    {
        var entities = _em.TopLevel;
        if (entities.Count == 0) return;
        if (entities.All(e => _canvas.HasMoved(e.Id)))
            ShowAdvanceDayDialog(allActed: true);
    }

    private void ShowAdvanceDayDialog(bool allActed)
    {
        string intro = allActed ? "All entities have acted.\n\n" : "";
        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"{intro}Advance to Day {_day + 1}?\n\n" +
            "Yes       Advance now\n" +
            "No        Take one extra hex move, then advance\n" +
            "Cancel    Stay on Day " + _day,
            "Advance Day?",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Yes)
            AdvanceDay();
        else if (result == MessageBoxResult.No)
            GrantExtraMoveBeforeAdvance();
        else
            SetStatus($"Day {_day} held — click Advance Day when ready.");
    }

    private void GrantExtraMoveBeforeAdvance()
    {
        var entity = _canvas.SelectedEntity
            ?? _em.TopLevel.FirstOrDefault(e => e.IsPlayer)
            ?? _em.TopLevel.FirstOrDefault();

        if (entity is null) { AdvanceDay(); return; }

        _canvas.ClearMovedId(entity.Id);
        _bonusMoveEntityId        = entity.Id;
        _advanceDayAfterBonusMove = true;
        _canvas.SetSelected(entity);
        RefreshEntityList();
        UpdateDayUi();
        SetStatus($"'{entity.Name}' may take one extra hex before Day {_day + 1}.  " +
                  "Click a target hex or press W to skip.");
    }

    // ── Weather panel ──────────────────────────────────────────────────

    private void OnWeatherChanged(object s, SelectionChangedEventArgs e)
    {
        if (_suppressWeatherEvent) return;
        _currentWeather = WeatherCombo.SelectedIndex <= 0
            ? "" : (string)WeatherCombo.SelectedItem;
        WeatherChanged?.Invoke(_currentWeather);
    }

    private void OnRollWeatherClick(object s, RoutedEventArgs e)
    {
        int idx = Random.Shared.Next(1, WeatherConditions.Length + 1);
        WeatherCombo.SelectedIndex = idx;
    }

    // ── Entity panel ───────────────────────────────────────────────────

    private void RefreshEntityList()
    {
        EntityList.SelectionChanged -= OnEntityListSelectionChanged;
        EntityList.Items.Clear();
        foreach (var entity in _em.TopLevel)
        {
            var tags = new List<string>();
            if (entity.IsGroup)    tags.Add("G");
            if (entity.Seafaring)  tags.Add("S");
            string playerMark = entity.IsPlayer ? "★ " : "";
            string prefix     = tags.Count > 0 ? $"[{string.Join("|", tags)}] " : "";
            string cnt        = entity.IsGroup && entity.Members.Count > 0
                                    ? $"  ({entity.Members.Count} mbr)" : "";
            bool acted  = _canvas.HasMoved(entity.Id);
            bool bonus  = entity.Id == _bonusMoveEntityId;
            string turn = acted ? " ✓" : bonus ? " ●" : "";
            string label = $"{playerMark}{prefix}{entity.Name}{cnt}  → {entity.Node}{turn}";

            var item = new ListBoxItem
            {
                Content = label,
                Tag     = entity.Id,
                Foreground = acted
                    ? new SolidColorBrush(ParseColor(entity.Color) with { A = 140 })
                    : new SolidColorBrush(ParseColor(entity.Color)),
            };
            EntityList.Items.Add(item);
        }
        EntityList.SelectionChanged += OnEntityListSelectionChanged;
    }

    private void OnEntityListSelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (EntityList.SelectedItem is not ListBoxItem item) return;
        var entity = _em.Get((string)item.Tag!);
        if (entity is null) return;
        // Guard: OnEntitySelected rebuilds the list then sets SelectedIndex to sync it.
        // That re-fires this handler — if the entity is already selected, bail out to
        // avoid the infinite loop: SetSelected → EntitySelected → OnEntitySelected →
        // RefreshEntityList → SelectedIndex = i → SelectionChanged → SetSelected → ∞
        if (_canvas.SelectedEntity?.Id == entity.Id) return;
        _canvas.SetHighlightedLocation(-1);
        LocationList.SelectedIndex = -1;
        _canvas.SetSelected(entity);
    }

    private void OnAddEntityClick(object s, RoutedEventArgs e)
    {
        var dlg = new MapAddEntityDialog(_grid.MaxNumber, PlayerName, Window.GetWindow(this));
        if (dlg.ShowDialog() != true) return;
        var (name, node, color, isGroup, seafaring) = dlg.Values;
        bool isPlayer = !string.IsNullOrEmpty(PlayerName) &&
            string.Equals(name.Trim(), PlayerName.Trim(), StringComparison.OrdinalIgnoreCase);
        var entity = new MapEntity
        {
            Name = name, Node = node, Color = color,
            IsGroup = isGroup, Seafaring = seafaring, IsPlayer = isPlayer,
        };
        _em.Add(entity);
        _canvas.RevealHex(node);
        RefreshLocationList();
        RefreshEntityList();
        _canvas.Refresh();
        NotifyPlayerLocation();
    }

    private void OnEditEntityClick(object s, RoutedEventArgs e)
    {
        if (EntityList.SelectedItem is not ListBoxItem item) return;
        var entity = _em.Get((string)item.Tag!);
        if (entity != null) OnRightClickEntity(entity);
    }

    private void OnRemoveEntityClick(object s, RoutedEventArgs e)
    {
        if (EntityList.SelectedItem is not ListBoxItem item) return;
        string eid = (string)item.Tag!;
        var sel = _canvas.SelectedEntity;
        if (sel?.Id == eid) _canvas.SetSelected(null);
        var entity = _em.Get(eid);
        if (entity?.IsGroup == true)
            _em.DisbandGroup(eid);
        else
            _em.Remove(eid);
        if (eid == _bonusMoveEntityId) _bonusMoveEntityId = "";
        RefreshEntityList();
        UpdateDayUi();
        _canvas.Refresh();
    }

    // ── Location panel ─────────────────────────────────────────────────

    private void RefreshLocationList()
    {
        LocationList.SelectionChanged -= OnLocationListSelectionChanged;
        LocationList.Items.Clear();
        foreach (var loc in _lm.All)
        {
            if (!_canvas.FogRevealed.Contains(loc.Node)) continue;
            string typeTag = string.IsNullOrEmpty(loc.LocationType)
                ? "" : $"  [{loc.LocationType}]";
            var item = new ListBoxItem
            {
                Content    = $"{loc.Name}{typeTag}  → {loc.Node}",
                Tag        = loc.Id,
                Foreground = new SolidColorBrush(ParseColor(loc.Color)),
            };
            LocationList.Items.Add(item);
        }
        LocationList.SelectionChanged += OnLocationListSelectionChanged;
    }

    private void OnLocationListSelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (LocationList.SelectedItem is not ListBoxItem item) return;
        var loc = _lm.Get((string)item.Tag!);
        if (loc is null) return;
        _canvas.SetSelected(null);
        EntityList.SelectedIndex = -1;
        string typeLine = string.IsNullOrEmpty(loc.LocationType)
            ? "" : $"\nType: {loc.LocationType}";
        string descLine = string.IsNullOrEmpty(loc.Description)
            ? "" : $"\n{loc.Description}";
        InfoLabel.Text = $"Location: {loc.Name}\nNode: {loc.Node}{typeLine}{descLine}";
        _canvas.SetHighlightedLocation(loc.Node);
        _canvas.Refresh();
    }

    private void OnAddLocationClick(object s, RoutedEventArgs e)
    {
        var dlg = new MapAddLocationDialog(_grid.MaxNumber, 1, Window.GetWindow(this));
        if (dlg.ShowDialog() != true) return;
        var (name, node, color, desc, locType) = dlg.Values;
        _lm.Add(new MapLocation
        {
            Name = name, Node = node, Color = color,
            Description = desc, LocationType = locType,
        });
        RefreshLocationList();
        _canvas.Refresh();
        NotifyPlayerLocation();
        SetStatus($"Location '{name}' placed at node {node}.");
    }

    private void OnEditLocationClick(object s, RoutedEventArgs e)
    {
        if (LocationList.SelectedItem is not ListBoxItem item) return;
        var loc = _lm.Get((string)item.Tag!);
        if (loc is null) return;
        var dlg = new MapAddLocationDialog(_grid.MaxNumber, loc.Node, Window.GetWindow(this))
        {
            InitialName = loc.Name,
            InitialDesc = loc.Description,
            InitialColor = loc.Color,
            InitialType  = loc.LocationType,
            Title        = "Edit Location",
        };
        if (dlg.ShowDialog() != true) return;
        var (name, node, color, desc, locType) = dlg.Values;
        _lm.Update(loc.Id, l =>
        {
            l.Name = name; l.Node = node; l.Color = color;
            l.Description = desc; l.LocationType = locType;
        });
        RefreshLocationList();
        _canvas.Refresh();
        NotifyPlayerLocation();
        SetStatus($"Location '{name}' updated.");
    }

    private void OnRemoveLocationClick(object s, RoutedEventArgs e)
    {
        if (LocationList.SelectedItem is not ListBoxItem item) return;
        var loc = _lm.Get((string)item.Tag!);
        if (loc is null) return;
        _lm.Remove(loc.Id);
        _canvas.SetHighlightedLocation(-1);
        RefreshLocationList();
        _canvas.Refresh();
        NotifyPlayerLocation();
        SetStatus($"Location '{loc.Name}' removed.");
    }

    // ── Canvas event handlers ──────────────────────────────────────────

    private void OnEntitySelected(MapEntity? entity)
    {
        RefreshEntityList();
        if (entity is null)
        {
            InfoLabel.Text = "None";
            MembersGroup.Visibility = Visibility.Collapsed;
            return;
        }

        string kindLine  = entity.IsGroup ? $"Group ({entity.Members.Count} members)" : "Character";
        string seaLine   = entity.Seafaring ? "\nSea­faring" : "";
        string flavorLine = string.IsNullOrEmpty(entity.FlavorText) ? "" : $"\n\n{entity.FlavorText}";
        InfoLabel.Text = $"{kindLine}: {entity.Name}\nNode: {entity.Node}{seaLine}{flavorLine}";

        // Sync list selection
        for (int i = 0; i < EntityList.Items.Count; i++)
        {
            if (EntityList.Items[i] is ListBoxItem li && (string)li.Tag! == entity.Id)
            {
                EntityList.SelectedIndex = i;
                break;
            }
        }

        // Group members
        MembersList.Items.Clear();
        if (entity.IsGroup)
        {
            MembersGroup.Visibility = Visibility.Visible;
            foreach (var mid in entity.Members)
            {
                var m = _em.Get(mid);
                if (m is null) continue;
                string label = m.Seafaring ? $"{m.Name}  [Seafaring]" : m.Name;
                MembersList.Items.Add(label);
            }
        }
        else
        {
            MembersGroup.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMoveRequested(MapEntity entity, int targetNode)
    {
        bool isBonus = entity.Id == _bonusMoveEntityId;

        _em.Move(entity.Id, targetNode);
        if (entity.IsPlayer) NotifyPlayerLocation();
        _canvas.RevealHex(targetNode);
        RefreshLocationList();
        CheckMergeOpportunity(targetNode, entity);

        if (_em.IsGroupMember(entity.Id))
        {
            _bonusMoveEntityId = "";
            RefreshEntityList();
            UpdateDayUi();
            return;
        }

        if (isBonus)
        {
            _bonusMoveEntityId = "";
            _canvas.MarkMoved(entity.Id);
            _canvas.SetSelected(entity);
            if (_advanceDayAfterBonusMove)
            {
                SetStatus($"'{entity.Name}' took extra move → node {targetNode}.  Advancing day…");
                AdvanceDay();
            }
            else
            {
                SetStatus($"'{entity.Name}' used bonus move → node {targetNode}.  [{ProgressStr()}]");
                CheckDayEnd();
            }
        }
        else
        {
            if (Random.Shared.NextDouble() < 0.25)
            {
                _bonusMoveEntityId = entity.Id;
                _canvas.SetSelected(entity);
                SetStatus($"'{entity.Name}' moved → node {targetNode}.  " +
                          "Lucky! One extra move — click target or press W to pass.");
            }
            else
            {
                _bonusMoveEntityId = "";
                _canvas.MarkMoved(entity.Id);
                _canvas.SetSelected(entity);
                SetStatus($"'{entity.Name}' moved → node {targetNode}.  [{ProgressStr()}]");
                CheckDayEnd();
            }
        }

        RefreshEntityList();
        UpdateDayUi();
    }

    private void CheckMergeOpportunity(int node, MapEntity moved)
    {
        var entities = _em.AtNode(node);
        if (entities.Count >= 2) ResolveEncounterOnHex(node, entities);
    }

    private void ResolveEncounterOnHex(int node, IReadOnlyList<MapEntity> entities)
    {
        if (entities.Count < 2) return;
        var names = string.Join(" & ", entities.Select(e => e.Name));
        var win   = Window.GetWindow(this);

        var dlg = new MapEncounterDialog(names, node, win);
        dlg.ShowDialog();
        switch (dlg.Result)
        {
            case MapEncounterDialog.EncounterResult.Combat:
                ResolveCombat(node, entities);
                return;
            case MapEncounterDialog.EncounterResult.Nothing:
                RefreshEntityList();
                _canvas.Refresh();
                return;
            case MapEncounterDialog.EncounterResult.Merge:
                break;
        }

        // Merge
        var groups = entities.Where(e => e.IsGroup).ToList();
        var indivs = entities.Where(e => !e.IsGroup).ToList();

        if (groups.Count > 0 && indivs.Count > 0)
        {
            var group = groups[0];
            var addNames = string.Join(", ", indivs.Select(e => e.Name));
            var ans = MessageBox.Show(win,
                $"{addNames} {(indivs.Count > 1 ? "are" : "is")} on the same hex " +
                $"as group '{group.Name}'.\nAdd them to the group?",
                "Join Group?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes)
            {
                foreach (var en in indivs) _em.AddToGroup(group.Id, en.Id);
                _canvas.SetSelected(group);
            }
        }
        else if (indivs.Count >= 2)
        {
            var ans = MessageBox.Show(win,
                $"Multiple entities on hex {node}:\n{names}\n\nMerge them into a group?",
                "Merge Into Group?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes)
            {
                string defaultName = string.Join(" & ",
                    indivs.Select(e => e.Name.Split(' ')[0]));
                var nameDialog = new MapInputDialog("Group Name", "Name the new group:",
                                                   defaultName, win);
                if (nameDialog.ShowDialog() == true && !string.IsNullOrEmpty(nameDialog.Value))
                {
                    var newGroup = _em.CreateGroupFromEntities(
                        indivs.Select(e => e.Id), nameDialog.Value, indivs[0].Color);
                    _canvas.SetSelected(newGroup);
                }
            }
        }

        RefreshEntityList();
        _canvas.Refresh();
    }

    private void ResolveCombat(int node, IReadOnlyList<MapEntity> entities)
    {
        var dlg = new MapCombatDialog(entities, node, Window.GetWindow(this));
        if (dlg.ShowDialog() != true)
        {
            RefreshEntityList(); _canvas.Refresh(); return;
        }

        var winnerIds = dlg.WinnerIds.ToHashSet();
        var losers    = entities.Where(e => !winnerIds.Contains(e.Id)).ToList();
        var loserAction = dlg.LoserAction;
        var neighbors = _grid.NeighborNumbers(node);

        foreach (var loser in losers)
        {
            if (loserAction == "remove")
            {
                RemoveEntityAndMembers(loser);
                SetStatus($"'{loser.Name}' was removed from the map after combat.");
            }
            else
            {
                if (neighbors.Count == 0)
                {
                    RemoveEntityAndMembers(loser);
                    SetStatus($"'{loser.Name}' has nowhere to flee — removed.");
                    continue;
                }
                var neighborItems = neighbors.Select(n =>
                {
                    var terrain = _tm.Get(n);
                    return terrain is not null ? $"{n}  [{terrain}]" : n.ToString();
                }).ToList();
                var picker = new MapItemPickDialog(
                    $"Move Loser — {loser.Name}",
                    $"Move '{loser.Name}' to which adjacent hex?",
                    neighborItems, Window.GetWindow(this));
                if (picker.ShowDialog() == true)
                {
                    int destNode = int.Parse(picker.SelectedItem!.Split(' ')[0]);
                    _em.Move(loser.Id, destNode);
                    SetStatus($"'{loser.Name}' fled to hex {destNode} after combat.");
                }
                else
                {
                    SetStatus($"'{loser.Name}' stays on hex {node}.");
                }
            }
        }

        if (_canvas.SelectedEntity is { } sel && _em.Get(sel.Id) is null)
            _canvas.SetSelected(null);

        RefreshEntityList();
        UpdateDayUi();
        _canvas.Refresh();
    }

    private void RemoveEntityAndMembers(MapEntity entity)
    {
        _canvas.ClearMovedId(entity.Id);
        if (entity.Id == _bonusMoveEntityId) _bonusMoveEntityId = "";
        if (entity.IsGroup)
        {
            foreach (var mid in entity.Members.ToList())
            {
                _canvas.ClearMovedId(mid);
                _em.Remove(mid);
            }
        }
        _em.Remove(entity.Id);
    }

    private void OnOriginClicked(double x, double y)
    {
        _grid.Reconfigure(origin: (x, y));
        _canvas.InvalidateGeoCache();
        _canvas.Refresh();
        SetStatus($"Origin set to ({x:0}, {y:0})  —  use Grid Settings to fine-tune size.");
    }

    private void OnRightClickEntity(MapEntity entity)
    {
        if (entity.IsGroup) EditGroup(entity);
        else                EditIndividual(entity);
    }

    private void OnCanvasRightClickHex(int node)
    {
        // Show terrain paint context menu
        var menu = new ContextMenu();
        // "Clear" item
        var clearItem = new MenuItem { Header = "Clear terrain" };
        clearItem.Click += (_, _) =>
        {
            _tm.Clear(node);
            _canvas.Refresh();
            NotifyPlayerLocation();
        };
        menu.Items.Add(clearItem);
        menu.Items.Add(new Separator());
        // One item per terrain type
        foreach (var t in MapTerrain.All)
        {
            var terrain = t;
            var menuItem = new MenuItem { Header = terrain };
            menuItem.Click += (_, _) =>
            {
                _tm.Set(node, terrain);
                _canvas.Refresh();
                NotifyPlayerLocation();
            };
            menu.Items.Add(menuItem);
        }
        menu.IsOpen = true;
    }

    private void EditIndividual(MapEntity entity)
    {
        var dlg = new MapEditEntityDialog(entity, Window.GetWindow(this));
        if (dlg.ShowDialog() != true) return;
        entity.Name       = dlg.Values.Name;
        entity.Color      = dlg.Values.Color;
        entity.Seafaring  = dlg.Values.Seafaring;
        entity.FlavorText = dlg.Values.FlavorText;
        RefreshEntityList();
        OnEntitySelected(entity);
        if (_canvas.SelectedEntity?.Id == entity.Id)
            _canvas.SetSelected(entity);
        _canvas.Refresh();
        SetStatus($"'{entity.Name}' updated.");
    }

    private void EditGroup(MapEntity group)
    {
        var dlg = new MapEditGroupDialog(group, _em, Window.GetWindow(this));
        if (dlg.ShowDialog() != true)
        {
            RefreshEntityList(); _canvas.Refresh(); return;
        }
        if (dlg.ShouldDisband)
        {
            _em.DisbandGroup(group.Id);
            _canvas.SetSelected(null);
            SetStatus($"Group '{group.Name}' disbanded.");
        }
        else
        {
            group.Name      = dlg.Values.Name;
            group.Seafaring = dlg.Values.Seafaring;
            group.FlavorText = dlg.Values.FlavorText;
            var newMbrs = dlg.Values.Members.ToHashSet();
            var oldMbrs = group.Members.ToHashSet();
            foreach (var id in oldMbrs.Except(newMbrs)) _em.RemoveFromGroup(group.Id, id);
            foreach (var id in newMbrs.Except(oldMbrs)) _em.AddToGroup(group.Id, id);
            _canvas.SetSelected(group);
            OnEntitySelected(group);
            SetStatus($"Group '{group.Name}' updated.");
        }
        RefreshEntityList();
        _canvas.Refresh();
    }

    // ── Toolbar buttons ────────────────────────────────────────────────

    private void OnOpenMapClick(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tiff",
            Title  = "Open Map Image",
        };
        if (dlg.ShowDialog() == true)
        {
            _canvas.LoadImage(dlg.FileName);
            MapPrefs.UpdateImage(dlg.FileName);
            SetStatus($"Map loaded: {dlg.FileName}");
        }
    }

    private void OnGridSettingsClick(object s, RoutedEventArgs e)
    {
        var dlg = new MapGridSettingsDialog(_grid, Window.GetWindow(this));
        dlg.SettingsChanged += v =>
        {
            _grid.Reconfigure(v.Size, v.Origin, v.Orientation, v.Cols, v.Rows);
            _canvas.InvalidateGeoCache();
            _canvas.Refresh();
        };
        var orig = (Size: _grid.Size, Origin: _grid.Origin,
                    Orientation: _grid.Orientation, Cols: _grid.Cols, Rows: _grid.Rows);
        if (dlg.ShowDialog() == true)
            SetStatus($"Grid updated  |  size={_grid.Size:0.0}  {_grid.Cols}×{_grid.Rows}");
        else
        {
            _grid.Reconfigure(orig.Size, orig.Origin, orig.Orientation, orig.Cols, orig.Rows);
            _canvas.InvalidateGeoCache();
            _canvas.Refresh();
            SetStatus("Grid settings cancelled — reverted.");
        }
    }

    private void OnSetOriginClick(object s, RoutedEventArgs e)
    {
        _canvas.SetOriginClickMode(true);
        SetStatus("Click on any hex centre on the map to set the grid origin there.");
    }

    private void OnResetWarpClick(object s, RoutedEventArgs e)
    {
        _grid.ResetWarp();
        _canvas.InvalidateGeoCache();
        _canvas.Refresh();
        SetStatus("Warp reset — grid restored to parametric layout.");
    }

    private void OnSaveMapClick(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter           = "Map save files|*.json",
            DefaultExt       = ".json",
            FileName         = "chult_map.json",
            Title            = "Save Map",
            InitialDirectory = AppFolders.Maps,
        };
        if (dlg.ShowDialog() == true)
            SaveState(dlg.FileName);
    }

    private void OnLoadMapClick(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter           = "Map save files|*.json",
            Title            = "Load Map",
            InitialDirectory = AppFolders.Maps,
        };
        if (dlg.ShowDialog() == true)
            LoadState(dlg.FileName, PlayerName);
    }

    private void OnFogChanged(object s, RoutedEventArgs e)
    {
        if (_canvas is null) return;   // fires during XAML init
        _canvas.FogEnabled = FogEnabledCheck.IsChecked == true;
        _canvas.Refresh();
    }

    private void OnResetFogClick(object s, RoutedEventArgs e)
    {
        _canvas.FogRevealed.Clear();
        foreach (var entity in _em.TopLevel)
            _canvas.FogRevealed.Add(entity.Node);
        RefreshLocationList();
        _canvas.Refresh();
        SetStatus("Fog of war reset — entity positions kept visible.");
    }

    private void OnOpacityChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel is null || _canvas is null) return;   // fires during XAML init
        int pct = (int)Math.Round(OpacitySlider.Value / 255 * 100);
        OpacityLabel.Text = $"Opacity: {pct}%";
        _canvas.SetGridOpacity((int)OpacitySlider.Value);
    }

    private void OnTeleportChanged(object s, RoutedEventArgs e)
    {
        if (_canvas is null) return;
        _canvas.SetTeleport(TeleportCheck.IsChecked == true);
    }

    // ── Keyboard shortcuts ─────────────────────────────────────────────

    private void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.W)
        {
            OnWaitClick(s, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _canvas.SetSelected(null);
            e.Handled = true;
        }
    }

    // ── Save / Load ────────────────────────────────────────────────────

    public void SaveState(string path)
    {
        var state = MapSaveState.From(
            _grid, _em, _lm, _tm, _canvas.FogRevealed, _day, _currentWeather);
        File.WriteAllText(path, state.Serialize());
        MapPrefs.UpdateSave(path);
        SetStatus($"Saved: {path}");
    }

    public void LoadState(string path, string playerName = "")
    {
        try
        {
            var json  = File.ReadAllText(path);
            var state = MapSaveState.Deserialize(json)
                ?? throw new InvalidDataException("Invalid save file");

            var (fog, day, weather) = state.Apply(_grid, _em, _lm, _tm, playerName);
            _day            = day;
            _currentWeather = weather;
            _canvas.FogRevealed.Clear();
            foreach (var n in fog) _canvas.FogRevealed.Add(n);
            foreach (var e in _em.TopLevel) _canvas.FogRevealed.Add(e.Node);

            _canvas.ClearMoved();
            _bonusMoveEntityId = "";
            _canvas.SetSelected(null);
            _canvas.SetHighlightedLocation(-1);

            // Sync weather combo
            _suppressWeatherEvent = true;
            int widx = WeatherCombo.Items.Cast<string>()
                .Select((item, i) => (item, i))
                .FirstOrDefault(t => t.item == _currentWeather).i;
            WeatherCombo.SelectedIndex = Math.Max(0, widx);
            _suppressWeatherEvent = false;
            WeatherChanged?.Invoke(_currentWeather);

            RefreshEntityList();
            RefreshLocationList();
            UpdateDayUi();
            _canvas.InvalidateGeoCache();
            _canvas.Refresh();
            NotifyPlayerLocation();
            MapPrefs.UpdateSave(path);
            SetStatus($"Loaded: {path}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"Could not load state:\n{ex.Message}",
                "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── In-process player location notification ────────────────────────

    /// <summary>
    /// Fires whenever the player entity moves, is added, or the terrain/location
    /// under the player changes.  The character sheet subscribes to this instead
    /// of polling a file.
    /// Signature: (node, terrain, locationName, locationType)
    /// node = -1 when no player entity exists.
    /// </summary>
    public event Action<int, string, string, string>? PlayerLocationChanged;

    /// <summary>
    /// Computes the player's current node/terrain/location and fires
    /// <see cref="PlayerLocationChanged"/>.  Call after any map mutation that
    /// could affect the player's position or surroundings.
    /// </summary>
    public void NotifyPlayerLocation()
    {
        var player = _em.All.FirstOrDefault(e => e.IsPlayer);
        if (player is null)
        {
            PlayerLocationChanged?.Invoke(-1, "", "", "");
            return;
        }
        string terrain = _tm.Get(player.Node) ?? "";
        var    loc     = _lm.All.FirstOrDefault(l => l.Node == player.Node);
        PlayerLocationChanged?.Invoke(
            player.Node, terrain, loc?.Name ?? "", loc?.LocationType ?? "");
    }

    /// <summary>
    /// Snapshot of the player's current location data without firing the event.
    /// Returns node=-1 when no player entity is on the map.
    /// </summary>
    public (int Node, string Terrain, string LocationName, string LocationType) PlayerLocation
    {
        get
        {
            var player = _em.All.FirstOrDefault(e => e.IsPlayer);
            if (player is null) return (-1, "", "", "");
            string terrain = _tm.Get(player.Node) ?? "";
            var    loc     = _lm.All.FirstOrDefault(l => l.Node == player.Node);
            return (player.Node, terrain, loc?.Name ?? "", loc?.LocationType ?? "");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private void SetStatus(string msg) => StatusLabel.Text = msg;

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return Colors.Gray; }
    }
}
