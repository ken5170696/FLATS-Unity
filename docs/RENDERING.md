# Rendering

The URP migration targets Unity 6000.3.24f1 and URP 17.3.0. Gamma is retained to avoid changing the authored flat colours, UI and texture values. The pipeline uses Forward rendering, full render scale, no MSAA, LDR output and a depth texture. The original optional FXAA is applied by the FLATS effects feature. Opaque-copy texture is unnecessary for the current effects; passes declare their colour/depth dependencies through RenderGraph. Compatibility mode is not required.

`Assets/_Flats/Editor/FlatsUrpSetup.cs` provides **FLATS > Rendering > Configure URP** to create/repair the production pipeline and renderer assets. Assign the pipeline in Graphics and every Quality level; platform quality defaults must be valid indices. An installed URP package alone does not configure rendering.

Surface shaders keep their existing GUIDs, names, material properties, texture/vertex colour formulas and blend modes. Opaque and cutout surfaces provide explicit URP depth, normals and motion-vector passes, including previous skinned vertices. Transparent surfaces retain their original depth-write behaviour. UI and scope render textures keep their existing serialized references.

`FlatsCameraStack` rebuilds screen camera stacks in existing depth order before each context render. Depth-only cameras become overlays of an earlier world-clearing base; cameras preceding a cinematic base retain their earlier order so they cannot cover the cinematic. Scope render textures remain independent bases and render before screen stacks so the HUD consumes the current frame. `FlatsSightTarget` still owns and releases each live scope target.

`Flats.Rendering.FlatsPostProcessFeature` performs rendering through native RenderGraph passes. Existing FxPro, AmplifyMotion, EdgeDetect, FXAA, grayscale, fisheye and colour-curve components retain serialized settings and the API used by gameplay and save data. Under SRP their old initialization paths do not allocate Built-in replacement cameras or run a second image-effect chain. Keep their script GUIDs and fields: removing them breaks existing prefab and gameplay references. Third-party attribution remains in `THIRD_PARTY_NOTICES.md`.

Migration validation is in progress. Compilation, player builds, visible effects, ordinary gameplay, per-platform support and performance are separate checks; this architecture description does not establish visual parity or complete platform acceptance. Do not remove shaders or effects merely to suppress a failed check.
