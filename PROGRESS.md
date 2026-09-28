# Progress Log

Tracks what's been done and the current state of the project. Newest entries on top.
See [DESIGN.md](DESIGN.md) for the design reference / architecture map.

---

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
