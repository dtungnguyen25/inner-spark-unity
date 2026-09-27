using System;
using UnityEngine;

namespace Pcb
{
    /// <summary>A stage's intro dialog: one asset per stage, assigned on the Board. Empty/unassigned = no dialog.</summary>
    [CreateAssetMenu(menuName = "PCB/Dialog Sequence", fileName = "DialogSequence")]
    public class DialogSequence : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            public string speakerName;
            public Sprite portrait;
            [TextArea(2, 5)] public string text;
        }

        public Line[] lines;
    }
}
