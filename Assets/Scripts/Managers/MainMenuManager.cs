using UnityEngine;
using UnityEngine.UI; // REQUIRED for RawImage
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Management")]
    [Tooltip("The name of the scene to load when the game starts.")]
    [SerializeField] private string gameWorldSceneName = "GameWorld";

    [Header("UI References")] // NEW HEADER
    [Tooltip("Assign the RawImage UI element here that will display the background video.")]
    [SerializeField] private RawImage backgroundVideoDisplay; // Assign in Inspector

    private bool isLoading = false;

    void Start()
    {
        Time.timeScale = 1f; // Ensure time is running correctly for the menu

        // Attempt to interact with GameManager
        if (GameManager.Instance != null)
        {
            // 1. Handle the black screen from the end-game sequence
            /*if (GameManager.Instance.blackScreenImage != null) // Check if blackScreenImage is assigned in GM
            {
                if (GameManager.Instance.blackScreenImage.gameObject.activeSelf)
                {
                    GameManager.Instance.blackScreenImage.gameObject.SetActive(false);
                    Debug.Log("MainMenuManager: Disabled GameManager's black screen.");
                }
            }
            else
            {
                Debug.LogWarning("MainMenuManager: GameManager's blackScreenImage is not assigned. Cannot ensure it's hidden.");
            }*/

            // 2. Configure and play the main menu background video
            if (backgroundVideoDisplay != null) // Check if the RawImage for video is assigned here
            {
                if (GameManager.Instance.mainMenuRenderTexture != null) // Check if GM has the render texture
                {
                    backgroundVideoDisplay.texture = GameManager.Instance.mainMenuRenderTexture;
                    backgroundVideoDisplay.enabled = true; // Make sure RawImage is visible
                    GameManager.Instance.PlayMainMenuBackgroundVideo();
                    Debug.Log("MainMenuManager: Requested GameManager to play main menu background video.");
                }
                else
                {
                    Debug.LogWarning("MainMenuManager: GameManager's mainMenuRenderTexture is not assigned. Menu background video won't display on RawImage.");
                    backgroundVideoDisplay.enabled = false; // Hide RawImage if no texture
                }
            }
            else
            {
                Debug.LogWarning("MainMenuManager: BackgroundVideoDisplay (RawImage) is not assigned in the Inspector. Cannot display menu background video on UI.");
                // If you had a non-UI way for GameManager to play the menu video, you could call it here.
                // For now, we assume it's for a UI RawImage.
            }
        }
        else // GameManager.Instance is null
        {
            Debug.LogError("MainMenuManager: GameManager.Instance is NOT FOUND! Menu functionalities relying on it (like video playback, starting game through GM) will fail.");
            // Potentially disable UI buttons that rely on GameManager here if it's critical
            if (backgroundVideoDisplay != null)
            {
                backgroundVideoDisplay.enabled = false; // Hide video display area if GM is missing
            }
        }
    }

    void Update()
    {
        if (!isLoading)
        {
            // Using Input.anyKeyDown might be too broad if you have UI buttons.
            // Consider if you want any key to start, or just specific ones,
            // or rely solely on UI Button clicks.
            if (Input.anyKeyDown)
            {
                if (!Input.GetKeyDown(KeyCode.Escape) && // Ignore Escape for starting
                    !Input.GetMouseButtonDown(0) &&   // Ignore left mouse clicks if they are for UI buttons
                    !Input.GetMouseButtonDown(1) &&   // Ignore right mouse
                    !Input.GetMouseButtonDown(2))      // Ignore middle mouse
                {
                    // Check if a UI element is currently selected (e.g. an input field)
                    // to prevent starting game while typing in an options menu for example.
                    // This is a simple check, more robust UI navigation handling might be needed.
                    if (UnityEngine.EventSystems.EventSystem.current == null || 
                        !UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject)
                    {
                        Debug.Log("MainMenuManager: 'Any Key' (non-mouse, non-escape) pressed to start game.");
                        StartGame();
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuitGame();
            }
        }
    }

    public void StartGame() // Called by UI Button or Update
    {
        if (isLoading) return;

        if (string.IsNullOrEmpty(gameWorldSceneName))
        {
            Debug.LogError("Game World Scene Name is not set in MainMenuManager!");
            return;
        }

        isLoading = true;
        Debug.Log($"MainMenuManager: Starting Game - Requesting intro video.");

        if (GameManager.Instance != null)
        {
            // --- ADD THIS SECTION ---
            Debug.Log("MainMenuManager: Stopping main menu background video and hiding its display.");
            GameManager.Instance.StopMainMenuBackgroundVideo();
            if (backgroundVideoDisplay != null)
            {
                backgroundVideoDisplay.enabled = false; // Hide the RawImage
                // Optionally, if you have a main menu canvas group, set its alpha to 0.
            }
            // --- END ADDED SECTION ---

            GameManager.Instance.TriggerIntroVideo(); // This will handle scene loading after intro
        }
        else
        {
            Debug.LogError("MainMenuManager: GameManager.Instance not found when trying to start game!");
            isLoading = false; 
        }
    }

    public void QuitGame() // Called by UI Button or Update
    {
        Debug.Log("MainMenuManager: Quitting Game...");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopMainMenuBackgroundVideo(); // Good practice to stop videos
        }

        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    // Optional: If you navigate away from the main menu to another UI scene (like Options)
    // that DOESN'T involve GameManager handling the transition, you might need this.
    // void OnDestroy()
    // {
    //     if (GameManager.Instance != null && !isLoading) // Only stop if not already loading (which stops it)
    //     {
    //          // This check ensures we don't stop it if StartGame already did.
    //          // However, if the scene is destroyed for other reasons, stop the video.
    //         GameManager.Instance.StopMainMenuBackgroundVideo();
    //     }
    // }
}