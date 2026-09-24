# Mod SDK preview — API 1.1.0

This preview supports crosshair data packages, data modules for game adapters (manifest schema 2) and the existing managed module contract. It does not expose weapons, maps or game modes as supported third-party APIs. Game compatibility and API compatibility are separate: declare both ranges, with an inclusive minimum and exclusive maximum.

This source revision uses API 1.1.0, which reads manifest schemas 1 and 2. FLATS 5.4.2 players use API 1.0.0 and only schema 1, so a schema 2 package is unavailable to them rather than misread. A schema 2 package declares `apiMinimum` 1.1.0 or later.

## Create a data package

Copy `docs/examples/crosshair/manifest.json`, choose your own stable lowercase ID and author, and edit the name, shape and size. Shape 0 is cross, 1 is dot and 2 is ring; size must be between 6 and 64. Crosshair modules must use `ClientOnly` scope. Package from the public checkout with Python 3:

```sh
python tools/package_mod.py docs/examples/crosshair/manifest.json --output Builds/Mods
```

This creates a ZIP containing `manifest.json` and a SHA-256 receipt. It needs no private repository or service. Keep each published ID/version immutable; change the version for any changed bytes. Use a distinct ID for a distinct module. Do not use the reserved `flats.*` namespace or impersonate `official.*` packages.

On desktop, open MOD, import the ZIP, review the installation, then enable it and restart FLATS. Only one crosshair provider can run at once: disable other crosshair providers before enabling this example. Enter Training to inspect the result. Disable and restart to restore the default crosshair; remove it after disabling it in every profile. A preset's shape/size are authored in its manifest. The official Custom Crosshair package additionally has the game's live configuration screen; arbitrary presets do not acquire that editor automatically.

Web/AOT supports crosshair data. The browser's manifest import finds a matching package in the configured catalogue; it does not install arbitrary pasted local data or run DLLs. An operator must publish the package to that catalogue, including immutable size/hash metadata. In Web, use Explore or paste the exact published manifest, review, install, enable and reload. If the service is unavailable, existing installed data remains usable; retry new downloads after service recovery. Hosting a personal catalogue is a separate operator task.

## Data modules (manifest schema 2)

A data module contains no code. It declares `kind: data` and the game `adapter` it targets, written as `name@major`. The game registers the adapters it implements. A package for an adapter the game does not have, or with a scope the adapter does not accept, is shown as incompatible instead of failing when enabled. A data module may omit `gameMinimum` and `gameMaximum`, because it depends on the adapter version rather than on a game release. Declare them only to exclude releases you know are wrong.

| Adapter | Scope | What it changes | Data |
|---|---|---|---|
| `crosshair@2` | `ClientOnly` | The local HUD crosshair | The first entry in `presets` |
| `enemy.tuning@1` | `RequiredForSession` | AI enemy health, damage and movement speed | `payload`: a JSON file with `schema: 1` and `health`, `damage`, `speed` multipliers between 0.25 and 4 (default 1) |

`crosshair@2` settings are `style` (`cross`, `dot`, `ring`, `crossDot`, `t`), `size` (6–64), `thickness` (1–8), `gap` (0–16), `color` and `outlineColor` (`#rrggbb` or `#rrggbbaa`), `opacity` (0.2–1) and `outline` (`true`/`false`). Values are strings, as in `docs/examples/crosshair-preset/manifest.json`. A preset lists only the values it changes; the others keep their defaults. Presets are checked against these settings when the package is installed, so an unknown name or an out-of-range value is rejected.

An `enemy.tuning@1` payload is part of the hashed package. Every player in a room therefore uses the same numbers, and the room agreement below rejects players whose package differs. See `docs/examples/enemy-tuning`.

The same packaging command handles schema 2 and includes the payload that sits next to the manifest:

```sh
python tools/package_mod.py docs/examples/crosshair-preset/manifest.json --output Builds/Mods
python tools/package_mod.py docs/examples/enemy-tuning/manifest.json --output Builds/Mods
```

Payloads are relative `.json` paths of at most 16 KiB. The packager checks the adapter's scope and the enemy tuning ranges. The game still performs the full validation when installing.

## Contracts and lifecycle

The preserved API types are `Flats.Modules.IFirstPartyModule`, `ModuleManifest`, `ModuleLifetime`, `ModuleScope`, `ModuleDependency` and `VersionRange` in `Flats.Core`. Compile against the same API version; do not reference Menu, FPSController, UI hierarchy or host implementation types. This release keeps the existing type names and assembly identity for binary compatibility.

The host validates the complete requested set before activation, orders dependencies before dependents, and calls `Initialize(lifetime)` followed by `Enable(lifetime)`. Register cleanup with `lifetime.Own(...)` **before** subscribing to events or installing resources. On failure the host calls `Disable()` and disposes the lifetime; cleanup must tolerate partial initialization. Removal/disable unwinds dependents first. Cleanup runs in reverse registration order, attempts every action, and records errors. Do not rely on `Disable` alone to release subscriptions.

Lifecycle callbacks are synchronous on the Unity main thread. Do not block them with downloads or disk work. Marshal Unity object access back to the main thread; cancel and join owned asynchronous work during cleanup, and never mutate scene objects after disposal. The host cannot automatically cancel work that a managed extension starts outside its lifetime. Activation changes normally take effect at restart; loading a DLL is not hot unloading, and disabling it does not unload its assembly.

Managed ZIPs declare `kind: managed`, a safe relative `.dll` `assembly` path and a public parameterless `entryType` implementing `IFirstPartyModule`. The implementation's ID, version, scope, API/game ranges, dependencies and conflicts must exactly match the package manifest. Desktop Mono can load these DLLs with the game's privileges: **this is not a sandbox**. Web and IL2CPP/AOT reject executable packages. No claim of mobile managed-module support is made.

## Settings, dependencies and multiplayer

Package manifests use schema 1 or 2. The official Custom Crosshair stores `crosshair@2` settings (listed above). Settings saved by the older schema 1 editor (`style`, `size`) are converted when loaded, keeping the thickness and gap the old renderer drew. Profiles retain requested module IDs/versions and settings independently from the installed package set and the running profile. The selected profile is the only record of which modules are enabled. Selecting a profile requires restart. A module the profile enables but that is no longer installed is skipped and reported in the confirmation; its intent is kept, so reinstalling restores it. Unsupported schemas and incompatible dependencies surface an error instead of silently altering the selected configuration. Existing stored data is retained for recovery.

Versions are three numeric components. A dependency uses `id`, `minimum`, `maximum`; every requested dependency must be present, compatible and enabled. Cycles, duplicate IDs and declared conflicts are rejected. Crosshair providers also conflict at activation even if an author omits the declaration. Package paths cannot escape the archive; transport verifies declared bytes/hash and installation uses the existing atomic recovery mechanism.

`ClientOnly` is for local visual changes, not gameplay rules. `RequiredForSession` participates in the running session's version/hash agreement; peers with different required modules cannot play together. Random matchmaking only joins rooms with the same required modules. Joining a room directly checks the full agreement; if a module is missing, the game names it and can open it in Explore. This is consistency checking, not anti-cheat or server authority. Do not change the active module set during a match. Reconnect must use the same running profile until a restart applies another one.

## Compatibility and migration

5.4.2 corrects the host's stale 5.3.5 compatibility identity. A package declaring a maximum of 5.4.0 is now correctly unavailable on 5.4.2. Its data is retained; install the author's compatible newer package, review enable intent and restart. Official/example crosshair package 1.0.1 declares `>=5.4.1 <5.5.0`; old 1.0.0 archives remain unchanged for older players. Authors must validate their own supported range; increasing a maximum alone is not runtime validation.

API 1.x retains these contracts; incompatible contract changes require a major API version and a documented migration. Game, API, package, settings and save schema versions must not be conflated. The SDK is a preview: see the release notes and live player manifest for actual platform validation; successful packaging alone is not in-game acceptance.

The 5.4.2 preview moves host implementation types `HttpModSource`, `PackageStore`, `DownloadQueue` and `ModProfiles` from `Flats.Core` to `Flats.Modules.Infrastructure`. Their namespace and source signatures remain `Flats.Modules`, but their assembly-qualified identities change. The supported managed contracts listed above, package/catalogue DTOs, download status DTOs and profile DTOs remain in `Flats.Core`. Existing modules using only the supported contracts do not need a reference to Infrastructure. There is no claim that a binary using arbitrary host internals remains compatible.

If an internal integration directly used a moved implementation type, rebuild it with references to both assemblies from the same game revision and update any assembly-qualified type names. Such use remains outside the supported SDK; ordinary mod authors should use the supported lifecycle contract or data packages instead. No type forwarder is provided: making Core forward to Infrastructure would introduce a circular assembly dependency. Rebuilding a DLL does not change its manifest compatibility range or establish runtime acceptance.
