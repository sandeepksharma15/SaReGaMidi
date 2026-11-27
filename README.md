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

## Development

- [Development plan](docs/development/DEVELOPMENT_PLAN.md)
- [Coding style](docs/development/CODING_STYLE.md)
- [Project memory](docs/development/PROJECT_MEMORY.md)
