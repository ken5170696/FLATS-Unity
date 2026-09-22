# Development contracts and services

Gameplay and legacy vendor integrations compile into Assembly-CSharp. Small assembly definitions isolate module Core, movement policy and UI presentation. Editor tools stay below Editor directories. Core/presentation assemblies must not reference Assembly-CSharp: connect features through their existing interfaces.

Eight build scenes are tracked, starting with MainMenu, followed by Tutorial and six maps. Shared GameInterface composition and nested UI prefabs keep scene bindings consistent. Resources-loaded assets and serialized events form runtime contracts even when no C# reference exists.

## Persistence and lifecycle

FlatsLocalProfile validates and migrates the legacy character/settings/progress fields. FlatsAtomicRecord stores checksummed records with backup/generation recovery. Future schemas fail without a silent downgrade. FlatsPreferences redirects preference access only for explicitly isolated verification profiles. Module settings and profiles have separate records; disabling a module releases its lifetime-owned resources.

Test initialization from an empty profile, save/restart, scene re-entry and repeated Play Mode entry. Keep normal domain reload enabled unless a change explicitly validates the disabled-reload case. No disabled-reload support claim is made.

Scope prefabs use per-instance render targets and masked RawImage surfaces. Keep the UV Rect's `serializedVersion: 2`; omitting it can import a zero-size UV rectangle in Unity 6 and display a single pixel across the lens. Keep each scope's image-effect shader references. Run `Sights`, then test aiming, changing weapons and resetting while aimed in a Player.

Navigation data is loaded by `FlatsOfflineNavigation`. `FlatsNavigationRepair.Run` rebuilds from static non-trigger collision geometry, preserving asset GUIDs. Warehouse and NightLand generate one-way drop links (maximum vertical drops 5 and 60 world units respectively) so existing raised/roof spawns can reach the map. No jump-across or upward links are generated. After rebaking, run `Navigation` and `NavigationPlay`, then check actual enemy pursuit/combat in those maps.

## Multiplayer

The bundled PUN interface is version 1.85 with SDK 4.1.1.14. Set a Photon PUN client App ID via `FLATS_PHOTON_APP_ID` before launching Unity; environment changes do not update an already-running editor. Desktop uses UDP, Web uses its existing secure-WebSocket integration and page-supplied client configuration. An App ID is a client identifier, not an administration key.

Test two isolated clients against an account you control: connect, create/join room, start match, move/fire, score, leave/rejoin and disconnect recovery. Module agreement must be checked on both participants. Never substitute a successful build or local crypto test for this network test.

## Modules

Local packages, profiles and built-in crosshair settings do not require a catalogue. Optional `FLATS_MOD_CATALOGUE_URL` selects an HTTPS service implementing the protocol in `Runtime/Core/Modules/ModSource.cs` under `Assets/_Flats`. In the Editor, development source settings are stored in `mod-source.json` under the module settings directory; schema 1 contains `url`. Development loopback HTTP is supported by the existing source validator; production transport keeps its HTTPS validation.

Example development source configuration:

```json
{"schema":1,"url":"https://catalogue.example.org"}
```

This is a protocol example, not a hosted service. Catalogue availability and package installation are separate from offline play. External code packages execute code and must come from a source you trust. Browser builds retain their existing data-preset restrictions and cannot execute desktop DLL modules.

The optional integration fixture driver accepts `FLATS_TEST_FIXTURES` for dependency archives. After importing this project, install a .NET SDK capable of targeting netstandard2.1 and run `python tools/build_module_fixtures.py Logs/module-fixtures`, then set `FLATS_TEST_FIXTURES` to that absolute output directory before launching Unity. These generated test packages must never be placed in a normal player profile. Fixture-dependent UI scenarios are separate from the self-contained default validation command. `FlatsModuleTests` supplies in-memory dependencies and temporary storage for the default regression suite.

## Repository boundary

Tracked roots are Assets, Packages, ProjectSettings, tools, docs and explicit repository metadata/documents. No parent-directory inputs are needed. Build products, imported caches, local preferences and raw verification evidence are ignored. Use `python tools/check_repository.py` to verify the tracked boundary and GUID coverage. Do not copy the old mixed repository or its Git history into this project.
