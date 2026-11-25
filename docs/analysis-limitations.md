# Analysis Limitations

What this documentation could not verify, inconsistencies found between documents and code, and open questions. Written
on 25.11.2026 for version 1.10.1. Remove an item once it is resolved.

Related: [README.md](README.md)

## How the documentation was produced

- Source of facts: the repository at `main` (commit `19b3942`, version 1.10.1), the CI workflow, Git history (152
  commits, PRs #1–#28, tags `v1.0`…`v1.10.1`), the Turkish documents (`README.md`, `Yardim/*.md`, `YAPILACAKLAR.md`,
  `release/SurumNotlari.md`) and the working sessions with the product owner.
- **Conversation history is only partly available.** The sessions that built versions 1.0–1.7 are not accessible; for
  those, decisions were recovered from `Yardim/Gereksinimler.md` and commit messages. The sessions of 1.8–1.10.1 were
  available as a summary plus the recent full conversation. Decisions labelled "session decision" come from those.
- **Nothing was compiled or executed** for this documentation. The environment is Linux without the .NET Framework
  toolchain. Build and test facts come from the CI configuration and from CI runs observed during development (last:
  75 tests passing on the 1.10.1 branch and on `main`).
- Class, method, field and file names were checked with text search against the source. Behavioural descriptions
  were taken from code reading and the requirement tables; they were not re-tested.

## Inconsistencies found (not changed)

| Topic | Where | What | Suggested fix |
|---|---|---|---|
| Version comments | comment in `AppInfo.cs`, comment on the "Resmî sürümü yayınla" step in `build-exe.yml` | Say the minor version is increased on every merge, without the approval step and the patch case ([versioning.md](versioning.md#rules)) | Reword with the next code change |
| `AppInfo.DevBuild` | `AppInfo.cs` | Internal build counter is 4 since early versions; unclear if still maintained | Remove or define a rule |
| DPAPI | `YAPILACAKLAR.md` idea "FTP parolasını şifreli saklama" | `PasswordProtector` (DPAPI) already exists in `FileTransfer.cs` for the legacy manual FTP password; the idea could reuse it | Mention in the backlog item |

## Not analysed in depth

| Area | Why | Risk |
|---|---|---|
| WinForms layout and painting code (`Theme.cs`, `DarkControls.cs`, `Icons.cs`, dialogs, tour) | Large, UI-only, not unit-tested | Low for architecture; UI regressions are found by manual tests |
| `HelpMarkdown.cs` Markdown subset | Not needed for workflow docs | Manual content must stay within what the renderer supports; check the manual in the app after editing |
| Exact `FtpWebRequest` behaviour against the team's FTP server | Environment-specific | Passive → active fallback is in code; real behaviour only on site |
| WebView2 lifecycle details (`EdgeSentinelPage`) | Read only at entry points | Memory / process use of `msedgewebview2.exe` after the Sentinel tab was opened |
| Performance with many cards | Not measured | See [threading-and-synchronization.md](threading-and-synchronization.md#known-risks) |
| Card-side behaviour (VxWorks image specifics, Sentinel server) | Not in this repository | Facts are from code comments and requirements |

## Open questions for the product owner

1. Is `AppInfo.DevBuild` still needed?
2. Branch naming: is one prefix scheme preferred for the future (e.g. only `feature/`, `fix/`, `docs/`)? History mixes
   several.
3. Is the serial break signal (TeraTerm Alt+B) used? (open in `YAPILACAKLAR.md`)
4. Open bug: some `.txt` files do not open on double-click in the file explorer; needs file name, size, path, error
   text, `logs\hatalar.log`, and `ll` output.
