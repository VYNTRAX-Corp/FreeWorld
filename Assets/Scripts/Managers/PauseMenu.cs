using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeWorld.Managers
{
    /// <summary>
    /// Handles button callbacks for Pause, Game Over and Round-End overlay screens.
    /// Attach once to UICanvas. Wire buttons via the setup script.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "MainMenu";

        // ── Pause screen ──────────────────────────────────────────────────────
        public void OnResume()
        {
            // UIManager.Resume() hides the screen, restores cursor, then calls TogglePause
            UIManager.Instance?.Resume();
        }

        // ── Shared: Pause & Game Over ─────────────────────────────────────────
        public void OnRestart()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.RestartGame();
        }

        public void OnMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
