# CLAUDE.md

Working notes for Claude Code in this repo. Global git/commit/response-style conventions live in the user's `~/.claude/CLAUDE.md` and apply here too — this file only covers things specific to this project.

## Stack

WPF (.NET, C#) desktop character sheet app for Tales of Adventure: Cloaks & Swords, with a hex-grid map/fog-of-war tool. Traditional WPF code-behind pattern (`.xaml` + paired `.xaml.cs`), no MVVM framework.

## WPF DrawingVisual performance rules

Established while fixing `HexMapCanvas.cs` — apply whenever writing or extending a `DrawingVisual` layer or any WPF draw method:
- Never allocate brushes/pens inside a draw method — cache and `.Freeze()` them (per-type dictionary cache for draw loops over many cells).
- Batch layer redraws — add `Quiet`/`NoRedraw` variants for operations always followed by a heavier redraw call.
- Trace the full call chain (including events) before adding a `RefreshXxx()` call, to avoid double rebuilds.

Full detail and code examples are worth re-reading before touching the map canvas, but don't need to be re-derived — they're already written up.

## Keeping context/token spend down

This repo's cost driver is a handful of oversized code-behind files that mix UI wiring, state, and drawing logic:

- `MainWindow.xaml.cs` (~1270 lines)
- `Map/MapView.xaml.cs` (~1080 lines)
- `Map/HexMapCanvas.cs` (~1030 lines)
- `Map/MapDialogs.cs` (~770 lines)

To avoid burning context on these:

- **Grep for the specific method/field/event name first**, don't `Read` a whole code-behind file end-to-end when only one handler or draw routine is relevant.
- **Delegate open-ended searches** ("where else does GridOpacity get read", "find every place a layer gets redrawn") **to the `Explore` subagent** rather than reading multiple large files directly in the main conversation.
- Before adding a new draw/refresh call in `HexMapCanvas.cs`, check the performance rules above first — re-deriving them by reading the whole file is exactly the kind of repeat cost this section exists to avoid.

## Where things live (quick index)

- Main window / top-level UI wiring: `MainWindow.xaml` + `MainWindow.xaml.cs`.
- Character data model & persistence: `Models/CharacterState.cs`, `Models/Persistence.cs`.
- Hex map feature: `Map/MapView.xaml(.cs)` (map UI/dialog wiring), `Map/HexMapCanvas.cs` (DrawingVisual rendering — see performance rules above), `Map/HexGrid.cs` (grid geometry/math), `Map/MapDialogs.cs`, `Map/MapSaveState.cs` (map persistence).
- Reusable controls: `Controls/SkillPickerBox.xaml.cs`, `Controls/AddEntryDialog.xaml(.cs)`, `Controls/DieBubble.cs`.
- Theming: `Resources/Themes.xaml`.
