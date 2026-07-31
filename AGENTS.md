# AGENTS.md

Guidance for AI coding agents working in this repository. `CLAUDE.md` imports this file, so
keep it the single source of truth and every agent reads the same instructions.

AngleSharp.Io is an AngleSharp extension package that provides additional requesters,
cookie handling, download support, and some DOM/network helpers for headless browsing.

## Commands

The orchestrator is Fallout (`build/Build.cs`), bootstrapped by `build.ps1` / `build.sh`
(`build.cmd` forwards to either). Default target is `RunUnitTests`.

```powershell
.\build.ps1                        # restore, compile, run the full test suite
.\build.ps1 -Target Compile        # other targets: Clean Restore Compile RunUnitTests
.\build.ps1 -Target Package        #   CopyFiles CreatePackage Package PrePublish Publish
```

For the normal edit/test loop use the SDK directly — much faster than the Fallout bootstrap:

```powershell
dotnet build src/AngleSharp.Io.sln
dotnet test src/AngleSharp.Io.Tests/AngleSharp.Io.Tests.csproj -f net8.0
dotnet test src/AngleSharp.Io.Tests/AngleSharp.Io.Tests.csproj -f net8.0 --filter "FullyQualifiedName~ClassicTests"
dotnet test src/AngleSharp.Io.Tests/AngleSharp.Io.Tests.csproj -f net8.0 --filter "Name=SettingSimpleSingleCookieInRequestAppearsInDocument"
```

- Always pass `-f net8.0` when iterating. On Windows both projects also target `net462` and
  `net472`, so omitting it runs everything three times.
- `TreatWarningsAsErrors` is on (`src/Directory.Build.props`) — a warning breaks the build.
  There is no separate lint step; the compiler is it.
- The package version is parsed from the top entry of `CHANGELOG.md` (`ReleaseNotesParser`),
  not from a csproj property. Release-worthy changes get a `CHANGELOG.md` line.
- `RunUnitTests` runs the suite twice, differing only in a `prefetched` environment variable
  that nothing in this repo currently reads — a single run is equivalent locally.
- There is no `global.json`; the bootstrap scripts use the STS channel, CI installs 10.0.x.

## Architecture

The main integration surface is `src/AngleSharp.Io/IoConfigurationExtensions.cs`:

- `WithRequesters(...)` registers the transport stack (`HttpClientRequester`, `DataRequester`,
  `FtpRequester`, `FileRequester`, `AboutRequester`).
- `WithCookies(...)` wires `AdvancedCookieProvider` (memory or file-backed) as the cookie service.
- `WithDownload(...)` / `WithStandardDownload(...)` wraps AngleSharp's document factory to
  intercept attachments / binary responses.

Cookie subsystem layout (`src/AngleSharp.Io/Cookie`):

- `AdvancedCookieProvider`: storage + RFC-like set/get behavior + expiry pruning.
- `CookieParser` / `WebCookie`: parsing and cookie model.
- `Helpers`: domain/path matching and canonicalization utilities.
- `NetscapeCookieSerializer`: persistence format conversion.
- `LocalFileHandler` / `MemoryFileHandler`: persistence backends.

Important cookie behavior notes:

- Domain canonicalization strips a leading dot (`.example.com` -> `example.com`) per RFC6265.
- Host-only cookies must match host exactly; non-host-only cookies use `DomainMatch(...)`.
- Be careful when filtering candidates for `GetCookie(...)`: exact-domain prefiltering can
  accidentally exclude valid parent-domain cookies for subdomains (issue #36).
- `GetPublicSuffix(...)` in `Helpers` is currently a placeholder (returns empty string), so avoid
  assuming public suffix protections are fully implemented.

## Code conventions

From `.github/CONTRIBUTING.md` and `.editorconfig` — these differ from typical modern C#:

- `using` directives go **inside** the namespace declaration.
- Framework type names, not keywords: `String`, `Int32`, `Boolean`, `Object`.
- Prefer `var` on the left-hand side wherever possible.
- Always use statement blocks; blank line between two non-simple statements.
- `ConfigureAwait(false)` on every `await`.
- 4 spaces, LF, UTF-8, trimmed trailing whitespace; 2 spaces in `*.csproj`.
- Older files use `#region Fields / ctor / Properties / Methods / Helpers`; match the file
  you are editing rather than converting it.
- Comments in the newer code explain *why* a non-obvious construct exists (prototype cycles,
  laziness, Jint quirks). Keep that density — do not narrate what the code already says.

## Tests

NUnit 3 (classic model: `Assert.AreEqual`), fixtures in `src/AngleSharp.Io.Tests`.

Useful test groups:

- `Cookie/ParsingTests.cs`: fast, deterministic unit tests for parser + provider behavior.
- `Cookie/ClassicTests.cs`: higher-level cookie behavior, includes some network-dependent tests.
- `Network/*Tests.cs`: requester-focused behavior.

Fast iteration commands:

```powershell
dotnet test src/AngleSharp.Io.Tests/AngleSharp.Io.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AngleSharp.Io.Tests.Cookie.ParsingTests"
dotnet test src/AngleSharp.Io.Tests/AngleSharp.Io.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AngleSharp.Io.Tests.Cookie"
```

Test-writing tips for cookie work:

- Prefer `MemoryFileHandler` to keep tests isolated and deterministic.
- For provider-level checks, exercise `ICookieProvider.SetCookie` + `ICookieProvider.GetCookie`
  directly with explicit `Url` instances.
- Network-backed tests (e.g., against httpbingo) can fail when payload formats change; treat those
  failures as potential external drift before assuming a regression in cookie logic.

## Repository notes

These files are synced from the `AngleSharp.GitBase` repository and should not be edited
here: `.editorconfig`, `.gitignore`, `.gitattributes`, `.github/*`, `build.ps1`, `build.sh`,
`tools/*`, `LICENSE`.

CI (`.github/workflows/ci.yml`) builds on Linux and Windows; on Windows it selects the Fallout
target from the branch (`main` → `Publish`, `devel` → `PrePublish`, otherwise the default).
`CONTRIBUTING.md` asks for feature branches (`feature/#777`) and pull requests against
`devel`.
