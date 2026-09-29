using System.Collections.Generic;
using UnityEngine;

namespace Pcb
{
    /// <summary>
    /// A GateMechanic that only opens once every one of its assigned switches is on (an AND gate).
    /// Assign the switches in the Inspector - no manual UnityEvent wiring needed, this subscribes to
    /// each switch itself. For a gate controlled by exactly one switch, keep using GateMechanic with
    /// its onToggle -> SetOpen wiring directly; that system is untouched.
    /// </summary>
    public class AndGateMechanic : GateMechanic
    {
        public List<SwitchMechanic> switches = new List<SwitchMechanic>();

        void OnEnable()
        {
            foreach (var s in switches) if (s) s.onToggle.AddListener(OnAnySwitchToggled);
            Recompute();
        }

        void OnDisable()
        {
            foreach (var s in switches) if (s) s.onToggle.RemoveListener(OnAnySwitchToggled);
        }

        void OnAnySwitchToggled(bool _) => Recompute();

        void Recompute()
        {
            bool allOn = switches.Count > 0;
            foreach (var s in switches)
                if (!s || !s.isOn) { allOn = false; break; }
            SetOpen(allOn);
        }
    }
}
