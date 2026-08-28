using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// Async scene loader with loading screen, progress bar,
/// and runtime environment-lighting refresh.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen instance;

    [Header("UI References")]
    [SerializeField] private GameObject loadingScreenPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text percentageText;
    [SerializeField] private Image fillImage;

    [Header("Environment Lighting")]
    [Tooltip("Forces Environment Lighting Source to Skybox after scene loading.")]
    [SerializeField] private bool forceSkyboxEnvironment = true;

    [Tooltip("Environment Lighting Intensity Multiplier.")]
    [SerializeField] private float environmentIntensityMultiplier = 0.8f;

    [Tooltip("Frames to wait after the new scene activates before refreshing lighting.")]
    [SerializeField] private int lightingRefreshDelayFrames = 2;

    [Header("Debug")]
    [SerializeField] private bool showLightingDebug = true;

    private bool isLoading = false;


    private void Awake()
    {
        // Singleton
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Keep loader alive between scenes
        DontDestroyOnLoad(gameObject);

        if (loadingScreenPanel != null)
            loadingScreenPanel.SetActive(false);

        UpdateUI(0f);
    }


    /// <summary>
    /// Load scene by name.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }


    /// <summary>
    /// Load scene by Build Index.
    /// </summary>
    public void LoadScene(int sceneBuildIndex)
    {
        if (isLoading)
            return;

        StartCoroutine(LoadSceneRoutine(sceneBuildIndex));
    }


    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;

        ShowLoadingScreen();

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        if (operation == null)
        {
            Debug.LogError(
                "Could not load scene: " + sceneName +
                ". Make sure it is added to Build Settings."
            );

            HideLoadingScreen();
            isLoading = false;

            yield break;
        }

        yield return RunLoad(operation);
    }


    private IEnumerator LoadSceneRoutine(int sceneBuildIndex)
    {
        isLoading = true;

        ShowLoadingScreen();

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                sceneBuildIndex,
                LoadSceneMode.Single
            );

        if (operation == null)
        {
            Debug.LogError(
                "Could not load scene Build Index: " +
                sceneBuildIndex
            );

            HideLoadingScreen();
            isLoading = false;

            yield break;
        }

        yield return RunLoad(operation);
    }


    private IEnumerator RunLoad(AsyncOperation operation)
    {
        operation.allowSceneActivation = false;

        /*
         * Unity loads the scene from 0 -> 0.9.
         * 0.9 means it is ready for activation.
         */
        while (operation.progress < 0.9f)
        {
            float progress =
                Mathf.Clamp01(operation.progress / 0.9f);

            UpdateUI(progress);

            yield return null;
        }

        // Show 100%
        UpdateUI(1f);

        // Allow one frame for UI
        yield return null;

        /*
         * Activate the destination scene.
         */
        operation.allowSceneActivation = true;

        /*
         * Wait until scene activation has completely finished.
         */
        while (!operation.isDone)
        {
            yield return null;
        }

        /*
         * IMPORTANT:
         * Wait a few frames for the destination scene's:
         *
         * RenderSettings
         * Skybox
         * Sun
         * Camera
         * Lighting
         *
         * to initialize.
         */
        for (int i = 0; i < lightingRefreshDelayFrames; i++)
        {
            yield return null;
        }

        /*
         * Refresh environment lighting AFTER
         * the destination scene is active.
         */
        RefreshEnvironmentLighting();

        /*
         * Give Unity another couple of frames
         * to update the ambient probe.
         */
        yield return null;
        yield return null;

        HideLoadingScreen();

        isLoading = false;
    }


    /// <summary>
    /// Forces Unity to refresh Skybox based Environment Lighting.
    /// </summary>
    private void RefreshEnvironmentLighting()
    {
        /*
         * Your Lighting window:
         *
         * Environment Lighting
         * Source = Skybox
         *
         * This forces the same setting at runtime.
         */
        if (forceSkyboxEnvironment)
        {
            RenderSettings.ambientMode =
                AmbientMode.Skybox;
        }


        /*
         * This is the runtime equivalent of:
         *
         * Lighting
         * -> Environment
         * -> Environment Lighting
         * -> Intensity Multiplier
         */
        RenderSettings.ambientIntensity =
            environmentIntensityMultiplier;


        /*
         * Rebuild / refresh Unity's environment probe
         * from the current scene's skybox.
         */
        DynamicGI.UpdateEnvironment();


        /*
         * Debug output so we can verify exactly
         * what Unity is using at runtime.
         */
        if (showLightingDebug)
        {
            Debug.Log(
                "========== RUNTIME LIGHTING =========="
            );

            Debug.Log(
                "Scene: " +
                SceneManager.GetActiveScene().name
            );

            Debug.Log(
                "Skybox: " +
                (RenderSettings.skybox != null
                    ? RenderSettings.skybox.name
                    : "NULL")
            );

            Debug.Log(
                "Ambient Mode: " +
                RenderSettings.ambientMode
            );

            Debug.Log(
                "Ambient Intensity: " +
                RenderSettings.ambientIntensity
            );

            Debug.Log(
                "Sun: " +
                (RenderSettings.sun != null
                    ? RenderSettings.sun.name
                    : "NULL")
            );

            Debug.Log(
                "======================================"
            );
        }
    }


    private void ShowLoadingScreen()
    {
        UpdateUI(0f);

        if (loadingScreenPanel != null)
            loadingScreenPanel.SetActive(true);
    }


    private void HideLoadingScreen()
    {
        if (loadingScreenPanel != null)
            loadingScreenPanel.SetActive(false);
    }


    /// <summary>
    /// Updates loading bar and loading percentage.
    /// </summary>
    private void UpdateUI(float value)
    {
        value = Mathf.Clamp01(value);

        if (progressBar != null)
        {
            progressBar.value = value;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = value;
        }

        if (percentageText != null)
        {
            percentageText.text =
                "LOADING: " +
                (value * 100f).ToString("F1") +
                "%";
        }
    }
}