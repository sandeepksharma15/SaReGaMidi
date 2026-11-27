# SaReGaMidi project memory

## Product decisions

- Accept printed Hindustani compositions in Bhatkhande notation. Keep an internal composition representation usable for on-screen notation, printing, and MIDI export.
- Export melody and theka as separate MIDI tracks. Mixcraft is the first DAW targeted.
- Derive the accompaniment from a standard tabla theka for the composition's taal. Keep the bol pattern independent of the playback instrument so it can also drive a Western drum kit.

## Implementation decisions

- The initial Windows desktop shell is `src/SaReGaMidi.UI`, a C# WPF application targeting .NET 11. The current milestone establishes the application entry point and main window.
- Keep the composition representation independent of WPF and MIDI so each output can use the same musical data.
- `src/SaReGaMidi.Music` adapts swara and tonic-relative pitch calculations from Clefer.Domain and Devanagari pitch symbols from Clefer.Lipi. Komal underlines, tivra strokes and saptak dots are separate from the glyph text. [Notation reference](https://www.indianmusic.in/downloads/d5694c6845bb4439abb25138180f939f_Hindustani_Music_ThBook1_Eng.pdf)
- The built-in taal catalog adapts six Clefer.Domain patterns and adds Sooltal, Ektal, Chautal and Tilwada with their own Devanagari thekas. Each entry covers one maatra and a null entry represents a rest. Rupak starts on both sam and khali and uses the 0–1–2 marking convention. Instrument mapping and stroke timing belong in playback. [Taal reference](https://naadabrahma.in/taal) · [Additional thekas](https://musicsheets.site/taals)
- Clefer's rendering layout, audio analysis models and AI-backed raag lookup remain reference material. Define composition timing and the interpretation of printed sustain and rest signs before reusing score-level code.

- Imports record user-confirmed page order in a versioned manifest beside copied scans. Each new composition has its own workspace with source and artifact folders. The work-folder setting applies to future imports. Startup reopens the last imported workspace by default.

- The versioned score file stores composition, sections, and maatras. Each section starts on an explicit beat of the taal. Swara durations are exact maatra fractions. Lyric text and review annotations are stored separately from swara events. Source page references are independent of cycle timing. Khatka, kan swar and outgoing meend attach to pitched swaras. Maatra grouping is a display mark, and S extends the lyric syllable without changing musical time.

- In the printed Sthayi of Ranjish Hi Sahi, the second pass borrows Dadra beats 1–3 from the upper row, then uses the lower row from beat 4. Borrowed swaras resolve through explicit written-maatra references while the written swara cells remain blank. Printed-line starts affect layout only.
- Transcription shows the written score. Performance pass ranges remain in the saved score for playback. Swaras within a maatra use tighter spacing and can shrink to fit the beat column.
- Transcription Details shows the calculated beat within the taal and uses the same Devanagari glyphs as Notation. Edit Maatra owns the source page, explicit start beat, and grouping controls.
- Prototype assisted transcription before MIDI export. Recognition runs locally using page geometry and Tesseract with the Hindi model. Use the ordered scans to generate an editable written draft, validate it with the score model, and surface uncertain readings for review. Keep repeat and return instructions as review cues until performance order can be confirmed in the app. Use local copies of the reference scans for regression tests without distributing them in the repository.
- Use printed ink geometry alongside OCR for notation-specific marks. Propose a meend only where an arc connects adjacent cells with readable pitches. Reserve visible review markers in the notation for unreadable swaras and keep other review cues in tooltips and Details.
- Detect printed lyric continuation signs by shape and preserve their count and position around the syllable. Use Hindi OCR to read the syllable and keep the OCR fallback for ambiguous shapes.
- Compare full-cell OCR with tighter crops for weak readings. Keep printed silence, lyric nukta, swara octave dots, and tivra strokes grounded in the scan geometry.
- Run Tesseract through the TesseractOCR NuGet package by default and ship the benchmarked Hindi model with the app. Keep the explicit executable override for comparisons using the same model, crops, segmentation, and word confidence aggregation.
- Explicit meend endpoints use stable swara anchors, following Clefer's span model. A span can cross multiple maatras and printed lines. Older next-swara meends remain readable.
- The meend editor can filter destinations by written maatra distance. A fully blank maatra keeps its rhythm position and may still borrow swaras during a pass. The score exposes each written S as a LyricalContinuation marker on the lyric line.

- Preserve an S printed as a swara sustain. It holds the preceding pitch for its written duration. An S in the lyric row independently continues the vowel.

- Automatically save successful OCR drafts in the workspace score. Transcribe reopens the saved score. Regeneration replaces it after confirmation and successful recognition. Manual edits use Save score.

The MIDI proof uses Type 1 files, one quarter note per maatra, 960 ticks per quarter, and one bar per taal cycle. An opening pickup keeps its beat offset from sam. The export dialog offers written order and saved passages. Melody uses the principal pitches and sustains. Theka uses a separate General MIDI drum mapping on channel 10. Ornamental playback and manual verification in Mixcraft are pending.

In-app playback auditions the exported MIDI through the Windows default MIDI output. Play and Export share passage, tonic and tempo for the open transcription. Live Melody and Theka volume switches affect the audition only.

During audition, highlight the sounding melody swara in the written notation using MIDI event timing. Borrowed maatras highlight the source glyph, and a sustain keeps the preceding glyph highlighted until its note ends.

Record agreed decisions and recurring constraints here. Keep task progress in the relevant work item or conversation.

Scale analysis counts principal note attacks in written order, pooling saptaks and counting kan separately. Notes strictly below the adjustable cutoff (default 5%) are rare. The analysis tonic defaults to C♯4.
