# Linux Runtime Compatibility

The Azure DevOps build agents are **Linux containers**, and the service ships in a Linux Docker image. **The Linux
build and tests are the source of truth, not Windows.** You can develop and run tests on a Windows dev box, but
everything you write — production code, unit tests, and service tests — MUST behave identically on Linux. A green
Windows build means nothing until the Linux build and tests are also green.

The differences below pass silently on Windows and only surface on the agent. Write code and tests to be Linux-safe
from the start; you do **not** need to run a Linux build locally before pushing.

## Source-file encoding

Save every `.cs` file as **UTF-8** (BOM preferred for mixed Windows/Linux teams). A non-ASCII literal — `€`, Greek
or accented letters, `–`, `×` — saved in the Windows-1252 codepage decodes to `�` (U+FFFD) on Linux, so the file
fails to compile or a string assertion compares against the wrong value.

The `.editorconfig` rule enforces this for new files:

```ini
[*.cs]
charset = utf-8-bom
```

In tests, prefer asserting against a shared constant or the production formatter's own output over a hand-typed
literal, so the test and the code share one source of the special character. When a literal is clearest, a Unicode
escape (`"€"` for `€`) is always correct on every platform and codepage.

## Case-sensitive paths

Linux filesystems are case-sensitive. Match case **exactly** for file names, folder names, namespaces,
embedded-resource paths, and `Include=` globs — `TestData/Foo.png` ≠ `testdata/foo.png`.

The root NuGet config MUST be named `nuget.config` (all lowercase). A `Nuget.config` / `NuGet.config` is picked up
on Windows but **silently ignored on Linux**, so restore falls back to defaults and can fail to find private feeds.

## File paths

Build paths with `Path.Combine` / `Path.DirectorySeparatorChar`, never a literal `\`. Load test assets via
`CopyToOutputDirectory` content and reference them relative to `AppContext.BaseDirectory`, not an absolute or
Windows-style path.

## Culture-dependent formatting

Always pass an explicit `CultureInfo` to `ToString`, `string.Format`, and parsing. Never rely on the ambient
`CurrentCulture` — Linux uses ICU, Windows uses NLS, and results differ (digit grouping, the space before a currency
symbol, etc.), so culture-free assertions are flaky by construction. Pin the culture in tests so the expected string
is deterministic:

```csharp
var greek = CultureInfo.GetCultureInfo("el-GR");
Assert.Equal(expected, amount.ToString("C", greek));
```

If an assertion is still brittle across ICU/NLS edge cases, assert structurally (contains the symbol, correct decimal
separator) instead of byte-for-byte.

## Graphics, fonts, and image snapshots

- `System.Drawing.Common` is **unsupported on Linux** (`PlatformNotSupportedException`). Use a cross-platform library
  (`SkiaSharp` or `SixLabors.ImageSharp`).
- Linux containers do not ship Windows system fonts. Bundle required font files in the repo, copy them to the output
  of every project that renders at runtime (including the **test project**), and load them by **explicit path**,
  never by system font name.
- Generate image/snapshot baselines **on Linux** (the platform CI uses). Text rasterization differs between Windows
  and Linux, so a Windows-recorded baseline will never match. Keep pixel tolerance tight enough to still catch real
  regressions.

## Containers in service tests

Service tests use Testcontainers (Redis, SQL Server), which needs a working Docker daemon — the Linux agents provide
one. Use Linux container images for every Testcontainers dependency so the same image runs locally and on the agent.

## Migrating an existing service

To move a service's CI/CD from Windows agents to Linux container agents — including the pipeline-template branch
switch and regenerating baselines — use the `dotnet-linux-agent-migration` skill.
