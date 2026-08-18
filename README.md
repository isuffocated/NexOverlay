# NexOverlay

A Windows desktop overlay for fast access to notes, snippets, files and clipboard history without leaving the current workspace.

NexOverlay is built with C# / .NET 10 and WPF.

## Features

- Global `CapsLock + Space` overlay gesture.
- Multi-monitor aware opening on the monitor under the cursor.
- Notes with local persistence.
- Snippets with search, editing and copy actions.
- File links with open and reveal-in-Explorer actions.
- Clipboard history with pinning, search and copy.
- Command palette for opening modules, running actions and searching workspace content.
- Recent activity and workspace counters.
- First-run guided tutorial.
- Switchable animated backgrounds:
  - Aurora
  - Particles
- Local-first storage using SQLite.

## How it works

Press:

```text
CapsLock + Space
```

A small handle appears at the top-center of the active monitor. Hover it to open NexOverlay.

Use the same gesture again while the overlay is open to arm the close handle.

## Data

Mutable data is stored locally under:

```text
%LocalAppData%\NexOverlay
```

This includes the SQLite database, cached data and local assets.

NexOverlay does not require an account or cloud backend.

## Build

Requirements:

- Windows 10/11
- .NET 10 SDK

Clone and build:

```powershell
git clone https://github.com/isuffocated/NexOverlay.git
cd NexOverlay
dotnet build NexOverlay.slnx -c Release
```

Run the application from Visual Studio or:

```powershell
dotnet run --project NexOverlay.App
```

## CI

Every push and pull request to `main` is built on GitHub Actions using a Windows runner.

The workflow verifies both Debug and Release configurations and publishes a self-contained `win-x64` artifact.

## Project structure

```text
NexOverlay.App       WPF UI and application lifecycle
NexOverlay.Core      shared domain models
NexOverlay.Storage   SQLite persistence
NexOverlay.Windows   Windows-specific integrations
```

## Status

NexOverlay is currently in beta.

The current focus is stability, interaction polish and packaging for the first public release.

## License

No open-source license has been granted yet. All rights reserved unless a license is added later.