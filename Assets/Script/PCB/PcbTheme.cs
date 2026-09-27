using System;
using UnityEngine;

namespace Pcb
{
    /// <summary>Shared look of every board. Created automatically by Tools > PCB > Level Editor.</summary>
    [CreateAssetMenu(menuName = "PCB/Theme", fileName = "PcbTheme")]
    public class PcbTheme : ScriptableObject
    {
        [Header("Materials (generated in Assets/PCB/Materials, edit freely)")]
        public Material boardMaterial;
        public Material copperMaterial;
        public Material capacitorMaterial;
        public Material metalMaterial;
        public Material holeMaterial;
        public Material chipMaterial;
        public Material plugMaterial;
        public Material sparkMaterial;
        [Tooltip("Unlit material for the spark trail and the direction arrows.")]
        public Material spriteMaterial;
        public Sprite triangle;

        [Header("Optional models (empty = simple shapes). Author them facing -Z, sitting at Z = 0.")]
        public GameObject capacitorPrefab;
        public GameObject viaPrefab;
        public GameObject startPrefab;
        public GameObject goalPrefab;

        [Serializable]
        public struct DecorationLook
        {
            public DecorType type;
            public GameObject prefab;
        }
        [Header("Decorations (optional models, empty = generic placeholder box)")]
        public DecorationLook[] decorationPrefabs;

        public GameObject GetDecorationPrefab(DecorType type)
        {
            foreach (var d in decorationPrefabs)
                if (d.type == type) return d.prefab;
            return null;
        }

        [Header("Scene")]
        public Color background = new Color32(228, 228, 228, 255);

        [Header("Board")]
        public float boardThickness = 0.16f;
        [Tooltip("Extra board around the outermost grid line, in world units.")]
        public float boardMargin = 0.5f;

        [Header("Traces")]
        public float traceWidth = 0.14f;
        public float traceHeight = 0.025f;

        [Header("Capacitor")]
        public float capacitorSize = 0.24f;
        public float capacitorHeight = 0.21f;

        [Header("Via")]
        public float viaSize = 0.2f;
        [Tooltip("How far the via sticks out of each face.")]
        public float viaLip = 0.02f;

        [Header("Goal chip")]
        public float chipHeight = 0.14f;

        [Header("Start plug")]
        public Vector3 plugSize = new Vector3(0.46f, 0.34f, 0.26f);

        [Header("Spark")]
        public Color spark = new Color32(134, 227, 255, 255);
        public Color sparkBlocked = new Color32(255, 90, 90, 255);
        [Tooltip("Emission multiplier. Higher = stronger bloom glow.")]
        public float sparkGlow = 4f;
        public float sparkSize = 0.12f;
        public float sparkLightRange = 1.5f;
        public float sparkLightIntensity = 2f;

        [Header("Editor")]
        [Tooltip("Colour of the hidden side, drawn over the board while editing only.")]
        public Color editorGhost = new Color(1f, 1f, 1f, 0.35f);

        public bool IsComplete =>
            boardMaterial && copperMaterial && capacitorMaterial && metalMaterial && holeMaterial &&
            chipMaterial && plugMaterial && sparkMaterial && spriteMaterial && triangle;
    }
}
