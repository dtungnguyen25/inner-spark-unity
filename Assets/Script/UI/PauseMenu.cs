using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pcb
{
    /// <summary>Esc toggles a pause overlay with Resume / Quit to Menu / Quit App. Lives in the gameplay scene.</summary>
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("Scene loaded by 'Quit to Menu'.")]
        public string mainMenuScene = "MainMenu";
        public GameObject panel;
        [Tooltip("Optional. Grayed out while paused, so it can't be clicked from underneath the pause panel.")]
        public Button restartButton;

        InputAction pauseAction;

        public bool IsPaused => panel && panel.activeSelf;

        void Awake()
        {
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            if (panel) panel.SetActive(false);
        }

        void OnEnable() => pauseAction.Enable();
        void OnDisable() => pauseAction.Disable();
        void OnDestroy() => pauseAction.Dispose();

        void Update()
        {
            if (pauseAction.WasPressedThisFrame()) Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume(); else Pause();
        }

        public void Pause()
        {
            if (panel) panel.SetActive(true);
            if (restartButton) restartButton.interactable = false;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (panel) panel.SetActive(false);
            if (restartButton) restartButton.interactable = true;
            Time.timeScale = 1f;
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }

        public void QuitApp()
        {
            Application.Quit(); // no effect in the editor or WebGL
        }
    }
}
