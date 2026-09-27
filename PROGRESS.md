# Progress Log

Tracks what's been done and the current state of the project. Newest entries on top.
See [DESIGN.md](DESIGN.md) for the design reference / architecture map.

---

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
