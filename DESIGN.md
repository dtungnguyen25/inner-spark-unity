# Inner Spark — Design & Reference Doc

This is the living reference for the project: what the game is, how the code is put
together, and the vocabulary used throughout the scripts. Code comments cover the
*how*; this file is for the *why* and *what's planned* — add to it as decisions get made.

> **Note:** most of this was reconstructed from reading the code (2026-09-27), since no
> design notes existed yet. Sections marked 🟡 are inferred and worth confirming or
> replacing with your actual intent.

---

## 1. Concept 🟡

A puzzle game built around a printed circuit board. The player controls a **Spark**
that rides along copper **traces** between fixed stop points (**nodes**), starting at a
**Start** plug and trying to reach a **Goal** chip. The board has a front and back side;
**Via** nodes let the spark punch through to flip which side is "live," changing which
traces are available — this looks like the central puzzle mechanic (routing + side-switching).

*Open questions:* What's the intended difficulty curve / puzzle vocabulary beyond
side-flipping? Is there a target platform (PC/WebGL/mobile)? Any narrative framing, or
is it abstract puzzle-only?

## 2. Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Move | WASD / Arrow keys | Left stick / D-pad |
| Flip side (on a Via) | Space | South button |
| Restart level | R / on-screen Restart button | Select |
| Confirm (advance after winning) | Space / Enter | South button |
| Pause / resume | Esc / on-screen Pause button | — |
| Advance dialog | Click Continue, or Submit while it's selected | Submit |
| Inspect board (tilt camera) | Hold left mouse + drag | — |

There is no Prev/Next level control any more (removed 2026-09-28) — winning advances to
the next level, and Stage Select in the Main Menu jumps to any level. Quitting is done
from the pause menu (Quit to Menu / Quit App), not Esc.

Movement reads an 8-way direction each frame; a new direction is only accepted once
input returns to neutral first (no key-repeat drift). Input is read in **screen space**,
so it stays intuitive even when the back of the board is shown mirrored
(`Spark.ScreenToBoard`).

While paused, all gameplay input is ignored — moves, flips, restart, confirm and mouse
inspection. Nothing pressed during the pause is queued up to fire on resume
(`PauseMenu.GamePaused`, checked by `Spark` and `BoardRig`). Gameplay input is also
blocked while a dialog is showing.

## 3. Vocabulary

| Term | Meaning |
|---|---|
| **Board** | Root of a level. Owns all nodes/traces/decorations, builds the movement graph and the generated 3D look. |
| **Node** (`PcbNode`) | A point the Spark can stop at. |
| **NodeType** | `Capacitor` (plain stop), `Via` (stop that exists on both sides — flip point), `Start` (spawn), `Goal` (level end). |
| **Trace** | A copper line between two nodes on one side of the board; the Spark slides along it automatically once entered. Can have bend points. |
| **Layer** | `Front` / `Back` — which face of the board something is on. Vias belong to both. |
| **Spark** | The player-controlled glowing sphere. |
| **Decoration** (`PcbDecoration`) | Purely cosmetic set-dressing (resistors, ICs, screw holes, silkscreen, copper pour...). Never part of the movement graph — invisible to gameplay and the level validator. |
| **Theme** (`PcbTheme`) | ScriptableObject holding every material, model override, and dimension used to render a board. One theme is shared by all levels. |
| **Rig** (`BoardRig`) | Camera pivot: frames the board, animates the turn-over, handles mouse-drag inspection. |

## 4. Architecture map

Runtime (`Assets/Script/PCB/`):
- **Board.cs** — collects nodes/traces/decorations under it, builds the exit graph (`TryPickExit`), triggers visual rebuilds, tracks front/back visibility.
- **Trace.cs** — path between two nodes (with bends), exposes path/distance queries used by both gameplay and the editor.
- **PcbNode.cs** — data-only component: type, layer, (goal) chip size.
- **PcbDecoration.cs** — data-only cosmetic component, deliberately excluded from the graph. *(Lives at `Assets/Script/PcbDecoration.cs`, one level up from the rest — same `Pcb` namespace.)*
- **Spark.cs** — player controller: reads input, walks the exit graph, animates movement/flip, builds its own visuals (core, glow, trail, direction arrows) from the theme.
- **BoardRig.cs** — perspective camera framing, turn-over animation, mouse-drag tilt.
- **BoardVisuals.cs** — procedurally builds the entire 3D look (board slab, traces, node models, decorations) from primitive meshes; everything it creates is `HideFlags.DontSave` so only gameplay data is ever serialized.
- **LevelManager.cs** — one per scene; owns the level list, spawn/restart/next/prev flow, win detection, on-screen HUD (`OnGUI`).
- **LevelList.cs** — ordered `ScriptableObject` list of level prefabs (the "play order").
- **PcbTheme.cs** — ScriptableObject: materials, optional model overrides per node/decoration type, and every tunable size/color.
- **PcbTypes.cs** — the core enums (`PcbLayer`, `NodeType`, `DecorType`).
- **PcbMechanics.cs** — 🟡 **scaffolding only, currently unused.** Defines `TraceMechanic` (`CanEnter` / `OnTraversed`) and `NodeMechanic` (`OnSparkArrive` / `OnSparkLeave`) base classes meant for gameplay modifiers (resistor blocking one direction, a switch, a key pickup, a locked door...). No concrete subclass exists yet — this is the intended extension point for adding puzzle mechanics beyond plain routing + flipping.
- **PcbVisualOwner.cs** — tags generated 3D parts with the source node/trace/decoration so scene clicks select the real object.

Editor tooling (`Assets/Script/PCB/Editor/`):
- **PcbAssetSetup.cs** — auto-generates the theme, materials, and sprites under `Assets/PCB` on first load; also upgrades an incomplete theme.
- **PcbLevelEditorWindow.cs** + **PcbLevelEditorWindow.Levels.cs** — `Tools > PCB > Level Editor`. Scene-view tools to place/drag/erase nodes, traces (with 45° auto-routing), and decorations; save/load levels as prefabs; a content-hash based "unsaved changes" indicator; and a **Validate Level** pass that flags: wrong start/goal counts, traces crossing layers without a via, ambiguous overlapping exits, exits only reachable via diagonal input, and vias with traces on only one side.
- **PcbSelectionRedirect.cs** — global hook so clicking a generated visual in the Scene view selects its owning node/trace instead.

UI & game flow (`Assets/Script/UI/`, uGUI + TextMeshPro, kept separate from `PCB/`):
- **GameFlow.cs** — static bridge carrying the chosen level index from the `MainMenu` scene into the gameplay scene (`RequestLevel` / `HasPendingRequest` / `TakeRequestedLevel`).
- **MainMenuController.cs** — Play / Stage Select / Quit on the `MainMenu` scene's main panel.
- **StageSelectController.cs** — builds one button per level from `LevelList` at runtime; Back returns to the main panel.
- **PauseMenu.cs** — Esc / Pause button toggles the pause panel (`Time.timeScale = 0`); Resume / Quit to Menu / Quit App; greys out the Restart button while paused; exposes static `GamePaused` for gameplay scripts.
- **DialogSequence.cs** — `ScriptableObject` (`Create > PCB > Dialog Sequence`): lines of `{ speakerName, portrait, text }`. One per stage, assigned on `Board.dialogSequence`.
- **DialogController.cs** — shows/advances the visual-novel-style dialog panel before play starts; `LevelManager` disables the Spark until it's dismissed.

Scenes: `MainMenu` (build index 0) → `SampleScene` (gameplay, build index 1).

## 5. Level data & workflow

- A level = a `Board` prefab under `Assets/PCB/Levels/`, referenced (in play order) by the single `LevelList.asset` at `Assets/PCB/LevelList.asset`.
- Saved prefabs contain **gameplay data only** — nodes, traces, decorations, and their settings. The 3D look is always regenerated at load (`Board.Rebuild`) from the shared `PcbTheme`, never serialized.
- Levels are authored directly by drawing in the Scene view with the Level Editor window open, not through any external tool or file format.
- Current levels: `Level 1`, `Level 02`, `Level 03`, `Level 04` (4 total as of 2026-09-27).

## 6. Open design space 🟡

Fill these in as they get decided — flagging them so future sessions don't have to
reverse-engineer intent from code again:

- [ ] What gameplay mechanics should `TraceMechanic`/`NodeMechanic` actually cover first? (blocking traces, one-way traces, switches/keys, timed elements, etc.)
- [ ] Target scope: how many levels, roughly what difficulty/length arc?
- [ ] Any progression/meta layer (level select screen, unlocks) beyond the built-in Prev/Restart/Next HUD?
- [ ] Target platform(s) and any performance constraints that should shape `BoardVisuals`' generated-geometry approach.
- [ ] Audio — nothing exists yet (no sound-related scripts).
- [ ] Visual identity beyond the placeholder-generated theme materials in `PcbAssetSetup`.
  Art has been imported into `Assets/Import Asset/` (character `Mesh_Sparky_Animated` + face/palette
  textures; `LevelProps`: capacitors, LEDs, switches ON/OFF, start/end plug nodes, PCB board modules,
  path module; a level scene FBX; portrait icons) but **not tested yet** and not wired into anything —
  likely destination is `PcbTheme`'s optional model slots (`capacitorPrefab`, `viaPrefab`,
  `startPrefab`, `goalPrefab`, `decorationPrefabs`).
