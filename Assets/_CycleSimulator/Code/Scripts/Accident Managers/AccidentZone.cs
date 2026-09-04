using UnityEngine;

/// <summary>
/// A volume that counts as an accident. Put this on a GameObject with a Collider
/// marked "Is Trigger" - a road edge, water, a pit, an oncoming-traffic lane.
///
/// The bicycle needs a <see cref="BicycleAccidentReset"/> component on its root
/// and a Rigidbody, which the SBP bicycle already has.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class AccidentZone : MonoBehaviour
{
    [Tooltip("Only objects with this tag set off the zone. Leave empty to allow anything carrying a BicycleAccidentReset.")]
    [SerializeField] private string triggeringTag = "Cycle";

    [Tooltip("Optional. Shown in the console when accident logging is enabled on the bicycle.")]
    [SerializeField] private string zoneName = "";

    [Tooltip("Fire again every frame the bicycle stays inside, instead of only on entry.")]
    [SerializeField] private bool triggerWhileInside = false;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (triggerWhileInside)
        {
            HandleContact(other);
        }
    }

    private void HandleContact(Collider other)
    {
        if (!string.IsNullOrEmpty(triggeringTag) && !other.CompareTag(triggeringTag))
        {
            return;
        }

        // The trigger may report a child collider, so search upwards from it.
        BicycleAccidentReset bicycle = other.GetComponentInParent<BicycleAccidentReset>();

        if (bicycle == null)
        {
            return;
        }

        string reason = string.IsNullOrEmpty(zoneName)
            ? $"entered zone {name}"
            : $"entered zone {zoneName}";

        bicycle.TriggerAccident(reason);
    }

    private void OnDrawGizmos()
    {
        Collider zoneCollider = GetComponent<Collider>();

        if (zoneCollider == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.25f);
        Bounds bounds = zoneCollider.bounds;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
