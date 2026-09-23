# Mod SDK preview — API 1.0.0

This preview supports crosshair data packages and the existing managed module contract. It does not expose weapons, maps or game modes as supported third-party APIs. Game compatibility and API compatibility are separate: declare both ranges, with an inclusive minimum and exclusive maximum. FLATS 5.4.2 uses API 1.0.0 and manifest schema 1.

## Create a data package

Copy `docs/examples/crosshair/manifest.json`, choose your own stable lowercase ID and author, and edit the name, shape and size. Shape 0 is cross, 1 is dot and 2 is ring; size must be between 6 and 64. Crosshair modules must use `ClientOnly` scope. Package from the public checkout with Python 3:

```sh
python tools/package_mod.py docs/examples/crosshair/manifest.json --output Builds/Mods
```

This creates a ZIP containing `manifest.json` and a SHA-256 receipt. It needs no private repository or service. Keep each published ID/version immutable; change the version for any changed bytes. Use a distinct ID for a distinct module. Do not use the reserved `flats.*` namespace or impersonate `official.*` packages.

On desktop, open MOD, import the ZIP, review the installation, then enable it and restart FLATS. Only one crosshair provider can run at once: disable other crosshair providers before enabling this example. Enter Training to inspect the result. Disable and restart to restore the default crosshair; remove it after disabling it in every profile. A preset's shape/size are authored in its manifest. The official Custom Crosshair package additionally has the game's live configuration screen; arbitrary presets do not acquire that editor automatically.

Web/AOT supports crosshair data. The browser's manifest import finds a matching package in the configured catalogue; it does not install arbitrary pasted local data or run DLLs. An operator must publish the package to that catalogue, including immutable size/hash metadata. In Web, use Explore or paste the exact published manifest, review, install, enable and reload. If the service is unavailable, existing installed data remains usable; retry new downloads after service recovery. Hosting a personal catalogue is a separate operator task.

## Contracts and lifecycle

The preserved API types are `Flats.Modules.IFirstPartyModule`, `ModuleManifest`, `ModuleLifetime`, `ModuleScope`, `ModuleDependency` and `VersionRange` in `Flats.Core`. Compile against the same API version; do not reference Menu, FPSController, UI hierarchy or host implementation types. This release keeps the existing type names and assembly identity for binary compatibility.

The host validates the complete requested set before activation, orders dependencies before dependents, and calls `Initialize(lifetime)` followed by `Enable(lifetime)`. Register cleanup with `lifetime.Own(...)` **before** subscribing to events or installing resources. On failure the host calls `Disable()` and disposes the lifetime; cleanup must tolerate partial initialization. Removal/disable unwinds dependents first. Cleanup runs in reverse registration order, attempts every action, and records errors. Do not rely on `Disable` alone to release subscriptions.

Lifecycle callbacks are synchronous on the Unity main thread. Do not block them with downloads or disk work. Marshal Unity object access back to the main thread; cancel and join owned asynchronous work during cleanup, and never mutate scene objects after disposal. The host cannot automatically cancel work that a managed extension starts outside its lifetime. Activation changes normally take effect at restart; loading a DLL is not hot unloading, and disabling it does not unload its assembly.

Managed ZIPs declare `kind: managed`, a safe relative `.dll` `assembly` path and a public parameterless `entryType` implementing `IFirstPartyModule`. The implementation's ID, version, scope, API/game ranges, dependencies and conflicts must exactly match the package manifest. Desktop Mono can load these DLLs with the game's privileges: **this is not a sandbox**. Web and IL2CPP/AOT reject executable packages. No claim of mobile managed-module support is made.

## Settings, dependencies and multiplayer

The package schema is 1. The official crosshair setting schema is independently 1 (`style`, `size`); validate values before saving. Profiles retain requested module IDs/versions and settings independently from the installed package set and the running profile. Selecting a profile requires restart. Missing modules, unsupported schemas or incompatible dependencies must surface an error, not silently alter the selected configuration. Existing stored data is retained for recovery.

Versions are three numeric components. A dependency uses `id`, `minimum`, `maximum`; every requested dependency must be present, compatible and enabled. Cycles, duplicate IDs and declared conflicts are rejected. Crosshair providers also conflict at activation even if an author omits the declaration. Package paths cannot escape the archive; transport verifies declared bytes/hash and installation uses the existing atomic recovery mechanism.

`ClientOnly` is for local visual changes, not gameplay rules. `RequiredForSession` participates in the running session's version/hash agreement; peers with different required modules cannot play together. This is consistency checking, not anti-cheat or server authority. Do not change the active module set during a match. Reconnect must use the same running profile until a restart applies another one.

## Compatibility and migration

5.4.2 corrects the host's stale 5.3.5 compatibility identity. A package declaring a maximum of 5.4.0 is now correctly unavailable on 5.4.2. Its data is retained; install the author's compatible newer package, review enable intent and restart. Official/example crosshair package 1.0.1 declares `>=5.4.1 <5.5.0`; old 1.0.0 archives remain unchanged for older players. Authors must validate their own supported range; increasing a maximum alone is not runtime validation.

API 1.x retains these contracts; incompatible contract changes require a major API version and a documented migration. Game, API, package, settings and save schema versions must not be conflated. The SDK is a preview: see the release notes and live player manifest for actual platform validation; successful packaging alone is not in-game acceptance.

The 5.4.2 preview moves host implementation types `HttpModSource`, `PackageStore`, `DownloadQueue` and `ModProfiles` from `Flats.Core` to `Flats.Modules.Infrastructure`. Their namespace and source signatures remain `Flats.Modules`, but their assembly-qualified identities change. The supported managed contracts listed above, package/catalogue DTOs, download status DTOs and profile DTOs remain in `Flats.Core`. Existing modules using only the supported contracts do not need a reference to Infrastructure. There is no claim that a binary using arbitrary host internals remains compatible.

If an internal integration directly used a moved implementation type, rebuild it with references to both assemblies from the same game revision and update any assembly-qualified type names. Such use remains outside the supported SDK; ordinary mod authors should use the supported lifecycle contract or data packages instead. No type forwarder is provided: making Core forward to Infrastructure would introduce a circular assembly dependency. Rebuilding a DLL does not change its manifest compatibility range or establish runtime acceptance.
