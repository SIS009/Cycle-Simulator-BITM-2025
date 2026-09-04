using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SBPScripts;

/// <summary>
/// Detects an accident (a hard collision, or entering an <see cref="AccidentZone"/>),
/// plays an on-screen particle effect, and returns the bicycle to its starting pose.
///
/// Put this on the bicycle root - the same object that owns the main Rigidbody.
/// Colliders that live on child objects can forward their hits through
/// <see cref="AccidentCollisionRelay"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class BicycleAccidentReset : MonoBehaviour
{
    [Header("Respawn")]
    [Tooltip("Optional. Leave empty to respawn at the pose the bicycle started the scene in.")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("Seconds the bicycle stays frozen at the crash spot before it is moved back.")]
    [SerializeField] private float freezeSeconds = 1f;

    [Tooltip("Accidents are ignored for this long after one has been handled.")]
    [SerializeField] private float accidentCooldownSeconds = 2f;

    [Header("Collision Detection")]
    [SerializeField] private bool detectCollisions = true;

    [Tooltip("Only collisions with objects on these layers count as an accident.")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Tooltip("Optional. Only objects with this tag count. Leave empty to accept any tag on the layers above.")]
    [SerializeField] private string obstacleTag = "";

    [Tooltip("Impacts slower than this (metres per second) are treated as scraping, not an accident.")]
    [SerializeField] private float minimumImpactSpeed = 3f;

    [Header("On-screen Effect")]
    [Tooltip("Particle system played when an accident happens. Assign a prefab instance from the scene.")]
    [SerializeField] private ParticleSystem accidentParticles;

    [Tooltip("Places the particle system in front of the camera so the effect covers the screen.")]
    [SerializeField] private bool placeParticlesInFrontOfCamera = true;

    [SerializeField] private float particleDistanceFromCamera = 1.5f;

    [Tooltip("Optional. Leave empty to use Camera.main.")]
    [SerializeField] private Camera effectCamera;

    [SerializeField] private AudioSource accidentAudio;

    [Header("Control Lock")]
    [Tooltip("Optional. Leave empty to find the BicycleController on this object automatically.")]
    [SerializeField] private BicycleController bicycleController;

    [Tooltip("Any extra behaviours that must be switched off while the bicycle is being reset.")]
    [SerializeField] private MonoBehaviour[] extraBehavioursToDisable;

    [Header("Events")]
    [Tooltip("Raised the moment an accident is registered, before the reset happens.")]
    public UnityEvent onAccident;

    [Tooltip("Raised once the bicycle is back at its respawn pose and controllable again.")]
    public UnityEvent onResetComplete;

    [Header("Debug")]
    [SerializeField] private bool printAccidents = false;

    /// <summary>Number of accidents registered since the scene loaded.</summary>
    public int AccidentCount { get; private set; }

    /// <summary>True while the bicycle is frozen or being moved back.</summary>
    public bool IsResetting { get; private set; }

    private readonly List<BodyPose> bodyPoses = new List<BodyPose>();
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private float nextAccidentAllowedTime;
    private Coroutine resetRoutine;

    private struct BodyPose
    {
        public Rigidbody body;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public bool wasKinematic;
    }

    private void Awake()
    {
        if (bicycleController == null)
        {
            bicycleController = GetComponentInChildren<BicycleController>();
        }

        CacheStartPose();
    }

    private void CacheStartPose()
    {
        if (respawnPoint != null)
        {
            respawnPosition = respawnPoint.position;
            respawnRotation = respawnPoint.rotation;
        }
        else
        {
            respawnPosition = transform.position;
            respawnRotation = transform.rotation;
        }

        bodyPoses.Clear();

        // Wheels and other child Rigidbodies simulate independently, so each one's
        // pose relative to the bicycle root is stored and restored as well.
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            bodyPoses.Add(new BodyPose
            {
                body = body,
                localPosition = transform.InverseTransformPoint(body.position),
                localRotation = Quaternion.Inverse(transform.rotation) * body.rotation,
                wasKinematic = body.isKinematic
            });
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!detectCollisions)
        {
            return;
        }

        if (!IsObstacle(collision.collider))
        {
            return;
        }

        if (collision.relativeVelocity.magnitude < minimumImpactSpeed)
        {
            return;
        }

        TriggerAccident($"collision with {collision.collider.name}");
    }

    /// <summary>
    /// Called by <see cref="AccidentCollisionRelay"/> for colliders that are not on the root.
    /// </summary>
    internal void ReportChildCollision(Collision collision)
    {
        OnCollisionEnter(collision);
    }

    internal bool IsObstacle(Collider other)
    {
        if ((obstacleLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(obstacleTag) && !other.CompareTag(obstacleTag))
        {
            return false;
        }

        // Ignore the bicycle's own colliders.
        return !other.transform.IsChildOf(transform);
    }

    /// <summary>
    /// Registers an accident: plays the effect and starts the reset.
    /// Zones, scripted events and UI buttons can all call this.
    /// </summary>
    public void TriggerAccident(string reason = "")
    {
        if (Time.time < nextAccidentAllowedTime)
        {
            return;
        }

        nextAccidentAllowedTime = Time.time + accidentCooldownSeconds;
        AccidentCount++;

        if (printAccidents)
        {
            Debug.Log($"Accident {AccidentCount}: {reason}", this);
        }

        PlayAccidentEffect();
        onAccident?.Invoke();

        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
        }

        resetRoutine = StartCoroutine(AccidentRoutine());
    }

    private IEnumerator AccidentRoutine()
    {
        IsResetting = true;
        SetControlEnabled(false);
        FreezeBodies();

        if (freezeSeconds > 0f)
        {
            yield return new WaitForSeconds(freezeSeconds);
        }

        MoveToRespawn();

        // Let one physics step run with the restored pose before handing control back.
        yield return new WaitForFixedUpdate();

        UnfreezeBodies();
        SetControlEnabled(true);
        IsResetting = false;
        resetRoutine = null;

        onResetComplete?.Invoke();
    }

    /// <summary>Moves the bicycle back to its respawn pose immediately, with no effect or delay.</summary>
    public void ResetNow()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        MoveToRespawn();
        UnfreezeBodies();
        SetControlEnabled(true);
        IsResetting = false;
    }

    private void MoveToRespawn()
    {
        Vector3 targetPosition = respawnPoint != null ? respawnPoint.position : respawnPosition;
        Quaternion targetRotation = respawnPoint != null ? respawnPoint.rotation : respawnRotation;

        transform.SetPositionAndRotation(targetPosition, targetRotation);

        for (int i = 0; i < bodyPoses.Count; i++)
        {
            BodyPose pose = bodyPoses[i];

            if (pose.body == null)
            {
                continue;
            }

            pose.body.position = targetPosition + targetRotation * pose.localPosition;
            pose.body.rotation = targetRotation * pose.localRotation;

            // Velocities cannot be written on a kinematic body; UnfreezeBodies clears
            // them again once the body is dynamic.
            if (!pose.body.isKinematic)
            {
                SetVelocity(pose.body, Vector3.zero);
                SetAngularVelocity(pose.body, Vector3.zero);
            }
        }

        Physics.SyncTransforms();
    }

    private void FreezeBodies()
    {
        for (int i = 0; i < bodyPoses.Count; i++)
        {
            Rigidbody body = bodyPoses[i].body;

            if (body == null)
            {
                continue;
            }

            if (!body.isKinematic)
            {
                SetVelocity(body, Vector3.zero);
                SetAngularVelocity(body, Vector3.zero);
            }

            body.isKinematic = true;
        }
    }

    private void UnfreezeBodies()
    {
        for (int i = 0; i < bodyPoses.Count; i++)
        {
            BodyPose pose = bodyPoses[i];

            if (pose.body == null)
            {
                continue;
            }

            pose.body.isKinematic = pose.wasKinematic;

            if (!pose.wasKinematic)
            {
                SetVelocity(pose.body, Vector3.zero);
                SetAngularVelocity(pose.body, Vector3.zero);
            }
        }
    }

    private void SetControlEnabled(bool isEnabled)
    {
        if (bicycleController != null)
        {
            bicycleController.enabled = isEnabled;
        }

        if (extraBehavioursToDisable == null)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in extraBehavioursToDisable)
        {
            if (behaviour != null)
            {
                behaviour.enabled = isEnabled;
            }
        }
    }

    private void PlayAccidentEffect()
    {
        if (accidentAudio != null)
        {
            accidentAudio.Play();
        }

        if (accidentParticles == null)
        {
            return;
        }

        if (placeParticlesInFrontOfCamera)
        {
            Camera camera = effectCamera != null ? effectCamera : Camera.main;

            if (camera != null)
            {
                Transform particleTransform = accidentParticles.transform;
                particleTransform.SetParent(camera.transform, false);
                particleTransform.localPosition = Vector3.forward * particleDistanceFromCamera;
                particleTransform.localRotation = Quaternion.identity;
            }
        }

        accidentParticles.Clear(true);
        accidentParticles.Play(true);
    }

    // Rigidbody.velocity was renamed to linearVelocity in Unity 6.
    private static void SetVelocity(Rigidbody body, Vector3 value)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = value;
#else
        body.velocity = value;
#endif
    }

    private static void SetAngularVelocity(Rigidbody body, Vector3 value)
    {
        body.angularVelocity = value;
    }
}
