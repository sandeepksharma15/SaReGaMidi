# SaReGaMidi project memory

## Product decisions

- Accept printed Hindustani compositions in Bhatkhande notation. Keep an internal composition representation usable for on-screen notation, printing, and MIDI export.
- Export melody and theka as separate MIDI tracks. Mixcraft is the first DAW targeted.
- Derive the accompaniment from a standard tabla theka for the composition's taal. Keep the bol pattern independent of the playback instrument so it can also drive a Western drum kit.

## Implementation decisions

- The initial Windows desktop shell is `src/SaReGaMidi.UI`, a C# WPF application targeting .NET 11. The current milestone establishes the application entry point and main window.
- Keep the composition representation independent of WPF and MIDI so each output can use the same musical data.
- `src/SaReGaMidi.Music` adapts swara and tonic-relative pitch calculations from Clefer.Domain and Devanagari pitch symbols from Clefer.Lipi. Komal underlines, tivra strokes and saptak dots are separate from the glyph text. [Notation reference](https://www.indianmusic.in/downloads/d5694c6845bb4439abb25138180f939f_Hindustani_Music_ThBook1_Eng.pdf)
- The built-in taal catalog adapts six Clefer.Domain patterns. Each entry covers one maatra and a null entry represents a rest. Rupak starts on both sam and khali and uses the 0–1–2 marking convention. Instrument mapping and stroke timing belong in playback. [Taal reference](https://naadabrahma.in/taal)
- Clefer's rendering layout, audio analysis models and AI-backed raag lookup remain reference material. Define composition timing and the interpretation of printed sustain and rest signs before reusing score-level code.

- Imports record user-confirmed page order in a versioned manifest beside copied scans. Each new composition has its own workspace with source and artifact folders. The work-folder setting applies to future imports. Startup reopens the last imported workspace by default.

- The versioned score file stores composition, sections, and maatras. Each section starts on an explicit beat of the taal. Swara durations are exact maatra fractions. Lyric text and review annotations are stored separately from swara events. Source page references are independent of cycle timing. Khatka, kan swar and outgoing meend attach to pitched swaras. Maatra grouping is a display mark, and S extends the lyric syllable without changing musical time.

- In the printed Sthayi of Ranjish Hi Sahi, the second pass borrows Dadra beats 1–3 from the upper row, then uses the lower row from beat 4. Borrowed swaras resolve through explicit written-maatra references while the written swara cells remain blank. Printed-line starts affect layout only.
- Explicit meend endpoints use stable swara anchors, following Clefer's span model. A span can cross multiple maatras and printed lines. Older next-swara meends remain readable.
- The meend editor can filter destinations by written maatra distance. A fully blank maatra keeps its rhythm position and may still borrow swaras during a pass. The score exposes each written S as a LyricalContinuation marker on the lyric line.

Record agreed decisions and recurring constraints here. Keep task progress in the relevant work item or conversation.
