using UnityEngine;

namespace Pcb
{
    /// <summary>Put on generated 3D visuals so clicking them in the Scene view selects the real node/trace.</summary>
    [AddComponentMenu("")]
    public class PcbVisualOwner : MonoBehaviour
    {
        public Component owner;
    }
}
