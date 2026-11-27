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

Record agreed decisions and recurring constraints here. Keep task progress in the relevant work item or conversation.
