using System.Collections.Generic;
using UnityEngine;

namespace Pcb
{
    /// <summary>Ordered list of level prefabs. Managed from Tools > PCB > Level Editor.</summary>
    [CreateAssetMenu(menuName = "PCB/Level List", fileName = "LevelList")]
    public class LevelList : ScriptableObject
    {
        public List<Board> levels = new List<Board>();

        public int Count => levels.Count;
        public Board this[int index] => levels[index];

        public int IndexOf(string levelName)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] && levels[i].levelName == levelName) return i;
            return -1;
        }
    }
}
