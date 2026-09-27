using System.Collections.Generic;
using UnityEngine;

namespace Pcb
{
    /// <summary>
    /// Builds the 3D look of a board from its nodes and traces using Unity's built-in meshes.
    /// Board space: the front face is at z = 0 facing -Z (towards the camera), the back face at z = thickness.
    /// Everything generated is marked DontSave, so levels and scenes only store the gameplay data.
    /// </summary>
    public static class BoardVisuals
    {
        public const string RootName = "~Visuals";

        static Mesh cube, cylinder, sphere;
        public static Mesh Cube => cube ? cube : cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        public static Mesh Cylinder => cylinder ? cylinder : cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        public static Mesh Sphere => sphere ? sphere : sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        // Built-in cylinder is 2 units tall along Y; this stands it up along the board normal (Z).
        static readonly Quaternion Upright = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>Direction pointing away from the board on that side (-1 front, +1 back).</summary>
        public static float Out(PcbLayer layer) => layer == PcbLayer.Front ? -1f : 1f;

        /// <summary>Z of the board surface on that side.</summary>
        public static float Surface(PcbLayer layer, float thickness) => layer == PcbLayer.Front ? 0f : thickness;

        public static Transform Build(Board board, List<Renderer> front, List<Renderer> back, List<Renderer> both)
        {
            var theme = board.theme;
            front.Clear(); back.Clear(); both.Clear();
            var root = new GameObject(RootName).transform;
            root.SetParent(board.transform, false);

            float t = theme.boardThickness;
            Vector2 size = board.Size;
            var slab = Group(root, "Board", Vector3.zero, board);
            Part(slab, Cube, new Vector3(size.x * 0.5f, size.y * 0.5f, t * 0.5f), Quaternion.identity,
                new Vector3(size.x + theme.boardMargin * 2f, size.y + theme.boardMargin * 2f, t), theme.boardMaterial, both);

            var path = new List<Vector2>();
            foreach (var trace in board.Traces)
            {
                if (!trace.IsValid) continue;
                trace.GetPath(board, false, path);
                BuildTrace(root, trace, path, theme, trace.layer == PcbLayer.Front ? front : back);
            }
            foreach (var node in board.Nodes)
                BuildNode(root, board, node, theme, node.IsVia ? both : node.layer == PcbLayer.Front ? front : back);

            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                tr.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                foreach (var c in tr.GetComponents<Component>()) c.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            }
            return root;
        }

        static void BuildTrace(Transform root, Trace trace, List<Vector2> path, PcbTheme theme, List<Renderer> list)
        {
            var g = Group(root, trace.name, Vector3.zero, trace);
            float w = theme.traceWidth, h = theme.traceHeight;
            float z = Surface(trace.layer, theme.boardThickness) + Out(trace.layer) * h * 0.5f;
            for (int i = 0; i < path.Count; i++)
            {
                // Round joint at every point, flat strip for every segment.
                Part(g, Cylinder, new Vector3(path[i].x, path[i].y, z), Upright, new Vector3(w, h * 0.5f, w), theme.copperMaterial, list);
                if (i == path.Count - 1) break;
                Vector2 a = path[i], b = path[i + 1], d = b - a;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                Part(g, Cube, new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, z), Quaternion.Euler(0f, 0f, angle),
                    new Vector3(d.magnitude, w, h), theme.copperMaterial, list);
            }
        }

        static void BuildNode(Transform root, Board board, PcbNode node, PcbTheme theme, List<Renderer> list)
        {
            float t = theme.boardThickness;
            Vector2 p = board.NodePosition(node);
            PcbLayer side = node.IsVia ? PcbLayer.Front : node.layer;
            float o = Out(side);
            var g = Group(root, node.name, new Vector3(p.x, p.y, node.IsVia ? 0f : Surface(side, t)), node);

            GameObject model = node.type switch
            {
                NodeType.Capacitor => theme.capacitorPrefab,
                NodeType.Via => theme.viaPrefab,
                NodeType.Start => theme.startPrefab,
                _ => theme.goalPrefab
            };
            if (model)
            {
                var m = Object.Instantiate(model, g, false);
                m.transform.localRotation = side == PcbLayer.Back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                list.AddRange(m.GetComponentsInChildren<Renderer>(true));
                return;
            }

            switch (node.type)
            {
                case NodeType.Capacitor:
                {
                    float d = theme.capacitorSize, h = theme.capacitorHeight;
                    Part(g, Cylinder, new Vector3(0f, 0f, o * h * 0.5f), Upright, new Vector3(d, h * 0.5f, d), theme.capacitorMaterial, list);
                    Part(g, Cylinder, new Vector3(0f, 0f, o * (h + 0.004f)), Upright, new Vector3(d * 0.8f, 0.004f, d * 0.8f), theme.metalMaterial, list);
                    break;
                }
                case NodeType.Via:
                {
                    float d = theme.viaSize, half = t * 0.5f + theme.viaLip;
                    Part(g, Cylinder, new Vector3(0f, 0f, t * 0.5f), Upright, new Vector3(d, half, d), theme.metalMaterial, list);
                    Part(g, Cylinder, new Vector3(0f, 0f, t * 0.5f), Upright, new Vector3(d * 0.45f, half + 0.003f, d * 0.45f), theme.holeMaterial, list);
                    break;
                }
                case NodeType.Start:
                {
                    Vector3 s = theme.plugSize;
                    Part(g, Cube, new Vector3(0f, 0f, o * s.z * 0.5f), Quaternion.identity, s, theme.plugMaterial, list);
                    for (int i = -1; i <= 1; i += 2)
                        Part(g, Cylinder, new Vector3(i * s.x * 0.22f, 0f, o * (s.z + 0.08f)), Upright,
                            new Vector3(0.06f, 0.08f, 0.06f), theme.metalMaterial, list);
                    break;
                }
                case NodeType.Goal:
                {
                    Vector2 c = node.chipSize;
                    float h = theme.chipHeight, lift = 0.02f;
                    Part(g, Cube, new Vector3(0f, 0f, o * (lift + h * 0.5f)), Quaternion.identity, new Vector3(c.x, c.y, h), theme.chipMaterial, list);
                    // Pin-1 dot
                    Part(g, Cylinder, new Vector3(-c.x * 0.5f + 0.14f, c.y * 0.5f - 0.14f, o * (lift + h + 0.002f)), Upright,
                        new Vector3(0.08f, 0.002f, 0.08f), theme.holeMaterial, list);
                    // Legs along the two long sides
                    bool tall = c.y >= c.x;
                    float along = tall ? c.y : c.x, across = tall ? c.x : c.y;
                    int count = Mathf.Max(2, Mathf.FloorToInt(along / 0.25f));
                    float step = along / count;
                    for (int i = 0; i < count; i++)
                    {
                        float a = -along * 0.5f + step * (i + 0.5f);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            float b = s * (across * 0.5f + 0.05f);
                            var pos = tall ? new Vector3(b, a, o * 0.025f) : new Vector3(a, b, o * 0.025f);
                            var scale = tall ? new Vector3(0.14f, 0.07f, 0.05f) : new Vector3(0.07f, 0.14f, 0.05f);
                            Part(g, Cube, pos, Quaternion.identity, scale, theme.metalMaterial, list);
                        }
                    }
                    break;
                }
            }
        }

        static Transform Group(Transform parent, string name, Vector3 localPosition, Component owner)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<PcbVisualOwner>().owner = owner;
            return go.transform;
        }

        public static MeshRenderer Part(Transform parent, Mesh mesh, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, Material material, List<Renderer> list)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            list?.Add(r);
            return r;
        }
    }
}
