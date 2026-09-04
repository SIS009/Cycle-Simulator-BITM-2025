using UnityEngine;

/// <summary>
/// Optional helper. Unity delivers OnCollisionEnter to the GameObject that owns the
/// Rigidbody, so a bicycle whose colliders sit on child objects with their own
/// Rigidbodies (wheels, frame parts) will not raise collisions on the root.
///
/// Put this on those child objects to forward their impacts to the
/// <see cref="BicycleAccidentReset"/> on the bicycle root.
/// </summary>
public sealed class AccidentCollisionRelay : MonoBehaviour
{
    [Tooltip("Optional. Leave empty to find the handler on a parent object automatically.")]
    [SerializeField] private BicycleAccidentReset accidentHandler;

    private void Awake()
    {
        if (accidentHandler == null)
        {
            accidentHandler = GetComponentInParent<BicycleAccidentReset>();
        }

        if (accidentHandler == null)
        {
            Debug.LogError(
                "No BicycleAccidentReset found on this object or its parents.", this);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (accidentHandler == null)
        {
            return;
        }

        accidentHandler.ReportChildCollision(collision);
    }
}
