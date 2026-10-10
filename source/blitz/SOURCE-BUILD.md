# Blitz pack source and checks

The generator, hook and fixtures are in this folder. Patchwork 0.8.2 source and its build instructions are in [the patcher repository](https://github.com/MrCool-888/patchwork). Build its test executable with `source/build.ps1 -Console -Tests`; all 46 scenarios should pass. The installer contains neither app's patch pack.

## Generate the pack

Use Node.js 20 or later. No npm dependency is required for generation. From the patch repository root:

```powershell
node .\source\blitz\build-pack.cjs 'C:\Path\To\Original\Blitz\resources\app.asar' .\Blitz-3.0.7.patchwork.json 'C:\Path\To\Original\Blitz\icudtl.dat'
```

The generator rejects archive or companion bytes other than the pinned 3.0.7.134 originals. Its output is deterministic for those bytes and the included `desktop-cleanup.js`. The optional final argument defaults to `icudtl.dat` beside the parent of the archive's `resources` directory. No app archive, companion or executable is distributed here.

## Transaction and independent checks

Compile `ValidatePack.cs` with the Windows Framework compiler, referencing an absolute path to the Patchwork 0.8.2 test build. Put the runner beside Patchwork.exe and Mono.Cecil.dll. Run it with the pack path, original archive path, a fresh workspace scratch directory and original companion path:

```powershell
& 'C:\Path\To\Patchwork\build-tests\ValidatePack.exe' .\Blitz-3.0.7.patchwork.json 'C:\Path\To\Original\app.asar' .\fresh-validation 'C:\Path\To\Original\icudtl.dat'
```

The runner copies both originals into the scratch directory, applies only to those copies, checks selection updates, two-file rollback, recovery, workers and exact restore, and verifies the supplied originals remain unchanged. Its `artifacts` folder contains `both.asar`, `clean.asar`, `compact.asar` and matching `.icudtl.dat` files for independent inspection.

`test-runtime.cjs` checks those archives, hook lifecycle and browser layout. It needs Playwright and an installed Chromium browser. Set `PATCHWORK_PLAYWRIGHT_MODULE` to a Playwright module path if it is not on Node's module path; set `PATCHWORK_CHROMIUM_EXECUTABLE` to an existing headless Chromium executable if needed.

```powershell
node .\source\blitz\test-runtime.cjs .\fresh-validation\artifacts 'C:\Path\To\Original\app.asar'
node .\source\blitz\test-updates.cjs .\fresh-validation\artifacts\both.asar
```

`test-updates.cjs` executes the changed update-policy functions with stubbed dependencies to check default automatic suppression, manual checks and explicit opt-in. Verify the companion footer independently with an XXH32 seed-zero implementation; only its last four bytes should differ.

## Native Electron hook fixture

This fixture tests the unchanged desktop hook, not the complete native Blitz bootstrap or companion checksum. Passing it does not establish live startup success.

Use an isolated copy of the Electron 31.2.1 runtime matching Blitz. Copy its runtime files and locales to a fresh workspace directory. Do not use the installed application directory or Blitz's original app resources. Create `resources/app/package.json` with `{"name":"patchwork-blitz-hook-fixture","version":"1.0.0","main":"test-electron.cjs"}`, and copy `test-electron.cjs` and `desktop-cleanup.js` into that fixture app folder.

Generate an ephemeral local HTTPS certificate with Python and `cryptography`:

```powershell
python .\source\blitz\generate-fixture-cert.py .\isolated-runtime\resources\app
```

Launch the isolated runtime with `--result=ABSOLUTE_WORKSPACE_RESULT_PATH`. A hidden PowerShell `Start-Process -WindowStyle Hidden` launch is suitable. Validation used `--disable-gpu --disable-software-rasterizer`, with the normal Electron sandbox enabled. Loopback access is required; a network-restricted process sandbox may need a local-only exception.

The fixture serves local HTTPS pages, trusts only its generated certificate in a private test partition, seeds memory/disk caches in an unrelated live view, and checks native request cancellation, CSS, startup, reload, navigation, recreation, fallback and minimum window sizing. It uses a private workspace profile and self-exits within 60 seconds. No public ad/auth/update service or Blitz account is used. Generated keys/certificates are not shipped. This test-only certificate handling is absent from the delivered hook.

See [Blitz validation](../../docs/blitz/VALIDATION.md) for evidence and remaining live-app checks. The successful native result is stored at `docs/blitz/native-electron.json`.
