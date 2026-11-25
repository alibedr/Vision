# Vision — Developer Documentation

This folder is the technical knowledge base for developing Vision. It is written for developers and AI coding
assistants. End-user documentation lives elsewhere and is in Turkish (see [Other documents](#other-documents)).

Start with [`../CLAUDE.md`](../CLAUDE.md) (Claude Code) or [`../AGENTS.md`](../AGENTS.md) (any other assistant). They
link back here.

## Index

| Document | Read it when you… |
|---|---|
| [architecture.md](architecture.md) | need the big picture: layers, channels to the card, main objects |
| [project-structure.md](project-structure.md) | look for where something lives in the repository |
| [modules.md](modules.md) | change a feature and need to know which files and classes own it |
| [dependencies.md](dependencies.md) | add or touch a .NET reference, NuGet package or card-side command |
| [execution-flow.md](execution-flow.md) | change start-up, shutdown, connection or transfer sequences |
| [threading-and-synchronization.md](threading-and-synchronization.md) | touch timers, async code, locks or anything shared between threads |
| [memory-management.md](memory-management.md) | open sockets, ports, files, timers, or allocate memory on the card |
| [build-system.md](build-system.md) | build, add a source file, change the project or CI |
| [coding-standards.md](coding-standards.md) | write code that should look like the existing code |
| [testing-strategy.md](testing-strategy.md) | add or run tests (every feature needs them) |
| [development-workflow.md](development-workflow.md) | start, finish or merge a feature (the mandatory process) |
| [versioning.md](versioning.md) | decide or change the version number |
| [release-process.md](release-process.md) | publish a release or touch `release/` |
| [decisions/README.md](decisions/README.md) | wonder why something is done a certain way, or want to propose something already rejected |
| [analysis-limitations.md](analysis-limitations.md) | need to know what this documentation could not verify |

## Other documents

These existing documents are authoritative for their own subject and are **not** replaced by `docs/`:

| File | Language | Content | Audience |
|---|---|---|---|
| [`../README.md`](../README.md) | Turkish | Project overview, features, requirements, build, versions | Everyone |
| [`../Yardim/KullanimKilavuzu.md`](../Yardim/KullanimKilavuzu.md) | Turkish | User manual; embedded into the exe and shown with F1 | Users |
| [`../Yardim/Gereksinimler.md`](../Yardim/Gereksinimler.md) | Turkish | Requirements G1–G60: what was asked, status, and **how it was met** (class / method level); version history | Developers |
| [`../YAPILACAKLAR.md`](../YAPILACAKLAR.md) | Turkish | Backlog: bugs, planned groups, ideas, rejected requests with reasons | Developers / product owner |
| [`../release/SurumNotlari.md`](../release/SurumNotlari.md) | Turkish | Release notes of the current version; used as the GitHub release body | Users |

`Yardim/Gereksinimler.md` is the most detailed record of past design decisions. When a section here says "see G42", it
means the G42 table in that file.

## Conventions used in these documents

- File paths are relative to the repository root unless the link says otherwise.
- **Confirmed** = verified in code, CI configuration or Git history. **Inferred** = strongly suggested by evidence but
  not stated anywhere. **Unknown** = could not be determined. Statements without a label are confirmed.
- Timing constants and limits are quoted from the code as of version 1.10.1. If you change one, update the document that
  quotes it.
