using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager Instance;

    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private float typeSpeed = 0.02f;
    [SerializeField] private float startDelay = 0.3f;
    [SerializeField] private float charLifetime = 3.0f; 

    private Coroutine subtitleCoroutine;
    private List<char> activeChars = new List<char>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void ShowSubtitle(string text)
    {
        ShowSubtitle(text, charLifetime);
    }

    public void ShowSubtitle(string text, float _ignoredDuration)
    {
        if (subtitleCoroutine != null)
            StopCoroutine(subtitleCoroutine);

        subtitleCoroutine = StartCoroutine(ShowWithCharTimers(text));
    }

    private IEnumerator ShowWithCharTimers(string fullText)
    {
        subtitleText.gameObject.SetActive(true);
        subtitleText.text = "";
        activeChars.Clear();

        if (startDelay > 0)
            yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < fullText.Length; i++)
        {
            char c = fullText[i];
            activeChars.Add(c);
            UpdateText();
            StartCoroutine(RemoveCharAfterDelay(i, charLifetime));
            yield return new WaitForSeconds(typeSpeed);
        }
    }

    private IEnumerator RemoveCharAfterDelay(int charIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (charIndex < activeChars.Count)
        {
            activeChars[charIndex] = '\0';  
            UpdateText();
        }

        if (!activeChars.Exists(c => c != '\0'))
        {
            subtitleText.gameObject.SetActive(false);
        }
    }

    private void UpdateText()
    {
        subtitleText.text = string.Concat(activeChars).Replace("\0", "");
    }
}
