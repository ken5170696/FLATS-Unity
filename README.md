# FLATS — Unity project

A maintained Unity reconstruction of FLATS, the flat-colour first-person shooter originally developed and published by **Foliage Games LLC**. This independent project preserves offline play, tutorial, training, combat modes, Photon multiplayer, controller input and the module system. It is not an official Foliage Games release.

## Current player releases

The [Windows 5.3.5-parity test ZIP](https://github.com/ken5170696/flats-downloads/releases/tag/v5.3.5-parity.20260923) was built from `36d97d2f73fc35e32d1cbc05e5e94c3a307bf979`. The extracted ZIP passed startup only; gameplay input on that ZIP, multiplayer and full platform acceptance have not been verified. It is unsigned. The [Web single-player preview](https://flats-site.tail2511fc.ts.net/play) uses the older `2fa10e6f6acbdd48794c3dd6305ad8606488ec07` source with bounded public-browser gameplay checks. The newer Web candidate timed out during public loading, so the older preview remains deployed. The [live release manifest](https://flats-site.tail2511fc.ts.net/data/releases.json) identifies each artifact's actual source and validation scope. This repository's `main` is the Built-in pipeline source baseline; development branches may contain unaccepted changes and are not player releases.

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

The project uses the **Built-in Render Pipeline**, Gamma colour space and the legacy Input Manager with InControl. Keep the render pipeline asset unset. Existing package dependencies do not make this a URP project; preserve `Packages/manifest.json` and `packages-lock.json` together.

## Build

Install the matching platform support module in Unity Hub. Windows x64 uses Mono; Android also needs the matching SDK, NDK and OpenJDK. iOS export requires macOS/Xcode for compilation and device deployment.

Close the Editor using this project, then run from the repository root:

```powershell
$unity = '<Unity installation>/6000.3.24f1/Editor/Unity.exe'
./tools/unity.ps1 -UnityEditor $unity -Task Windows
```

Output: `Builds/Portal/Windows/FLATS.exe`. Keep the complete folder, including `FLATS_Data`, `UnityPlayer.dll` and the Mono runtime. Other tasks are `Import`, `Web`, `Linux`, `Mac`, `Android` and `IOS`. Local build products and logs are ignored by Git. Build availability does not establish full gameplay support on every platform.

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
- **Module catalogue:** optionally set `FLATS_MOD_CATALOGUE_URL` to a compatible public HTTPS catalogue. Local modules remain available without one.
- **Compiler/import errors:** verify the Unity version and registry access, then inspect the Console. Do not copy another checkout's Library folder.
- **Pink materials:** confirm Built-in/Gamma settings and shader import completion.
- **Save data:** keep normal player profiles out of Git. Corrupt or newer-format records are preserved with a recovery notice.

## Attribution and terms

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Public source availability does not grant a blanket open-source licence to the original game, bundled assets or vendor code. Existing component terms and attribution remain applicable.
