# SaReGaMidi development plan

## Goal

Bring a printed Bhatkhande composition into the Windows app as an editable score. Use the reviewed score for on-screen notation, printing, and MIDI export with separate melody and theka tracks. Mixcraft is the first DAW we will verify.

## Starting point

The .NET 11 WPF shell runs. `SaReGaMidi.Music` already provides relative swara pitch, Devanagari glyphs with Bhatkhande marks, and six taal patterns. The first benchmark is the supplied three-page Dadra setting of “Ranjish Hi Sahi” and its accompanying notation legend.

## Milestones

| Milestone | Build | Checkpoint |
| --- | --- | --- |
| 1. One printed composition in the app | Add a small, UI-independent score model in `SaReGaMidi.Music`, a versioned score file, and a WPF view with basic entry and correction by maatra. Begin with the opening Dadra phrase to validate the model, then transcribe the notated sthayi and antara from all three pages. Save and reopen the work as one composition. Review it against the ordered source scans. | Open, edit, save, and reopen the composition. Check that notes and lyrics stay aligned across line and page breaks, the pickup lands in the correct place relative to sam, and ornaments and return cues remain visible. Record ambiguous readings for review. |
| 2. Assisted transcription prototype | Add an offline Generate draft workflow for ordered scans. Use image geometry and local Hindi OCR to map printed rows and maatra cells into the existing score model. Flag uncertain readings and repeat cues for review. | Compare with the manually reviewed Ranjish Hi Sahi score and an unseen composition from the same book. Check swaras, marks, durations, lyric alignment, and page order. Measure how much correction the draft saves. |
| 3. MIDI proof in Mixcraft | Export a Type 1 Standard MIDI File with named melody and theka tracks. Use an explicit Sa and tempo, and a separate mapping from bols to drum sounds. Start with a reviewed passage with unambiguous timing and repeat order. | Check pitch, onset, duration, rests, and cycle alignment automatically. In Mixcraft, verify the editable tracks, tempo, pickup, and alignment over several cycles. Check how the DAW grid represents Dadra. [MIDI Association](https://midi.org/standard-midi-files) · [Acoustica import guide](https://acoustica.kayako.com/article/713-how-do-i-import-mid-files-into-mixcraft) |
| 4. Print and full export | Add print preview, pagination, and an export workflow for the reviewed composition. Expose Sa, tempo, and drum mapping in the UI. Give the user a clear way to resolve how printed return cues should be played. | Print the score and import the reviewed performance into Mixcraft. Check that reflow preserves notation, lyric alignment, and section boundaries. Check that the performance follows the agreed playback order. |
| 5. Expand musical coverage | Prioritize features from more printed examples. Candidates include MIDI realization of meend, kan swar and khatka, compound theka bols, different percussion mappings, and additional taals and editions. Evaluate text and position extraction for selectable PDFs. | Add fixtures and listening checks as features are introduced. Revisit deeper DAW integration after using the MIDI workflow on real pieces. |

## What the first score needs to preserve

- **Timing and cycle position.** Keep exact fractions of a maatra, including the legend's halves, thirds, and 1½ + ½ example. Store the melody's entry relative to sam. Treat printed lines and pages as layout while retaining a way to locate passages in the source.
- **Separate swara and lyric rows.** The legend distinguishes the swara dash for pitch continuation, the S-shaped lyric extender, and the asterisk for silence. Keep a printed blank available for review when its meaning is uncertain.
- **Ornaments and return cues.** Preserve markings such as meend, kan swar, and khatka as notation in the first score. Keep a printed return cue and the instruction for remaining verses available for review. Decide the playback order with the reader before exporting those passages.
- **Source conventions.** Use the supplied book's legend for this transcription. Consult Omenad's comparison and NIOS for context when working with another edition. Keep edition-specific typography out of the musical timing model. [Omenad comparison](https://omenad.net/articles/omeswarlipi.htm) · [NIOS notation chapter](https://digital.nios.ac.in/topic.php?id=242en)
- **Performance settings.** Keep the score's pitches relative to Sa and its timing in maatras. Decide how maatras map to MIDI quarter notes during the Mixcraft proof. Register, tempo, and drum sounds can then be chosen for playback.
