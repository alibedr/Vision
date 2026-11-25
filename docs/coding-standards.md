# Coding Standards

These are the conventions the existing code follows. Match them; do not introduce a new style in a file that has an
established one.

Related: [architecture.md](architecture.md) · [threading-and-synchronization.md](threading-and-synchronization.md) ·
[testing-strategy.md](testing-strategy.md)

## Language level

C# 7.3 on .NET Framework 4.6.1, but the code is written in a conservative subset (checked across the code base):

| Feature | Used? | Write instead |
|---|---|---|
| String interpolation `$"…"` | No | `"a " + b` concatenation |
| Null-conditional `?.`, `?[]` | No | explicit `if (x != null)` |
| `out var` | No | declare first: `bool fallback; ConfigStore.ResolvePath(out fallback);` |
| `nameof` | No | — |
| Expression-bodied members | Rare | `get { return _x; }` |
| `var` | Yes, for obvious types | |
| `async` / `await`, LINQ, lambdas | Yes | |

New code should stay inside this subset so it reads like the rest.

## Naming

- Types, methods, properties: `PascalCase`, English (`CardSession`, `ExecuteAsync`, `TransferActive`).
- Private fields: `_camelCase` (`_commandGate`, `_sessions`); constants and static readonly: `PascalCase`
  (`PartThresholdBytes`, `MaxViews`).
- Async methods end in `Async`.
- Some domain names are Turkish where they mirror user-visible concepts or stored data (`config.xml` elements like
  `<BootKayitlari>`, log folder names `TelnetLoglari`, `SeriKanalLoglari`). Keep stored names stable: changing them
  breaks existing configs.
- Folder and file names created at run time are ASCII (no Turkish characters) to avoid encoding problems
  (decided in 1.10).
- Test methods: Turkish, `Area_WhatHappens` (`Yukleme_AyniAdliDosyaIkiKartaAyniAnda`, `Vt_BoyutDegisinceIcerikKorunur`).

## Comments and documentation

- Comments and XML doc comments are **Turkish**. Public and non-obvious members have `/// <summary>`.
- Explain *why*, especially for card behaviour and timings ("VxWorks loglarını ilk bağlanan oturuma verir…").
- Reference the requirement number when code implements or fixes one: `(G53)`, `(1.10.1, G60)`.

## User-facing text

- All UI text, messages and logs for users are **Turkish**, short and cause-oriented ("Bağlantı reddedildi (kart bu
  portta dinlemiyor olabilir)"). Use `ErrorText.Describe` for exceptions; send technical detail to
  `ErrorText.LogTechnical` / `LogCrash` (`logs\hatalar.log`), not to the user.
- Prefer fewer buttons: the product owner explicitly prefers menus and existing controls over new buttons. Propose new
  buttons before adding them.
- Destructive or broad actions ask for confirmation with the safe choice as default (e.g. reboot defaults to "Vazgeç",
  G45).

## Structure

- Keep protocol and parsing logic out of forms. Put it in a logic / session class that can be tested
  (see [architecture.md](architecture.md#layers)).
- New card-facing logic should depend on `ITransferHost`, `IBootConsole`, `IFileUploader` or `ISerialIo`, not on
  concrete classes, so tests can use fakes.
- Big forms are split into `partial` files by area (`CardView.Actions.cs`). Add a new partial when a file grows a new
  area rather than growing the main file indefinitely.
- Tunable timings that tests must shorten are `internal static` (e.g. `CardSession.OwnRebootRetryMs`); user-tunable
  values belong in `AppSettings` / `ConnectionMonitorOptions`.
- Use `Theme` factories (`Theme.MakeButton`, `Theme.MakeLabel`) and `Theme` colours; never hard-code colours, so all
  themes work.

## Error handling

- Expected failures (network, card errors, FTP) are returned as results (`OpResult`, `ShellResponse`,
  `CardFtpResult`) with a Turkish message, not thrown to the UI.
- Unexpected exceptions are caught by the global handlers in `Program` and logged; do not swallow exceptions silently
  in new code: at least `ErrorText.LogTechnical(context, ex)`.
- Card-side resources must be cleaned up on every failure path ([memory-management.md](memory-management.md)).

## Files and encoding

- Files are UTF-8 **without BOM** (one exception today: `BootChangePanel.cs` has a BOM). Keep what the file has.
- Line endings follow `.gitattributes` (CRLF in the working tree for `.cs`, `.csproj`, `.sln`, `.md`, `.xml`).
- When generating C# from scripts, escape sequences such as `\n` inside C# string literals must be written so they end
  up as `\n` in the file (a raw newline inside a C# string breaks the build: CS1010).
