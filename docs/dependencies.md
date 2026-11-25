# Dependencies

Vision deliberately has almost no third-party dependencies, so that it builds and runs on closed (offline) networks.

Related: [build-system.md](build-system.md) · [architecture.md](architecture.md) ·
[decisions/README.md](decisions/README.md)

## Runtime and framework

| Dependency | Version | Used for | Source |
|---|---|---|---|
| .NET Framework | 4.6.1 (target), runs on any later 4.x | Everything | `Vision.csproj` `<TargetFrameworkVersion>` |
| Windows Forms | framework | UI | `System.Windows.Forms` reference |
| `System.Net.Sockets.TcpClient` | framework | Telnet (`TelnetClient`), Sentinel port probe (`SentinelProbe`) | — |
| `System.Net.FtpWebRequest` | framework | FTP (`FtpUploader`) | — |
| `System.IO.Ports.SerialPort` | framework | Serial (`SerialPortIo`) | — |
| `System.Management` (WMI, `Win32_PnPEntity`) | framework | Friendly COM port names (`SerialPorts`) | `Vision.csproj` reference |
| `System.Security` (DPAPI) | framework | `PasswordProtector` (legacy manually entered FTP password) | `Vision.csproj` reference |
| `System.Xml` / `XmlSerializer` | framework | `config.xml`, `Komutlar.xml` | — |
| Win32 API via P/Invoke | Windows | Scroll position and redraw control of `RichTextBox` (`SendMessage`, `GetScrollInfo`, `EM_*`, `WM_SETREDRAW`), `CHARFORMAT2` highlighting | `CardView*.cs`, `SerialTerminalView.cs`, `FileExplorerView.DragOut.cs` |

## NuGet

| Package | Version | Used for | Where it ships |
|---|---|---|---|
| `Microsoft.Web.WebView2` | 1.0.1072.54 | Sentinel page (`EdgeSentinelPage`) | `lib\Microsoft.Web.WebView2.Core.dll`, `lib\Microsoft.Web.WebView2.WinForms.dll`, `lib\runtimes\win-{x86,x64,arm64}\native\WebView2Loader.dll` next to `Vision.exe` |

- Declared in `packages.config`; restored into `packages/` (git-ignored). CI downloads the same version explicitly.
- The DLLs are moved into `lib\` (not beside the exe) by the CI packaging step and found at run time by
  `LibFolder.Setup` (called first in `Program.Main`).
- The **Microsoft Edge WebView2 Runtime** must be installed on the user's machine. If it is missing, `SentinelPage`
  falls back to `LegacySentinelPage` (`WebBrowser` control in IE11 mode, set by `SentinelView.EnableModernPageMode`).

## Build-time only

| Dependency | Version | Used for |
|---|---|---|
| `Microsoft.NETFramework.ReferenceAssemblies.net461` | 1.0.3 | CI only: reference assemblies, because current `windows-latest` images lack the 4.6.1 targeting pack |
| MSBuild (`microsoft/setup-msbuild@v2`) | runner default | CI build |
| Visual Studio 2019 (16.11) or later | — | Local build ([build-system.md](build-system.md)) |

## Test project

`VisionTests` references `Vision.csproj` and framework assemblies only. There is **no test framework package**: the test
runner is `VisionTests/Program.cs` (see [testing-strategy.md](testing-strategy.md)).

## Card-side dependencies (VxWorks 6.4)

Vision depends on what the card image provides. Nothing is installed on the card.

| Card feature | Used by | If missing |
|---|---|---|
| Telnet server and `->` shell printing `value = N = 0x…` | Everything that runs commands (`CardSession.ExecuteAsync`) | No automation possible |
| `devs`, `ll` (fallback `ls`), `dosFsShow` | File explorer listing and free space | `ll` missing → `ls` fallback; free space unknown → upload not blocked |
| `copy`, `mkdir`, `rm`, `rmdir`, `xdelete`, `mv`, `xcopy` (in-card folder copy only; never over the FTP device) | File operations | Operation fails with a message |
| `open`, `read`, `write`, `lseek`, `close`, `malloc`, `free`, `tickGet` | Part-wise transfer of files ≥ 64 MB, cleanup after a dropped connection | Large transfers fail |
| `version` (prints `Boot line:`) | FTP credentials (`CardFtpConfigurator`) | No FTP; text files can still be opened through the shell |
| netDrv FTP device (`/host.<device>`) | Card side of every transfer | No file transfer |
| `bspBootChange` / `bootChange` | Boot settings | — |
| `reboot`, `FPGAGiveBoardReset` | Reboot detection (`CardSession.IsRebootCommand`) | — |
| `SentinelWebServerBaslat`, `SentinelWebServerBitir`, web server on 8080 | Sentinel (needs a current Voltran image) | Sentinel tab shows nothing to start |

## External services

| Service | Needed for |
|---|---|
| FTP server reachable from the PC **and** the card (normally the card's boot host) | File transfer |
| GitHub (repository `alibedr/Vision`, Actions, Releases) | Build, tests and releases ([build-system.md](build-system.md), [release-process.md](release-process.md)) |

## Internal dependency rules

- UI classes may depend on session and logic classes, never the other way round.
- Logic that needs the card goes through `ITransferHost` / `IBootConsole`, and FTP through `IFileUploader`, so it can be
  tested with fakes. Keep new logic behind these interfaces.
- `SerialSession` talks to `ISerialIo`, never to `SerialPort` directly.
