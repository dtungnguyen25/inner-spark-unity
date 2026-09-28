using UnityEngine;

namespace Pcb
{
    public class GateMechanic : TraceMechanic
    {
        public bool isOpen = false;
        public GameObject closedVisual;
        private GameObject autoVisual;

        public void SetOpen(bool open)
        {
            isOpen = open;
            Debug.Log($"Gate {gameObject.name} set to {(isOpen ? "Open" : "Closed")}");
            UpdateVisual();
        }

        public override bool CanEnter(Spark spark, bool reversed)
        {
            if (!isOpen) Debug.Log($"Spark blocked by closed gate on {gameObject.name}");
            return isOpen;
        }

        void Start()
        {
            if (closedVisual == null)
            {
                autoVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                autoVisual.name = "AutoGateVisual";
                autoVisual.transform.SetParent(transform, false);
                autoVisual.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                
                var trace = GetComponent<Trace>();
                if (trace && trace.from && trace.to)
                {
                    // Place roughly between the nodes
                    Vector3 fromPos = trace.from.transform.position;
                    Vector3 toPos = trace.to.transform.position;
                    autoVisual.transform.position = Vector3.Lerp(fromPos, toPos, 0.5f);
                    
                    // Nudge it outward slightly so it's visible on the board surface
                    autoVisual.transform.position += trace.transform.forward * -0.1f;
                }

                var rend = autoVisual.GetComponent<Renderer>();
                if (rend) rend.material.color = Color.red;

                closedVisual = autoVisual;
            }
            UpdateVisual();
        }

        void UpdateVisual()
        {
            if (closedVisual)
            {
                closedVisual.SetActive(!isOpen);
            }
        }
    }
}
