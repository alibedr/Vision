# AGENTS.md — Rules for AI coding assistants

Vision is a Windows Forms (C# 7.3, .NET Framework 4.6.1) multi-card terminal for VxWorks 6.4 boards: telnet, file
transfer over FTP, boot settings, serial console and the Sentinel health page. Product owner: Ali Bedir (writes in
Turkish). Repository: `alibedr/Vision`.

This file holds the project rules for any AI assistant. Details live in [`docs/`](docs/README.md); if this file and
`docs/` ever disagree, `docs/` and the code win — then fix this file. Claude Code also reads [`CLAUDE.md`](CLAUDE.md).

## Before you change anything

1. Read [`docs/architecture.md`](docs/architecture.md) and the module section in [`docs/modules.md`](docs/modules.md)
   for the area you touch.
2. Read the matching requirement groups (G…) in [`Yardim/Gereksinimler.md`](Yardim/Gereksinimler.md) — they explain
   why the code is the way it is.
3. Check [`docs/decisions/README.md`](docs/decisions/README.md) and "Yapılmayacaklar" in
   [`YAPILACAKLAR.md`](YAPILACAKLAR.md) so you do not re-propose something already rejected.
4. If the product owner asked to discuss first, or the change adds UI or touches architecture: **propose a plan and
   wait for approval. Do not write code yet.**

## Non-negotiable technical rules

- One telnet socket per card; features reuse the card's `CardSession` (never open a second connection).
- Telnet carries commands, FTP carries file content. Every FTP name written during an operation must be unique
  (`FileBrowser.FtpTempName`, `FtpPartBase`).
- Anything allocated or opened on the card (`malloc`, `open`) is registered for cleanup in the same step
  ([`docs/memory-management.md`](docs/memory-management.md)).
- No controls touched from background threads; shell access only through the card's command gate
  ([`docs/threading-and-synchronization.md`](docs/threading-and-synchronization.md)).
- Nothing is installed on the card; no new third-party dependency without the product owner's approval.
- UI text, messages, comments, commit messages and user documents are **Turkish**; identifiers are English.
- Follow the code style of the file you edit ([`docs/coding-standards.md`](docs/coding-standards.md)): no `$""`, no
  `?.`, no `out var`.
- New `.cs` files must be added to `Vision.csproj` (or `VisionTests/VisionTests.csproj`).

## Definition of done for a change

1. Implemented on a branch from `main` (never on `main`), without unrelated refactoring.
2. Tests added or updated in `VisionTests` (regression test for every bug) — [`docs/testing-strategy.md`](docs/testing-strategy.md).
3. CI green on the branch (build + all tests). Report the CI summary; never claim a result CI did not produce.
4. Documentation updated in the same branch, only where affected: user manual, tour, `Yardim/Gereksinimler.md`
   (G section + "Sürüm notları" row), `README.md`, `YAPILACAKLAR.md`, `docs/` — table in
   [`docs/development-workflow.md`](docs/development-workflow.md#7-documentation-and-version-c).
5. Version: every merge to `main` raises it, **but ask first** — propose a number (minor for features, patch for a
   bug fix), then set exactly the number the product owner approves or names — [`docs/versioning.md`](docs/versioning.md).
6. Branch build (`Vision.zip` from the `build-<branch>` pre-release) handed to the product owner, with a test checklist
   when the UI changed.

## Merging and releasing

- Merge **only** when the product owner explicitly approves ("mergeleyebiliriz").
- Before merging a code change: refresh `release/Vision/` from the branch build and update `release/SurumNotlari.md` —
  [`docs/release-process.md`](docs/release-process.md).
- CI on `main` publishes `v<version>` and removes older releases. Never re-use a version number.
- Documentation-only changes: branch + PR + approved merge; ask about the version for these too.

## Keeping documentation alive

When a change alters architecture, dependencies, build, tests, versioning, release or a convention, update the
matching `docs/` file (and this file or `CLAUDE.md` for a new convention) in the same branch. Do not touch documents the
change does not affect.
