# PhotoSafe Check v0.3.6 — Storage Mode Selection

Use a fresh source extraction and run `dotnet restore`, `dotnet build`, `dotnet run` from the nested `PhotoSafeCheck` directory. This update changes startup and storage routing; **do not merge individual source files into an older release**.

On first launch, select Portable or Local and choose whether to remember the selection. If you already have `%LOCALAPPDATA%/PhotoSafeCheck/state.json`, you can enable carry-over when switching to Portable. The application does **not** remove the old data automatically; use the explicit cleanup option after confirming the new scan history and pairing are available.

Existing Chrome/Edge extension version 0.3.4 remains compatible and does not need reinstalling.

More details: [STORAGE_MODES.md](STORAGE_MODES.md).
