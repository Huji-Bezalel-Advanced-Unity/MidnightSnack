// GameWorldInitializer.cs (Example - place in GameWorld scene)
using UnityEngine;
using System.Collections;

public class GameWorldInitializer : MonoBehaviour
{
    void Start()
    {
        // Ensure players/camera are positioned before fading in
        StartCoroutine(FinalizeLoadAndFadeIn());
    }

    IEnumerator FinalizeLoadAndFadeIn()
    {
        // Optional: Wait a frame or two to ensure all Start methods have run
        // and initial positions are set.
        yield return null; 
        yield return null;

        if (GameManager.Instance != null && GameManager.Instance.blackScreenOverlay != null)
        {
            if (GameManager.Instance.blackScreenOverlay.gameObject.activeSelf)
            {
                Debug.Log("GameWorldInitializer: Fading out black screen.");
                // GameManager.Instance.blackScreenOverlay.CrossFadeAlpha(0f, 0.5f, true); // Example fade out
                // StartCoroutine(FadeOutBlackScreen(GameManager.Instance.blackScreenOverlay, 0.5f));

                // For now, just disable it after a short delay to test
                yield return new WaitForSeconds(0.2f); // Small delay to ensure game renders once
                GameManager.Instance.blackScreenOverlay.gameObject.SetActive(false);
                Debug.Log("GameWorldInitializer: Black screen deactivated.");
            }
        }
    }

    // IEnumerator FadeOutBlackScreen(Image screen, float duration)
    // {
    //     float currentTime = 0;
    //     Color originalColor = screen.color; // Assuming it's black
    //     float startAlpha = screen.color.a; // Should be 1 if faded in

    //     while (currentTime < duration)
    //     {
    //         currentTime += Time.deltaTime;
    //         float alpha = Mathf.Lerp(startAlpha, 0f, currentTime / duration);
    //         screen.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    //         yield return null;
    //     }
    //     screen.gameObject.SetActive(false);
    // }
}