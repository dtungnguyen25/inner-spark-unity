using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pcb
{
    /// <summary>Lives in the MainMenu scene. Wires the Play / Stage Select / Quit buttons.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Scene that contains the LevelManager / gameplay.")]
        public string gameplayScene = "SampleScene";
        [Header("Wiring")]
        public GameObject mainPanel;
        public GameObject stageSelectPanel;

        public void Play()
        {
            GameFlow.RequestLevel(0);
            SceneManager.LoadScene(gameplayScene);
        }

        public void OpenStageSelect()
        {
            if (mainPanel) mainPanel.SetActive(false);
            if (stageSelectPanel) stageSelectPanel.SetActive(true);
        }

        public void Quit()
        {
            Application.Quit(); // no effect in the editor or WebGL
        }
    }
}
