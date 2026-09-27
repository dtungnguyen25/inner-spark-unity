using UnityEngine;

namespace Pcb
{
    /// <summary>
    /// Purely cosmetic board dressing (resistors, silkscreen, screw holes, ...). Its 3D look is generated
    /// by the Board, same as a PcbNode, but it is never collected into the movement graph — Board.TryPickExit,
    /// Spark, and LevelValidator never see it, so it can never affect gameplay.
    /// </summary>
    public class PcbDecoration : MonoBehaviour
    {
        public DecorType type = DecorType.Resistor;
        [Tooltip("Side this decoration sits on.")]
        public PcbLayer layer = PcbLayer.Front;
        [Tooltip("Spin around the board normal, in degrees.")]
        public float rotationDegrees = 0f;
    }
}
