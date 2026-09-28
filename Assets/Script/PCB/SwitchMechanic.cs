using UnityEngine;
using UnityEngine.Events;

namespace Pcb
{
    public class SwitchMechanic : NodeMechanic
    {
        public bool isOn = false;
        public UnityEvent<bool> onToggle;

        public void Toggle()
        {
            isOn = !isOn;
            Debug.Log($"Switch {gameObject.name} toggled. New state: (isOn: {isOn})");
            onToggle?.Invoke(isOn);
        }
    }
}
