# Changelog

## 1.0.0

- Export the current Subtitle Edit document to Adobe Audition tab-delimited CSV markers, without Python or a separate input-file conversion step.
- Add persistent point/range marker, marker-name, description, selected-row and output-folder options.
- Preserve text from both ASS and SRT input through Classic's subtitle parser; strip display tags and flatten multiline descriptions.
- Use decimal millisecond times, invariant formatting, UTF-8 without BOM, quoted fields and atomic, collision-safe output.
- Integrate with Classic's plugin menu and configurable shortcuts. The optional Classic host extension supplies a toolbar split button and selected-row context.
- Validation: 27 converter checks and 11 native Classic host checks passed. Audition import itself has not been automated or field-tested for this release.
