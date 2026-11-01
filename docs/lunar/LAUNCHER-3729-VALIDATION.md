# Lunar Client 3.7.29-ow patch source

Pack 0.1.7 adapts the 0.1.6 launcher operations to the exact 3.7.29-ow launcher inspected on 2026-11-01. It preserves the previous game, bootstrap and in-game UI operations. It requires the exact fingerprints in the JSON; a future Lunar update needs a new inspection.

To rebuild with Python 3, provide an unpatched copy of the matching launcher directory and the included 0.1.6 pack:

```powershell
python build-3729.py --old-pack LunarClient-3.7.24-ow-0.1.6.patchwork.json --launcher-dir "$env:LOCALAPPDATA\Programs\Lunar Client" --output LunarClient-3.7.29-ow.patchwork.json
```

The builder checks both launcher file hashes and the input pack hash and requires every replacement to occur exactly once. It includes no Lunar executable or original application assets. The two Node.js probes exercise extracted original and patched launcher main JavaScript in isolated fixtures; supply the version, original main JS path and patched main JS path to either probe.

Verification: Patchwork 1.1.1's production parser and preview accepted all five options against copies. The full transaction fixture passed deterministic preview, interrupted-write rollback, option removal/reselection and exact restore. The resulting game, bootstrap and UI hashes match the previously validated 0.1.6 outputs; the actual launcher verifier and updater methods passed isolated Node fixtures; all four modified launcher JavaScript files passed syntax checks. No patched live launcher, visual layout, game session, multiplayer traffic, or real update installation was tested for this version.

The launcher patch changes an Electron integrity fuse and invalidates the executable signature. Local cosmetics' multiplayer privacy remains unverified. Restore patches through Patchwork before updating Lunar, and do not force a restore if files have changed externally.
