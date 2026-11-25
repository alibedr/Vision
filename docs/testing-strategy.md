# Testing Strategy

Every change that adds or changes behaviour ships with automated tests in `VisionTests`. CI runs all tests on every
push; a single failing test stops the build, so no package, pre-release or release is produced.

Related: [development-workflow.md](development-workflow.md) · [build-system.md](build-system.md) ·
[modules.md](modules.md)

## Infrastructure

| Item | Value |
|---|---|
| Project | `VisionTests/VisionTests.csproj` (console exe, .NET Framework 4.6.1), references `Vision.csproj` |
| Framework | None. Own runner in `VisionTests/Program.cs`: finds every method marked `[Test]` (`TestAttribute`) by reflection, runs `void` and `async Task` methods, prints `GEÇTİ` / `KALDI`, emits `::error::` lines for GitHub Actions, exits 1 on any failure |
| Assertions | `VisionTests/Check.cs`: `Check.True`, `Equal`, `Null`, `NotNull`, `SameBytes`; failures throw `CheckFailedException` |
| Samples | `VisionTests/Ornekler/*.txt` — real card outputs (`ll_ata0a.txt`, `devs_eski.txt`, `devs_refs.txt`, `dosFsShow_ata0a.txt`, `bootChange_oturum.txt`), read with `Check.Sample(name)` |
| Access to internals | `InternalsVisibleTo("VisionTests")` in `AppInfo.cs` |
| Run locally | `VisionTests\bin\Release\VisionTests.exe` or with a filter: `VisionTests.exe Yukleme` (matches `Class.Method`, case-insensitive) |
| Run in CI | Step "Testleri derle ve çalıştır" in `.github/workflows/build-exe.yml`, after the main build |
| Current size | 75 tests (version 1.10.1) |

## Test files and fakes

| File | Tests | Covers | Fake |
|---|---|---|---|
| `ParserTests.cs` | 13 | Shell response capture (`ShellResponse`: return value, negative values / addresses, missing command, late output before the echo), `DevsParser`, `DosFsShowParser`, `ll` / `ls` parsing, `BootChangeParser`, `bspBootChange` command recognition — with real samples | — |
| `TransferTests.cs` | 12 | Upload (small file, parts byte-identical, cancel deletes partial file, cleanup kept after a drop, wrong read count caught), download (small, parts), in-card copy / move, folder upload with skipped files, parallel uploads to several cards (G60) | `FakeCard` (`ITransferHost`: in-memory card disk and FTP, VxWorks shell emulation of `ll`, `copy`, `open`/`read`/`write`/`lseek`/`close`, `malloc`/`free`, `value =`), `FakeFtp` (`IFileUploader`; `AfterUpload` hook; FTP dictionary can be shared between cards) |
| `ConnectionTests.cs` | 7 | Outage episodes, planned reboot, reconnect order, orphan connection (G52–G54) | `FakeServer` (real `TcpListener` on localhost: accept, drop, come back) |
| `BootRecordTests.cs` | 12 | Boot record read / plan / apply / verify | `FakeBootCard` (`IBootConsole`: asks fields one by one, mixes logs, splits values into chunks) |
| `SerialTests.cs` | 19 | Send delays, key sequences, UTF-8 decoding, carriage return, `VtScreen` emulation and mode switching, resize | `FakeSerialIo` (`ISerialIo`) |
| `EventLogTests.cs` | 6 | `CardEventLog` write / read, card key includes port, size cap, CSV escaping, corrupt lines, file and transfer events | — |
| `SettingsTests.cs` | 6 | Grid order (group, numeric IP), quick-command migration and limits, part-transfer card memory limit, copy timeout grows with size, theme palette sanity | — |

## What every change must include

1. **New behaviour → new test.** Write the test against the logic class through the existing interfaces and fakes.
   Extend a fake (e.g. a new shell command in `FakeCard`) rather than bypassing it.
2. **Bug fix → regression test** that fails on the old code for the reported reason (e.g. the G60 tests interleave two
   uploads through a shared FTP dictionary).
3. **Real card output** for a new parser goes into `VisionTests/Ornekler/` (anonymise IPs/passwords if needed) and is
   used through `Check.Sample`.
4. **Timing-dependent logic**: make the timing `internal static` and shorten it in the test, as `ConnectionTests`
   does with `CardSession.OwnRebootRetryMs` and friends.
5. **New test file** → add it to `VisionTests/VisionTests.csproj` (`<Compile Include=…>`), otherwise it is not compiled.
6. **All tests green in CI** before asking for a merge. Report the CI summary line (`N test, N geçti, 0 kaldı`).

Testing cannot be skipped unless a documented technical limitation makes it impossible. In that case say so in the PR
and in `Yardim/Gereksinimler.md` (status **Kısmen**: implemented, needs verification on real hardware).

## What is not automated

| Area | How it is verified today |
|---|---|
| WinForms UI (layout, focus, keyboard, drawing) | Manual test checklist delivered to the product owner as an interactive checklist page (one per version, e.g. "Vision 1.10 Deneme Listesi") together with the branch build (`Vision.zip`) |
| Real cards, real FTP, real COM ports, WebView2 | Manual testing by the product owner on the lab cards |
| Performance with many cards | Not measured yet (see [threading-and-synchronization.md](threading-and-synchronization.md#known-risks)) |

There is no code-coverage tool. Coverage is maintained by the rule above: logic that changes gets tests in the same
branch.

## Ideas already recorded

From [`../YAPILACAKLAR.md`](../YAPILACAKLAR.md) "Fikirler": tests with a real BIOS capture (TeraTerm binary log) for the
serial Screen view, and more real card outputs (`bspBootChange`, `ll`) as samples.
