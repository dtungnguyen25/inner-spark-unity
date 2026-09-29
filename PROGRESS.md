# Progress Log

Tracks what's been done and the current state of the project. Newest entries on top.
See [DESIGN.md](DESIGN.md) for the design reference / architecture map.

---

## 2026-09-29 (evening) — multi-switch AND gate

**Changes made:**
- **New: `AndGateMechanic.cs`** (`Assets/Script/PCB/`, subclasses `GateMechanic`) — for gates that
  need *several* switches all on before they open, not just one. Existing `GateMechanic`/
  `SwitchMechanic` are completely untouched, so every already-wired single-switch gate (e.g.
  `Level 04`'s) keeps working exactly as before.
  - Why a new class instead of extending `GateMechanic`: single-switch gates are driven by the
    switch calling `SetOpen(bool)` directly (an unconditional "set to this state," no combining
    logic) — wiring *two* switches to the same `SetOpen` the same way would just mean "whichever
    was pressed last wins," not "both must be on." `AndGateMechanic` instead holds its own list of
    `SwitchMechanic`s, subscribes to each one's `onToggle` itself in `OnEnable`, and only calls the
    inherited `SetOpen(true)` once every switch in the list is on.
  - Setup is simpler than the single-switch case too: assign the switches to the `Switches` list on
    the gate and you're done — no per-switch Inspector event wiring needed, the gate does the
    subscribing itself.
  - Inherits `GateMechanic`'s placeholder-visual behavior (`closedVisual`, auto red cube if unset)
    unchanged; the designer already has real art for both the single- and multi-switch cases to
    swap in.

### Not done yet / pending
- Not tested in Unity yet — needs a scene with 2+ switches wired to one `AndGateMechanic` to confirm.
- Assumed "must ALL be on" (AND) reading of the request; flag if OR ("any one opens it") was
  actually wanted instead — that'd be a different (smaller) change.

## 2026-09-29 (later) — scrollable Stage Select, Via model bug

**Changes made:**
- **Bug fix — Via node invisible from the back when a custom model is assigned.**
  `theme.viaPrefab` (now `Via Demo.prefab`, a thin disc) is placed at the board's *front*
  surface (`z = 0`) like every other node model, but a Via is meant to sit **through** the
  board and poke out both faces — the old built-in procedural via shape did that correctly
  by offsetting itself to the board's mid-thickness internally, but that offset was never
  applied to the model-override path, so the disc sat almost entirely on the front and
  never reached the back surface. Fixed in `BoardVisuals.BuildNode`: the instantiated model
  is now shifted `+t * 0.5f` on its local Z when the node `IsVia`, matching where the
  built-in shape already centers itself. Code-only, no scene changes needed.
- **Stage Select list is now scrollable** (designer, manual Editor setup): existing
  `ButtonContainer` re-parented into a `Scroll View > Viewport`, given a `Content Size
  Fitter` (Vertical = Preferred Size) so it grows with the level count, `Scroll Rect`
  wired to it, vertical-only. No code involved — `StageSelectController` just adds
  buttons as children of `ButtonContainer` regardless of what wraps it.

### Not done yet / pending
- Confirm in Unity that the via now renders correctly on both sides after the fix above.
- Switch mechanic: designer asked about placing a `Switch` node from the Level Editor —
  see the next entry / DESIGN.md for what's possible today vs. what still needs the dev.

## 2026-09-29 — catching up on teammates' work

**Session summary:** Designer asked me to check what changed since the last logged entry.
Read through git history (nothing here was written by me) and updated DESIGN.md to match;
no code changes this entry, just documentation catching up to what's already in `main`.

### What landed (via `git log`, not seen in PROGRESS.md before now)
- **First concrete gameplay mechanic — Switch/Gate** (Truong Quang Nghia, branch `feat/switch`,
  merged into `main` today): new `NodeType.Switch`, `SwitchMechanic.cs` (`NodeMechanic` —
  `Toggle()` fires `UnityEvent<bool> onToggle`), `GateMechanic.cs` (`TraceMechanic` — blocks
  entry unless `isOpen`, `SetOpen(bool)`). `Spark.TryFlip()` now special-cases `Switch` nodes:
  the flip input calls `Toggle()` instead of turning the board over. `Board`/`PcbTheme`/
  `BoardVisuals` extended with a `switchNode`/`switchPrefab`/`switchVariants` slot (placeholder
  shape: a metal cylinder with a disc on top, like a squat capacitor). **This is the first thing
  to ever come out of the `TraceMechanic`/`NodeMechanic` extension points** — see DESIGN.md § 4.
  - Wired and working in `Level 04`: a Switch's `onToggle` → a Gate's `SetOpen`.
  - `Level 04` itself was heavily rebuilt alongside this (32×20 → 10×10, ~2450 lines of old
    node/trace/decoration data removed) — reads like it became a small test bed for the
    mechanic rather than a finished level; worth confirming with the dev before treating it
    as a real level 4 again.
  - Rough edges worth a look, not fixed: leftover `Debug.Log` calls in both scripts;
    `GateMechanic.CanEnter` ignores the `reversed` parameter (blocks both directions —
    may or may not be intended); `GateMechanic` auto-spawns a plain red cube if no
    `closedVisual` is assigned (fine as a placeholder, just flagging it's not final art).
- **Large art/VFX asset drop** ("Andrii", `andrii.nhuien@gameloft.com`, commit `5a8ba4f`,
  191 files, art-only — no `.cs` changes): per-color materials for LEDs, PCB boards and
  nodes; more `PlugNode` size variants (2/3/4); Sparky's own materials + textures; three
  VFX prefabs — `VFX_BurstOfSparks`, `VFX_Sparky_Idle`, `VFX_Sparky_Trail` (likely meant for
  the Spark's idle/movement feel, given the names). **Imported but not wired into anything
  yet** — same status as the first art batch from two days ago.
- Both branches merged into `main` by the designer just now (merge commit `0aeb810`); no
  conflicts.

### Not done yet / pending
- Everything under "Not done yet" in the two entries below is still open (Switch/Gate is
  new since then, doesn't change that list).
- Nothing from either art drop is wired into `PcbTheme` / the Look-Catalog system yet.
- Worth deciding whether `Level 04`'s rebuild is meant to become the real level 4, or if the
  switch/gate testing should move to its own scratch level.

## 2026-09-28 (evening) — art investigation + model slots & per-level look system

**Session summary:** Checked the imported art, then built a way to use it: model slots for the
board and traces, and a system for choosing different models per level and per node, driven from
the Level Editor.

### Findings (no code changes)
- **`Mesh_Sparky_Animated.fbx` has no mesh in it.** Parsed the file directly: 39 bones +
  4 animations (`Sparky_Idle`, `Sparky_Move_Start`, `Sparky_Move_Finish`, `Sparky_Win`), but
  0 geometry, 0 skin deformers, 0 materials. The import settings are fine — it's an export problem
  (probably "Selected Objects" with only the armature selected). **Ask the artist to re-export with
  the mesh + armature**; replace the `.fbx` and keep its `.meta`.
- Imported model sizes (Unity units): `Path_module` 1.0 long × 0.15 wide × 0.10 tall;
  `PCBboard_module*` a 1×1 tile, 0.02 thick. All exported Y-up (lying flat), so they need a
  wrapper prefab rotated to face −Z.
- `Level/LevelScene_3D.fbx_Scene.fbx` is a town scene (trees, cars, vending machine…), not a PCB
  level — ask the artist whether it's background art or included by mistake.

### Code changes
- **Board tile + trace + trace bend model slots** (`PcbTheme.boardTilePrefab`, `tracePrefab`,
  `traceBendPrefab`, plus `boardTileSize`). Unlike node models (placed at authored size), these
  are **resized to fit** the theme's numbers (`BoardVisuals.Fit`), so `traceWidth`/`traceHeight`/
  `boardThickness` stay the single source of truth for gameplay (spark height, camera framing).
  - Board: tile repeated across the board + margin, count rounded to a whole number.
  - Trace: one stretched piece per straight segment; with a bend prefab, pieces stop at the bend
    and the bend piece is placed there; without one, pieces overlap half a width at corners.
  - Empty slots = the old built-in shapes, so nothing changed for existing levels.
- **Per-level / per-node look system.** Which model wins: node's own `model` → level's
  `Board.look` → theme slot.
  - `Board.BoardLook look` (saved in the level prefab): board tile, trace, bend, and a default
    model per node type. Resolution helpers on `Board` (`NodeModel`, `TraceModel`…), which
    `BoardVisuals` now uses. Added to `Board.ComputeSignature` so edits redraw immediately.
  - `PcbNode.model` / `PcbDecoration.model`: optional per-object override.
  - `PcbTheme` **Catalog** (`capacitorVariants`, `viaVariants`, `startVariants`, `goalVariants`,
    `boardTileVariants`, `traceVariants`, `traceBendVariants`): the choices the editor offers.
    Decoration variants = several `decorationPrefabs` entries of the same type (first = default).
- **Level Editor** (new file `PcbLevelEditorWindow.Look.cs`):
  - **Look (this level)** section: dropdowns for each look field + **Copy Look From** another level.
  - **Paint** tool: pick Nodes/Decorations + type + a thumbnail, click to paint, Shift+Click to
    reset, **Paint All / Reset All** buttons. Only paints objects of the chosen type. Undo works.
  - Ctrl+Click type change (Node/Decor tools) now clears a painted model from the old type.
  - Fixed the stale Play-mode note (mentioned the removed Prev/Next keys).
- **Node rotation:** `PcbNode.rotationDegrees` spins a node's model (or built-in shapes) around
  the board normal; visual only, trace directions unaffected. Level Editor Node tool has a
  Rotation slider (15° steps) applied to new nodes; Shift+Click applies it to an existing node.
  Goal chip click area / outlines follow the rotation. **Tested by the designer in Unity: working.**
- Runtime + editor code compiled with `dotnet build`; **not yet tested in Unity by me**.

### Designer-side setup (seen in the project)
New prefabs in `Assets/PCB/Prefabs/`: `Board_Tile`, `Trace_Path`, `Capacitor Demo`,
`Capacitor Demo 1`, `Start Demo`, `Goal Demo`, `Via Demo` (old `Capacitor F` / `Start F` /
`Goal B` / `Via` prefabs removed). Guided on adding capacitor variants via the theme's Catalog.

### Not done yet / pending
- Test in Unity: board tiles + traces + bends on both sides, Paint tool, Look section, Copy Look,
  saving/reloading a level keeps painted models, unsaved-changes indicator.
- Artist: re-export the character FBX with the mesh; clarify the town scene FBX.
- Open questions from the look design: a "random from catalog" option for level defaults?
  Are the Switch ON/OFF and LED models cosmetic (→ catalog) or future mechanics
  (→ `TraceMechanic`/`NodeMechanic`)?
- Possible UX tweak: the Paint palette shows the default model twice ("Default (X)" and "X") —
  could hide the second when it matches.
- Pause-input fix from earlier today still worth a quick re-test.

## 2026-09-28 (later)

**Changes made:**
- **Bug fix — input leaking through the pause menu:** moves/flips pressed while paused were
  registered and played the moment you unpaused. `PauseMenu` now exposes a static
  `GamePaused`; `Spark.ReadInput` drops any queued move/flip while it's true (and keeps
  tracking the held direction, so a key held through Resume doesn't fire either), and
  `BoardRig` ignores mouse-drag inspection while paused. `LevelManager` already skipped
  restart/confirm while paused.
- `DESIGN.md` brought up to date: Controls table (Prev/Next removed, Esc = pause, Restart/Pause
  buttons, dialog Continue), UI scripts + scenes added to the architecture map, corrected
  `PcbDecoration.cs` location.
- Logged the art import (commit `6f9039d`, `Assets/Import Asset/`: Sparky character, level props,
  level scene FBX, portraits) — **imported but not tested yet**, nothing wired up to it.
- Noted: a second dialog asset `Lvl2_Intro` exists alongside `Lvl1_Intro`.

**To test:** pause mid-level, press directions / Space / drag the mouse, resume → spark
should not move or flip until a fresh input after resuming.

## 2026-09-28

**Session summary:** Full day building out the Main Menu / Stage Select / Pause / Dialog
system end to end: planned it, wrote all the scripts, the designer did the manual Editor
scene setup, then we hunted down three real runtime bugs surfaced by testing, and finished
by adding a couple of extra player-facing UI requests. Long session — details below,
grouped by phase rather than chronologically.

### Design decisions
uGUI for all new UI (not IMGUI, to stay editable/reskinnable), a separate `MainMenu`
scene (not an overlay in the gameplay scene), a pause overlay rather than a HUD button
for quitting to menu, visual-novel-style dialog (name + portrait + text) authored as a
`ScriptableObject` per stage, Stage Select auto-populated from the existing `LevelList`,
and all new scripts kept in `Assets/Script/UI/` separate from `Assets/Script/PCB/` to
avoid colliding with the dev's work there.

### Scripts added (`Assets/Script/UI/`)
- `GameFlow.cs` — static bridge carrying the requested level index from the Main Menu
  scene into the gameplay scene. `RequestLevel(index)` sets it; `HasPendingRequest`
  (added later, see bug #3 below) lets `LevelManager` check before consuming it;
  `TakeRequestedLevel(fallback)` consumes it, falling back to `LevelManager.startLevel`
  if nothing requested it, so direct in-editor testing of the gameplay scene is unaffected.
- `DialogSequence.cs` — `ScriptableObject` (`Create > PCB > Dialog Sequence`): an array
  of `{ speakerName, portrait, text }` lines. One asset per stage, assigned via the
  optional `Board.dialogSequence` field in the regular Inspector (not the custom PCB
  Level Editor window, which doesn't expose it).
- `DialogController.cs` — shows/advances a modal dialog panel; exposes `IsShowing`.
  Advance is driven entirely by the Continue button (click, or keyboard/gamepad Submit
  once it's the selected UI element) — no separate key bindings, avoids double-advance.
- `PauseMenu.cs` — Esc (or a Pause button, added later) toggles a pause panel
  (`Time.timeScale = 0`); `Resume()` / `QuitToMenu()` (loads the MainMenu scene) /
  `QuitApp()` for the panel's three buttons. Also now optionally disables a Restart
  button while paused (see below).
- `MainMenuController.cs` — Play / Stage Select / Quit for the MainMenu scene's main panel.
- `StageSelectController.cs` — builds one button per level **from the existing
  `LevelList`** at runtime (no manual upkeep as levels are added/reordered); Back button
  returns to the main panel.

### Existing files touched
- `Board.cs` — one new optional field: `public DialogSequence dialogSequence;`. Empty =
  no dialog, level behaves exactly as before.
- `LevelManager.cs` — reads `GameFlow`/`dialogController`/`pauseMenu` as described above;
  removed the old `Esc → Application.Quit()` binding entirely (now `PauseMenu`'s job);
  **removed the whole Prev/Next feature** (IMGUI buttons, keyboard/gamepad bindings,
  the now-dead `Previous()` method, the "Level: [ ]" HUD hint) per a later request —
  `Next()` itself stays, since winning still uses it to advance; `Spawn()` now takes a
  `showDialog` bool: `GoTo()` (Play/Stage Select/Next/initial load) passes `true`,
  `Restart()` passes `false`, so replaying a level you've already seen the intro for
  drops straight into gameplay instead of showing the dialog again.

### Manual Editor setup (done by the designer this session)
`MainMenu.unity` created (Canvas/EventSystem with the Input System UI Input Module,
main panel, stage-select panel + button template) and added to Build Settings at index 0
(`SampleScene` at index 1); `SampleScene` got its own Canvas/EventSystem plus `PauseMenu`
and `DialogController` GameObjects, wired into `LevelManager`; TextMeshPro Essentials
imported; a test `DialogSequence` created and assigned to Level 1.

### Bugs found and fixed during testing
1. **Edit-mode crash:** `MissingReferenceException` in `BoardVisuals.BuildNode` while
   actively editing a level — a node/trace/decoration destroyed mid-`Rebuild()` (e.g. via
   Undo or the Erase tool) wasn't being skipped. Added `if (!x) continue;` guards in
   `Board.ComputeSignature`, `Board.BuildGraph`, `Board.RemoveLegacyComponents`, and
   `BoardVisuals.Build` — mirrors the `Trace.IsValid` guard pattern already used elsewhere.
2. **MainMenu buttons rendering blank:** Stage Select / Quit / Back showed no text even
   with correct-looking Font Asset/Material in the Inspector. Root cause, found by reading
   the `.unity` file directly: TMP's internal `m_hasFontAssetChanged` flag was stuck at
   `1` on all three (vs `0` on the working Play button) — the mesh was never actually
   rebuilt after the font was reassigned via Inspector. Fixed by retyping the Text Input
   content directly (forces a rebuild) and saving outside Play Mode. Also fixed along the
   way: `Back` had no font asset at all and still said "Button"; `Quit` was pointed at
   `LiberationSans SDF - Fallback` instead of the real font asset.
3. **Stage Select always landed on level 1:** `LevelManager.Start()`'s "a Board is sitting
   in this scene + we're in the Editor ⇒ must be direct editor-testing" heuristic always
   won, since `SampleScene` always has a Board in it for editing — it silently ignored
   whatever Stage Select actually requested. Fixed by adding `GameFlow.HasPendingRequest`
   and checking it first in `Start()`, before that heuristic runs.
4. **Dialog levels: no dialog shown, movement locked forever:** `DialogController` lived
   directly on its own panel GameObject, which starts disabled in the scene (correctly,
   per earlier advice). Because it starts disabled, `Awake()` is deferred until the first
   `Show()` call reactivates it — but `Awake()` unconditionally called
   `panel.SetActive(false)` again, immediately undoing that very activation from inside
   itself. Fixed by removing the redundant `SetActive(false)` from `Awake()` (the panel's
   saved inactive state already covers "starts hidden"). Confirmed `PauseMenu` doesn't
   have this problem — its panel is a separate GameObject, not itself.

### New player-facing features added (end of session)
- Real uGUI **Restart** button in `SampleScene` (OnClick → `LevelManager.Restart()`),
  alongside the existing `R` key.
- Real uGUI **Pause** button (OnClick → `PauseMenu.Toggle()`), equivalent to `Esc`.
- `PauseMenu.restartButton` (optional field): `Pause()`/`Resume()` toggle its
  `interactable` state so Restart can't be clicked out from under the pause panel.
- Restart skips the dialog on replay (see `Spawn(showDialog)` above).

### Not done yet / pending for next session
- Designer still needs to drag `RestartButton` onto `PauseMenu`'s new **Restart Button**
  field in the Inspector — guided, not yet confirmed done.
- Worth a full end-to-end re-test in one pass: Stage Select into *every* level (not just
  the first couple tested), dialog shows on first entry and is skipped on restart, pause
  button + restart-button-disable-while-paused all together.
- `TraceMechanic`/`NodeMechanic` gameplay mechanics are still unbuilt (unchanged from
  the previous session — see DESIGN.md § Open design space).
- Local-only settings diffs from the previous session are still untouched.

## 2026-09-27

**Session summary:** First full scan of the project (no prior docs existed). Reviewed
every script, the scene, and the level list; fixed two small issues; created this doc
and DESIGN.md.

**Changes made:**
- Deleted `Assets/Script/Brainstorm.cs` (+ its `.meta`) — an empty, unused default
  MonoBehaviour stub not referenced by any scene or prefab.
- Fixed a minor leak in `LevelManager.Spawn()`: it subscribed
  `spark.Arrived += OnArrived` on every spawn without ever unsubscribing from the
  previous spark. Harmless in practice (the old Spark is destroyed with its rig before
  the new one exists), but now unsubscribes explicitly before creating the next one.
- Added `DESIGN.md` and this file — first project documentation of any kind beyond
  Unity's default URP template readme.

**State of the project at this point:**
- Core gameplay loop is complete and working: `Board` / `Trace` / `PcbNode` / `Spark` /
  `LevelManager` / `BoardRig` all wired together, front/back flipping via Via nodes
  functional, win detection + HUD (Prev/Restart/Next) functional.
- Custom in-editor level design tool (`Tools > PCB > Level Editor`) is complete:
  place/drag/erase nodes, traces (with 45° auto-routing), decorations; save/load levels
  as prefabs; unsaved-changes tracking; a level validator (start/goal counts, cross-layer
  trace errors, ambiguous exits, diagonal-only-reachable exits, dead-end vias).
- 4 levels exist and are registered in `LevelList.asset`: `Level 1`, `Level 02`,
  `Level 03`, `Level 04`. `Level 04` is the one currently open in `SampleScene.unity`.
- `PcbMechanics.cs` defines `TraceMechanic` / `NodeMechanic` extension points for
  gameplay modifiers (blocking traces, switches, pickups, etc.) but **no concrete
  mechanic has been implemented yet** — routing + side-flipping is the only mechanic
  in the game so far.
- No audio in the project yet.
- No compile errors, no TODO/FIXME markers found anywhere in the codebase at time of scan.

**Known minor items, not yet acted on:**
- Working tree has a handful of local-only, non-code diffs (`.vscode/settings.json`,
  `ProjectSettings/EditorBuildSettings.asset`, `UserSettings/...`) plus an untracked
  `inner-spark-unity.slnx` — these look like IDE/solution regeneration after the project
  was renamed from `spark-kun` to `inner-spark-unity`. Not touched; harmless either way.

**Suggested next steps** (not started):
- Decide and implement the first concrete `TraceMechanic`/`NodeMechanic` (see DESIGN.md § Open design space).
- Consider committing the pending local settings changes or adding them to `.gitignore` if they're meant to stay machine-local.
