using UnityEngine;

namespace Pcb
{
    /// <summary>
    /// Base for mechanics that live on a trace (Resistor, Diode, Fuse, Door...).
    /// Add the subclass to the same GameObject as the Trace.
    /// </summary>
    public abstract class TraceMechanic : MonoBehaviour
    {
        /// <summary>Return false to stop the spark from entering. 'reversed' = travelling to -> from.</summary>
        public virtual bool CanEnter(Spark spark, bool reversed) => true;

        /// <summary>Called when the spark reaches the far end of the trace.</summary>
        public virtual void OnTraversed(Spark spark, bool reversed) { }
    }

    /// <summary>
    /// Base for mechanics that live on a node (charged capacitor, switch, key pickup...).
    /// Add the subclass to the same GameObject as the PcbNode.
    /// </summary>
    public abstract class NodeMechanic : MonoBehaviour
    {
        public virtual void OnSparkArrive(Spark spark) { }
        public virtual void OnSparkLeave(Spark spark) { }
    }
}
