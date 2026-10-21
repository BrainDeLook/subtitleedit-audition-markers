# Audition Markers for Subtitle Edit Classic

**Export subtitle cues as Adobe Audition CSV markers with one click. No Python required.**

[Download](https://github.com/BrainDeLook/subtitleedit-audition-markers/releases/latest) · [Русский](README.ru.md) · [Changelog](CHANGELOG.md) · [Subtitle Edit Classic](https://github.com/BrainDeLook/subtitleedit-classic)

A separate DLL plugin for the Windows Forms Classic editor. Inspired by the supplied DeCoCo Converter 1.4 workflow; conversion is implemented in C# and uses the editor's current document, including unsaved changes. It does not modify the subtitle or its Undo history.

## Install

1. Download `AuditionMarkers-1.0.0.zip` from Releases and extract it.
2. Double-click `Install.cmd`, or copy `AuditionMarkers.dll` into `%APPDATA%\Subtitle Edit\Plugins`. For a portable installation use its own `Plugins` directory instead.
3. Restart Classic. Use **Tools → Audition markers**. You can assign a shortcut under **Options → Settings → Shortcuts → Plugins**.

The first export opens settings once. Later exports write directly to the remembered destination and show the result in Classic's status bar. No Python, additional model or API service is used.

### Toolbar button

The **Audition markers** split button requires the optional plugin-toolbar extension in Classic (local build 4.0.28.5 or a later build that includes the extension). Its main area exports; its arrow opens settings.

The published Classic 4.0.28.4 installer supports the plugin through its menu and shortcut, but does not yet have the toolbar extension or selected-row context. An optional, separately packaged `ClassicToolbar-4.0.28.5.zip` is provided on the plugin release for that exact Windows Classic baseline. Its update script verifies the original EXE, keeps a rollback copy and replaces only that EXE. It does not replace the normal Classic installer or its dependencies. Source for the host extension is in the [Classic repository](https://github.com/BrainDeLook/subtitleedit-classic).

## One-click behavior

For `episode.ass`, the default output is `episode_audition.csv` next to the subtitle. Existing files are retained: subsequent exports create `episode_audition_1.csv`, and so on. A custom output folder can be remembered. An unnamed document asks for a folder once. A missing or unwritable destination reports an error rather than silently writing elsewhere.

| Setting | Default |
| --- | --- |
| Marker kind | Point at the subtitle start, matching the original script |
| Range mode | Optional: duration equals subtitle end minus start |
| Name | `Маркер 1`, `Маркер 2`, …; optionally clean cue text |
| Description | Clean subtitle text, with line breaks flattened |
| Scope | All cues; selected-only mode requires the extended Classic host |
| Destination | Next to the subtitle, or a remembered absolute folder |

The output uses these **tab-separated** columns despite the `.csv` extension:

```text
Name    Start    Duration    Time Format    Type    Description
```

Time Format is `decimal`, Type is `Cue`, and times use total minutes, seconds and three decimal places. Files use UTF-8 without BOM, matching the supplied converter. Quotes are escaped. Overlapping cues remain separate markers; their timings are not adjusted. Sorting follows document order.

## Import into Audition

Open the corresponding audio or session and use **File → Import → Markers from CSV**. Check a few marker positions, Cyrillic descriptions and range endpoints. This exports marker data; it does not transfer audio, create audio clips, or control Audition automatically.

The current release checks real generated CSV and native Classic dispatch. Import in Adobe Audition itself remains a manual check. The output schema follows the provided, previously used DeCoCo converter.

## Settings and removal

Settings are stored in `Plugins\AuditionMarkers.settings.xml` under Classic's application-data directory. The plugin does not change `Settings.xml` or choose a default shortcut that could conflict with existing keys.

To remove it, close Classic and delete `AuditionMarkers.dll`; delete the settings XML too if you want to reset its preferences. Exported CSV files remain yours.

## Build and tests

On Windows with .NET Framework 4.8 and an installed Classic:

```powershell
./build.ps1 -ClassicDirectory 'C:\Program Files\Subtitle Edit Classic' -Test
```

The output is `dist\AuditionMarkers.dll`. No NuGet or Python installation is needed. `tests/Tests.cs` covers formats, timing, quoting, selection and output safety. `tests/HostIntegration.cs` is the native test harness for a Classic build with the toolbar extension; run it only with isolated test application data.

## License

[GPL-3.0](LICENSE.txt). This is an independent plugin for [Subtitle Edit Classic](https://github.com/BrainDeLook/subtitleedit-classic), not an Adobe product. Adobe Audition is a trademark of Adobe.
