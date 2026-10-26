# Changelog

## 1.2.1

- Fixed the MP4 XMP UUID to match Adobe XMP Toolkit. Version 1.2.0 wrote a wrong identifier, causing Audition to report no supported markers. Re-embedding migrates files carrying the old plugin track.
- Added an independent UUID regression fixture and migration checks.
- Added the explicit Audition import instruction to settings and marked-copy results: File → Import → Markers from File… (Alt+/), choose the marked MP4.
- Verified in Adobe Audition 2026: all 16 selected markers from a user video imported into the multitrack session, with Cyrillic names, original row numbers and correct timestamps; a test range imported at 1.234–1.984 seconds. Audio/video bytes were unchanged.

## 1.2.0

- Added common all/selected-row/actor marker filtering, including multiple ASS/SSA actors and an explicit unassigned-actor entry. Numbering retains original document row references.
- Added Adobe XMP Cue marker embedding inside MP4, with XML escaping, millisecond tracks, existing-metadata preservation, bounds validation, cancellation and staged atomic replacement.
- Added an optional post-hard-subtitle host hook and plugin-owned checkbox/settings control. With no plugin installed, Classic adds no marker UI or processing. Clip offsets and boundary-crossing ranges are rebased/clamped.
- Added manual marked-video copy export from settings; retained one-click CSV export as the default toolbar action.
- This requires the optional Classic 4.0.28.6 host extension for live actor settings and automatic hard-subtitle integration. MOV/MKV and batch embedding are not enabled.
- Validation: 61 converter/XMP/file checks, 21 native toolbar/settings checks and seven actual hard-subtitle integration checks passed. Encoded video/audio packet hashes remain identical after embedding and the output decodes. Audition import/display is still a manual acceptance check.

## 1.1.0

- Added an optional selected-row numbering mode: `Маркер 1 (25)`, `Маркер 2 (38)`, etc., retaining the original document row in parentheses without a label.
- The preference is saved, defaults to off for existing users, and is available only for selected-row exports with numbered names. Export-all and text-as-name behavior are unchanged.
- Added conversion, preference compatibility and native settings/toolbar checks for the new option. Audition import remains a manual check.

## 1.0.0

- Export the current Subtitle Edit document to Adobe Audition tab-delimited CSV markers, without Python or a separate input-file conversion step.
- Add persistent point/range marker, marker-name, description, selected-row and output-folder options.
- Preserve text from both ASS and SRT input through Classic's subtitle parser; strip display tags and flatten multiline descriptions.
- Use decimal millisecond times, invariant formatting, UTF-8 without BOM, quoted fields and atomic, collision-safe output.
- Integrate with Classic's plugin menu and configurable shortcuts. The optional Classic host extension supplies a toolbar split button and selected-row context.
- Validation: 27 converter checks and 11 native Classic host checks passed. Audition import itself has not been automated or field-tested for this release.
