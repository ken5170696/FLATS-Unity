# Development contracts and services

Gameplay and legacy vendor integrations compile into Assembly-CSharp. Small assembly definitions isolate module Core, module infrastructure, movement policy and UI presentation. Editor tools stay below Editor directories. Core/presentation assemblies must not reference Assembly-CSharp: connect features through their existing interfaces.

The 5.4.2 candidate routes desktop gameplay through `IPlayerInputSource` and the read-only `IGameSessionContext` in Core. Menu binds each controller using its existing scene composition; legacy public state remains a compatibility boundary. `WeaponAmmoPolicy` owns deterministic reload conservation while the controller retains animation and RPC entry points. Do not add test drivers to the runtime source. Other input platforms retain their adapters and share the gameplay pause gate.

The module page receives `IModHost` and `IModCenter` from `ModulePageBinding`; it no longer fetches a singleton per operation. The service receives platform/source/work policy through `IModCenterPlatform`. Readiness means initialization succeeded; settled failure is separate and must remain visible/recoverable. These host interfaces are internal application boundaries, not additions to the external SDK. See [SDK preview](MOD_SDK.md) for the supported external contracts and an independently packageable data example.

Eight build scenes are tracked, starting with MainMenu, followed by Tutorial and six maps. Shared GameInterface composition and nested UI prefabs keep scene bindings consistent. Resources-loaded assets and serialized events form runtime contracts even when no C# reference exists.

## Persistence and lifecycle

Game version is `PlayerSettings.bundleVersion`, mirrored by `ModRules.GameVersion` for compatibility checks. The source gate checks that specific pair. `ModRules.ApiVersion` is the independent module API version; `FlatsLocalProfile.Profile.schema` and other persistence record schemas govern their respective data migrations. Platform build numbers are separate store metadata. Do not synchronize these unrelated numbers. Increment schema versions only with explicit migration and newer-format rejection behavior.

FlatsLocalProfile validates and migrates the legacy character/settings/progress fields. FlatsAtomicRecord stores checksummed records with backup/generation recovery. Future schemas fail without a silent downgrade. FlatsPreferences redirects preference access only for explicitly isolated verification profiles. Module settings and profiles have separate records; disabling a module releases its lifetime-owned resources.

Test initialization from an empty profile, save/restart, scene re-entry and repeated Play Mode entry. Keep normal domain reload enabled unless a change explicitly validates the disabled-reload case. No disabled-reload support claim is made.

Scope prefabs use per-instance render targets and masked RawImage surfaces. Keep the UV Rect's `serializedVersion: 2`; omitting it can import a zero-size UV rectangle in Unity 6 and display a single pixel across the lens. Keep each scope's image-effect shader references. Check aiming, weapon changes and scene reset in a Player after modifying these assets.

Navigation data is loaded by `FlatsOfflineNavigation`. `FlatsNavigationRepair.Run` rebuilds from static non-trigger collision geometry, preserving asset GUIDs. Warehouse and NightLand generate one-way drop links (maximum vertical drops 5 and 60 world units respectively) so existing raised/roof spawns can reach the map. No jump-across or upward links are generated. After rebaking, check spawn routes and actual enemy pursuit/combat in those maps.

## Multiplayer

The bundled PUN interface is version 1.85 with SDK 4.1.1.14. Set a Photon PUN client App ID via `FLATS_PHOTON_APP_ID` before launching Unity; environment changes do not update an already-running editor. Desktop uses UDP, Web uses its existing secure-WebSocket integration and page-supplied client configuration. An App ID is a client identifier, not an administration key.

Test two isolated clients against an account you control: connect, create/join room, start match, move/fire, score, leave/rejoin and disconnect recovery. Module agreement must be checked on both participants. Never substitute a successful build or local crypto test for this network test.

## Modules

`Flats.Core` retains module contracts, package/catalogue/profile/download DTOs, dependency planning and deterministic validation rules. `Flats.Modules.Infrastructure` contains HTTP transfer, disk/archive package storage, download queue persistence and module profile storage; its asmdef references Core, while Core has no infrastructure reference. Both assemblies exclude Unity engine references. `IModSource` and `IModJson` stay in Core as ports. Using `InvalidDataException` for validation does not perform disk I/O.

Unity-dependent module settings persistence, host composition and service/UI adapters remain outside these assemblies. Do not add File/Directory/network operations to Core; put implementations behind the existing ports in Infrastructure. The asmdef boundary prevents a Core reference to host infrastructure types, but does not itself prohibit .NET BCL I/O calls. The implementation-type migration and binary limits are documented in [SDK compatibility](MOD_SDK.md#compatibility-and-migration).

Local packages, profiles and built-in crosshair settings do not require a catalogue. Optional `FLATS_MOD_CATALOGUE_URL` selects an HTTPS service implementing the protocol in `Runtime/Core/Modules/ModSource.cs` under `Assets/_Flats`. In the Editor, development source settings are stored in `mod-source.json` under the module settings directory; schema 1 contains `url`. Development loopback HTTP is supported by the existing source validator; production transport keeps its HTTPS validation.

Portal builds bundle `FLATS_MOD_CATALOGUE_URL` in a temporary `Resources/FlatsModCatalogue` text asset, then remove it in `finally`. The asset and its metadata are ignored by Git. Set the variable before starting the build process, for both desktop and WebGL; rebuilding without it intentionally produces an offline-only catalogue configuration. Never commit operator-specific endpoints. Browser requests reject redirects, omit credentials, enforce HTTPS/origin boundaries and cap catalogue/images at 2 MiB. Package downloads are bounded by the declared archive size and validated by the shared size/hash/manifest installer. Browser downloads restart after pause; desktop supports Range resume. Custom Crosshair (`official.custom-crosshair`) is no longer preinstalled: download it from Explore, install, enable and restart. Web supports client-only crosshair packages, dependency plans, profiles and removal; executable packages remain desktop-only. Legacy built-in settings are migrated without automatically installing or enabling the new package.

Example development source configuration:

```json
{"schema":1,"url":"https://catalogue.example.org"}
```

This is a protocol example, not a hosted service. Catalogue availability and package installation are separate from offline play. External code packages execute code and must come from a source you trust. Browser builds install supported crosshair data packages and cannot execute desktop DLL modules.

## Repository boundary

Tracked roots are Assets, Packages, ProjectSettings, tools, docs and explicit repository metadata/documents. No parent-directory inputs are needed. Build products, imported caches, local preferences and raw verification evidence are ignored. Do not copy the old mixed repository or its Git history into this project.

## First-party coding conventions

Follow the surrounding file for substantive edits; use four spaces and LF for new Editor tools (see `.editorconfig`). Do not reformat legacy gameplay, vendor code, generated code or serialized assets in a behavior patch. Use PascalCase for new types/methods, camelCase for locals/fields, explicit visibility and the existing assembly's namespace. Preserve global serialized component types and all serialized field, UnityEvent, animation, RPC, resource and JSON names unless the same change migrates and verifies their contracts. Comments should explain ownership, constraints or non-obvious decisions. Add abstractions only for an observed responsibility boundary.
