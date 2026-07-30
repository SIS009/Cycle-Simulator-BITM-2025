using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalTeleportationManager : MonoBehaviour
{
    [Header("Portal Colliders")]
    [SerializeField] private GameObject portalExitCollider;

    [Header("Scene")]
    [SerializeField] private string targetSceneName;

    private AsyncOperation sceneLoadOperation;
    private bool isLoading;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == portalExitCollider && !isLoading)
        {
            StartLoadingScene();
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

        LoadingScreen.instance.LoadScene(targetSceneName);
    }
}