# Validation scope

The Windows acceptance baseline was exercised on 22 September 2026 with Unity 6000.3.24f1 (4e7b9b5b6244), Windows 11, Direct3D 11 and an NVIDIA RTX 5050 Laptop GPU. Profiles were isolated from normal player data. Development MCP services were stopped. This is a bounded regression baseline, not a guarantee of zero defects or complete parity with every original release.

The clean-clone suite at `bcdfbb484350c5796c0ac7945b73d0e4f07cb081` passed; subsequent tutorial navigation changes are in `c68e5cbe026ee45b1709d98435e605b8723c8e82`. The latter was also checked with native navigation traversal and a rebuilt Windows Player. No earlier project acceptance labels are used as evidence here. Before publishing any later revision, repeat the affected checks and record the exact checked-out commit using `git rev-parse HEAD` and the generated build provenance.

| Check | Status | Actual scope and reproducible entry point |
| --- | --- | --- |
| Fresh clone, package restoration and compilation | PASS | Clone without Library/build products; `tools/unity.ps1 -Task Import` |
| Scene/prefab contracts and module/storage regressions | PASS | `-Task Validate`; eight enabled scenes, all prefabs, 20 module tests and additional persistence/lifecycle checks |
| Repeated Editor Play Mode | PASS | `-Task PlayMode`; MainMenu entered/exited twice, isolated profile, zero console errors, shared UI material hashes unchanged |
| Scope rendering | PASS | `-Task Sights`; reflex, 2x, 4x, 6x and 8x camera output and actual masked RawImage GPU rendering, including red/blue regions and generated UVs; separate Player aiming/reset checks |
| Shaders and audio | PASS | `-Task Shaders`, `-Task Audio`; GPU contracts and clip/reference checks |
| Navigation | PASS | `-Task Navigation`; six maps plus Tutorial connectivity. `-Task NavigationPlay`; all 21 selected spawn routes traversed by native agents. This does not exhaustively test combat AI |
| Windows x64 Mono build | PASS | `-Task Windows`; executable Player launched, zero build errors. Legacy/deprecation warnings remain |
| Offline core gameplay | PASS | Fresh welcome, menu, Survival/Training, movement, firing/ammo, reload, damage, pause/resume, scene reset and return to menu; separate isolated settings/module persistence restart |
| Complete Tutorial | PASS | Look/move, shoot/reload, switch, kill both enemies, pick up sniper, aim, jump and exit. Repeated after restoring Tutorial navigation; both enemies moved on complete paths, zero runtime errors |
| Optional service configuration | PASS | Invalid/unconfigured Photon client ID produces an actionable message; offline Tutorial remains playable. No catalogue configuration leaves local/built-in module controls available |
| Two-client Photon combat | PASS | Owned test invitation room, real online clients, start, movement, firing, replicated kill/death totals and respawn; tested on NightLand and Warehouse during this extraction |
| Repository boundary | PASS | `python tools/check_repository.py`; allowlist, metadata/GUIDs, secret/dependency patterns and link checks; separate review of the new Git history |
| Web build and browser gameplay | NOT RUN | Build entry point retained; needs separate browser, input, storage and networking acceptance |
| Linux/macOS native gameplay | NOT RUN | Build entry points retained; native runtime hosts required |
| Android device gameplay | NOT RUN | Device, touch, audio and lifecycle acceptance required |
| iOS build/device gameplay | NOT RUN | macOS/Xcode signing and device acceptance required |
| LAN/mobile, controller and VR coverage | NOT RUN | Existing functionality retained; not certified by desktop mouse/keyboard results |
| Network interruption/reconnect and full online module agreement | NOT RUN | Requires dedicated failure and multi-client module scenarios; ordinary combat connectivity does not establish these |

Commands above require `-UnityEditor <path-to-Unity.exe>`; complete copyable examples are in [README](../README.md). Run from the repository root. Reports include `Logs/asset-validation.txt`, `sight-validation.txt`, `parity-shader-gpu.json`, `parity-audio-assets.json`, `parity-nav-connectivity.json`, `navigation-play.txt`, `playmode-validation.txt` and timestamped task logs. Build provenance identifies the built source commit. Runtime verification writes timestamped state, Player logs and optional screenshots to the supplied evidence directory.

Raw acceptance logs/screenshots, temporary test profiles and export/history audits are deliberately outside the public history. For each release, retain an external record of commit, environment, exact commands/operations, PASS/FAIL/NOT RUN/BLOCKED status and evidence location. A successful event-dispatch receipt alone is not a gameplay result; check resulting state and visuals. The opt-in integration input drives normal controller/UI paths and does not assign health, transforms, hit results or network scores.

Known coverage limits: no exhaustive weapon/map/rule combination matrix, long-session soak, disabled-domain-reload guarantee, or proof of feature parity with every original binary. Platform build availability is not a runtime support certification. Re-run the relevant tests when changing scenes, serialized contracts, lifecycle, package versions or service integration.
