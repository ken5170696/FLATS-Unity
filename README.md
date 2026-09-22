# FLATS — Unity project

A maintained Unity reconstruction of FLATS, the flat-colour first-person shooter originally developed and published by **Foliage Games LLC**. This independent project preserves offline play, tutorial, training, combat modes, Photon multiplayer, controller input and the module system. It is not an official Foliage Games release.

## Open and play

1. Clone this repository and open its root (the folder containing `Assets`, `Packages` and `ProjectSettings`) in Unity Hub. Git LFS and submodules are not required.
2. Install **Unity 6000.3.24f1**, revision `4e7b9b5b6244`. Windows x64 uses the Mono backend supplied with the Windows editor. Install the matching platform support module before building another target; Android also needs the Hub-provided SDK, NDK and OpenJDK. iOS export requires a Mac with Xcode for compilation and device deployment.
3. Allow Package Manager restoration and the initial asset import to finish. This needs network access to the public Unity registry. No AI tools, private files, external repository or API key is needed for offline development.
4. Open `Assets/_Flats/Scenes/MainMenu.unity` and press **Play**. A new local profile receives defaults. Use the main menu to enter singleplayer or training. Stop Play Mode before changing scenes or running validation.

```sh
git clone https://github.com/ken5170696/FLATS-Unity.git
cd FLATS-Unity
```

On the first Player launch, acknowledge the original welcome/update panel with **OK**, then select **Play > Singleplayer**. Training and Tutorial are available there; Survival selects a game map.

Rendering uses the **Built-in Render Pipeline**, Gamma colour space and the **legacy Input Manager** with InControl. Keep the pipeline asset unset. URP, Shader Graph and VFX packages remain dependencies of the existing package/shader compilation; their presence does not make this a URP project. Keep `manifest.json` and `packages-lock.json` together.

Controls: WASD movement, mouse look, left mouse fire, right mouse aim, Space jump, R reload, E change weapon, Q pick up, Escape pause. The standalone switch `-flats-tutorial` enters Tutorial once after normal initialization.

## Build and validate

Close the editor using this project before running batch commands. From the repository root in PowerShell, set the path to your installed editor:

```powershell
$unity = '<Unity installation>/6000.3.24f1/Editor/Unity.exe'
./tools/unity.ps1 -UnityEditor $unity -Task Import
./tools/unity.ps1 -UnityEditor $unity -Task Validate
./tools/unity.ps1 -UnityEditor $unity -Task PlayMode
./tools/unity.ps1 -UnityEditor $unity -Task Shaders
./tools/unity.ps1 -UnityEditor $unity -Task Sights
./tools/unity.ps1 -UnityEditor $unity -Task Navigation
./tools/unity.ps1 -UnityEditor $unity -Task NavigationPlay
./tools/unity.ps1 -UnityEditor $unity -Task Windows
./Builds/Portal/Windows/FLATS.exe
```

Editor equivalents: **Flats > Validation > Validate project and run regression tests** and **Flats Recovery > Portal > Build Windows**. The historical menu label is retained for compatibility. Validation checks all enabled build scenes and all prefabs for missing scripts/references, then runs module lifecycle, dependency, storage recovery and configuration regressions. Reports go to ignored `Logs/`; build reports go to `Builds/Portal/`. A nonzero exit or missing success marker fails the command.

Keep the entire `Builds/Portal/Windows` folder, including `FLATS_Data`, `UnityPlayer.dll` and Mono runtime. Other build tasks are `Web`, `Linux`, `Mac`, `Android`, `IOS`. Build availability is not proof of gameplay support: consult [the validation matrix](docs/VALIDATION.md).

`PlayMode` enters/exits MainMenu twice with a fresh isolated profile, checks initialization/console errors and verifies that shared UI material assets remain unchanged. `Shaders` runs GPU shader contracts; use a graphics-capable host. `Sights` checks all five scope cameras and the actual RawImage mesh UVs, including the legacy Rect serialization regression. `Navigation` checks all six maps; `NavigationPlay` traverses all 20 spawn routes on Warehouse and NightLand using the enemy prefab's agent settings. It tests native navigation, not combat decisions. `Audio` checks clip decoding and references.

For an isolated runtime test that does not change your normal profile:

```powershell
./Builds/Portal/Windows/FLATS.exe -screen-fullscreen 0 -flats-verify -flats-module-settings-dir "$PWD/Logs/profile-test" -flats-evidence-dir "$PWD/Logs/runtime-test" -logFile "$PWD/Logs/runtime-test.log"
```

Use a fresh directory for first-run tests; reuse it to test persistence. The verification probe is opt-in. A launch or a screenshot alone does not validate combat or multiplayer.

## Working on the game

| Area | Entry point |
| --- | --- |
| Playable scenes | `Assets/_Flats/Scenes` (MainMenu first in Build Settings) |
| Player | `Assets/_Flats/Runtime/Gameplay/FPSController.cs`; `Assets/Resources/Flatman.prefab` |
| Weapons and combat | `Runtime/Gameplay/Gun.cs`, `Bullet.cs`, `Character.cs`, `Runtime/Core/WeaponCatalog.cs` under `_Flats` |
| Shared UI | `_Flats/Prefabs/UI/GameInterface.prefab`; `Runtime/UI/Menu.cs` and `Settings.cs` |
| Profile and settings | `_Flats/Runtime/Platform/FlatsLocalProfile.cs`, `FlatsPreferences.cs`, `FlatsAtomicRecord.cs` |
| Multiplayer | `_Flats/Runtime/Networking`; `Assets/ThirdParty/Photon` and `ExitGames` |
| Modules | `_Flats/Runtime/Core/Modules`, `Runtime/Modules`, `Runtime/UI/ModuleManagementPage*` |
| Editor/build tools | `_Flats/Editor`, `Assets/Editor`, `tools` |
| Other dependencies | `Assets/ThirdParty`, `Assets/Plugins`, `Packages` |

`FPSPlayerControl` is a retained legacy example; the playable character uses `FPSController`. Edit UI prefabs instead of independently changing each scene instance. Preserve `.meta` files and GUIDs when moving assets. Preserve serialized field names, resource paths, UnityEvent/AnimationEvent method names and Photon RPC names; use a deliberate migration if a contract must change. Do not delete assets merely because a text search finds no reference.

For a weapon change, inspect the weapon prefab and its `Gun` component, make the smallest change, run validation, then test firing, reload, pickup and scene re-entry. For UI changes, test main menu, paused gameplay and returning from a match. For module changes, add lifecycle/failure tests alongside `FlatsModuleTests` and check profile persistence. See [architecture and services](docs/DEVELOPMENT.md).

## Services and troubleshooting

- **Photon:** set `FLATS_PHOTON_APP_ID` to your own Photon PUN client App ID before starting the editor/player, or place it in `photon-app-id.txt` in `Application.persistentDataPath`. Never commit dashboard credentials. Without configuration the online menu reports the problem; offline play remains available. Two-client connectivity is a separate acceptance test.
- **Module catalogue:** optionally set `FLATS_MOD_CATALOGUE_URL` to a compatible public HTTPS catalogue. The project has no private default host. Local modules and built-in crosshair settings remain available without a catalogue. The Editor also exposes development source configuration; see [services](docs/DEVELOPMENT.md).
- **Import/compiler failure:** verify the exact Unity version and registry access, then inspect the Console and `Logs`. Do not copy another checkout's `Library` as a fix.
- **Missing target:** install that target's module for this exact editor version in Hub.
- **Profile failure:** the game preserves corrupt/newer records and displays a notice instead of silently overwriting them. Test with the isolated launch command before touching normal saves.
- **Pink materials:** confirm Built-in/Gamma settings and that shader import completed; do not assign a URP asset as a workaround.

Normal Windows saves use the project's `FlatsRecovery/Flats Offline` Unity persistent-data location and legacy PlayerPrefs. Keep normal data out of Git. The public project is the ongoing Unity development location; the previous mixed workspace is an archive, not a second source of truth. Move future Unity changes through ordinary branches and reviewed commits here.

## Attribution and terms

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Public source availability does not grant a blanket open-source licence to the original game, bundled assets or vendor code. Existing component terms and attribution remain applicable. This repository does not claim independently verified third-party redistribution rights or apply MIT to the whole project.
