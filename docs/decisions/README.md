# Decision Record

Key technical and product decisions, why they were made, and what was rejected. Each entry points to its source. The
full history (cause, solution, classes, tests) is in [`../../Yardim/Gereksinimler.md`](../../Yardim/Gereksinimler.md);
rejected user requests are also listed with reasons under "Yapılmayacaklar" in
[`../../YAPILACAKLAR.md`](../../YAPILACAKLAR.md).

Before proposing a change that contradicts an entry here, read its source and explain what has changed.

## Adding a decision

Add a row to the right table: short decision, reason, rejected alternative, source (G number, version, or "session
decision" with date). Keep this file a summary; details go into `Yardim/Gereksinimler.md`.

## Architecture and protocols

| # | Decision | Reason | Rejected | Source |
|---|---|---|---|---|
| D1 | One telnet socket per card, shared by all features | VxWorks sends post-boot logs to the first session; a second socket would split logs and waste card sessions | Separate connection for the file explorer | G3, G53, architecture |
| D2 | Own telnet client; refuse all telnet options | Shell stays in plain line mode; no dependency | Telnet library from NuGet | `TelnetClient.cs` |
| D3 | File content via FTP, commands via telnet | Binary data through the shell is slow and corrupts; the card already uses FTP to boot | Hex / base64 over telnet | G22, G23 |
| D4 | FTP credentials read from the card (`version`), kept in memory only | Nothing to configure; always matches the card | User-entered FTP settings (early versions) | G23 |
| D5 | Files ≥ 64 MB in 64 MB parts, joined on the card with `open/read/write` and an 8 MB buffer | A single `copy` of 2.5 GB brought telnet down (netDrv loads the file into RAM); constant ~72 MB RAM, progress and instant cancel | 256 MB parts (RAM), 32 MB parts (twice the per-part overhead) | G39, G41, G42 |
| D6 | Folder upload with `mkdir` + one `copy` per file | Know exactly which file failed; `xcopy` over the FTP device brought the shell down | Single `xcopy` | G18.9, G57 |
| D7 | Unique FTP temp names per upload (`_tkt_…`, `_parca_…`) | The same file / folder can go to several cards at once without overwriting or deleting each other's copies | Shared plain names | G60 (1.10.1) |
| D8 | TCP keepalive for drop detection (~9 s) | Measures the socket in use; sends nothing to the shell | ICMP ping, periodic Enter | G7, G18 |
| D9 | The Vision that sent the reboot reconnects first (2 s vs 30 s + 15 s) | Boot logs go to the person who rebooted | Equal retry for everyone | G53 |
| D10 | Sentinel state by TCP connect / close on 8080 | Start / stop functions return nothing; probing does not make the server render a page | HTTP requests | G28 |
| D11 | Own VT100 emulator for the serial Screen view | BIOS / boot manager menus use cursor addressing; no suitable WinForms terminal control | Keep using TeraTerm next to Vision | G59 |
| D12 | Serial keys fixed to TeraTerm defaults (Backspace = BS, Delete = DEL) | The BIOS menu opens with DEL; users' TeraTerm has "Delete key" checked | Configurable key codes | G59.6, session decision 1.10 |
| D13 | .NET Framework 4.6.1 + WinForms, no installer | Runs from a copied folder on Windows 10/11 and closed networks | .NET 6+ / WPF (runtime to distribute) | G-Geliştirme |
| D14 | Only dependency: WebView2 for the Sentinel page, DLLs in `lib\` | Sentinel page needs modern JavaScript; folder stays tidy | Bundling a browser; IE-only page | G28, G37 |

## Product and UI

| # | Decision | Reason | Source |
|---|---|---|---|
| P1 | Cards stay in a grid sorted by group and IP; no free moving / resizing | Free layout brings persistence, overlap and resolution problems; "Büyüt" is for focus | G58.5 |
| P2 | No settings-folder menu item | Hand-edited `config.xml` can break and show an empty card list | G58.6 |
| P3 | Ctrl+Shift+L theme shortcut removed | Theme is chosen from the menu | G58.8 |
| P4 | Right click in the serial view opens a menu (no TeraTerm-style paste) | Same as telnet; avoids accidental paste. Paste: Ctrl+V / Shift+Insert, with confirmation for multi-line text | G59.9 |
| P5 | No quick-keys button bar in the serial view | Avoid button clutter; keys go to the card from the keyboard | G59.5 |
| P6 | Fewer buttons in general | Product owner preference | session decisions 1.9–1.10 |
| P7 | `Yardim/Gereksinimler.md` is not shown or embedded in the application | Developer document only | G33.2 |
| P8 | FTP password in boot records stored in plain text in `config.xml` | Product owner's choice; DPAPI encryption is an open idea (would make records machine-bound) | G51, YAPILACAKLAR |
| P9 | Log folder names are ASCII (`TelnetLoglari`, `SeriKanalLoglari`) | Avoid encoding problems | session decision 1.10 |
| P10 | Global boot window: command sent to all cards at once; card command line locked only in that window | Matches how the team changes boot settings; avoids typing into a boot prompt | G58 |

## Process

| # | Decision | Reason | Source |
|---|---|---|---|
| W1 | Discuss and plan before coding when asked or when UI / architecture changes | Product owner wants to decide scope and UX | session decisions |
| W2 | Merge only on explicit approval | Product owner tests every build on real cards first | session decisions |
| W3 | Feature versions bump minor, bug-fix-only versions bump patch; never re-release a number | Unambiguous bug reports; CI does not overwrite releases | 1.10.1 |
| W4 | Only the latest release is kept on GitHub and in `release/` | Users always get the fixed version | G38, PR #22 |
| W5 | Collect requests in `YAPILACAKLAR.md`, fix bugs immediately, group improvements into versions | Predictable versions that can be tested in one session | YAPILACAKLAR (PR #26) |
| W6 | Manual test checklists as interactive pages, delivered with the branch build | Product owner request | session decision 1.10 |
