# Changelog

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
