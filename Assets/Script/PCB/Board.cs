using System.Collections.Generic;
using UnityEngine;

namespace Pcb
{
    /// <summary>
    /// Root of a level. Collects every PcbNode/Trace under it, builds the movement graph
    /// and the 3D look. Runs in edit mode so the board updates while you draw it.
    /// Gameplay happens in board-local XY; the front face is z = 0, the back face z = thickness.
    /// </summary>
    [ExecuteAlways]
    public class Board : MonoBehaviour
    {
        public struct Exit
        {
            public Trace trace;
            public bool reversed;
            public PcbNode target;
            public Vector2 direction;
            public PcbLayer layer;
        }

        public string levelName = "New Level";
        public PcbTheme theme;
        [Min(0.1f)] public float cellSize = 0.5f;
        public Vector2Int sizeInCells = new Vector2Int(32, 20);
        [Tooltip("How closely input must match a trace to take it (1 = exact, 0.5 = within 60 degrees).")]
        [Range(0.1f, 1f)] public float inputTolerance = 0.5f;
        [Tooltip("Side being edited in the Level Editor.")]
        public PcbLayer editorView = PcbLayer.Front;
        [HideInInspector] public string savedPath; // prefab this board was last saved to / loaded from (editor use)

        readonly List<PcbNode> nodes = new List<PcbNode>();
        readonly List<Trace> traces = new List<Trace>();
        readonly Dictionary<PcbNode, List<Exit>> exits = new Dictionary<PcbNode, List<Exit>>();
        static readonly List<Exit> NoExits = new List<Exit>();

        readonly List<Renderer> frontRenderers = new List<Renderer>();
        readonly List<Renderer> backRenderers = new List<Renderer>();
        readonly List<Renderer> sharedRenderers = new List<Renderer>();
        Transform visuals;
        int visualsSignature;
        bool showBothSides;

        public PcbLayer View { get; private set; }
        public IReadOnlyList<PcbNode> Nodes => nodes;
        public IReadOnlyList<Trace> Traces => traces;
        public Vector2 Size => (Vector2)sizeInCells * cellSize;
        public float Thickness => theme ? theme.boardThickness : 0.16f;
        /// <summary>World-space centre of the board volume.</summary>
        public Vector3 Center => transform.TransformPoint(new Vector3(Size.x * 0.5f, Size.y * 0.5f, Thickness * 0.5f));

        void Awake()
        {
            if (!Application.isPlaying) return;
            showBothSides = false;
            Rebuild();
        }

        void Update()
        {
            if (Application.isPlaying) return;
            showBothSides = true; // editing: everything visible, the editor draws the hidden side as a ghost
            Rebuild();
        }

        /// <summary>Re-collects nodes and traces, rebuilds the movement graph and (if anything changed) the 3D look.</summary>
        public void Rebuild()
        {
            GetComponentsInChildren(true, nodes);
            GetComponentsInChildren(true, traces);
            RemoveLegacyComponents();
            BuildGraph();

            if (!theme || !theme.IsComplete) return;
            int signature = ComputeSignature();
            if (visuals && signature == visualsSignature) return;
            DestroyVisuals();
            visuals = BoardVisuals.Build(this, frontRenderers, backRenderers, sharedRenderers);
            visualsSignature = signature;
            ApplyVisibility();
        }

        /// <summary>Removes the generated 3D objects (the editor does this before saving a level).</summary>
        public void DestroyVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == BoardVisuals.RootName) Kill(child.gameObject);
            }
            visuals = null;
            frontRenderers.Clear();
            backRenderers.Clear();
            sharedRenderers.Clear();
        }

        void BuildGraph()
        {
            exits.Clear();
            foreach (var t in traces)
            {
                if (!t.IsValid) continue;
                AddExit(t.from, t, false, t.to);
                AddExit(t.to, t, true, t.from);
            }
        }

        void AddExit(PcbNode node, Trace trace, bool reversed, PcbNode target)
        {
            if (!exits.TryGetValue(node, out var list)) exits[node] = list = new List<Exit>();
            list.Add(new Exit
            {
                trace = trace,
                reversed = reversed,
                target = target,
                direction = trace.ExitDirection(this, reversed),
                layer = trace.layer
            });
        }

        public IReadOnlyList<Exit> GetExits(PcbNode node) =>
            node && exits.TryGetValue(node, out var list) ? list : NoExits;

        /// <summary>Picks the exit on 'layer' that best matches a board-space direction.</summary>
        public bool TryPickExit(PcbNode node, PcbLayer layer, Vector2 direction, out Exit exit)
        {
            exit = default;
            bool found = false;
            float best = inputTolerance;
            Vector2 dir = direction.normalized;
            foreach (var e in GetExits(node))
            {
                if (e.layer != layer) continue;
                float d = Vector2.Dot(e.direction, dir);
                if (d > best) { best = d; exit = e; found = true; }
            }
            return found;
        }

        // ---------------------------------------------------------------- sides

        /// <summary>Sets the side the player is on. In play mode the other side is hidden.</summary>
        public void SetView(PcbLayer layer)
        {
            View = layer;
            showBothSides = !Application.isPlaying;
            ApplyVisibility();
        }

        /// <summary>Shows both sides (used while the board is turning over).</summary>
        public void ShowBothSides()
        {
            showBothSides = true;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            foreach (var r in frontRenderers) if (r) r.enabled = showBothSides || View == PcbLayer.Front;
            foreach (var r in backRenderers) if (r) r.enabled = showBothSides || View == PcbLayer.Back;
        }

        // ---------------------------------------------------------------- coordinates

        public Vector2 NodePosition(PcbNode node) => WorldToLocal(node.transform.position);
        public Vector2 WorldToLocal(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 LocalToWorld(Vector2 local) => transform.TransformPoint(local);

        /// <summary>World position on (or above) the surface of one side.</summary>
        public Vector3 SurfaceToWorld(Vector2 local, PcbLayer layer, float height = 0f) =>
            transform.TransformPoint(new Vector3(local.x, local.y,
                BoardVisuals.Surface(layer, Thickness) + BoardVisuals.Out(layer) * height));

        public Vector2 SnapLocal(Vector2 local)
        {
            float x = Mathf.Clamp(Mathf.Round(local.x / cellSize), 0, sizeInCells.x);
            float y = Mathf.Clamp(Mathf.Round(local.y / cellSize), 0, sizeInCells.y);
            return new Vector2(x, y) * cellSize;
        }

        // ---------------------------------------------------------------- helpers

        int ComputeSignature()
        {
            unchecked
            {
                int h = 17;
                void Add(int v) => h = h * 31 + v;
                void AddV(Vector2 v) { Add(Mathf.RoundToInt(v.x * 1000f)); Add(Mathf.RoundToInt(v.y * 1000f)); }

                Add(Id(theme));
                if (!Application.isPlaying) Add(JsonUtility.ToJson(theme).GetHashCode()); // live theme tweaks while editing
                Add(sizeInCells.x); Add(sizeInCells.y); Add(Mathf.RoundToInt(cellSize * 1000f));
                foreach (var n in nodes)
                {
                    Add(Id(n)); Add((int)n.type); Add((int)n.layer);
                    AddV(NodePosition(n)); AddV(n.chipSize); Add(n.name.GetHashCode());
                }
                foreach (var t in traces)
                {
                    Add(Id(t)); Add(Id(t.from)); Add(Id(t.to));
                    Add((int)t.layer); Add(t.bends.Count);
                    foreach (var b in t.bends) AddV(b);
                }
                return h;
            }
        }

        /// <summary>Levels made with the first 2D version carry sprites and lines; strip them.</summary>
        void RemoveLegacyComponents()
        {
            foreach (var n in nodes)
            {
                if (n.TryGetComponent(out SpriteRenderer sr)) Kill(sr);
                if (n.transform.localScale != Vector3.one && !Application.isPlaying) n.transform.localScale = Vector3.one;
            }
            foreach (var t in traces)
                if (t.TryGetComponent(out LineRenderer lr)) Kill(lr);
            var background = transform.Find("Background");
            if (background && background.GetComponent<SpriteRenderer>()) Kill(background.gameObject);
        }

        // Identity hash for change detection. Avoids GetInstanceID(), which newer Unity 6 versions reject.
        static int Id(Object o) => o ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o) : 0;

        static void Kill(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
