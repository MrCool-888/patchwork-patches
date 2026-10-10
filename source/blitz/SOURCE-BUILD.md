# Blitz pack source and checks

The generator, hook and fixtures are in this folder. Patchwork 0.8.0 source and its build instructions are in [the patcher repository](https://github.com/MrCool-888/patchwork). Build its test executable with `source/build.ps1 -Console -Tests`; all 44 scenarios should pass. The installer contains neither app's patch pack.

## Generate the pack

Use Node.js 20 or later. No npm dependency is required for generation. From the patch repository root:

```powershell
node .\source\blitz\build-pack.cjs 'C:\Path\To\Original\Blitz\resources\app.asar' .\Blitz-3.0.7.patchwork.json
```

The generator rejects any archive other than the pinned 3.0.7.134 original. Its output is deterministic for those original bytes and the included `desktop-cleanup.js`. No app archive or executable is distributed here.

## Transaction and independent checks

Compile `ValidatePack.cs` with the Windows Framework compiler, referencing an absolute path to the Patchwork 0.8.0 test build. Put the runner beside Patchwork.exe and Mono.Cecil.dll. Run it with the pack path, original archive path and a fresh workspace scratch directory:

```powershell
& 'C:\Path\To\Patchwork\build-tests\ValidatePack.exe' .\Blitz-3.0.7.patchwork.json 'C:\Path\To\Original\app.asar' .\fresh-validation
```

The runner copies the archive into the scratch directory, applies only to that copy, checks selection updates, rollback, recovery, workers and exact restore, and verifies the supplied original remains unchanged. Its `artifacts` folder contains `both.asar`, `clean.asar` and `compact.asar` for independent inspection.

`test-runtime.cjs` checks those archives, hook lifecycle and browser layout. It needs Playwright and an installed Chromium browser. Set `PATCHWORK_PLAYWRIGHT_MODULE` to a Playwright module path if it is not on Node's module path; set `PATCHWORK_CHROMIUM_EXECUTABLE` to an existing headless Chromium executable if needed.

```powershell
node .\source\blitz\test-runtime.cjs .\fresh-validation\artifacts 'C:\Path\To\Original\app.asar'
```

## Native Electron fixture

Use an isolated copy of the Electron 31.2.1 runtime matching Blitz. Copy its runtime files and locales to a fresh workspace directory. Do not use the installed application directory or Blitz's original app resources. Create `resources/app/package.json` with `{"name":"patchwork-blitz-hook-fixture","version":"1.0.0","main":"test-electron.cjs"}`, and copy `test-electron.cjs` and `desktop-cleanup.js` into that fixture app folder.

Generate an ephemeral local HTTPS certificate with Python and `cryptography`:

```powershell
python .\source\blitz\generate-fixture-cert.py .\isolated-runtime\resources\app
```

Launch the isolated runtime with `--result=ABSOLUTE_WORKSPACE_RESULT_PATH`. A hidden PowerShell `Start-Process -WindowStyle Hidden` launch is suitable. Validation used `--disable-gpu --disable-software-rasterizer`, with the normal Electron sandbox enabled. Loopback access is required; a network-restricted process sandbox may need a local-only exception.

The fixture serves local HTTPS pages, trusts only its generated certificate in a private test partition, seeds memory/disk caches in an unrelated live view, and checks native request cancellation, CSS, startup, reload, navigation, recreation, fallback and minimum window sizing. It uses a private workspace profile and self-exits within 60 seconds. No public ad/auth/update service or Blitz account is used. Generated keys/certificates are not shipped. This test-only certificate handling is absent from the delivered hook.

See [Blitz validation](../../docs/blitz/VALIDATION.md) for evidence and remaining live-app checks. The successful native result is stored at `docs/blitz/native-electron.json`.
