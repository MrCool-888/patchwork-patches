# Authoring and releasing patch packs

Patch packs are separate JSON release assets. They are not bundled with Patchwork and are not automatically applied when downloaded. Keep every pack tied to exact original SHA-256 fingerprints, preview it against an isolated copy of the supported build, and include reproducible source and validation instructions.

The complete beginner workflow, schema outline, field limits, source publishing rules, and 0.10.0 operation reference are in [Patchwork's authoring guide](https://github.com/MrCool-888/patchwork/blob/main/docs/AUTHORING.md) and [patch format](https://github.com/MrCool-888/patchwork/blob/main/PATCH-FORMAT.md). New file, ZIP-member and ASAR payload operations require `minimumPatcherVersion: "0.10.0"`.

## Release checklist

1. Pin the exact app version/build and hash the original version marker and every edited file/member.
2. Keep stable lowercase bundle and patch IDs. Raise the three-part `packVersion` for changed contents.
3. Declare patch dependencies, conflicts, target-location hints and process hints when needed.
4. Test preview, apply, selection changes, pack updates and byte-exact restore on disposable copies with the production Patchwork build.
5. Keep the recipe generator, source code and smoke commands in this repository. Synthetic fixtures demonstrate format behavior only; they do not establish compatibility with a real application.
6. Attach the `.json`/`.patchwork` file as a GitHub release asset with a stable filename and GitHub digest metadata. Patchwork ignores repository JSON and source ZIPs. Keep each published asset below 1 MiB; one source accepts at most 20 distinct assets.
7. For prereleases, state the required Patchwork version, supported original build, known limits, source revision and restore instructions in the release notes.

See [examples](../examples/README.md) for runnable isolated fixtures covering config/resources, managed IL, Electron ASAR, Java ZIP and synthetic raw binary changes. They are demonstrations, not official app patch packs.
