# Project map

Open **FLATS → Project Overview** in Unity for scene and prefab shortcuts. Use Unity 6000.3.24f1. Start Play Mode from MainMenu for menu and profile workflows; an isolated map is not the full application startup path.

## Where to make a change

| Intent | Authoring surface | Behavior |
| --- | --- | --- |
| Sound slider appearance | SettingsScreen prefab → Sound | Menu.Volume binds values and saves settings |
| Keyboard/controller binding layout | SettingsScreen prefab → Control | Menu.Controls handles capture and conflicts |
| Module page fixed controls and dialogs | ModulesScreen prefab | ModuleManagementPage binds its serialized view references to services |
| Shared HUD and cameras | GameInterface and GameplayHUD prefabs | Menu composes game state; player presenters update HUD state |
| Shared weapon balance | Runtime/Core/WeaponCatalog.cs | Immutable defaults feed GunInfo at startup; Gun holds per-instance ammunition and copied stats |
| Map geometry, lights, spawn points | Individual map scene | Map and gameplay components |
| Runtime rendering | URP assets and camera prefabs | See RENDERING.md before altering stacks or effects |
| Imported models, textures, audio | Existing Art and Audio folders | Import settings and original source attribution |
| Build configuration | ProjectSettings and tools/unity.ps1 | Normal platform build commands |

The fixed module view is stored in `ModulesScreen.prefab`. Dynamic items instantiate `ModuleListRow`, `ModuleDownloadRow`, `ModuleCatalogueCard`, `ModuleProfileCard`, `ModuleFilterButton`, `ModuleLoadingCard` and `ModuleEmptyState` templates. Edit those templates for item typography and spacing. The page component owns the exposed responsive margins and split breakpoint; LayoutGroups own their child spacing. Consult those owners before changing a driven child RectTransform.

Weapon balance currently has a code authoring entry, not an Inspector definition asset. For example, change the `reloadTime` argument of the intended `WeaponDefinition` in `WeaponCatalog`, then enter Training through MainMenu, fire and reload that weapon. `Gun.Start` copies defaults through `GunInfo`; changing the corresponding prefab's `Gun.reloadTime` is overwritten at startup. Runtime `currentAmmo` is an instance value, while the catalog's stable index is also a save/network contract. Do not reorder definitions to rearrange the menu.

For aiming, `FPSController` keeps the world/projectile camera on the player camera rig and moves only Gun Camera toward the weapon's authored sight anchor. `FlatsSightTarget` aligns the lens camera to the same world aim while retaining the lens image roll and authored magnification. Script import execution order is intentional: IKController (0) poses the chest and camera rig, FPSController (50) positions the weapon camera, FlatsSightTarget (75) positions the lens, and Bullet (100) records the visible tracer. Preserve this order when editing Script Execution Order. Check a fixed target before/after aiming, firing, reloading, switching weapons and returning to MainMenu, including looking up and down. Do not reparent the world camera under a weapon or compensate a ray mismatch by moving the HUD crosshair.

`FPSController.GetBulletTrailOrigin` maps the locally rendered muzzle into the world camera for tracer and muzzle-flash presentation. `Bullet.LateUpdate` captures this origin after the weapon camera pose is updated, then keeps the trail fixed in world space. Remote players retain their world-space muzzle; projectile velocity, collision and damage do not use this visual mapping. Verify visible muzzle/tracer alignment as well as world-camera stability when editing ADS.

The player Prefab's `LODGroup` owns every body mesh renderer. `FPSController.SetBodyRenderLayer` keeps those renderers on the local weapon-view layer together, including after team synchronization, and restores their team layer for existing carrying states. When adding a body LOD, register its renderer in that group. Preserve the separate team layers on colliders and hit targets; camera visibility must not change their collision or damage filtering.

For example, change `ModulesScreen` → `SettingsControls/StyleChoices` HorizontalLayoutGroup spacing to adjust the three crosshair choices. Save the prefab, start from MainMenu, open an installed crosshair package's configuration and check all choices at narrow and wide sizes. `ConfirmationDialog.prefab` owns common message padding/action spacing. `MainMenuScreen` → `MenuTileArtwork` owns per-illustration framing; raw Image size can be driven by that component.

Before a local build, save your scene and Prefab Mode work. The build command rejects unsaved authoring state without saving it for you. Resolve a rejected build at the named scene/prefab, then retry; do not discard work just to make the build pass. Build contents come from `ProjectSettings/EditorBuildSettings.asset` (MainMenu first), and platform settings remain in ProjectSettings. Source and packed-asset reports accompany the built Player.

## Hierarchy and references

`RoomScreen/MatchingDetails/PlayerList` owns the waiting-room list's top alignment, padding and spacing through its VerticalLayoutGroup. `PlayerButton` owns each row's preferred height. `RoomPlayerListLayout` schedules one layout refresh after the animated screen becomes active, including when rows were added while hidden; it does not override these authored values.

Group by responsibility, not by Unity component type: a screen owns its heading, navigation, content and modal presentation. A reusable prefab owns a coherent editable feature. Avoid extracting every decorative rectangle into its own prefab.

Inspector references express ownership. Keep runtime services and transient selection/cancellation state out of serialized data. Use a prefab's own references for its internal controls and bind external services at the composition boundary. Repeated data should instantiate an authored row/card template, then bind values and callbacks.

Legacy Menu code and animation clips still depend on child order and paths such as `Settings/Control`. Reparenting these objects requires migrating both code references and animation bindings in the same change. Renaming or moving folders alone does not resolve those dependencies. Preserve asset GUIDs and serialized UnityEvents.

## A change is reviewable when

1. The intended scene or prefab can be opened and its content understood before Play Mode.
2. Layout, typography and component settings have an explicit authoring owner. Startup does not destroy the authored visual tree.
3. Behavior changes have been exercised through the real user flow. Check keyboard, controller, pointer, localization and supported aspect ratios for UI changes.
4. Import/compilation, player build and runtime acceptance are recorded separately. An Editor result is not a player result.

See [asset workflow](ASSET_WORKFLOW.md), [development contracts](DEVELOPMENT.md) and [rendering](RENDERING.md). Keep local migration utilities, fixtures and acceptance evidence outside this public source tree.

## Unity guidance

- [Prefabs](https://docs.unity3d.com/6000.3/Documentation/Manual/Prefabs.html): reusable serialized objects edited through Prefab Mode.
- [Architect code as a project scales](https://unity.com/how-to/how-architect-code-your-project-scales): separate responsibilities and expose the settings designers need.
- [Design UI for multiple resolutions](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html): anchors, reference resolution and canvas scaling.

Apply these principles to the existing game incrementally. They do not justify a wholesale framework replacement or moving animation-bound objects without a migration.
