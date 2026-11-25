# Versioning

Vision uses `MAJOR.MINOR` for feature versions and `MAJOR.MINOR.PATCH` for bug-fix-only versions. **Every merge to
`main` raises the version, but only after the product owner has approved the number.** The version is set by hand in
`AppInfo.cs` on the branch; CI does not increment anything, it only reads the version and publishes a release for it.

Related: [release-process.md](release-process.md) · [development-workflow.md](development-workflow.md)

## Where the version lives

All in `AppInfo.cs`, changed together:

| Location | Example (current) | Used for |
|---|---|---|
| `[assembly: AssemblyVersion("…")]` | `1.10.1.0` | .NET assembly identity |
| `[assembly: AssemblyFileVersion("…")]` | `1.10.1.0` | Windows file properties |
| `[assembly: AssemblyInformationalVersion("…")]` | `1.10.1` | Product version shown by Windows |
| `public const string Version = "…"` in `AppInfo` | `1.10.1` | UI (`AppInfo.Signature`: "Vision v1.10.1 · Geliştiren: Ali Bedir"), About, **and CI** (release tag) |

CI extracts the version with the regex `public const string Version = "([0-9.]+)"`; keep that line's exact shape.

`AppInfo.DevBuild` (`internal const int`, currently 4) is an old internal build counter. It is not shown and has not
been updated since early versions (Unknown whether it is still meant to be maintained; see
[analysis-limitations.md](analysis-limitations.md)).

## Rules

**The product owner decides the number (decision of 25.11.2026).** Before every merge to `main`:

1. Propose a number with a one-line reason, using the defaults below.
2. Ask. Do not change `AppInfo.cs` before the answer.
3. Use exactly what the product owner says: "okey" → the proposed number; "1.12 ile mergele" → 1.12; "artırma" → no
   change.
4. Commit the version change on the branch, let CI build it, then merge.

Defaults to propose:

| Change merged to `main` | Proposal | Evidence |
|---|---|---|
| A feature version (one or more features / a feedback round) | **Minor +1**: 1.9 → 1.10 | Confirmed: v1.0 … v1.10 |
| Bug fix only, no new feature | **Patch +1**: 1.10 → 1.10.1 | Confirmed: 1.10.1 |
| Documentation / backlog only (`*.md`, `docs/`) | Ask as well; until 25.11.2026 such merges kept the version (PR #26, #28). If a new number is approved, the `AppInfo.cs` change triggers CI and a release with the same exe content | Confirmed rule, history |

- **Never republish the same number** for a different build. CI does not overwrite an existing release (it only
  refreshes its notes), and two different exes with one number make bug reports ambiguous. This was the explicit
  decision for 1.10.1.
- The **major** version stays 1. When the serial channel (a large new feature) arrived, the product owner chose 1.8
  rather than 2.0. A major bump needs the product owner's decision.
- Minor numbers are decimal, not fractions: 1.9 → **1.10** → 1.11.
- The version change is part of the feature branch, so the branch build (`build-<branch>`) already shows the new
  number for testing.

## Pre-release builds

Branch builds are published as GitHub pre-releases named `build-<branch>` (re-created on each push). They carry
whatever version the branch's `AppInfo.cs` has; they are test builds, not versions. They are deleted when `main` is
released.

## History

| Version | Tag | Content (short) |
|---|---|---|
| 1.0 | `v1.0` | First official release |
| 1.1–1.7 | `v1.1` … `v1.7` | Large file transfer, downloads, safety, tests, event log, boot records, reconnect order |
| 1.8 | `v1.8` | Serial channel |
| 1.9 | `v1.9` | Beta feedback round |
| 1.10 | `v1.10` | Serial fixes, VT100 screen |
| 1.10.1 | `v1.10.1` | Unique FTP temp names for parallel uploads; card name in transfer windows |

Full history: "Sürüm notları" table in [`../Yardim/Gereksinimler.md`](../Yardim/Gereksinimler.md) and
"Önceki sürümlerde gelenler" in [`../release/SurumNotlari.md`](../release/SurumNotlari.md).

## Wording in other files

`README.md` ("Sürümler") states the rule above. The comment in `AppInfo.cs` and the comment on the release step of the
workflow still say "küçük sürüm bir artırılır" on every merge, without the approval step and the patch case; they are
code / CI files and were left unchanged in a documentation-only change.
