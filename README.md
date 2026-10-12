# Patchwork patches

Patch packs for [Patchwork](https://github.com/MrCool-888/patchwork), a tool for applying and restoring patches to supported apps. Patch packs are separate from the Patchwork installer.

Project-authored source, documentation, and patch definitions are licensed under [GPL-2.0-only](LICENSE). Third-party material remains under its applicable terms.

## Available patches

This repository includes packs for:

- **Lunar Client** — launcher cleanup, local cosmetics, startup improvements, and promotion removal. See the [Lunar guide](docs/lunar/README.md) for supported builds and limitations.
- **Blitz** — desktop cleanup and compact window options. See the [Blitz guide](docs/blitz/README.md) for supported versions and restore instructions.
- **Proton VPN** — optional interface and client behavior patches. See [validation notes](VALIDATION.md) and [authentication research](AUTH-RESEARCH.md) for details and known limitations.

Compatibility depends on the exact app files supported by each pack. Check its documentation before applying. Packs do not grant access to paid services or features.

## Install

1. Install [Patchwork](https://github.com/MrCool-888/patchwork).
2. In Patchwork, add `https://github.com/MrCool-888/patchwork-patches` under **Patch sources**.
3. Select a compatible pack, review the preview, then apply it with the target app closed.

You can also download a `.patchwork.json` file from a release and import it with **Add patch file**. Use Patchwork's History to restore a pack.

## Examples

The [synthetic examples](examples/README.md) demonstrate patch authoring and update/restore behavior. They are fixtures, not compatibility packs for real apps. See the [authoring guide](docs/AUTHORING.md) to create a pack.

## Disclaimer

This project is not affiliated with, endorsed by, or supported by any app or its developers whose software has patches here. The project and its software are provided “as is,” without warranties, and without support.
