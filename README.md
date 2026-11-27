# SaReGaMidi

SaReGaMidi is a Windows desktop project for bringing printed Bhatkhande notation into a DAW as separate melody and theka MIDI tracks. Mixcraft is the first DAW targeted.

The desktop shell, music library, and source image importer are in place.

## Run the shell

On Windows with the .NET 11 SDK installed, run these commands from the repository root.

```powershell
dotnet build SaReGaMidi.slnx
dotnet run --project src/SaReGaMidi.UI
```

## Import composition pages

1. Select **Import composition**, then **Add images** and choose the composition scans. Add more in a separate batch if needed.
2. Select each page to preview it. Use **Move up**, **Move down**, and **Remove** to put the scans in reading order.
3. Select **Import pages** to copy the scans in the numbered order and open the source review screen.

Each import gets its own work folder with numbered scans in `source`, a `composition.json` page manifest, and an `artifacts` folder for later output. Choose **Open work folder** from source review to see the saved files.

Use **Settings** to choose Light or Dark and set the work folder for future imports, and choose whether to reopen the previous workspace (default) or start blank. By default, imports go under `%LOCALAPPDATA%\SaReGaMidi\Work`. Existing composition folders stay where they were created.

## Transcribe a composition

From source review, select **Transcribe**. The **Notation** tab shows Devanagari swaras above the lyrics with vibhaag marks. Select a maatra to edit it using the swara palette and live preview. Select a swara to add khatka, kan swar or meend to the next swara. The grouping curve applies to the whole maatra. Use **S Lyric continuation** to insert an extender in the lyric row. Set the first maatra of each section's **Start beat** relative to sam. The **Details** tab is available for table editing.

For the three original Ranjish Hi Sahi scans in reading order, **Load Ranjish Hi Sahi draft** brings in the printed sthayi and antara as a draft. Review the notes and cues against the book. **Save score** stores the transcription as `score.json` in the workspace. Open **Transcribe** again to resume editing.

### Generate a draft from scans

Draft generation runs offline inside SaReGaMidi using [TesseractOCR 5.5.4](https://www.nuget.org/packages/TesseractOCR/5.5.4) by Charles Weld and Kees van Spelde. Build and publish include the native libraries and the [Hindi model from tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast/blob/main/hin.traineddata). Windows requires the matching [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist). Clear any existing `SAREGAMIDI_TESSERACT` user environment variable and restart the app to use the bundled engine.

Open **Transcribe** in a new workspace, choose the taal, and select **Generate draft**. The draft opens beside the original scans. The prototype reads layouts with visible vibhaag dividers and equal-length divisions, such as Dadra. Review the flagged layout, swaras, lyrics, and any missing pages before selecting **Save score**. Complex ornaments, unequal subdivisions, and repeat paths require musical review.

### Test recognition

Set `SAREGAMIDI_REFERENCE_SCANS` to a local folder containing the three original Ranjish Hi Sahi scans. The scans stay outside the repository. Run `dotnet run --project tests/SaReGaMidi.Recognition.Tests -- -showLiveOutput` to check the detected rows, line lengths, pickups, and repeat blanks against the existing transcription. Set `SAREGAMIDI_RUN_OCR_BENCHMARK=1` to include exact timed-swara and lyric comparisons.

For an EXE comparison, rerun the benchmark with `SAREGAMIDI_TESSERACT` set to the full path of a Tesseract 5 executable. Both paths use the bundled Hindi model, the same PNG crops, and single-line segmentation.

## Development

- [Development plan](docs/development/DEVELOPMENT_PLAN.md)
- [Coding style](docs/development/CODING_STYLE.md)
- [Project memory](docs/development/PROJECT_MEMORY.md)
