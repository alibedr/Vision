# Versioning

Vision uses `MAJOR.MINOR` for feature versions and `MAJOR.MINOR.PATCH` for bug-fix-only versions. The version is set
**by hand** in `AppInfo.cs` on the branch before merging; CI does not increment anything, it only reads the version and
publishes a release for it.

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

| Change merged to `main` | Version change | Evidence |
|---|---|---|
| A feature version (one or more features / a feedback round) | **Minor +1**: 1.9 → 1.10 | Confirmed: v1.0 … v1.10 |
| Bug fix only, no new feature | **Patch +1**: 1.10 → 1.10.1 | Confirmed: decided for 1.10.1 |
| Documentation / backlog only (`*.md`, `docs/`) | None | Confirmed: e.g. PR #26, #28 |
| Release folder / CI only | None, unless the exe changes | Inferred from PR #22 |

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

## Documented discrepancy

`README.md` ("Sürümler"), the comment in `AppInfo.cs` and the comment on the release step of the workflow say the minor
version is "increased on every merge to main". In practice it is increased **by hand in the branch**, only for merges
that change the application, and bug-fix versions use the patch number. These texts were not changed by the
documentation task; update them when convenient.
