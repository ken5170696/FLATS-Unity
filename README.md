# FLATS — Unity project

A maintained Unity reconstruction of FLATS, the flat-colour first-person shooter originally developed and published by **Foliage Games LLC**. This independent project preserves offline play, tutorial, training, combat modes, Photon multiplayer, controller input and the module system. It is not an official Foliage Games release.

## Current player releases

The [5.4.3 player release](https://github.com/ken5170696/flats-downloads/releases/tag/v5.4.3) is built from the [v5.4.3 source tag](https://github.com/ken5170696/FLATS-Unity/releases/tag/v5.4.3). The [live release manifest](https://flats-site.tail2511fc.ts.net/data/releases.json) identifies each platform's exact source, package and validation scope. Windows, Linux, macOS and Android packages are available; iOS export is not an installable IPA. Source changes after the tag do not update player downloads until another release is published.

The [Mod SDK preview](docs/MOD_SDK.md) documents the API 1.1.0 contracts and data packages (a crosshair preset and an enemy tuning module) that you can author without modifying game code.

## Open and play

1. Clone this repository and add its root to Unity Hub. Git LFS and submodules are not required.
2. Install **Unity 6000.3.24f1**, revision `4e7b9b5b6244`.
3. Allow Package Manager restoration and asset import to finish. Offline development requires no private repository, API key or external tooling.
4. Open `Assets/_Flats/Scenes/MainMenu.unity` and press **Play**.

```sh
git clone https://github.com/ken5170696/FLATS-Unity.git
```

On first launch, acknowledge the welcome/update panel with **OK**, then select **Play > Singleplayer**. Training and Tutorial are available there; Survival selects a map. The `-flats-tutorial` launch switch enters Tutorial after normal initialization.

Controls: WASD movement, mouse look, left mouse fire, right mouse aim, Space jump, R reload, E change weapon, Q pick up, Escape pause.

This branch migrates rendering to **Universal Render Pipeline 17.3.0**, retaining Gamma colour space and the legacy Input Manager with InControl. Use the FLATS pipeline and renderer in `Assets/_Flats/Settings/Rendering`; every quality level must reference that pipeline. Preserve `Packages/manifest.json` and `packages-lock.json` together. See [rendering](docs/RENDERING.md) for architecture and migration limitations.

## Build

Install the matching platform support module in Unity Hub. Windows x64 uses Mono; Android also needs the matching SDK, NDK and OpenJDK. iOS export requires macOS/Xcode for compilation and device deployment.

Close the Editor using this project, then run from the repository root:

```powershell
$unity = '<Unity installation>/6000.3.24f1/Editor/Unity.exe'
python tools/check_source.py
./tools/unity.ps1 -UnityEditor $unity -Task Windows
```

Output: `Builds/Portal/Windows/FLATS.exe`. Keep the complete folder, including `FLATS_Data`, `UnityPlayer.dll` and the Mono runtime. Other tasks are `Import`, `Web`, `Linux`, `Mac`, `Android` and `IOS`. Local build products and logs are ignored by Git. Build availability does not establish full gameplay support on every platform.

Install Python 3 (3.12 is used in CI) and make `python` available on PATH. The command runs source integrity before starting Unity, including new untracked assets. Save Scene and Prefab Mode changes explicitly before building from the Editor; the build refuses unsaved scene/prefab state. A dirty Git working tree is allowed for local candidates: `source-snapshot.json` records each source hash, and `build-provenance.json` distinguishes that snapshot from its base commit. `packed-assets.json` lists packed source assets by size for investigating build contents; it is not a runtime-memory or compressed-download measurement. Detailed build reporting adds diagnostic build overhead. None of these commands publishes a release.

## Project layout

| Area | Location |
|---|---|
| Scenes | `Assets/_Flats/Scenes` |
| Game code | `Assets/_Flats/Runtime` |
| Shared interface | `Assets/_Flats/Prefabs/UI` |
| Build and navigation authoring tools | `Assets/_Flats/Editor` |
| Game assets | `Assets/_Flats/Art`, `Assets/Resources` |
| Third-party integrations | `Assets/ThirdParty`, `Assets/Plugins` |

Preserve `.meta` files and GUIDs, serialized field names, resource paths, UnityEvent methods and Photon RPC names. Edit shared UI prefabs instead of changing every scene independently. See [development contracts](docs/DEVELOPMENT.md) for persistence, navigation and service configuration, and [asset authoring and import](docs/ASSET_WORKFLOW.md) before changing game assets.

## Services and troubleshooting

- **Photon:** set `FLATS_PHOTON_APP_ID` to your own Photon PUN client App ID before launching Unity, or use `photon-app-id.txt` in `Application.persistentDataPath`. Offline play works without it.
- **Module catalogue:** set `FLATS_MOD_CATALOGUE_URL` to a compatible public HTTPS catalogue before launching the Portal build command. The build bundles this URL for players, including WebGL, and removes the temporary configuration asset afterwards. Players do not need an environment variable for a configured build. Desktop runtime overrides remain supported. Local modules remain available without a catalogue. Custom Crosshair is downloaded from Explore, not preinstalled. WebGL supports verified crosshair package downloads, installation, profiles, enable/disable and removal; code packages require desktop FLATS. The catalogue must allow credential-free CORS requests from the hosting page.
- **Compiler/import errors:** verify the Unity version and registry access, then inspect the Console. Do not copy another checkout's Library folder.
- **Pink materials:** confirm the FLATS URP pipeline/renderer references, Gamma settings and shader import completion; inspect shader compiler errors before replacing materials.
- **Save data:** keep normal player profiles out of Git. Corrupt or newer-format records are preserved with a recovery notice.

## Attribution and terms

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Public source availability does not grant a blanket open-source licence to the original game, bundled assets or vendor code. Existing component terms and attribution remain applicable.
