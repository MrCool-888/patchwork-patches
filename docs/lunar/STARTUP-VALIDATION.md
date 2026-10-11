# Lunar startup fallback validation — 2026-10-11

## Finding and targeted change

With the original game JAR and Genesis downloader, Minecraft 1.8.9 waited inside Java `HttpClient.send` in Lunar's Maven downloader at the 30-, 90- and 180-second snapshots. A TCP connection remained in `SYN_SENT` to port 443, and normal launcher cancellation ended the attempt. Original Minecraft 1.17.1 reproduced the same wait on launch and cancel/retry. These controls retained the clean launcher and update blocker, so they are original game/bootstrap controls rather than a completely untouched launcher. Java had started; the samples do not establish a RAM, graphics or JVM creation failure.

Lunar's own version configuration identified `org.slf4j:slf4j-api:2.0.11` as the first uncached dependency in both affected profiles. Minecraft 1.21.11 reached IPC and the main menu in about 46 seconds with 125 valid dependencies already cached, so that warm-cache run did not exercise this missing-dependency path.

At the time of the stalled run, probes recorded the first Fabric repository timing out, Mojang returning 404, Maven Central returning 200, and mirrors returning 403. The actual repository URI held by the live client was not captured. Connecting that evidence to the first repository is an inference from the declared artifact order and correlated probes; the underlying network-route cause is unknown. The successful `slf4j-api` download from a fresh isolated original was later 1,905 ms, and from the candidate was 1,318 ms, after the network recovered about seven hours later. A successful live startup after recovery does not demonstrate that the candidate fixed the live incident.

The optional **Faster bootstrap download fallback** patch shortens the shared Java `HttpClient` connect timeout from 180 seconds to 15 seconds. The request timeout remains 180 seconds. The original nested repository loops made nine failing attempts before reaching an alternate in their first round, and 45 before exhausting a single repository. The patch tries each of the six existing repositories once. Repository order, ZIP validation, cache acceptance and final failure reporting remain unchanged. The single-URL helper retains its original retries, with the shorter shared connection deadline. The patch permits earlier fallback when another repository can serve the dependency; it cannot repair the route or obtain a dependency missing from every repository. It also removes repeated chances to recover from transient errors across all repositories; cancelling and retrying a launch starts a fresh attempt.

The Genesis edit changes one HTTP helper class through ten short splices, totaling 24 inserted/replacement bytes. All other archive entries remain byte-identical. Patchwork requires original Genesis SHA-256 `40797ad1d1449ffff7c6e7342b79a3edc8d08517505dc2d57ce9d97e0669389c`. The separate launcher exception accepts only patched Genesis SHA-1 `106f52481af4a63ba559bb817e9ccea087f387f7` when the upstream manifest still expects original SHA-1 `ea2f19ae5fcb3941df77637b184a1ff02157d6b1`. Changed or newer bootstraps are rejected. No permanent agent is installed.

## Controlled regression

`source/lunar/StalledFallbackTest.java` and `source/lunar/test-stalled-fallback.py` provide an isolated A/B test. Before each mode, the Java harness requires a three-second TCP timeout to IANA TEST-NET-1 (`192.0.2.1:443`); if the endpoint connects or behaves differently, that run aborts. It then gives the production downloader the stalled endpoint first and a loopback server returning a tiny valid ZIP second. The parent process imposes a 34-second limit per mode.

The original must still be blocked in `HttpClient.send` at 30 seconds, with its caller waiting and zero fallback requests. The candidate must fail over to the loopback repository and complete once, within 25 seconds. An independent rerun confirmed the original at 30.007 seconds with the caller in `WAITING`, downloader/send frames present, `SYN_SENT`, and zero fallback requests; the candidate completed in 15.074 seconds with one fallback request. The final portable source runner with `-Xverify:all` passed again: original still waiting at 30.013 seconds, candidate complete at 15.101 seconds. These results validate the targeted downloader fallback under a reproduced connection stall; they do not establish the underlying cause of the live network incident or prove that all startup hangs are resolved.

The portable runner requires explicit local paths for the Java 17 runtime, compiler Java, ECJ jar, original jar, candidate jar and scratch directory. It compiles into scratch, tests the stalled TEST-NET connection with a loopback fallback, applies the per-process timeouts and writes a short JSON summary inside scratch. It does not patch, launch or write to Lunar's installed files or change firewall/routing settings. Use:

```powershell
python source/lunar/test-stalled-fallback.py `
  --java 'C:\path\to\java17\bin\java.exe' `
  --compiler-java 'C:\path\to\compiler-java\bin\java.exe' `
  --ecj 'C:\path\to\ecj.jar' `
  --original 'C:\path\to\original-genesis.jar' `
  --patched 'C:\path\to\candidate-genesis.jar' `
  --scratch 'C:\path\to\scratch'
```

## Other verification and live status

The broader candidate checks passed: Java 17/21/25 matrix across original, minimal candidate and Patchwork candidate (nine runs, 14 assertions each), including duration, HTTP 503 retry, 404, predicate rejection, exhaustion and valid-cache bypass. `-Xverify:all` passed. Both pack files for Lunar 3.7.17-ow and 3.7.24-ow passed deterministic four-file preview, partial-write rollback, direct 0.1.3-to-0.1.4 upgrade, updater deselection/reselection, cosmetics removal and exact restoration of all original files. The actual JavaScript bootstrap strict guards and updater checks passed. Only the Genesis HTTP class changes.

On 2026-10-11, launcher **3.7.24-ow** with all four patches reached the Minecraft 1.17.1 main menu; baking completed at 09:02:32 CDT after Java started at 09:01:34 CDT, and the launcher received IPC at 09:02:55.448. Visual inspection and the user confirmed that run. The previously missing SLF4J archive was created at 09:01:37 and subsequently passed ZIP CRC validation. A repeat 1.17.1 launch reached the menu, with IPC at 09:15:05.122. Minecraft 1.8.9 reached its main menu on three launches, with IPC at 09:08:55.160, 09:10:25.432 and 09:11:33.129. Minecraft 1.21.11 reached the menu twice, with IPC at 09:17:11.803 and 09:19:36.050. Each game was closed normally.

One repeated 1.21.11 launch was cancelled while the launcher displayed **Updating**: its card eventually returned to **Launch Game**, no Java game window remained, and a subsequent retry reached the main menu. Earlier cancel clicks raced into a completed launch and were not counted as cancellations. This checks the shared cancellation/retry lifecycle; an interrupted download under the earlier real network fault was not tested with the patch because the route had recovered.

| Version | Original game/bootstrap control | Patched compatibility |
| --- | --- | --- |
| 1.8.9 | HTTP wait at 30/90/180 seconds; normal cancellation | Three main-menu launches |
| 1.17.1 | Same HTTP wait; cancellation/retry reproduced it | Two main-menu launches |
| 1.21.11 | Warm-cache main-menu launch, about 46 seconds | Two main-menu launches; cancellation then successful retry |

Lunar and Minecraft were closed before restoring the original JVM argument values. All three profiles were verified to have their exact original `NULL` values; pipeline timing and the temporary diagnostic agent are no longer configured. After restarting Lunar, a final 1.21.11 launch without diagnostics rebuilt its cache and reached the main menu, with IPC at 09:25:38.751, then exited normally. Private snapshot evidence is retained locally, with no profile identifiers, launch arguments, account data or raw logs in this release. No world or multiplayer server was entered. Launcher 3.7.17-ow was tested on copies only.

## References

- [Java thread monitoring API](https://docs.oracle.com/en/java/javase/17/docs/api/java.management/java/lang/management/ThreadMXBean.html)
- [Java HTTP connection timeout API](https://docs.oracle.com/en/java/javase/17/docs/api/java.net.http/java/net/http/HttpClient.Builder.html)
