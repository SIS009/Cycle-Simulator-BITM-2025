using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Remove this line if you are not using TextMeshPro

/// <summary>
/// Handles an asynchronous scene load while displaying a loading screen
/// with an animated progress bar and percentage text.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen instance;

    [Header("UI References")]
    [SerializeField] private GameObject loadingScreenPanel; // Full-screen panel shown while loading
    [SerializeField] private Slider progressBar;            // The loading bar (Slider, min 0 / max 1)
    [SerializeField] private TMP_Text percentageText;       // Optional "42%" label (use Text if no TMP)
    [SerializeField] private Image fillImage;               // Optional: fill Image if you use a bar without a Slider

    [Header("Settings")]
    [Tooltip("How quickly the bar catches up to the real progress. Higher = snappier.")]
    [SerializeField] private float fillSpeed = 2f;

    [Tooltip("Seconds to hold on 100% before activating the scene.")]
    [SerializeField] private float holdAtFullDuration = 0.25f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingScreenPanel != null)
            loadingScreenPanel.SetActive(false);
    }

    /// <summary>Call this from a button or another script to load a scene by name.</summary>
    public void LoadScene(string sceneName)
    {
        gameObject.SetActive(true);
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    /// <summary>Call this to load a scene by build index instead.</summary>
    public void LoadScene(int sceneBuildIndex)
    {
        gameObject.SetActive(true);
        StartCoroutine(LoadSceneRoutine(sceneBuildIndex));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return RunLoad(SceneManager.LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneRoutine(int sceneBuildIndex)
    {
        yield return RunLoad(SceneManager.LoadSceneAsync(sceneBuildIndex));
    }

    private IEnumerator RunLoad(AsyncOperation operation)
    {
        if (loadingScreenPanel != null)
            loadingScreenPanel.SetActive(true);

        // Don't let the scene swap in the instant it finishes loading.
        operation.allowSceneActivation = false;

        float displayedProgress = 0f;

        while (!operation.isDone)
        {
            // Unity reports 0.0 -> 0.9 during load, then 0.9 -> 1.0 on activation.
            // Normalize so the bar reaches a true 100%.
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);

            // Smoothly move the displayed value toward the real progress.
            displayedProgress = Mathf.MoveTowards(
                displayedProgress, targetProgress, fillSpeed * Time.deltaTime);

            UpdateUI(displayedProgress);

            // Once the smoothed bar has visually reached the end, activate the scene.
            if (displayedProgress >= 0.999f && targetProgress >= 1f)
            {
                UpdateUI(1f);
                if (holdAtFullDuration > 0f)
                    yield return new WaitForSeconds(holdAtFullDuration);

                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    private void UpdateUI(float value)
    {
        if (progressBar != null)
            progressBar.value = value;

        if (fillImage != null)
            fillImage.fillAmount = value;

        if (percentageText != null)
            percentageText.text = "LOADING: " + Mathf.RoundToInt(value * 100f) + "%";
    }
}