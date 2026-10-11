# Lunar 0.1.6: repeated startup repository waits

Tested on 2026-10-11. Pack 0.1.6 changes only the optional **Faster bootstrap download fallback** definition, now version 0.1.1. It requires Patchwork 0.9.2 and the exact inspected originals for launcher 3.7.17-ow or 3.7.24-ow, multiver 29a43e6 and Genesis 0.1.0-SNAPSHOT.

## Reproduced behavior and cause

The user reported that Minecraft 26.3/Fabric still stayed at **Starting JVM** with 0.1.5. During their three-minute test, the temporary local Java agent captured the main thread at 30, 90 and 180 seconds inside `HttpClient.send`, called by Lunar's multi-repository Maven downloader. Java had started. These samples did not show a Java monitor deadlock or establish a RAM or graphics cause.

Correlated process and cache observations showed that downloads were progressing slowly. A connection to Fabric's repository stayed in SYN_SENT while alternate-repository connections succeeded. The actual cache is `%APPDATA%\.minecraft\libraries`; Lunar's 26.3 configuration requests native artifacts for other platforms as well as Windows. An isolated probe using the real 0.1.5 downloader fetched a missing public dependency through Mojang in 15.768 seconds, after the first repository's 15-second connection timeout. The precise URI in the user's thread capture was not logged; identifying Fabric as the live first failing origin uses the configured repository order and correlated connection observations.

The confirmed repeated-delay mechanism is that 0.1.5 tries the same unreachable origin again for every uncached artifact. The next live baseline still displayed Starting JVM at 22, 35, 64 and 94 seconds. Two overlapping Lunar JVMs were found; both were stopped before the candidate was applied. Immediately before the candidate's first launch, 21 dependencies declared in the 26.3 section remained absent from the real cache. Cache hits were ZIP-validated; the host cache was not cleared or artificially prepopulated for the candidate.

## Targeted change

The multi-repository path delegates its single `HttpClient.send` call to an independently written helper. After `HttpConnectTimeoutException`, the helper remembers that requested origin (normalized scheme, host and effective port) for the current JVM. Later artifacts skip it and continue through the existing alternatives. Every fresh JVM retries all origins.

The helper does not remember HTTP errors, request timeouts, generic I/O errors or interruptions. It changes no files or settings and logs no arguments, properties, credentials or URLs. The 15-second connection deadline and 180-second request deadline remain. Cache ZIP checks, content acceptance and all other original Genesis members remain unchanged. The direct single-URL retry method is byte-identical to 0.1.5. Reachable repositories keep their order. All four other patch definitions and the launcher executable, game JAR and in-game UI bytes are unchanged from 0.1.5; the launcher ASAR only receives the new strict bootstrap result hash.

## Controlled before/after test

`BatchFallbackTest.java` runs the actual Maven downloader from each JAR in a separate JVM with a fresh scratch cache. Its first repository is a TEST-NET endpoint that must silently time out in a three-second preflight; the alternate is a loopback server returning a valid fixture ZIP. It downloads three different coordinates and validates each archive. Only the isolated process's repository instances change; no OS routing or live-cache edits are used.

| Bootstrap | Artifact 1 | Artifact 2 | Artifact 3 | Total | Valid archives / fallback hits |
| --- | ---: | ---: | ---: | ---: | --- |
| Previous 0.1.5 | 15,202 ms | 15,004 ms | 15,004 ms | 45,210 ms | 3 / 3 |
| Candidate 0.1.6 | 15,197 ms | 4 ms | 3 ms | 15,204 ms | 3 / 3 |

This reproduces the failure mechanism and demonstrates that the targeted change removes repeated waits. Successful menu launches alone would not establish this causal result.

## Automated compatibility and restoration

- The helper's fake-client fixture passed 15 checks on each of Java 17, 21 and 25 with `-Xverify:all`: origin normalization and independence, non-connection failures, HTTP 404/503 and response/body preservation.
- The actual modified downloader passed 14 assertions on each of those three runtimes: deadlines, 503 fallback, 404 skipping, rejected-content fallback, exhaustion and valid-cache network bypass.
- Both full-sized packs passed preview, deterministic output, injected partial-write rollback, direct 0.1.5 upgrade, updater/startup/promotion removal and reselection, cosmetic removal and byte-exact five-file restoration. Promotion-only selection changed/restored exactly one file.
- Both strict launcher bootstrap verifier probes passed original-expected/new-result acceptance, changed/upstream hash rejection, cosmetics compatibility and ordinary/missing-file handling. Modified launcher JavaScript passed syntax checks.
- JAR CRCs passed. Original constant-pool entries, branch offsets, exception tables and stack frames are retained. Only the existing HTTP initializer and multi-repository method attributes differ from the original HTTP class; all original Genesis members outside that class are byte-identical. Two independently written helper classes are added.

The original Genesis SHA-256 is `40797ad1d1449ffff7c6e7342b79a3edc8d08517505dc2d57ce9d97e0669389c`; its SHA-1 is `ea2f19ae5fcb3941df77637b184a1ff02157d6b1`. The deterministic current result is SHA-256 `ec31b0ec46acce89e3b5b73ce678262595865b92494b4872f6ae8fd2e59b864f`, SHA-1 `12ec07a090a9d071363d1884a958682cd00c7d91`. The launcher exception requires both the original upstream expected hash and this known result. Arbitrary modified bootstraps and new upstream originals are not accepted.

## Live launch matrix

The user authorized Codex to take over testing. All five options were applied through the normal Patchwork 0.9.2 transaction on launcher 3.7.24-ow. Temporary diagnostic JVM arguments were restored to the original NULL before these launches. Times are approximate UI observations from launch to a confirmed main menu. No world or multiplayer server was entered.

| Test | Result |
| --- | --- |
| 26.3 / Fabric, 21 declared dependencies absent before first launch | Main menu at about 86 s; Building Cache was visible at 39 s |
| 26.3 / Fabric, repeat 1 | Main menu at about 58 s |
| 26.3 / Fabric, repeat 2 | Main menu at about 57 s |
| 26.3 / Fabric, cancel during Starting JVM | Starting JVM observed at 13 s; cancelled at about 18.6 s; card reset and 0 running instances confirmed |
| 26.3 / Fabric, retry after cancellation | Main menu at about 54 s |
| 1.8.9 / Lunar | Main menu at about 46 s |
| 1.17.1 / Lunar | Main menu at about 74 s |
| 1.21.11 (selected loader's UI name not confirmed) | Main menu at about 69 s |

All seven menu launches passed. Private menu screenshots for the three older versions were reviewed. The cancellation/reset state was captured in computer-use tool evidence, but was not saved as a separate image. A final read-only check matched all five live target files to the validated candidate, confirmed all four profiles' original NULL JVM settings, and found all 21 previously absent archives present with valid ZIP contents. No Lunar Java game process remained. Lunar Home was left open with 26.3 selected and zero running instances.

## Limits and delivery

The underlying Fabric connection/route failure remains unknown. This patch avoids repeated connection waits; it does not repair that route, eliminate normal cache-building time or resolve every Starting JVM hang. A skipped origin that recovers during the same launch is retried only in a fresh JVM. A dependency unavailable from the remaining repositories can still fail. The first 26.3 launch used a partly populated real cache, not a completely empty installation. Earlier original-bootstrap controls and 0.1.4 validation are retained in `STARTUP-VALIDATION.md`.

Launcher 3.7.17 was tested on copies only. Multiplayer cosmetic privacy, world movement and every promotional UI surface remain unverified. The user reported that 0.1.5's in-game cleanup worked; the precise coverage of that report was not separately measured here.

Release assets contain both actual importable JSON files, instructions, this report and the complete committed patch source. Private originals, decompiled upstream code/configuration, raw logs, thread captures, screenshots, profile backups, launch arguments and account files are excluded. Build and fixture instructions are in `source/lunar/BUILD.md`. Restore before future Lunar/game updates and retain Patchwork 0.9.2 or newer for the large UI-file backup.
