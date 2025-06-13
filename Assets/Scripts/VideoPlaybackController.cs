using UnityEngine;
using UnityEngine.UI;    // Required for RawImage
using UnityEngine.Video; // Required for VideoPlayer
using System;

[RequireComponent(typeof(VideoPlayer))] // AudioSource is now optional on the prefab itself
public class VideoPlaybackController : MonoBehaviour
{
    [Header("Components (Assigned from Prefab or found/added in Awake)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource audioSource; // Can be null if not using AudioSource output
    [SerializeField] private Canvas videoCanvas;       // For fullscreen event videos
    [SerializeField] private RawImage videoDisplayImage; // For fullscreen event videos

    [Header("Settings")]
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private KeyCode skipKey1 = KeyCode.Escape;
    [SerializeField] private KeyCode skipKey2 = KeyCode.KeypadEnter;

    private Action onVideoCompleteCallback; // For one-shot videos
    private bool isPlaying = false;
    private bool isStopping = false;
    
    private bool isLoopingAtEnd = false;
    private double endLoopStartTime = 0;
    private Action onReachedLoopPointCallback;
    private Action onCompleteIfNoLoopCallback;

    // --- For UI Background Video ---
    private RawImage uiTargetRawImage = null; // If playing directly onto a RawImage's texture
    private RenderTexture externalRenderTexture = null; // If playing onto an external Render Texture

    void Awake()
    {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>(); // It's okay if this remains null

        videoPlayer.playOnAwake = false;
        if (audioSource != null) audioSource.playOnAwake = false; // Only if AudioSource exists

        // Default audio output mode, can be overridden by Play()
        videoPlayer.audioOutputMode = (audioSource != null) ? VideoAudioOutputMode.AudioSource : VideoAudioOutputMode.Direct;
        if (audioSource != null)
        {
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        videoPlayer.prepareCompleted += VideoPrepared;
        videoPlayer.loopPointReached += VideoLoopPointReached; // Changed from VideoFinished
        videoPlayer.errorReceived += VideoError;

        // Hide UI elements by default; Play() will enable them if used.
        if (videoCanvas != null) videoCanvas.gameObject.SetActive(false);
        if (videoDisplayImage != null) videoDisplayImage.enabled = false;
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= VideoPrepared;
            videoPlayer.loopPointReached -= VideoLoopPointReached;
            videoPlayer.errorReceived -= VideoError;
        }
    }
    
    public void PlayWithLoopAtEnd(VideoClip clipToPlay,
                              double loopStartTime, // Time from the beginning of the video where looping should start
                              Action onReachedLoopPoint,
                              Action onCompleteIfNoLoop, // Called if video finishes before loopStartTime
                              VideoAudioOutputMode audioOutput = VideoAudioOutputMode.None,
                                AudioSource specificAudioSource = null)
    {
        if (isStopping) { Debug.LogWarning("VPC: PlayWithLoopAtEnd called while stopping."); return; }
        if (clipToPlay == null) { Debug.LogError("VPC: PlayWithLoopAtEnd with null clip!"); onCompleteIfNoLoop?.Invoke(); return; }

        if (videoPlayer.isPlaying || videoPlayer.isPrepared)
        {
            videoPlayer.Stop();
        }
        onVideoCompleteCallback = null; // Clear any standard one-shot callback

        Debug.Log($"VPC: Preparing '{clipToPlay.name}' to play, will loop from time {loopStartTime:F2} / {clipToPlay.length:F2}.");
        
        this.isLoopingAtEnd = true;
        this.endLoopStartTime = loopStartTime;
        this.onReachedLoopPointCallback = onReachedLoopPoint;
        this.onCompleteIfNoLoopCallback = onCompleteIfNoLoop; // Store this new callback
        this.isPlaying = false;
        this.isStopping = false;
        this.uiTargetRawImage = null; // Assuming event videos don't use these directly
        this.externalRenderTexture = null;

        videoPlayer.clip = clipToPlay;
        videoPlayer.isLooping = false; // IMPORTANT: Main loop is off, we manually control the end loop.

        // --- Configure Audio Output (same as your Play method) ---
        videoPlayer.audioOutputMode = audioOutput;
        if (audioOutput == VideoAudioOutputMode.AudioSource)
        {
            AudioSource sourceToUse = specificAudioSource ?? this.audioSource;
            if (sourceToUse != null) videoPlayer.SetTargetAudioSource(0, sourceToUse);
            else videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }

        // --- Configure Video Output (assuming fullscreen event video) ---
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        if (videoPlayer.targetCamera == null) videoPlayer.targetCamera = Camera.main;

        if (videoCanvas != null) videoCanvas.gameObject.SetActive(true);
        if (videoDisplayImage != null)
        {
            videoDisplayImage.texture = null;
            videoDisplayImage.color = Color.black;
            videoDisplayImage.enabled = true;
        }
        videoPlayer.prepareCompleted += VideoPrepared_LoopAtEnd; // Use a specific prepare handler
        videoPlayer.Prepare();
    }
    
    public bool IsPlaying() 
    {
        // Return true if the videoPlayer component exists and its isPlaying property is true.
        return this.videoPlayer != null && this.videoPlayer.isPlaying;
    }
    
    public bool IsCurrentlyPlaying() // Or just IsPlaying() if no conflict
    {
        // Considers if prepared and meant to be playing, or actually playing
        return this.isPlaying || (this.videoPlayer != null && this.videoPlayer.isPlaying);
    }

    private void VideoPrepared_LoopAtEnd(VideoPlayer vp)
    {
        vp.prepareCompleted -= VideoPrepared_LoopAtEnd; // Unsubscribe self
        if (isStopping || vp.clip == null) return;

        Debug.Log($"VPC: Prepared '{vp.clip.name}' for loop-at-end. Starting playback.");
        if (videoDisplayImage != null) videoDisplayImage.color = Color.white;

        vp.Play();
        isPlaying = true;

        // If loopStartTime is effectively at or after the video's end, the onCompleteIfNoLoop should be called
        // by the standard loopPointReached mechanism.
        if (endLoopStartTime >= vp.length - 0.1) // Small buffer for precision
        {
            Debug.LogWarning($"VPC: Loop start time ({endLoopStartTime:F2}) is at or after video length ({vp.length:F2}). Video will play once.");
            // The onCompleteIfNoLoop will be triggered by VideoLoopPointReached
        }
    }

    void Update()
    {
        // Skip logic only applies if we have a callback (i.e., it's a one-shot event video)
        if (allowSkip && isPlaying && onVideoCompleteCallback != null && (Input.GetKeyDown(skipKey2) || Input.GetKeyDown(skipKey1)))
        {
            Debug.Log($"Video skipped: {videoPlayer.clip?.name}");
            StopPlaybackAndCallback();
        }
    }

    /// <summary>
    /// Plays a video with flexible options for looping, UI rendering, and audio.
    /// </summary>
    /// <param name="clipToPlay">The video clip asset.</param>
    /// <param name="onComplete">Callback for when a NON-LOOPING video finishes.</param>
    /// <param name="loop">Should the video loop?</param>
    /// <param name="targetRenderTextureForUI">RenderTexture to output to (for UI RawImage).</param>
    /// <param name="targetRawImageForUI">Direct RawImage to update (alternative to RenderTexture, less common for backgrounds).</param>
    /// <param name="audioOutput">How to handle audio.</param>
    /// <param name="specificAudioSource">Specific AudioSource to use if audioOutput is AudioSource.</param>
    public void Play(VideoClip clipToPlay,
                     Action onComplete,
                     bool loop = false,
                     RenderTexture targetRenderTextureForUI = null,
                     RawImage targetRawImageForUI = null, // Optional direct RawImage target
                     VideoAudioOutputMode audioOutput = VideoAudioOutputMode.None, // Default to None if not specified
                     AudioSource specificAudioSource = null)
    {
        if (isStopping)
        {
            Debug.LogWarning("VideoPlaybackController: Play called while already stopping. Ignoring.");
            return;
        }
        if (clipToPlay == null)
        {
            Debug.LogError("VideoPlaybackController: Play called with null VideoClip!");
            onComplete?.Invoke();
            return;
        }

        // Stop any current playback cleanly
        if (videoPlayer.isPlaying || videoPlayer.isPrepared)
        {
            Debug.LogWarning("VideoPlaybackController: VideoPlayer was busy, stopping previous playback first.");
            videoPlayer.Stop();
        }
        // Clear previous one-shot callback
        onVideoCompleteCallback = null;

        Debug.Log($"VideoPlaybackController: Preparing video '{clipToPlay.name}'. Loop: {loop}");
        this.onVideoCompleteCallback = loop ? null : onComplete; // Only set callback if not looping
        this.isPlaying = false;
        this.isStopping = false;
        isLoopingAtEnd = false; 
        this.uiTargetRawImage = targetRawImageForUI;
        this.externalRenderTexture = targetRenderTextureForUI;

        videoPlayer.clip = clipToPlay;
        videoPlayer.isLooping = loop;

        // --- Configure Audio Output ---
        videoPlayer.audioOutputMode = audioOutput;
        if (audioOutput == VideoAudioOutputMode.AudioSource)
        {
            AudioSource sourceToUse = specificAudioSource ?? this.audioSource; // Use provided or own
            if (sourceToUse != null)
            {
                videoPlayer.SetTargetAudioSource(0, sourceToUse);
                if (!sourceToUse.playOnAwake) sourceToUse.Stop(); // Ensure it's not playing something else
            }
            else
            {
                Debug.LogWarning($"VideoPlaybackController: AudioOutputMode is AudioSource, but no valid AudioSource was provided or found. Audio might not play for clip '{clipToPlay.name}'.");
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // Fallback
            }
        }

        // --- Configure Video Output ---
        if (targetRenderTextureForUI != null)
        {
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = targetRenderTextureForUI;
            if (videoCanvas != null) videoCanvas.gameObject.SetActive(false); // Ensure default canvas is hidden if rendering to RT
            if (videoDisplayImage != null) videoDisplayImage.enabled = false;
            // The external RawImage using this RT should be managed by the caller
        }
        else if (targetRawImageForUI != null) // Direct RawImage (less common for BG, but an option)
        {
            videoPlayer.renderMode = VideoRenderMode.APIOnly; // We'll manually update the RawImage texture
            if (videoCanvas != null) videoCanvas.gameObject.SetActive(false);
            if (videoDisplayImage != null) videoDisplayImage.enabled = false;
        }
        else // Assume fullscreen event video using the built-in Canvas/RawImage
        {
            videoPlayer.renderMode = VideoRenderMode.CameraNearPlane; 
            if (videoPlayer.targetCamera == null) videoPlayer.targetCamera = Camera.main; 

            if (videoCanvas != null) videoCanvas.gameObject.SetActive(true); // Show the dedicated video canvas
            if (videoDisplayImage != null)
            {
                videoDisplayImage.texture = null; 
                videoDisplayImage.color = Color.black; // Show black while preparing << GOOD
                videoDisplayImage.enabled = true;
            }
        }
        videoPlayer.Prepare();
    }

    void VideoPrepared(VideoPlayer vp)
    {
        if (isStopping || vp.clip == null) return;

        Debug.Log($"VideoPlaybackController: Prepared '{vp.clip.name}'. Starting playback.");

        // If using the built-in RawImage for fullscreen event videos
        if (externalRenderTexture == null && uiTargetRawImage == null && videoDisplayImage != null)
        {
            videoDisplayImage.texture = videoPlayer.texture; 
            videoDisplayImage.enabled = true;
            videoDisplayImage.color = Color.white; // << GOOD, becomes visible with video content
        }
        // If using externalRenderTexture, the UI RawImage is handled by the caller.
        // If using uiTargetRawImage (APIOnly), Update will handle texture assignment.

        if (videoCanvas != null && externalRenderTexture == null && uiTargetRawImage == null)
        {
             videoCanvas.gameObject.SetActive(true); // Ensure canvas for event video is visible
        }


        vp.Play();
        isPlaying = true;
    }

    void VideoLoopPointReached(VideoPlayer vp)
    {
        if (isLoopingAtEnd) // If this video was set up for loop-at-end
        {
            if (vp.isLooping) // If we successfully set it to loop from Update()
            {
                // Already looping from the desired point, Unity handles it.
                // GameManager will stop it via ForceStop() when Enter is pressed.
                Debug.Log($"VPC: Looping video '{vp.clip?.name}' (end-loop mode) reached actual end, continuing loop from {endLoopStartTime:F2}.");
                // We could force vp.time = endLoopStartTime here again for safety, but vp.isLooping should handle it.
            }
            else // It reached the end but wasn't set to loop (e.g., loop point was >= length)
            {
                Debug.Log($"VPC: Video '{vp.clip?.name}' (end-loop mode) finished before loop section or loop point was too late.");
                isLoopingAtEnd = false; // Exit this special mode
                isPlaying = false;
                Action callback = onCompleteIfNoLoopCallback;
                onCompleteIfNoLoopCallback = null;
                callback?.Invoke(); // Invoke the specific callback for this case
                // Don't call StopPlaybackAndCallback() as that's for the generic onVideoComplete
            }
        }
        else // Standard non-looping video
        {
            Debug.Log($"VPC: Non-looping video '{vp.clip?.name}' finished naturally (standard path).");
            StopPlaybackAndCallback(); // This calls the generic onVideoCompleteCallback
        }
    }

    void VideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"VideoPlaybackController Error: {message} (Clip: {source.clip?.name})");
        StopPlaybackAndCallback(); // Treat error as completion of a one-shot video
    }

    private void StopPlaybackAndCallback()
    {
        if (isStopping) return; // Already processed
        if (!isPlaying && !videoPlayer.isPrepared && !videoPlayer.isPlaying)
        {
             // If we try to stop something that was never really playing or prepared fully,
             // still try to run callback and hide UI.
             Debug.LogWarning("StopPlaybackAndCallback called when player might not have been fully active.");
        }


        isStopping = true;
        isPlaying = false;

        if (videoPlayer.isPlaying) videoPlayer.Stop();

        // Hide UI elements if they were for this video controller instance
        if (videoCanvas != null && externalRenderTexture == null && uiTargetRawImage == null) {
            videoCanvas.gameObject.SetActive(false);
        }
        if (uiTargetRawImage != null) uiTargetRawImage.enabled = false; // Hide direct RawImage target


        Action callback = onVideoCompleteCallback;
        onVideoCompleteCallback = null; // Clear before invoking to prevent re-entrancy issues
        
        Debug.Log("VideoPlaybackController: Invoking onVideoCompleteCallback (if any).");
        callback?.Invoke();

        // The GameManager is responsible for Destroying this GameObject instance.
    }

    // Public method to explicitly stop playback (e.g., when GameManager wants to switch videos)
    public void ForceStop()
    {
        Debug.Log($"VideoPlaybackController: ForceStop called for clip '{videoPlayer.clip?.name}'.");
        onVideoCompleteCallback = null; // Ensure no lingering callback is invoked on a forced stop
        StopPlaybackAndCallback();
    }
}