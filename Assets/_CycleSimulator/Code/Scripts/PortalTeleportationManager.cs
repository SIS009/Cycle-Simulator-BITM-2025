using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalTeleportationManager : MonoBehaviour
{
    [Header("Portal Colliders")]
    [SerializeField] private GameObject portalStartCollider;
    [SerializeField] private GameObject portalExitCollider;

    [Header("Scene")]
    [SerializeField] private string targetSceneName;

    private AsyncOperation sceneLoadOperation;
    private bool isLoading;

    private void OnTriggerEnter(Collider other)
    {
        if (BelongsTo(other, portalStartCollider))
        {
            StartLoadingScene();
            return;
        }

        if (BelongsTo(other, portalExitCollider))
        {
            ActivateLoadedScene();
        }
    }

    private void StartLoadingScene()
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("Target scene name has not been assigned.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError(
                $"Scene '{targetSceneName}' could not be loaded. " +
                "Make sure it has been added to the build configuration."
            );
            return;
        }

        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        isLoading = true;

        sceneLoadOperation = SceneManager.LoadSceneAsync(
            targetSceneName,
            LoadSceneMode.Single
        );

        if (sceneLoadOperation == null)
        {
            Debug.LogError($"Failed to begin loading '{targetSceneName}'.");
            isLoading = false;
            yield break;
        }

        // Load the scene, but do not switch to it yet.
        sceneLoadOperation.allowSceneActivation = false;

        while (sceneLoadOperation.progress < 0.9f)
        {
            yield return null;
        }

        Debug.Log($"Scene '{targetSceneName}' is ready for activation.");
    }

    private void ActivateLoadedScene()
    {
        if (sceneLoadOperation == null)
        {
            Debug.LogWarning(
                "The cyclist reached the exit portal before scene loading started."
            );
            return;
        }

        // The new scene activates as soon as loading is ready.
        sceneLoadOperation.allowSceneActivation = true;
    }

    private static bool BelongsTo(Collider collider, GameObject portalObject)
    {
        if (collider == null || portalObject == null)
            return false;

        return collider.gameObject == portalObject ||
               collider.transform.IsChildOf(portalObject.transform);
    }
}