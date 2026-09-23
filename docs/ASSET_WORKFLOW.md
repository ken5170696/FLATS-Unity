# Asset authoring and import

`Assets/_Flats` contains project-owned game content; `Assets/ThirdParty` and `Assets/Plugins` contain vendor integrations. Keep each component's existing attribution in `THIRD_PARTY_NOTICES.md`. Record the source and applicable permission for new externally sourced content before publishing it. Do not infer a licence from a filename or from public availability.

## Placement and identity

- Put scenes in `_Flats/Scenes`, reusable prefabs in `_Flats/Prefabs`, textures, models, materials and shaders in the corresponding `_Flats/Art` area, animation in `_Flats/Animation`, and audio in `_Flats/Audio`. Use the existing nearby naming pattern and a descriptive name; reserve `Shared` for actual shared content.
- Keep source files used to produce an asset outside Unity's generated `Library`, `Temp`, `Builds` and `Logs`. Commit the Unity input asset and its `.meta` together. Build output and imported caches are not source.
- Move or rename assets in the Editor when possible. In Git, move the `.meta` with the asset and confirm its GUID stays the same. Check prefab and scene references, nested overrides, persistent UnityEvent methods, animation events, `Resources.Load` keys, and reflected or string-based shader names. An asset with no static C# reference may still be loaded dynamically.
- Keep `Assets/Resources` paths stable. Do not consolidate apparently duplicate meshes, materials or textures by hash without checking serialized references and instance ownership.

## Import decisions

Inspect the target platform and actual use before changing import settings. For textures and sprites, check dimensions, alpha, wrap/filter mode, compression, mipmaps and sprite mesh/UV data in the Editor and in a Player. For audio, check codec, load type, channel layout and audible playback of the event. For models, preserve scale, mesh topology, subasset IDs, material slots and animation bindings. Record a measured quality or memory reason for platform overrides; do not normalize all importers in bulk.

The restored Teddy sprite UVs, scope UV rectangles, headshot text size, audio decoding and UI theme materials are compatibility-sensitive. Shared material or mesh assets are not owned by an individual scene object. Runtime copies, render textures and event subscriptions need an explicit owner and teardown. The scope prefabs use per-instance render targets; keep their `serializedVersion: 2` UV Rect data. Camera depth shaders depend on the existing build variant retention and camera depth setup. Verify these in a built Player after related shader or build-setting edits; an Editor preview alone is insufficient.

## Review before delivery

1. Review the asset and `.meta` diff, GUID stability, import settings, platform overrides and attribution. Inspect affected scenes and prefabs in Unity, including persistent events and missing-script warnings.
2. Run the private repository-boundary and isolated-clone validation workflow documented in the maintainer repository. Public contributors can run the normal Unity `Import` task and affected platform builds from `tools/unity.ps1`.
3. Exercise the changed asset in its normal game path on every affected published platform. Record source commit, build result and runtime result separately. If a platform is unavailable, record `NOT_TESTED` rather than extending another platform's result.
