// PauseMenuManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    void Start()
    {
        // Ensure pause menu is hidden and time is running
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        if (pauseMenuPanel == null) return;
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f; // Freeze time
        isPaused = true;
        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        if (pauseMenuPanel == null) return;
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f; // Resume time
        isPaused = false;
        Debug.Log("Game Resumed");
    }

    // --- THIS is the key change for Option B ---
    public void RestartLevel()
    {
        Debug.Log("Restarting Level (Scene Reload)...");
        // Crucially, ensure time scale is resumed BEFORE reloading,
        // otherwise the reloaded scene might start paused.
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex); // Reload the current scene
    }
    // ------------------------------------------

    public void LoadMainMenu()
    {
        Debug.Log("Loading Main Menu...");
        Time.timeScale = 1f; // Ensure time scale is resumed
        SceneManager.LoadScene(mainMenuSceneName);
    }
}