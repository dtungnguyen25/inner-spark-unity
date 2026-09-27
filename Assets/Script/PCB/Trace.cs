using System.Collections.Generic;
using UnityEngine;

namespace Pcb
{
    /// <summary>A copper line between two nodes on one side of the board. The spark follows its bends automatically.</summary>
    public class Trace : MonoBehaviour
    {
        public PcbNode from;
        public PcbNode to;
        public PcbLayer layer = PcbLayer.Front;
        [Tooltip("Bend points between 'from' and 'to', in board-local space.")]
        public List<Vector2> bends = new List<Vector2>();

        readonly List<Vector2> scratch = new List<Vector2>();

        public bool IsValid => from && to && from != to;

        /// <summary>Board-local points from one end to the other, without duplicates.</summary>
        public void GetPath(Board board, bool reversed, List<Vector2> result)
        {
            result.Clear();
            if (!IsValid) return;
            Append(result, board.NodePosition(from));
            foreach (var b in bends) Append(result, b);
            Append(result, board.NodePosition(to));
            if (reversed) result.Reverse();
        }

        public void GetWorldPath(Board board, bool reversed, List<Vector3> result)
        {
            GetPath(board, reversed, scratch);
            result.Clear();
            foreach (var p in scratch) result.Add(board.LocalToWorld(p));
        }

        public Vector2 ExitDirection(Board board, bool reversed)
        {
            GetPath(board, reversed, scratch);
            return scratch.Count < 2 ? Vector2.zero : (scratch[1] - scratch[0]).normalized;
        }

        /// <summary>Distance from a board-local point to this trace.</summary>
        public float DistanceTo(Board board, Vector2 local)
        {
            GetPath(board, false, scratch);
            float best = float.MaxValue;
            for (int i = 0; i < scratch.Count - 1; i++)
            {
                Vector2 a = scratch[i], ab = scratch[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(local - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(local, a + ab * t));
            }
            return best;
        }

        static void Append(List<Vector2> list, Vector2 p)
        {
            if (list.Count == 0 || (list[list.Count - 1] - p).sqrMagnitude > 1e-6f) list.Add(p);
        }
    }
}
