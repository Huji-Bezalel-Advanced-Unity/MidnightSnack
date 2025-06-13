using UnityEngine;
using UnityEngine.Video;         // Needed for VideoClip
using UnityEngine.SceneManagement; // Needed for scene loading
using System;
using System.Collections; // Needed for Action
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // Simple Singleton Pattern
    public static GameManager Instance { get; private set; }
    
    [Header("Player References (for End Game Teleport)")]
    [SerializeField] private Transform darkPlayerTransform; // Assign Dark Player's Transform
    [SerializeField] private Transform lightPlayerTransform; // Assign Light Player's Transform
    [SerializeField] private Vector2 darkPlayerEndPosition;  // Can be the same as newPositionEnd from DarkPlayerController
    [SerializeField] private Vector2 lightPlayerEndPosition; // Specific end position for Light 

    [Header("Scene Management")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string gameWorldSceneName = "GameWorld";

    [Header("Main Menu Video")]
    [SerializeField] private VideoClip mainMenuBackgroundVideo;
    [SerializeField] public RenderTexture mainMenuRenderTexture; // Made public for MainMenuManager to access (or use a getter)

    [Header("Video Playback (Events)")]
    [SerializeField] private GameObject videoPlayerPrefab;
    [SerializeField] private VideoClip introVideo;
    [SerializeField] private VideoClip middleVideo;
// [SerializeField] private VideoClip cooperativeEndingVideo; // REMOVE
// [SerializeField] private VideoClip soloEndingVideo;    // REMOVE
    [SerializeField] private VideoClip gameEndingVideo; // NEW: The main ending video
    [SerializeField] private float gameEndingVideoLoopStartTime = 5.0f; // Time FROM THE END to start looping (e.g., 5 seconds)
    [SerializeField] private VideoClip finalCreditsVideo;

    [Header("Mid-Game Trigger")]
    [Tooltip("The EventManager ID that triggers the middle video.")]
    [SerializeField] private string middleVideoTriggerID = "PlayMiddleVideo";
    
    
    public Image blackScreenOverlay;

    private GameObject currentEventVideoInstance = null; // For one-shot event videos
    private GameObject currentMenuBackgroundVideoInstance = null; // Specifically for menu BG
    private bool isSubscribedToEvents = false;
    private bool currentEndingIsCooperative;
    private Coroutine endingVideoInputCoroutine = null;
    
    void Awake()
    {
        Debug.Log($"GAMEMANAGER_AWAKE: Instance for {gameObject.name}. Current Instance is {Instance?.gameObject.name}", gameObject);
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // /if (blackScreenImage != null) blackScreenImage.gameObject.SetActive(false);/
        // /else Debug.LogWarning("GM: Black Screen Image not assigned.");/
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        if (isSubscribedToEvents) return;
        EventManager.OnObjectActivate += HandleMiddleVideoTrigger;
        // EventManager.OnGameEndedStep1 -= HandleGameEndedInitialSetup; // REMOVE, this is now handled by scene load
        EventManager.OnGameEndedFinal += HandleGameEndedFinal_PlayVideoAndLoadMenu; // <<< LISTEN for final trigger
        isSubscribedToEvents = true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!isSubscribedToEvents) return;
        EventManager.OnObjectActivate -= HandleMiddleVideoTrigger;
        // EventManager.OnGameEndedStep1 -= HandleGameEndedInitialSetup; // REMOVE
        EventManager.OnGameEndedFinal -= HandleGameEndedFinal_PlayVideoAndLoadMenu;
        isSubscribedToEvents = false;
    }
    
    private void HandleGameEndedFinal_PlayVideoAndLoadMenu() // Or HandleFinalInputPlayVideo
    {
        Debug.Log("GM - Final Video Stage: Input received. Playing ending video sequence.");

        if (gameEndingVideo == null)
        {
            Debug.LogError("GM: gameEndingVideo is not assigned! Cannot play ending sequence.");
            // Fallback to credits or main menu
            ProceedToCreditsOrMainMenu();
            return;
        }

        // Black screen should ideally be active and Time.timeScale = 0f
        // Ensure this is set by a previous step (e.g., OnGameEndedStep1 or similar)
        if (blackScreenOverlay != null) blackScreenOverlay.gameObject.SetActive(true);
        Time.timeScale = 0f; // Pause game simulation

        Debug.Log($"GM: Playing game ending video: {gameEndingVideo.name}");

        StopCurrentEventVideo(); // Ensure no other event video is playing

        currentEventVideoInstance = Instantiate(videoPlayerPrefab);
        VideoPlaybackController videoController = currentEventVideoInstance.GetComponent<VideoPlaybackController>();
        
        if (videoController != null)
        {
            // Calculate the actual time point for loop start
            double loopStartTimeFromEnd = (double)gameEndingVideo.length - gameEndingVideoLoopStartTime;
            if (loopStartTimeFromEnd < 0) loopStartTimeFromEnd = 0; // Sanity check

            videoController.PlayWithLoopAtEnd(
                clipToPlay: gameEndingVideo,
                loopStartTime: loopStartTimeFromEnd,
                onReachedLoopPoint: () => {
                    Debug.Log($"GM: Ending video '{gameEndingVideo.name}' reached loop point. Waiting for Enter key.");
                    // Start listening for the Enter key once the loop point is reached
                    if (endingVideoInputCoroutine != null) StopCoroutine(endingVideoInputCoroutine);
                    endingVideoInputCoroutine = StartCoroutine(WaitForEnterKeyToEndVideoLoop(videoController));
                },
                onCompleteIfNoLoop: () => { // This will be called if loopStartTime is >= video length
                    Debug.Log($"GM: Ending video '{gameEndingVideo.name}' completed without looping (loop point too late).");
                    ProceedToCreditsOrMainMenu();
                }
            );
        }
        else
        {
            Debug.LogError("GM: VideoPlaybackController not found on prefab for gameEndingVideo.");
            ProceedToCreditsOrMainMenu();
        }
    }

    private IEnumerator WaitForEnterKeyToEndVideoLoop(VideoPlaybackController activeVideoController)
    {
        Debug.Log("GM: Now waiting for Enter key to proceed past ending video loop...");
        while (true) // Loop indefinitely until Enter is pressed
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Debug.Log("GM: Enter key pressed during ending video loop.");
                if (activeVideoController != null && activeVideoController.IsCurrentlyPlaying()) // Check if controller is still valid
                {
                    activeVideoController.ForceStop(); // Stop the looping ending video
                }
                // The Destroy(currentEventVideoInstance) will happen in PlayEventVideo or if we call StopCurrentEventVideo()
                // For now, let's assume ProceedToCreditsOrMainMenu will handle it if necessary.
                currentEventVideoInstance = null; // Clear the reference as it's being stopped.

                ProceedToCreditsOrMainMenu();
                yield break; // Exit coroutine
            }
            yield return null; // Wait for the next frame
        }
    }
    
    private void ProceedToCreditsOrMainMenu()
    {
        if (endingVideoInputCoroutine != null)
        {
            StopCoroutine(endingVideoInputCoroutine);
            endingVideoInputCoroutine = null;
        }
        StopCurrentEventVideo(); // Ensure the ending video instance is cleaned up if it was still somehow referenced

        if (finalCreditsVideo != null)
        {
            Debug.Log($"GM: Playing final credits video: {finalCreditsVideo.name}");
            // Use the standard PlayEventVideo for the credits
            PlayEventVideo(finalCreditsVideo, () => {
                Debug.Log($"GM: Final credits video ('{finalCreditsVideo.name}') finished. Returning to Main Menu.");
                Time.timeScale = 1f; 
                LoadMainMenuScene(); 
            });
        }
        else
        {
            Debug.LogWarning("GM: finalCreditsVideo not assigned. Skipping to Main Menu.");
            Time.timeScale = 1f;
            LoadMainMenuScene();
        }
    }
    
    private bool CheckIfCooperativeEndingConditionWasMet()
    {
        // This logic needs to be re-evaluated.
        // Perhaps the GameEndingManager (if it still exists for this purpose)
        // sets a static flag or a property on GameManager BEFORE loading the "Final" scene.
        // For simplicity, let's assume cooperative for now.
        Debug.LogWarning("CheckIfCooperativeEndingConditionWasMet() needs implementation if different ending videos are used.");
        return true; 
    }
    
    private void HandleLoadSceneRequest(string sceneName)
    {
        Debug.Log($"GameManager: Received request to load scene '{sceneName}'.");
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("GameManager: Load scene request received with an empty scene name!");
            return;
        }

        // Stop any ongoing videos before loading a new scene
        StopCurrentEventVideo();
        StopMainMenuBackgroundVideo();

        Time.timeScale = 1f; // Ensure time is running
        SceneManager.LoadScene(sceneName);
    }
    
    private void HandleGameEndedInitialSetup(bool wasCooperativeEnding)
    {
        Debug.Log("GM - STEP 1: Game Ended Initial Setup. Cooperative: " + wasCooperativeEnding);
        currentEndingIsCooperative = wasCooperativeEnding; 

        Time.timeScale = 0f; 

        // --- A. Turn ON Black Screen FIRST ---
        /*if (blackScreenImage != null)
        {
            blackScreenImage.gameObject.SetActive(true);
            Debug.Log("GM - STEP 1A: Black screen activated.");
        }
        else
        {
            Debug.LogWarning("GM - STEP 1A: No black screen image assigned.");
        }*/

        // --- B. Teleport BOTH players ---
        // It's generally cleaner if GameManager handles this coordinated teleport for the end sequence.
        if (darkPlayerTransform != null)
        {
            darkPlayerTransform.position = darkPlayerEndPosition;
            Rigidbody2D drb = darkPlayerTransform.GetComponent<Rigidbody2D>();
            if (drb != null) { drb.linearVelocity = Vector2.zero; drb.isKinematic = true; }
            Debug.Log($"GM - STEP 1B: Dark Player teleported to {darkPlayerEndPosition}");

            // Tell DarkPlayer it's now in the "endGame" input waiting state
            DarkPlayerController dpc = darkPlayerTransform.GetComponent<DarkPlayerController>();
            if (dpc != null) { dpc.EnterEndGameInputState(); } // We'll add this method to DarkPlayerController
        }
        else { Debug.LogWarning("GM - STEP 1B: Dark Player Transform not assigned for teleport."); }

        if (lightPlayerTransform != null)
        {
            lightPlayerTransform.position = lightPlayerEndPosition;
            Rigidbody2D lrb = lightPlayerTransform.GetComponent<Rigidbody2D>();
            if (lrb != null) { lrb.linearVelocity = Vector2.zero; lrb.isKinematic = true; }
            Debug.Log($"GM - STEP 1B: Light Player teleported to {lightPlayerEndPosition}");
            // Light player might also need an "end game" state if it has input
        }
        else { Debug.LogWarning("GM - STEP 1B: Light Player Transform not assigned for teleport."); }


        // --- C. Force Camera Advance ---
        // The camera needs to know where to go. If its "ForceAdvanceToNextSection"
        // correctly targets the final view, that's fine. Otherwise, you might need
        // a CameraController.PanToSpecificLocation(Vector3 target) or similar.
        // For now, assuming ForceAdvanceToNextSection does the right thing after players are moved.
        //TODO under this should not be commented 
        /*if (CameraController.Instance != null) // Assuming CameraController is a singleton or easily accessible
        {
            Debug.Log("GM - STEP 1C: Forcing camera advance.");
            CameraController.Instance.ForceAdvanceToNextSection(); // Or a more specific method
        } else { Debug.LogWarning("GM - STEP 1C: CameraController instance not found to force advance."); }*/


        // --- D. Dark Player is now waiting for input (handled by its EnterEndGameInputState()) ---
        // No separate event needed from GameManager to signal "ready for input" if DarkPlayer sets its own flag.

        // The coroutine DelayedNotifyPlayersReady and the OnPlayersTeleportedForEndGame event
        // might become redundant if GameManager orchestrates these steps directly.
        // Let's remove them for this simpler direct flow.

        Debug.Log("GM - STEP 1: Initial end game setup complete. Dark Player should be waiting for input.");
    }

    
    private IEnumerator DelayedNotifyPlayersReady(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // Wait even if Time.timeScale = 0
        Debug.Log("GM: Player/Camera repositioning assumed complete. Signalling ready for final input (no event needed here, DarkPlayer is already waiting).");
        // Actually, no separate event is needed here for GameManager.
        // DarkPlayerController.endGame flag is the key.
        // This coroutine was more if GameManager needed to do something after teleport.
        // We can simplify: the critical part is DarkPlayerController setting its endGame flag.
    }
    
    // STEP 2: DarkPlayerController presses key, triggers EventManager.OnGameEndedFinalInputReceived
    private void HandlePlayersReadyForFinalInput()
    {
        // This method might not be needed in GameManager if DarkPlayer directly triggers OnGameEndedFinalInputReceived.
        // However, if GameManager needs to do something between player teleport and player input enabling, it can go here.
        // For now, let's assume DarkPlayer's Update loop handles enabling input check via its endGame flag.
        Debug.Log("GM: Received OnPlayersTeleportedForEndGame. Players/Camera should be set. DarkPlayer will now wait for input.");
    }


    // STEP 3: Called when DarkPlayerController (after its 'endGame' flag is true and key is pressed)
    //         triggers EventManager.OnGameEndedFinalInputReceived
    private void HandleFinalInputPlayVideo()
    {
        Debug.Log("GM - FINAL INPUT: Final input received. Starting end game video sequence.");
    
        // Determine which main ending video to play (cooperative or solo)
        bool wasCooperative = CheckIfCooperativeEndingConditionWasMet(); // Your existing logic
        VideoClip firstEndingClip = gameEndingVideo;

        // Optional: Ensure black screen is on if the videos don't have their own fade from black
        // /if (blackScreenImage != null) blackScreenImage.gameObject.SetActive(true);/
        Time.timeScale = 0f; // Pause game for the entire video sequence

        // --- Play the FIRST ending video ---
        Debug.Log($"GM: Playing first ending video: {firstEndingClip?.name}");
        PlayEventVideo(firstEndingClip, () => {
        // This is the onComplete callback for the FIRST video
        Debug.Log($"GM: First ending video ('{firstEndingClip?.name}') finished.");

        // --- Now, play the SECOND (credits) video ---
        if (finalCreditsVideo != null)
        {
            Debug.Log($"GM: Playing final credits video: {finalCreditsVideo.name}");
            PlayEventVideo(finalCreditsVideo, () => {
                // This is the onComplete callback for the SECOND video
                Debug.Log($"GM: Final credits video ('{finalCreditsVideo.name}') finished. Returning to Main Menu.");
                Time.timeScale = 1f; // Ensure time is running for menu
                LoadMainMenuScene(); // This will also make MainMenuManager hide the black screen.
            });
        }
        else
        {
            // If there's no second video, just go to main menu
            Debug.LogWarning("GM: finalCreditsVideo not assigned. Skipping to Main Menu.");
            Time.timeScale = 1f;
            LoadMainMenuScene();
        }
    });
    }
    
    private void HandleGameEndedStep2()
    {
        Debug.Log("GameManager: Handling Game Ended Step 3 - Hiding black screen.");
        /*if (blackScreenImage != null)
        {
            blackScreenImage.gameObject.SetActive(false);
            Debug.Log("GameManager: Black screen deactivated.");
        }*/
        // By this point, the main menu should be visible and its background video playing.
        // Time.timeScale should already be 1f from loading the main menu.
    }
    
    void OnEnable()
    {
        if (Instance == this) SubscribeToEvents();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            UnsubscribeFromEvents();
            Instance = null;
        }
    }

    // --- Public Method for MainMenuManager to Trigger Intro ---
    public void TriggerIntroVideo()
    {
        // No need to StopMainMenuBackgroundVideo here if MainMenuManager already did.
        // But it's harmless if called again.
        // GameManager.Instance.StopMainMenuBackgroundVideo(); 

        // --- 1. Fade TO Black (or just set active if no fade) ---
        if (blackScreenOverlay != null)
        {
            // For an instant black screen before intro:
            blackScreenOverlay.gameObject.SetActive(true);
            blackScreenOverlay.CrossFadeAlpha(1f, 0f, true); // Ensure it's fully opaque instantly
            Debug.Log("GM: Black screen activated BEFORE intro video.");
        }

        PlayEventVideo(introVideo, () => {
            Debug.Log("GameManager: Intro video finished. Loading game world (screen should be black).");
            // Screen is already black from before intro.
            LoadGameWorldScene(); // Scene load happens "behind" the black screen
            // Black screen will be faded out by GameWorld's own logic once it's ready
        });
    }

    // --- Event Handlers for Event Videos ---
    private void HandleMiddleVideoTrigger(string receivedID, GameObject source)
    {
        if (receivedID == middleVideoTriggerID)
        {
            Debug.Log($"GameManager: Received middle video trigger ID '{receivedID}'.");
            PlayMiddleVideo(); // This calls PlayEventVideo internally
        }
    }

    private void HandleGameEndedStep1(bool wasCooperativeEnding)
    {
        Debug.Log("GameManager: Handling Game Ended Step 1 - Playing ending video.");
        VideoClip clipToPlay = gameEndingVideo;

        // Ensure any other UI/game elements are paused or hidden if necessary
        Time.timeScale = 0f; // Often good to pause game during ending cutscenes

        PlayEventVideo(clipToPlay, () => {
            Debug.Log("GameManager: Ending video finished (Step 1 complete).");
            Time.timeScale = 1f; // Restore time if paused

            // --- TURN ON BLACK SCREEN ---
            /*if (blackScreenImage != null)
            {
                blackScreenImage.gameObject.SetActive(true);
                // You could add a fade-in for the black screen here if desired using a coroutine
                Debug.Log("GameManager: Black screen activated.");
            }
            else
            {
                Debug.LogWarning("GameManager: No black screen image assigned to fade to.");
            }*/
            // --------------------------

            // --- TRIGGER STEP 2 EVENT ---
            // Optionally add a small delay before triggering Step 2 to let black screen be visible
            StartCoroutine(DelayedTriggerStep2(0.5f)); // Example: 0.5 second delay
            // EventManager.TriggerGameEndedStep2(); // If no delay needed
            // ----------------------------
        });
    }
    
    private IEnumerator DelayedTriggerStep2(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // Use Realtime if Time.timeScale might be 0
        Debug.Log("GameManager: Delay complete, triggering Game Ended Step 2.");
        EventManager.TriggerGameEndedStep2();
    }
    

    // Public method to trigger middle video manually
    public void PlayMiddleVideo(Action onCompleteExternal = null)
    {
        Time.timeScale = 0f; // Pause game

        PlayEventVideo(middleVideo, () => {
            Time.timeScale = 1f; // Resume game
            Debug.Log("GameManager: Middle video finished.");
            onCompleteExternal?.Invoke();
        });
    }

    // --- Methods for Main Menu Background Video ---
    public void PlayMainMenuBackgroundVideo()
    {
        if (mainMenuBackgroundVideo == null) { Debug.LogWarning("GM: MainMenuBackgroundVideo clip not assigned."); return; }
        if (mainMenuRenderTexture == null) { Debug.LogWarning("GM: MainMenuRenderTexture not assigned."); return; }
        if (videoPlayerPrefab == null) { Debug.LogWarning("GM: videoPlayerPrefab not assigned."); return; }

        StopCurrentEventVideo();
        StopMainMenuBackgroundVideo(); 

        currentMenuBackgroundVideoInstance = Instantiate(videoPlayerPrefab);
        VideoPlaybackController controller = currentMenuBackgroundVideoInstance.GetComponent<VideoPlaybackController>();
        AudioSource sourceOnPrefab = currentMenuBackgroundVideoInstance.GetComponent<AudioSource>();

        if (controller != null)
        {
            // --- CORRECTED AUDIO CHECK ---
            bool videoHasAudio = mainMenuBackgroundVideo.audioTrackCount > 0;
            // --- -----------------------

            controller.Play(
                clipToPlay: mainMenuBackgroundVideo,
                onComplete: null, 
                loop: true,
                targetRenderTextureForUI: mainMenuRenderTexture,
                targetRawImageForUI: null, 
                // --- CORRECTED AUDIO DECISION ---
                audioOutput: (videoHasAudio && sourceOnPrefab != null) ? VideoAudioOutputMode.AudioSource : 
                             (videoHasAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None),
                // --- --------------------------
                specificAudioSource: sourceOnPrefab
            );
            Debug.Log("GameManager: Started playing main menu background video.");
        }
        else
        {
            Debug.LogError("GM: VideoPlaybackController not found on prefab for menu BG.");
            if (currentMenuBackgroundVideoInstance != null) Destroy(currentMenuBackgroundVideoInstance);
            currentMenuBackgroundVideoInstance = null;
        }
    }


    public void StopMainMenuBackgroundVideo()
    {
        if (currentMenuBackgroundVideoInstance != null)
        {
            Debug.Log("GameManager: Stopping main menu background video.");
            VideoPlaybackController controller = currentMenuBackgroundVideoInstance.GetComponent<VideoPlaybackController>();
            if (controller != null)
            {
                controller.ForceStop(); // Use the new ForceStop method
            }
            Destroy(currentMenuBackgroundVideoInstance);
            currentMenuBackgroundVideoInstance = null;
        }
    }

    // --- Internal Playback for One-Shot Event Videos ---
    private void PlayEventVideo(VideoClip clip, Action onComplete)
    {
        // ... (Your existing PlayEventVideo logic is mostly fine, ensure it uses the full VideoPlaybackController.Play signature)
        if (videoPlayerPrefab == null) { Debug.LogError("GM: videoPlayerPrefab is null."); onComplete?.Invoke(); return; }
        if (clip == null) { Debug.LogWarning("GM: PlayEventVideo called with null clip."); onComplete?.Invoke(); return; }

        StopCurrentEventVideo();
        StopMainMenuBackgroundVideo();

        currentEventVideoInstance = Instantiate(videoPlayerPrefab);
        VideoPlaybackController controller = currentEventVideoInstance.GetComponent<VideoPlaybackController>();
        AudioSource sourceOnPrefab = currentEventVideoInstance.GetComponent<AudioSource>();

        if (controller != null)
        {
            bool videoHasAudio = clip.audioTrackCount > 0;
            controller.Play(
                clipToPlay: clip,
                onComplete: () => 
                {
                    if (currentEventVideoInstance != null) Destroy(currentEventVideoInstance);
                    currentEventVideoInstance = null;
                    onComplete?.Invoke(); 
                },
                loop: false, 
                targetRenderTextureForUI: null, 
                targetRawImageForUI: null,
                audioOutput: (videoHasAudio && sourceOnPrefab != null) ? VideoAudioOutputMode.AudioSource :
                    (videoHasAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None),
                specificAudioSource: sourceOnPrefab
            );
        }
        else
        {
            Debug.LogError("GM: VideoPlaybackController not found on prefab for event video.");
            if (currentEventVideoInstance != null) Destroy(currentEventVideoInstance);
            currentEventVideoInstance = null;
            onComplete?.Invoke();
        }
    }


    // Helper to stop current event video
    private void StopCurrentEventVideo()
    {
        if (currentEventVideoInstance != null)
        {
            Debug.Log("GameManager: Stopping current event video.");
            VideoPlaybackController controller = currentEventVideoInstance.GetComponent<VideoPlaybackController>();
            if (controller != null)
            {
                controller.ForceStop();
            }
            Destroy(currentEventVideoInstance);
            currentEventVideoInstance = null;
        }
    }

    // --- Scene Loading ---
    
    private void LoadGameWorldScene()
    {
        HandleLoadSceneRequest(gameWorldSceneName);
    }

    private void LoadMainMenuScene()
    {
        HandleLoadSceneRequest(mainMenuSceneName);
    }
    /*private void LoadGameWorldScene()
    {
        if (string.IsNullOrEmpty(gameWorldSceneName)) { Debug.LogError("Game World Scene Name is empty!"); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameWorldSceneName);
    }

    private void LoadMainMenuScene()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName)) { Debug.LogError("Main Menu Scene Name is empty!"); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }*/
}