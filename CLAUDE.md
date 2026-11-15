# TwitchDownloader – working notes for Claude Code

## What this is
C# / .NET 10 tool for downloading Twitch VODs, clips and chat, and rendering chat to video.

| Project | What | Where things live |
|---|---|---|
| `TwitchDownloaderCore` | Library used by both front ends | `TwitchHelper.cs` (GQL/CDN calls, image cache), `VideoDownloader`/`ClipDownloader`/`ChatDownloader`/`ChatUpdater`/`TsMerger`, `ChatRenderer.cs` (SkiaSharp + ffmpeg pipe), `Models/M3U8*.cs`, `Options/`, `Tools/`, `Chat/` (JSON/HTML/TXT writers) |
| `TwitchDownloaderCLI` | Command line front end (CommandLineParser verbs) | `Program.cs`, one verb per file in `Modes/`, option classes in `Modes/Arguments/`, `Tools/` (`CliTaskProgress`, `PreParseArgs` legacy-syntax rewrite, `FileCollisionHandler`) |
| `TwitchDownloaderWPF` | Windows-only GUI (`net10.0-windows`) | `Page*.xaml(.cs)`, `Services/`, `TwitchTasks/` (queue items), `Translations/*.resx`, `Themes/*.xaml` |
| `*.Tests` | xunit v3 on Microsoft.Testing.Platform, one test class per production class | `TwitchDownloaderCore.Tests/TestHelpers` (shared by all three), fixtures in the top-level `TestData/` (copied into every test project; `TestData/README.md` says where each file comes from), `TwitchDownloaderCLI.Tests/EndToEndTests` (runs the built CLI, network blocked), `WpfResourceTests` (resx/theme checks that run on every OS) |

## Prerequisites
- .NET 10 SDK (`global.json` selects the Microsoft.Testing.Platform runner). If `dotnet --version` reports no SDK, point `DOTNET_ROOT`/`PATH` at an install (e.g. `export DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH`).
- `ffmpeg` on PATH for render/merge/E2E tests; they call `TestData.RequireFfmpeg()` and **skip** (not fail) without it. CI installs it on all three OSes.
- WPF builds only on Windows. On Linux/macOS compile-check it with `-p:EnableWindowsTargeting=true` (the WPF tests cannot run there).

## Build
```sh
dotnet build TwitchDownloaderCore
dotnet build TwitchDownloaderCLI          # binary: TwitchDownloaderCLI/bin/Debug/net10.0/TwitchDownloaderCLI
dotnet build TwitchDownloaderWPF.Tests -p:EnableWindowsTargeting=true   # Linux/macOS compile check of WPF + its tests
dotnet publish TwitchDownloaderCLI -p:PublishProfile=Linux            # profiles: Windows Linux LinuxAlpine LinuxArm LinuxArm64 MacOS MacOSArm64
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows          # Windows only; .github/workflows/build.yml shows the release recipe
```

## Verify a change (run these before calling anything done)
```sh
dotnet test --project TwitchDownloaderCore.Tests                                   # ~1,220 tests (~40 skipped), ~35 s test time / ~40 s wall
dotnet test --project TwitchDownloaderCLI.Tests                                    # ~390 tests incl. 61 E2E, ~10 s test time / ~15 s wall
dotnet test --project TwitchDownloaderCore.Tests --filter-class "TwitchDownloaderCore.Tests.DownloaderTests.VideoDownloaderTests"
dotnet test --project TwitchDownloaderCore.Tests --filter-method "TwitchDownloaderCore.Tests.ModelTests.M3U8Tests.Parse_IgnoresBlankLines"
dotnet test --project TwitchDownloaderCLI.Tests --filter-namespace "TwitchDownloaderCLI.Tests.EndToEndTests"       # E2E only (~8 s)
dotnet test --project TwitchDownloaderCLI.Tests --filter-not-namespace "TwitchDownloaderCLI.Tests.EndToEndTests"   # CLI unit tests only
dotnet test --project TwitchDownloaderWPF.Tests                                    # Windows only
dotnet build TwitchDownloaderWPF.Tests -p:EnableWindowsTargeting=true              # what Linux/macOS can do for WPF
```
Filters are Microsoft.Testing.Platform options (`--filter-class`, `--filter-method`, `--filter-namespace`, `--filter-trait`, each with a `--filter-not-*` twin); test names are fully qualified.

"Done" means: Core and CLI suites green locally (and WPF on Windows if you touched it), no new failures and no test un-skipped that still fails, the suites did not get noticeably slower than the times above, and every new test has a what/why comment.

### Coverage and the CI report
```sh
dotnet test --project TwitchDownloaderCore.Tests --results-directory results/test-results-local/Core \
  --report-junit --report-junit-filename Core.junit.xml \
  --coverlet --coverlet-output-format cobertura --coverlet-include "[TwitchDownloader*]*" --coverlet-exclude "[*.Tests]*"
git diff --name-only master...HEAD > changed.txt
python3 .github/scripts/test_report.py --results results --changed changed.txt --repo-root . --output report.md
```
`test_report.py` expects `--results` to hold `test-results-<platform>/` folders with `*.junit.xml` and `coverage*.cobertura*.xml`; it prints per-platform pass/fail, failing tests, per-project line coverage and the least-covered files you touched. Current line coverage: Core ~97 %, CLI ~86 %, WPF ~10 % (UI code-behind is untested). Do **not** run the E2E tests under coverlet: the child CLI processes race on the coverage file (CI runs them in a separate, uninstrumented pass).

## Test conventions (enforced by review)
- Every test class and test method has a comment saying **what** it checks and **why** it matters (which user-visible behaviour breaks). Name tests `Subject_Scenario_Expectation` or `DoesX_WhenY`.
- Unit tests never touch the network. Fake HTTP with `FakeHttpMessageHandler` (records requests) and swap the static clients with `HttpClientOverride` (`TwitchHelper.httpClient`, `ChatDownloader.HttpClient`, `ClipDownloader.HttpClient`, `UpdateHandler.HttpClient`); `VideoDownloader._httpClient` is an instance field. Any test doing that must be in `[Collection(StaticHttpClientCollection.NAME)]`. `FakeTwitch` (Core `DownloaderTests`) fakes all Twitch/CDN endpoints from the production DTOs. E2E runs route HTTP through a dead proxy.
- Process-global state (Console.In/Out, cwd, env vars, `Settings.Default`) only inside `[Collection(ProcessStateCollection.NAME)]` and restored with the `ConsoleCapture`/`EnvironmentVariableOverride`/`WorkingDirectoryOverride` helpers.
- To test a non-public member, make it `internal`: every production project exposes internals to its test projects (`InternalsVisibleTo`). Don't use `[UnsafeAccessor]` or reflection by name; they compile fine and only fail at runtime when the member is renamed. Do not change production behaviour in a test PR.
- Fixtures live in `TestData/` and are small, synthetic and anonymized; the one exception is `Render/real_chat_60s.json.gz` (~0.8 MB, a real chat with anonymized commenters, used only by the render preview). Every generated fixture has a script in `TestData/generators/`; change the generator, re-run it and commit both (see `TestData/README.md`). Binary types are declared in `TestData/.gitattributes`; `*.cs` files are CRLF in the working copy per the root `.gitattributes`.
- Python (`.github/scripts/`, `TestData/generators/`) is Python 3.10+ with full type hints and must pass `mypy --strict .github/scripts/*.py TestData/generators/*.py` (CI checks it).
- Temp files go in `TempDirectory` (deleted on dispose). Write cross-platform: `Path.Combine`, `\n` for console assertions (`ConsoleCapture` normalises), no assumptions about case-sensitive file systems (macOS is not), `/tmp` may be `/private/tmp` on macOS, Windows cannot delete the current directory or an open file, no hard-coded timings below ~50 ms.
- Found something wrong while writing a test? Keep the test asserting the behaviour you believe is *correct* and skip it with a reason:
  `Skip = "Bug: <root cause and symptom>"` when the code is clearly wrong (wrong variable, off-by-one, crash on valid data, leak, regression), or
  `Skip = "Existing behavior in v<version>: <what it does>"` when it may be intentional (validation/parsing strictness, required option combinations,
  error-reporting style, UI choices), so a maintainer can decide later. Use `Assert.SkipWhen(..., "Bug: ...")` for platform-specific ones.
  `grep -rnE '"(Bug|Existing behavior in v[0-9.]+): ' --include=*.cs .` lists them; fixing or accepting one means removing its Skip (and, if the
  current behaviour is the intended one, rewriting the test to assert it). Don't skip for any other reason except a missing ffmpeg (`TestData.RequireFfmpeg()`).
- Keep the suites fast: Core ≈ 30 s, CLI ≈ 15 s. Fake delays (cancel the token) instead of waiting for retry back-offs.
- Block-scoped namespaces, `Assert.Equal(expected, actual)`, one assertion topic per test, no `Thread.Sleep` to "wait for" work.

## CI (.github/workflows)
- `test.yml` – on push to master and every PR: ubuntu/windows/macos matrix, installs ffmpeg, runs Core + CLI (+ WPF on Windows) with coverage, then the E2E namespace uninstrumented, uploads JUnit/cobertura, and the `report` job posts/updates one sticky PR comment (built by `.github/scripts/test_report.py`). A `python` job type-checks the Python scripts with `mypy --strict`.
- `test-report-comment.yml` – posts that comment for fork PRs (runs with the base repo's token after `Tests` completes).
- `render-preview.yml` – a `/render-preview` PR comment (owner/member/collaborator) renders the sample chats, the alpha mask and a real highlight chat with the base and PR builds, and posts the frames side by side with a difference image (images live on the `render-previews` branch). Renders are lossless and deterministic, so "✅ identical" means pixel-identical. Run it locally against two CLI builds with `python3 .github/scripts/render_preview.py --base <base CLI> --head <PR CLI> --fixtures TestData --out preview`.
- `build.yml` / `build-pr.yml` – release/PR publish builds with the profiles above.
