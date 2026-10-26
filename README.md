# Audition Markers for Subtitle Edit Classic

**Export subtitle cues as Adobe Audition CSV markers with one click. No Python required.**

[Download](https://github.com/BrainDeLook/subtitleedit-audition-markers/releases/latest) · [Русский](README.ru.md) · [Changelog](CHANGELOG.md) · [Subtitle Edit Classic](https://github.com/BrainDeLook/subtitleedit-classic)

A separate DLL plugin for the Windows Forms Classic editor. Inspired by the supplied DeCoCo Converter 1.4 workflow; conversion is implemented in C# and uses the editor's current document, including unsaved changes. It does not modify the subtitle or its Undo history.

## Install

1. Download `AuditionMarkers-1.2.0.zip` from Releases and extract it.
2. Double-click `Install.cmd`, or copy `AuditionMarkers.dll` into `%APPDATA%\Subtitle Edit\Plugins`. For a portable installation use its own `Plugins` directory instead.
3. Restart Classic. Use **Tools → Audition markers**. You can assign a shortcut under **Options → Settings → Shortcuts → Plugins**.

The first export opens settings once. Later exports write directly to the remembered destination and show the result in Classic's status bar. No Python, additional model or API service is used.

### Version 1.2: actor filtering and markers inside MP4

This is the same optional **Audition Markers** plugin; CSV export remains the default toolbar action.

In plugin settings choose **All subtitles**, **Selected rows**, or **Selected actors**. Actors are read from the ASS/SSA Actor field, with an explicit **<No actor>** choice; names in subtitle text are not guessed. Multiple actors can be checked. The filter affects markers only: unchosen subtitles still appear in hard-subtitle exports. Sequential names such as `Маркер 1 (25)` are also available for actor-filtered exports.

Install `ClassicHost-4.0.28.6.zip` from this release to enable fresh actor context in toolbar settings and the optional post-render hook. The updater accepts only the verified 4.0.28.4 and 4.0.28.5 EXEs and keeps a rollback EXE. This updates host support, not a second plugin. With Audition Markers absent, the hard-subtitle window has no marker control and writes no marker metadata.

For hard subtitles, choose MP4 and enable **Audition markers (MP4)** in the Audio group. The adjacent `…` button opens the plugin settings. On successful rendering, the plugin writes an Adobe XMP Cue track into a UUID box inside the video file. No CSV or XMP sidecar is required. Cut exports rebase and clip marker times to the resulting video's origin; the original subtitle row number is retained. Disabled embedding leaves the normal export path intact. Batch embedding and non-MP4 containers are currently rejected when enabled, before encoding starts.

The settings dialog also offers **Embed in existing MP4…**. It creates a separate `<video>_audition.mp4` copy, preserving the selected input video and avoiding existing output names. This action uses the current settings draft; use Save to remember the draft for subsequent exports.

Writes use cancellable streaming, a temporary file beside the output and atomic replacement. Allow free space approximately equal to the output video for automatic embedding (more for creating a separate manual copy). Existing unrelated XMP properties and tracks are retained; only this plugin's named track is replaced on repeat embedding. Malformed/ambiguous XMP and unsupported QuickTime-style files are refused rather than overwritten.

**Verification boundary:** XMP structure, real MP4 decoding, unchanged encoded video/audio packets and actual hard-subtitle postprocessing are tested. Automatic appearance/import of the embedded track in Adobe Audition itself is not yet verified. Treat the MP4 route as experimental until checked in your Audition build. Plain MP4 chapters are not used as a substitute for Adobe markers.

### Toolbar button

The **Audition markers** split button requires the optional plugin-toolbar extension in Classic (local build 4.0.28.5 or a later build that includes the extension). Its main area exports; its arrow opens settings.

The published Classic 4.0.28.4 installer supports the plugin through its menu and shortcut, but does not yet have the toolbar extension or selected-row context. The current release provides `ClassicHost-4.0.28.6.zip` for the verified Classic 4.0.28.4 / 4.0.28.5 baselines. Its update script verifies the original EXE, keeps a rollback copy and replaces only that EXE. It does not replace the normal Classic installer or its dependencies. Source for the host extension is in the [Classic repository](https://github.com/BrainDeLook/subtitleedit-classic).

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

When **selected-only export** is enabled, the optional **Number from 1** setting produces `Маркер 1 (25)`, `Маркер 2 (38)`, etc. Parentheses contain the original document row, without an extra label. It is off by default to preserve existing settings, and applies to numbered marker names; text-as-name keeps the cue text. Exporting all cues keeps its existing numbering.

The output uses these **tab-separated** columns despite the `.csv` extension:

```text
Name    Start    Duration    Time Format    Type    Description
```

Time Format is `decimal`, Type is `Cue`, and times use total minutes, seconds and three decimal places. Files use UTF-8 without BOM, matching the supplied converter. Quotes are escaped. Overlapping cues remain separate markers; their timings are not adjusted. Sorting follows document order.

## Import into Audition

Open the corresponding audio or session and use **File → Import → Markers from CSV**. Check a few marker positions, Cyrillic descriptions and range endpoints. This exports marker data; it does not transfer audio, create audio clips, or control Audition automatically.

The current release checks real generated CSV, embedded MP4 XMP and native Classic dispatch. Display/import in Adobe Audition itself remains a manual check. The output schema follows the provided, previously used DeCoCo converter.

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
