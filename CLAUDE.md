# CLAUDE.md

Instructions for Claude Code in this repository. The shared project rules are in [`AGENTS.md`](AGENTS.md) — **read it
first**; it is the single source for workflow, testing, documentation, versioning and merge rules. This file only adds
what is specific to working as Claude Code here.

Documentation index: [`docs/README.md`](docs/README.md).

## Read before implementing

| Task | Read |
|---|---|
| Any change | `AGENTS.md`, [`docs/architecture.md`](docs/architecture.md), the area in [`docs/modules.md`](docs/modules.md), its G groups in `Yardim/Gereksinimler.md` |
| Connection, commands, transfers | [`docs/execution-flow.md`](docs/execution-flow.md), [`docs/threading-and-synchronization.md`](docs/threading-and-synchronization.md), [`docs/memory-management.md`](docs/memory-management.md) |
| Tests | [`docs/testing-strategy.md`](docs/testing-strategy.md) |
| Starting / finishing / merging | [`docs/development-workflow.md`](docs/development-workflow.md), [`docs/versioning.md`](docs/versioning.md), [`docs/release-process.md`](docs/release-process.md) |
| "Why is it like this?" | [`docs/decisions/README.md`](docs/decisions/README.md) |

## Working with the product owner

- Reply in **Turkish**, plainly and briefly. Explain what changed, the CI test result, what to test, and what is not
  verified on real hardware.
- If asked to discuss first ("önce tartışalım", "direkt değişiklik yapmak yok"): analyse and propose, no code.
- Prefer fewer buttons; propose UI additions before building them.
- Merge only after explicit approval. An approval to *add* something is not an approval to *merge* it.
- Before every merge, ask which version number to use (propose one); change `AppInfo.cs` only to the approved number.
- Manual test checklists go out as an interactive, checkable page, together with the branch build.

## Environment facts (Claude Code session)

- Linux container, **no .NET Framework toolchain**: code cannot be compiled or tested locally. Push and use CI
  (`.github/workflows/build-exe.yml`); wait for the run and read its log (`N test, N geçti, 0 kaldı`).
- Doc-only branches do not trigger CI: `gh workflow run build-exe.yml --ref <branch>` if a build is needed.
- `gh` commands that go through GraphQL failed with 403 in these sessions (observed with `gh release list`); use the
  REST API via `gh api`:
  - list releases: `gh api repos/alibedr/Vision/releases`
  - download an asset: `gh api -H "Accept: application/octet-stream" repos/alibedr/Vision/releases/assets/<id> > Vision.zip`
  - open a PR: `gh api -X POST repos/alibedr/Vision/pulls --input pr.json` (write `pr.json` with UTF-8, Turkish text kept)
  - merge: `gh api -X PUT repos/alibedr/Vision/pulls/<n>/merge -f merge_method=merge`
- Pushes can fail transiently: retry (`for i in 1 2 3; do git push && break; sleep 15; done`). The message "This
  repository moved" is harmless.
- If a stop hook says a branch has no remote: the push usually succeeded; set the upstream
  (`git config --add remote.origin.fetch "+refs/heads/<b>:refs/remotes/origin/<b>"`, `git fetch`,
  `git branch -u origin/<b>`).
- Working-tree line endings are CRLF for `*.cs`, `*.md`, `*.csproj`, `*.xml` (`.gitattributes`). When editing with
  scripts, read and write preserving `\r\n`.
- When generating C# with Python, a `\n` meant for a C# string must be written as `\\n` in the Python source, otherwise
  the build breaks (CS1010).
- Commit messages: Turkish subject + body, ending with the attribution lines the session requires. PR bodies: "Özet"
  and "Test" sections, ending with the session's attribution lines.

## Delivering builds

After CI is green on a branch, download `Vision.zip` from the `build-<branch>` pre-release and send it to the product
owner as a file, with a short Turkish summary of what to test.
