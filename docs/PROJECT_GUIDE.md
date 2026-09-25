# Project map

Open **FLATS → Project Overview** in Unity for scene and prefab shortcuts. Use Unity 6000.3.24f1. Start Play Mode from MainMenu for menu and profile workflows; an isolated map is not the full application startup path.

## Where to make a change

| Intent | Authoring surface | Behavior |
| --- | --- | --- |
| Sound slider appearance | SettingsScreen prefab → Sound | Menu.Volume binds values and saves settings |
| Keyboard/controller binding layout | SettingsScreen prefab → Control → Bindings → KeyboardList / ControllerList | One scrolling list per device; Menu.Controls binds `Content/Binding<n>/Button` by action index, handles capture, and swaps a button that is already used |
| Controller settings page | SettingsScreen prefab → Control → Bindings → ControllerList and ControllerPreview | Sections (`Section…` rows) hold the layout preset, bindings, stick and feedback rows (`Pad…`). Each row's PreviewTarget holds the text shown in ControllerPreview. FlatsGamepad stores the preferences |
| Module page fixed controls and dialogs | ModulesScreen prefab | ModuleManagementPage binds its serialized view references to services |
| Shared HUD and cameras | GameInterface and GameplayHUD prefabs | Menu composes game state; player presenters update HUD state |
| Touch controls and notch clearance | GameplayHUD prefab (Fire, Reload, Jump, Zoom, Interact, OpenMenu) and its SafeAreaInsets list | EasyTouch buttons are read by axis name; FPSController shows Interact only when something can be picked up |
| Spectator overlay | WatchCamera prefab → Canvas | WatchCamera switches players and its OpenMenu button opens pause and Settings |
| General control rows (sensitivity, aim sensitivity, handedness) | SettingsScreen prefab → Control | Menu.Options and Menu.Controls update settings by row name |
| Shared weapon balance | Runtime/Core/WeaponCatalog.cs | Immutable defaults feed GunInfo at startup; Gun holds per-instance ammunition and copied stats |
| Map geometry, lights, spawn points | Individual map scene | Map and gameplay components |
| Runtime rendering | URP assets and camera prefabs | See RENDERING.md before altering stacks or effects |
| Imported models, textures, audio | Existing Art and Audio folders | Import settings and original source attribution |
| Build configuration | ProjectSettings and tools/unity.ps1 | Normal platform build commands |

The fixed module view is stored in `ModulesScreen.prefab`. Dynamic items instantiate `ModuleListRow`, `ModuleDownloadRow`, `ModuleCatalogueCard`, `ModuleProfileCard`, `ModuleFilterButton`, `ModuleLoadingCard` and `ModuleEmptyState` templates. Edit those templates for item typography and spacing. The page component owns the exposed responsive margins and split breakpoint; LayoutGroups own their child spacing. Consult those owners before changing a driven child RectTransform.

Weapon balance currently has a code authoring entry, not an Inspector definition asset. For example, change the `reloadTime` argument of the intended `WeaponDefinition` in `WeaponCatalog`, then enter Training through MainMenu, fire and reload that weapon. `Gun.Start` copies defaults through `GunInfo`; changing the corresponding prefab's `Gun.reloadTime` is overwritten at startup. Runtime `currentAmmo` is an instance value, while the catalog's stable index is also a save/network contract. Do not reorder definitions to rearrange the menu.

`SafeAreaInsets` moves each listed edge-anchored element by the screen's safe-area inset (notches and rounded corners) plus its `margin`. The authored or code-set position stays the base, so handedness and saved touch layouts still work. Add a new edge control to the list when it should stay clear of a notch. Centre-anchored elements are not moved. Check touch layouts in the Device Simulator with a notched landscape device, in both handedness settings.

The contextual `GameplayHUD` → `Interact` button uses the EasyTouch axis `Interact`. Its label reads Swap, Pick up or Drop, depending on the target. Keep it a direct child of the HUD canvas, because EasyTouch anchors controls to their parent canvas. Existing HUD children are also addressed by sibling index, so add new HUD children after them.

Every Settings page uses one box model inside the 600×340 panel: 24 units of padding on each side. Labels start at the left content edge and controls end at the right content edge, or at the scrollbar when the page scrolls. Scrollbars are 6 units wide with an 8-unit gap. On the controller page the list and ControllerPreview are separated by a 24-unit gutter and share their top and bottom. The status line sits below the lists. Elements outside the panel, such as the category bar and Restore defaults, keep a 10-unit gap. Keep new rows on these edges so switching pages does not shift the columns. A binding row's `Action` text fills the row height. Menu centres it when there is no hint and top-aligns it above the `Detail` hint line.

Settings content that does not fit the shared 340-unit page panel scrolls rather than paging or growing the panel. Examples are the binding lists and `ExtraSettings/ExtraScroll`. The pattern is a ScrollRect with a RectMask2D viewport, a top-pivoted `Content`, a thin auto-hiding scrollbar and `ScrollToSelection`. `ScrollToSelection` opens the list at the top and scrolls keyboard or controller selection into view. Menu controls draw with theme materials whose shader cannot be clipped. Inside a list, give each such Image a `ClippedThemeGraphic`: it draws the same theme colour, including the animated selected state, with the default UI material. Rows are found by name at any depth (`Menu.SettingValue`, `Menu.SettingsRow`).

Controller settings follow current console FPS patterns in FLATS' flat style:
- Layout presets: Default, Tactical and Bumper Jumper. Any other button change reports Custom.
- Look speed per axis, a response curve (Linear keeps the original feel), stick deadzones (Auto keeps the controller profile's value) and vibration strength.
- A preview pane shows the white `ControllerFlat.png` with the bound button marked, or a live stick tester (`StickTesterGraphic`).
- LB/RB jump between sections.

Edit row text and explanations on each row's PreviewTarget. Edit the marker positions on ControllerPreview → Points (0–1 over the controller image). Button names follow the connected controller family (Xbox or PlayStation). They are not translated.

`PointerFocusPolicy` on GameInterface's EventSystem clears a selection left after a mouse or touch click. Menu buttons draw Selected like hover, so a clicked button otherwise stayed lit. Keyboard or controller input restores the remembered focus.

Kill Cinematic (SettingsScreen → ExtraSettings → `KillCinematic`, preference `ui.v1.killCinematic`) controls the headshot and mortal-shot slow-motion camera. When it is OFF, `Supershot.PlayKill` plays only the authored text animation on the `EffectCamera` prefab canvas. Time scale, cameras, input and the pause menu are untouched. VIP and team-kill cinematics always play, because they end the round.

Aim sensitivity is a separate personal preference (`controls.v1.aimSensitivity`). "Match camera" keeps the camera sensitivity. The other values replace it while aimed, and weapon zoom still divides the result. It is included in save export and import.

For aiming, `FPSController` keeps the world/projectile camera on the player camera rig and moves only Gun Camera toward the weapon's authored sight anchor. `FlatsSightTarget` aligns the lens camera to the same world aim while retaining the lens image roll and authored magnification. Script import execution order is intentional: IKController (0) poses the chest and camera rig, FPSController (50) positions the weapon camera, FlatsSightTarget (75) positions the lens, and Bullet (100) records the visible tracer. Preserve this order when editing Script Execution Order. Check a fixed target before/after aiming, firing, reloading, switching weapons and returning to MainMenu, including looking up and down. Do not reparent the world camera under a weapon or compensate a ray mismatch by moving the HUD crosshair.

`FPSController.GetBulletTrailOrigin` maps the locally rendered muzzle into the world camera for tracer and muzzle-flash presentation. `Bullet.LateUpdate` captures this origin after the weapon camera pose is updated, then keeps the trail fixed in world space. Remote players retain their world-space muzzle; projectile velocity, collision and damage do not use this visual mapping. Verify visible muzzle/tracer alignment as well as world-camera stability when editing ADS. The tracer fades from transparent at its tail to the shooter colour at the bullet; adjust `Bullet.trailAlpha` (opacity along the trail) and `trailSegments` on the `Bullet` and `Grenade_Hand` Prefabs.

The player Prefab's `LODGroup` owns every body mesh renderer. `FPSController.SetBodyRenderLayer` keeps those renderers on the local weapon-view layer together, including after team synchronization, and restores their team layer for existing carrying states. When adding a body LOD, register its renderer in that group. Preserve the separate team layers on colliders and hit targets; camera visibility must not change their collision or damage filtering.

For example, change `ModulesScreen` → `SettingsControls/StyleChoices` HorizontalLayoutGroup spacing to adjust the three crosshair choices. Save the prefab, start from MainMenu, open an installed crosshair package's configuration and check all choices at narrow and wide sizes. `ConfirmationDialog.prefab` owns common message padding/action spacing. `MainMenuScreen` → `MenuTileArtwork` owns per-illustration framing; raw Image size can be driven by that component.

Before a local build, save your scene and Prefab Mode work. The build command rejects unsaved authoring state without saving it for you. Resolve a rejected build at the named scene/prefab, then retry; do not discard work just to make the build pass. Build contents come from `ProjectSettings/EditorBuildSettings.asset` (MainMenu first), and platform settings remain in ProjectSettings. Source and packed-asset reports accompany the built Player.

## Hierarchy and references

Map scenes share one root layout:

| Root | Contents |
| --- | --- |
| `[Gameplay]` | Controllers, team bases, spawn, gun and way points. Most are instances of `Prefabs/Systems` |
| `[Environment]` | Visible map geometry, lights and `Original`. `Original` has disabled renderers, but its enabled colliders are the map's collision |
| `[Glass]` | Breakable glass: `GlassController<n>`, `GlassRoot<n>` and the combined `Glass_Baked<n>` renderer |
| `[Baking]` | Mesh Baker objects used only to rebake combined meshes, tagged `EditorOnly` |
| `[Services]` | Scene-level services |
| `UICamera` | The GameInterface prefab instance |

`[Glass]/GlassRootOriginal(ForEdit)` is the inactive authoring copy of the glass panes. It and the `[Baking]` bakers are tagged `EditorOnly`: they stay editable but are not built into players. Leave an object untagged if runtime code or another component uses it.

Some system instance names differ from their prefab names on purpose (`WayPoints`, `PhaseSkippers`, `RedTeamBase`/`BlueTeamBase`, `GunPoints`). Runtime code finds them by name, so keep those names. The same applies to `UICamera`, the HUD instance `UI`, `Menu`, `Message` and `Sight` inside GameInterface. MainMenu's `InControl` and `ReignServices` call `DontDestroyOnLoad` and must stay scene roots.

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
