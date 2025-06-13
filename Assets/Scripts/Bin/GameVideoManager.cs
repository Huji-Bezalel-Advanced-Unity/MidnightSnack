using UnityEngine;
using UnityEngine.Video;
using System; // For Action
using UnityEngine.SceneManagement; // For scene loading

public class GameVideoManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Assign your VideoPlayerPrefab here.")]
    [SerializeField] private GameObject videoPlayerPrefab;
    // --- Added Header for specific clips ---
    [Header("Video Clips")]
    [SerializeField] private VideoClip introVideo;
    [Tooltip("Video to play at an event (e.g., halfway through level).")]
    [SerializeField] private VideoClip middleVideo;
    [Tooltip("Cooperative Ending Video (Dark Player opened the Light Player's door).")]
    [SerializeField] private VideoClip cooperativeEndingVideo;
    [Tooltip("Solo Ending Video (Dark Player reached the end alone).")]
    [SerializeField] private VideoClip soloEndingVideo;

    [Header("Gameplay Scene Name")]
    [SerializeField] private string gameplaySceneName = "GameWorld"; // Set the exact name of your main gameplay scene

    // --- Internal State ---
    private bool hasPlayedIntro = false; // Only play once
    private bool isPlayingMiddleVideo = false;

    // --- Optional Singleton, but not strictly required for this design (we can just use Instance or assignment) ---
    public static GameVideoManager Instance { get; private set; }

    void Awake()
    {
        // Singleton Setup (optional) - uncomment if you want to make this persistent
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this; // If this script instance is created at the right time and you are sure is correct
        // DontDestroyOnLoad(gameObject); // Prevent it from being destroyed when loading a new scene
    }

    void OnEnable()
    {
        // Subscribe to relevant events to trigger middle and end videos.
        EventManager.OnLevelSectionCompleted += HandleLevelSectionComplete; // For triggering the middle video
        EventManager.OnLevelSectionCompleted += HandleLevelSectionComplete; // For triggering the middle video
        EventManager.OnLevelSectionCompleted += HandleLevelSectionComplete; // For triggering the middle video
        // Other events to be added later (level and end)
        EventManager.OnLevelSectionCompleted += CheckAndStartGameplay; // For triggering start video when the level starts
        EventManager.OnLevelSectionCompleted += HandleLevelSectionComplete; // For triggering end game

    }

    void OnDisable()
    {
        EventManager.OnLevelSectionCompleted -= HandleLevelSectionComplete; // Middle video trigger
        EventManager.OnLevelSectionCompleted -= HandleLevelSectionComplete; // Middle video trigger
        EventManager.OnLevelSectionCompleted -= HandleLevelSectionComplete; // End video trigger
    }

    void Start()
    {
        // --- INTRO VIDEO ON START ---
        if (introVideo != null && !hasPlayedIntro && SceneManager.GetActiveScene().name != gameplaySceneName)
        {
            PlayVideo(introVideo, () => {
                 hasPlayedIntro = true;
                 // We would load the gameplay scene after the intro here BUT
                 // since its going to be loaded from the main menu in this case,
                 // we will skip.
                Debug.Log("Intro video finished.");
            });
        }
    }

    // --- Trigger the middle video when appropriate ---
    private void HandleLevelSectionComplete(int levelIndex)
    {
        // Determine when the middle video should be played
         // For example, play the middle video after level 1
         if (levelIndex == 1)
         {
             Debug.Log("Triggering Middle Video for Level 1 complete.");
              PlayMiddleVideo();
         }
    }

    // --- Trigger Gameplay Video ---
     private void CheckAndStartGameplay(int levelIndex)
     {
         if (levelIndex == 0 && !hasPlayedIntro)
         {
             hasPlayedIntro = true; // Set flag so we don't replay
             Debug.Log("Starting Intro Video...");
             PlayVideo(introVideo, () =>
             {
                // Now we can load the gameplay, since the intro is finished
                SceneManager.LoadScene(gameplaySceneName); // Or call mainmenuManager.StartGame() if applicable
             });
         }
     }


    public void PlayMiddleVideo()
    {
        if (isPlayingMiddleVideo) return; // Prevent multiple plays
        isPlayingMiddleVideo = true;
        Debug.Log("Triggering middle video.");
        PlayVideo(middleVideo, () => {
             isPlayingMiddleVideo = false;
              // Resume game logic?
             Time.timeScale = 1f; // Re-enable game play
        });
    }

    // --- Trigger End Videos (Cooperative or Solo) ---
    public void PlayEndingVideo(bool cooperativeEnding)
    {
         VideoClip clip = cooperativeEnding ? cooperativeEndingVideo : soloEndingVideo;

        if (clip == null)
        {
            Debug.LogError($"PlayEndingVideo: Missing the {(cooperativeEnding ? "cooperative" : "solo")} ending video clip!");
            // Fallback? Load main menu directly.
             SceneManager.LoadScene("MainMenu"); // Use your main menu scene name
            return;
        }

         PlayVideo(clip, () => {
            // --- Load Main Menu after End Video ---
            Debug.Log("Ending video complete. Returning to Main Menu.");
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu"); // Use your actual menu scene name.
        });
    }

    // --- Core Video Playback Function ---
    private void PlayVideo(VideoClip clip, Action onComplete)
    {
        if (videoPlayerPrefab == null)
        {
            Debug.LogError("GameVideoManager: Video Player Prefab not assigned!");
            onComplete?.Invoke();
            return;
        }

        // Instantiate the video player prefab (and create it on screen)
        GameObject videoInstance = Instantiate(videoPlayerPrefab);
        VideoPlaybackController controller = videoInstance.GetComponent<VideoPlaybackController>();

        if (controller != null)
        {
            controller.Play(clip, () => { // Pass a local, non-static method
                 // Ensure the video instance is destroyed *after* the video is finished (and callback is completed)
                Destroy(videoInstance); // Clean up the instantiated prefab
                onComplete?.Invoke(); // Invoke the callback
            });
        }
        else
        {
            Debug.LogError("Video Player Prefab is missing the VideoPlaybackController script!");
            Destroy(videoInstance);
            onComplete?.Invoke(); // Invoke the callback anyway
        }
    }
}