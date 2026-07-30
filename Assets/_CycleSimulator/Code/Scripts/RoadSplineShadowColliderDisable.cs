using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class RoadSplineShadowColliderDisable : MonoBehaviour
{
    [SerializeField] private GameObject roadSplineParent;

    private void Start()
    {
        StartCoroutine(DisableShadowsAndColliders());
    }

    private IEnumerator DisableShadowsAndColliders()
    {
        yield return new WaitForSeconds(0.2f);

        if (roadSplineParent == null)
        {
            Debug.LogError("Road Spline Parent is not assigned.", this);
            yield break;
        }

        MeshRenderer[] meshRenderers =
            roadSplineParent.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        Collider[] colliders =
            roadSplineParent.GetComponentsInChildren<Collider>(true);

        foreach (Collider roadCollider in colliders)
        {
            roadCollider.enabled = false;
        }
    }
}