# Project Structure

Vision is one classic (non-SDK) C# project, `Vision.csproj`, plus one test project, `VisionTests/VisionTests.csproj`. All
application source files sit in the repository root (no sub-folders, single namespace `Vision`).

Related: [modules.md](modules.md) · [build-system.md](build-system.md) · [testing-strategy.md](testing-strategy.md)

## Top level

| Path | What it is | Notes |
|---|---|---|
| `*.cs` (67 files, ~31 100 lines) | Application source, namespace `Vision` | Each file must also be listed in `Vision.csproj` (`<Compile Include=…>`); see [build-system.md](build-system.md#adding-a-source-file) |
| `Vision.csproj`, `Vision.sln` | Project and solution | .NET Framework 4.6.1, `WinExe`, `AnyCPU` |
| `packages.config` | NuGet: `Microsoft.Web.WebView2` 1.0.1072.54 only | Restored to `packages/` (git-ignored) |
| `AppInfo.cs` | Name, version, author, assembly attributes, `InternalsVisibleTo("VisionTests")` | Single place for the version; see [versioning.md](versioning.md) |
| `Program.cs` | Entry point (`Program.Main`) and global exception handlers | See [execution-flow.md](execution-flow.md) |
| `Komutlar/Komutlar.xml` | Command Library content (VxWorks 6.4 shell commands) | Embedded resource `Komutlar.Varsayilan.xml`; read-only at run time |
| `Yardim/KullanimKilavuzu.md` | User manual (Turkish) | Embedded resource `Yardim.KullanimKilavuzu.md`, shown with F1 by `HelpDialog` / `HelpMarkdown` |
| `Yardim/Gereksinimler.md` | Requirements G1–G60 and version history (Turkish) | **Not** embedded; developer document only (G33.2) |
| `VisionTests/` | Test project (console exe) | See [testing-strategy.md](testing-strategy.md) |
| `VisionTests/Ornekler/` | Real card output samples (`ll`, `devs`, `dosFsShow`, `bootChange`) | Copied to the test output folder |
| `release/` | Current release: `release/Vision/` (unzipped `Vision.zip`) and `release/SurumNotlari.md` | Only the latest version is kept; see [release-process.md](release-process.md) |
| `.github/workflows/build-exe.yml` | The only CI workflow | Build, test, package, pre-release, official release, cleanup |
| `docs/` | This developer documentation, plus `simge.png` / `simge.svg` (icon previews used by `README.md`) | Changes here do not trigger CI |
| `README.md` | Turkish project overview | |
| `YAPILACAKLAR.md` | Turkish backlog | Process described at its top |
| `CLAUDE.md`, `AGENTS.md` | Instructions for AI assistants | |
| `Vision.ico`, `simge.png`, `simge.svg` | Application icon and sources | |
| `.gitattributes` | Line endings: LF in the repository, CRLF in the working tree for `*.cs`, `*.csproj`, `*.sln`, `*.md`, `*.xml` | |
| `.gitignore` | `bin/`, `obj/`, `.vs/`, `packages/`, `logs/`, `Konfig/`, `HazirExe/`, `*.zip`, … | `release/Vision/` *is* committed (binary) |

## Source file naming

- One main type per file, named after it (`CardSession.cs`, `TelnetClient.cs`). Small helper types live in the same
  file (e.g. `FileExplorerLogic.cs` holds `FileBrowser`, all parsers, `FsPath`, `OpResult`).
- Large UI classes are split into `partial` files named `<Class>.<Area>.cs`: `CardView.Actions.cs`,
  `CardView.Highlights.cs`, `CardView.Sentinel.cs`, `CardView.Tour.cs`, `FileExplorerView.DragOut.cs`,
  `FileExplorerView.MultiSelect.cs`, `FileExplorerView.Operations.cs`, `CommandLibraryView.Pages.cs`,
  `CommandLibraryView.Transfer.cs`. In the `.csproj` they carry `<DependentUpon>`.
- `TourAccessors.cs` collects partial-class members that expose controls to the application tour.

## Largest files (good to know before editing)

| File | Lines | Content |
|---|---|---|
| `FileExplorerLogic.cs` | ~2 730 | `FileBrowser` (all file operations and transfers), parsers, `FsPath` |
| `MainForm.cs` | ~1 990 | Main window, tabs, session dictionary, UI timer, tour steps |
| `CardView.cs` | ~1 300 | Terminal view of one card |
| `CardSession.cs` | ~1 110 | Per-card session, state machine, command execution |
| `SerialTerminalView.cs` | ~1 000 | One serial port view (log + screen) |
| `Theme.cs` | ~900 | Colours, fonts, control factories |

## Runtime folders (not in Git)

| Path | Created by |
|---|---|
| `%APPDATA%\TelnetCards\` | `ConfigStore` (config, command histories) |
| `<exe folder>\logs\` | `ErrorText`, `CardEventLog`, `TerminalSnapshot` |
| `<exe folder>\lib\` | Shipped with the zip; WebView2 DLLs, resolved by `LibFolder.Setup` |
| `%TEMP%\TelnetCards\SaltOkunur\…` | `ReadOnlyCopies` (read-only "open" copies; cleaned at start-up after 12 h and at exit) |
| `%TEMP%\TelnetCards\Surukle\…` | `DragOutCopies` (drag-to-Windows placeholders; same cleanup) |
| `%LOCALAPPDATA%\TelnetCards\WebView2` | `EdgeSentinelPage` (WebView2 user data) |
