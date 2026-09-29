# Roguelike Survival

A co-op survival mode for 1–4 players with no designed final stage. It keeps the FLATS
gunplay, movement, maps and interface and adds a run structure: stages, bounty money,
a personal shop, builds, events and chapter routes.

## Playing

- **Entry**: `Play › Singleplayer › Roguelike Survival` tile (the singleplayer page is a horizontal row of
  the mode tiles with the map tile under it; solo: difficulty, map, continue/start) or a multiplayer room
  with rule **Roguelike Survival** (the room objective is the difficulty: Normal / Hard / Chaos;
  the squad is 1–4 players).
- **Stage loop**: Prep (shop, ready up) → Combat (one main objective plus director waves)
  → Cleared → Reward (pick one of three, free) → next stage. Every fifth stage is a chapter
  finale; after it you choose a route (map + modifier) and can **Evacuate** (bank the record)
  or **Continue** (a checkpoint is written and the next map loads).
- **Money**: every kill pays a bounty to *every* connected squad member (headshot kills pay
  ×1.5). The stage budget is normalised so more enemies never mean more money per player.
  Objective completion, event success and rescues pay bounded extras.
- **Controls**: keyboard uses the FLATS bindings (Interact holds/picks up/revives, TAB opens the overview,
  Q/E switch its tabs); a gamepad uses the pad bindings (Change = interact, Back = overview, LB/RB = tabs);
  phones get two HUD touch buttons (Interact while a prompt is up, Overview), tap the ultimate and tactical slots
  to use them, and have an Overview button on every run screen. `RogueInput` is the single place these are read.
  Prompts name the player's actual binding in brackets (`[E]`, `[Mouse button 4]`, `[RB]`); HUD key caps use short
  names (`E`, `M4`, `Space`) and widen to fit. On touch the prompt names the on-screen control instead.
- **Down / death**: a lethal hit downs you for 30 s; a teammate holds *Interact* for 3 s to
  revive. A downed player drops prone for everyone; their own view lowers, tilts and loses most of its colour,
  and they cannot move, jump, fire or melee (only a charged Emergency Revive works). Bleeding out is a full death: you return at the next safe stage with your build
  and a 20% wallet tax. If nobody is alive (and no Emergency Revive is armed) the run ends.
- **Controls**: everything Classic uses, plus *Ultimate* (default `F` / left bumper) and
  *Tactical* (default `C` / D-pad down). Both are rebindable in Settings › Control.
- **HUD**: top-left chips (stage, wallet, enemies alive), the objective panel (icon, name,
  progress text and bar, event and emergency lines), a boss bar for finale targets, ability slots (tactical
  and ultimate with cooldown/charge fill and the bound key), a squad list, a vitals panel bottom left
  (health with a damage trail, shield, bleed-out timer), a weapon panel bottom right and a bounty popup under
  the crosshair. The shared health bar and ammo text fade out while the mode runs; the shared pause button keeps
  working under a HUD-styled face. On narrow screens (4:3, phones held upright) the objective panel moves
  under the chips. Every objective
  or event prop, finale enemy, marked elite and downed teammate shows a **waypoint**: an icon
  with its distance in metres, clamped to the screen edge with an arrow when off screen. In Clear Out
  the last three enemies get markers after 20 s; markers of the same kind that land on one spot merge
  into one (`Last enemies x3`).
- **Item kinds**: stats, cores, mods, tacticals and ultimates each have a colour and a text label on shop
  rows, reward cards and the overview; the shop header shows slot use (`Cores 1/2  Mods 3/6 ...`) and each
  card says which slot it takes and which core it pairs with (`RogueItemKinds`).
- **TAB overview**: `Tab` (pad *Back*) opens a tabbed panel — *Shop* (buy during prep),
  *Player* (health, stats, cores, mods, ultimate charge), *Squad* (every player's state and
  wallet), *Weapons* (both guns with the build's multipliers applied) and *Run* (stage, map,
  route, budget, checkpoint). `Q`/`E`, `1`–`5` or the bumpers switch tabs; `Tab`/`Esc` close it.
- **Checkpoints**: written only at safe boundaries (a stage's Prep, or the chapter shop).
  `Play › Roguelike › Continue` resumes the last checkpoint. A game update that keeps every id the
  checkpoint refers to still resumes it (prices and texts follow the new version); an unreadable
  checkpoint is renamed `*.retired-<UTC>` when a new run starts, so it never blocks later saves. In co-op
  a player is identified by nickname and actor number; a player who rejoins, or a squad that continues in a
  new room, takes back the saved entry with the same nickname. The Classic profile and its
  scores are never touched.

## Content (chapter 1 seed values)

| Kind | Count | Notes |
|---|---|---|
| Stat upgrades | 4 × 5 tiers | Vitality +12%, Firepower +8%, Magazine +15% (≥ +1 round), Agility +6% per tier |
| Build cores | 8, max 2 equipped | Precision, Assault, Suppression, Reload Burst, Ricochet, Demolition, Marker, Mobility |
| Mods | 26, max 6 equipped | see `RogueCatalog.Mods` |
| Tactical | 3, one slot | Double Jump (passive), Dash, Shield |
| Ultimates | 7, one slot, 0–100 charge | Infinite Fire, Lethal Shot, Invincible, Emergency Revive (once per run), Enemy Sight, Chain Bullets, Homing Bullets |
| Enemy roles | 6 | Rifleman, Rusher, Marksman, Shield Bearer, Flanker, Jammer (each with a silhouette marker) |
| Objectives | 5 | Clear Out, Hold the Zone, Deliver the Crate, Protect the Repair, Break Out |
| Events | 8 | Moving Supply, Alarm Cache, Low Gravity, Power Reroute, Repair Device, Risk Contract, Elite Hunt, Lure Crate |
| Emergencies | 4 | Gas Leak, Power Outage, Mobile Bomb, Reinforcement Signal |
| Finales | 3 | Commander, Vault, Convoy |
| Routes | 4 | Quiet, Hot, Strange, Rich |

Prices are fixed per chapter (`RogueDepth.PriceMultiplier`) and never react to wallet size.
Depth curves saturate: enemy health caps at ×4.5, damage at ×2.2, concurrent enemies at 24,
the per-player budget at about 7× the chapter-1 value.

## Where things live

| Area | Location | Owner |
|---|---|---|
| Pure rules (no engine) | `Assets/_Flats/Runtime/Core/Roguelike/` (`Flats.Core.Roguelike`) | economy, shop transactions, builds, director, run state machine, save DTOs |
| Content data | `RogueCatalog.cs` | items, roles, objectives, events, emergencies, finales, maps, routes |
| Unity adapters | `Assets/_Flats/Runtime/Gameplay/Roguelike/` | `RoguelikeController` (on the SingleplayerController prefab), `RoguePlayer`, `RogueEnemyRole`, hooks, transport, save store |
| Menu entry | `Assets/_Flats/Runtime/UI/Menu.Roguelike.cs`, `Menu.ModeTiles.cs` | the Roguelike page; the singleplayer mode row (`Resources/UI/ModeTiles.prefab`) and the seven-map vote grid (`Resources/UI/ModeGrid.prefab`), both filled with copies of the authored MainButtons tile by `FlatsModeTilesView` |
| Screens | `Assets/Resources/UI/Roguelike/RogueScreen.prefab`, `RogueOfferRow.prefab` | authored uGUI; `RogueScreenView` binds them |
| HUD | `Assets/Resources/UI/Roguelike/RogueHud.prefab` (`RogueHudView`, squad row and waypoint templates inside) | instantiated under the gameplay `UI` canvas when a run starts; `RogueWaypoint` components on world objects feed the markers; layout values (narrow width, positions, key cap widths) are serialized on the view |
| Item kinds | `Assets/_Flats/Runtime/UI/Roguelike/RogueItemKinds.cs` | kind colours, labels and weapon display names shared by rows, cards, overview and HUD |
| Downed look | `Assets/_Flats/Runtime/Gameplay/Roguelike/RogueDownedPresentation.cs` | prone pose, owner camera roll and colour; its tuning fields are serialized |
| TAB overview | `Assets/Resources/UI/Roguelike/RogueOverview.prefab`, `RogueStatRow.prefab` | `RogueOverviewView` (tabs, rows); `RoguelikeController.Overview.cs` fills the tabs |
| Icons | `Assets/Resources/UI/Roguelike/RogueIconSet.prefab`, sprites in `Assets/_Flats/Art/UI/Textures/` (+ `Roguelike/`) | `RogueIcons` looks sprites up by file name; add a sprite to the set to use it |
| World props | `Assets/Resources/UI/Roguelike/RogueFlat.mat` | the flat material objective props and enemy markers instantiate (FLATS "Texture Only" shader, so it ships in players) |
| Text | `Assets/Resources/FlatsChinese.txt` | every string is an English key with a Chinese entry |

Mode identity: `Singleplayer.rule == 5` (solo) or `Multiplayer.rule == 9` (co-op), read through
`RoguelikeMode`. Legacy classes call `RogueHooks` behind `RoguelikeMode.Active` checks and are
otherwise unchanged; Classic Survival, Assortment, Headshot, Training, Tutorial and the PvP rules
do not read any of the mode's state.

## Authoring

- **Add a mod or core**: append an `ItemDef` to `RogueCatalog.Mods`/`Cores` and give it an effect in
  `BuildStats.Compute` (numbers) or the adapter (behaviour). Add the name and effect text to
  `FlatsChinese.txt`. `RogueCatalog.Validate()` and the dotnet tests fail on duplicate ids.
- **Add an event/objective**: append an `EncounterDef` (map tags it requires/excludes, cooldown in
  stages, mutual exclusions), implement its pure state machine in `RogueEvents`/`RogueObjectives`,
  and a world runner in the adapter (`RogueObjectiveRunner.Create`).
- **Map candidate points**: objectives use the map's `SpawnPoints`, `WayPoints` and `PhaseSkippers`
  transforms as candidates and filter them by NavMesh reachability at runtime; Warehouse and
  NightLand carry the `droplinks` tag so one-way-drop content is excluded there.
- **UI**: the prefabs above are the authoring source — edit them in the Prefab stage (fonts,
  colours, spacing, anchors). Views only bind text, sprites and fill amounts; they never rebuild
  the tree. To give a new prop a waypoint call `RogueWaypoint.Attach(go, iconName, labelKey,
  tint, height, priority)`; the label key is translated per client.
- **Balance**: `tools/unity-validation/roguelike-tests` (private) runs the pure rules and a fixed-seed
  economy simulation for 1/2/4 players and several headshot rates.
