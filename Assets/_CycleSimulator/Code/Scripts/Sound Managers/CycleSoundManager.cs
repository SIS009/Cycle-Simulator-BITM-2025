using System;
using UnityEngine;
using UnityEngine.Audio;
using SBPScripts;

// ============== INSTRUCTION ==============
// Create an empty GameObject in the level, name it "Cycle Sounds", and add this script.
// Leave "Find Bicycle Automatically" ticked and the BicycleController is picked up on its own.
// Assign the clips:
//   Rolling Clip -> cycle_moving_on_concrete   (on the Default surface entry)
//   Accident Clip-> accident
//   Horn Clip    -> car_horn_single
// Every AudioSource is created at run time, so nothing else needs wiring.
// Each level can use its own rolling clip - concrete on Earth, and a different
// clip for the Mars, Europa and Titan surfaces.

/// <summary>
/// Drives the cycling sound from the bicycle's own state: a speed-driven rolling
/// loop while it moves, plus accident and horn sounds on request.
///
/// One of these per level, on any GameObject.
/// </summary>
[DisallowMultipleComponent]
public sealed class CycleSoundManager : MonoBehaviour
{
    /// <summary>
    /// A rolling sound for one kind of ground. The entry whose <see cref="groundTag"/>
    /// matches the ground under the rear wheel is used; entries with an empty tag act
    /// as the fallback.
    /// </summary>
    [Serializable]
    public sealed class SurfaceSound
    {
        [Tooltip("Only used to identify this entry in the inspector.")]
        public string name = "Default";

        [Tooltip("Ground with this tag uses this sound. Leave empty to make this the fallback.")]
        public string groundTag = "";

        [Tooltip("Looping sound played while the bicycle rolls over this ground.")]
        public AudioClip rollingClip;

        [Range(0f, 2f)]
        [Tooltip("Multiplies the rolling volume on this ground.")]
        public float volumeScale = 1f;

        [Range(0.1f, 3f)]
        [Tooltip("Multiplies the rolling pitch on this ground.")]
        public float pitchScale = 1f;
    }

    /// <summary>The manager in the currently loaded level, for other scripts to call into.</summary>
    public static CycleSoundManager Instance { get; private set; }

    [Header("Bicycle")]
    [Tooltip("Optional. Leave empty to find the BicycleController in this level automatically.")]
    [SerializeField] private BicycleController bicycleController;
    [SerializeField] private bool findBicycleAutomatically = true;

    [Header("Output")]
    [Tooltip("Optional. Routes every cycling sound through this mixer group.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Range(0f, 1f)]
    [Tooltip("Master volume for all sounds this script plays.")]
    [SerializeField] private float masterVolume = 1f;

    [Tooltip("Moves the audio sources with the bicycle. Needed when Spatial Blend is above 0.")]
    [SerializeField] private bool followBicycle = true;

    [Range(0f, 1f)]
    [Tooltip("0 plays the sounds in 2D, 1 plays them fully positioned in the world.")]
    [SerializeField] private float spatialBlend = 0f;

    [Header("Movement Detection")]
    [Tooltip("Speed (m/s) the bicycle must reach before it counts as moving.")]
    [SerializeField] private float startMovingSpeed = 0.8f;

    [Tooltip("Speed (m/s) the bicycle must drop below before it counts as stopped. Keep it under Start Moving Speed.")]
    [SerializeField] private float stopMovingSpeed = 0.35f;

    [Tooltip("Speed (m/s) treated as full volume and pitch. 0 uses the BicycleController's top speed.")]
    [SerializeField] private float maximumSpeed = 0f;

    [Header("Rolling Sound")]
    [Tooltip("One entry per ground type. The first entry with an empty tag is the fallback.")]
    [SerializeField]
    private SurfaceSound[] surfaces =
    {
        new SurfaceSound { name = "Default", groundTag = "", volumeScale = 1f, pitchScale = 1f }
    };

    [Tooltip("Rolling volume across the speed range, from standstill (0) to Maximum Speed (1).")]
    [SerializeField]
    private AnimationCurve rollingVolumeBySpeed = AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f);

    [Tooltip("Rolling pitch across the speed range, from standstill (0) to Maximum Speed (1).")]
    [SerializeField]
    private AnimationCurve rollingPitchBySpeed = AnimationCurve.Linear(0f, 0.85f, 1f, 1.35f);

    [Range(0f, 1f)]
    [Tooltip("Rolling volume while the rider is coasting instead of pedalling.")]
    [SerializeField] private float coastingVolumeMultiplier = 0.75f;

    [Tooltip("How quickly the rolling volume follows the speed. Larger is snappier.")]
    [SerializeField] private float rollingFadeSpeed = 3f;

    [Tooltip("Silences the rolling sound while the bicycle is off the ground.")]
    [SerializeField] private bool muteRollingWhenAirborne = true;

    [Header("Ground Check")]
    [Tooltip("Layers the ground check ray can hit.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("How far below the rear wheel the ground is searched for.")]
    [SerializeField] private float groundCheckDistance = 1.5f;

    [Tooltip("Seconds between ground checks. Ground rarely changes every frame.")]
    [SerializeField] private float groundCheckInterval = 0.25f;

    [Header("One-shot Sounds")]
    [Tooltip("Played by PlayAccident(). Hook this up to BicycleAccidentReset's On Accident event.")]
    [SerializeField] private AudioClip accidentClip;
    [Range(0f, 1f)] [SerializeField] private float accidentVolume = 1f;

    [Tooltip("Played by PlayHorn().")]
    [SerializeField] private AudioClip hornClip;
    [Range(0f, 1f)] [SerializeField] private float hornVolume = 1f;

    [Tooltip("Optional. Press this key to sound the horn. Set to None to disable.")]
    [SerializeField] private KeyCode hornKey = KeyCode.None;

    [Header("Debug")]
    [SerializeField] private bool printStateChanges = false;

    /// <summary>Current speed of the bicycle in metres per second.</summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>True while the bicycle is rolling rather than standing still.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Name of the surface entry currently driving the rolling sound.</summary>
    public string CurrentSurfaceName => activeSurface != null ? activeSurface.name : "None";

    private AudioSource rollingSource;
    private AudioSource oneShotSource;

    private SurfaceSound activeSurface;
    private SurfaceSound fallbackSurface;

    private float currentRollingVolume;
    private float nextGroundCheckTime;
    private float nextBicycleSearchTime;
    private bool isMuted;

    private void Awake()
    {
        Instance = this;

        fallbackSurface = FindFallbackSurface();
        activeSurface = fallbackSurface;

        rollingSource = CreateAudioSource("Cycle Rolling Loop", true);
        oneShotSource = CreateAudioSource("Cycle One Shots", false);

        FindBicycleController();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private AudioSource CreateAudioSource(string sourceName, bool loop)
    {
        GameObject holder = new GameObject(sourceName);
        holder.transform.SetParent(transform, false);

        AudioSource source = holder.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        source.outputAudioMixerGroup = outputMixerGroup;
        source.volume = 0f;

        return source;
    }

    private SurfaceSound FindFallbackSurface()
    {
        if (surfaces == null || surfaces.Length == 0)
        {
            return null;
        }

        foreach (SurfaceSound surface in surfaces)
        {
            if (surface != null && string.IsNullOrEmpty(surface.groundTag))
            {
                return surface;
            }
        }

        // No entry was left untagged, so the first one stands in as the fallback.
        return surfaces[0];
    }

    private void Update()
    {
        if (bicycleController == null)
        {
            if (findBicycleAutomatically && Time.unscaledTime >= nextBicycleSearchTime)
            {
                FindBicycleController();
                nextBicycleSearchTime = Time.unscaledTime + 1f;
            }

            FadeOutRolling();
            return;
        }

        if (followBicycle)
        {
            transform.position = bicycleController.transform.position;
        }

        UpdateSpeed();
        UpdateMovementState();
        UpdateSurface();
        UpdateRollingSound();

        if (hornKey != KeyCode.None && Input.GetKeyDown(hornKey))
        {
            PlayHorn();
        }
    }

    private void UpdateSpeed()
    {
        Rigidbody bicycleRigidbody = bicycleController.rb;

        CurrentSpeed = bicycleRigidbody != null
            ? GetVelocity(bicycleRigidbody).magnitude
            : 0f;
    }

    /// <summary>
    /// Gates the rolling loop. Two different thresholds are used so a bicycle
    /// hovering around one speed cannot flicker the sound on and off.
    /// </summary>
    private void UpdateMovementState()
    {
        if (!IsMoving && CurrentSpeed >= startMovingSpeed)
        {
            IsMoving = true;

            if (printStateChanges)
            {
                Debug.Log("Cycle sounds: started moving.", this);
            }

            return;
        }

        if (IsMoving && CurrentSpeed <= stopMovingSpeed)
        {
            IsMoving = false;

            if (printStateChanges)
            {
                Debug.Log("Cycle sounds: came to a stop.", this);
            }
        }
    }

    private void UpdateSurface()
    {
        if (surfaces == null || surfaces.Length <= 1)
        {
            return;
        }

        if (Time.time < nextGroundCheckTime)
        {
            return;
        }

        nextGroundCheckTime = Time.time + Mathf.Max(0.02f, groundCheckInterval);

        SurfaceSound detected = fallbackSurface;
        Transform rearWheel = bicycleController.rPhysicsWheel != null
            ? bicycleController.rPhysicsWheel.transform
            : bicycleController.transform;

        Vector3 rayOrigin = rearWheel.position + Vector3.up * 0.2f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance + 0.2f,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            foreach (SurfaceSound surface in surfaces)
            {
                if (surface == null || string.IsNullOrEmpty(surface.groundTag))
                {
                    continue;
                }

                // Compared as plain strings: CompareTag throws when the tag has not
                // been created in the Tag Manager yet.
                if (string.Equals(hit.collider.tag, surface.groundTag, StringComparison.Ordinal))
                {
                    detected = surface;
                    break;
                }
            }
        }

        if (detected == activeSurface)
        {
            return;
        }

        activeSurface = detected;

        if (printStateChanges)
        {
            Debug.Log($"Cycle sounds: surface is now {CurrentSurfaceName}.", this);
        }
    }

    private void UpdateRollingSound()
    {
        AudioClip rollingClip = activeSurface != null ? activeSurface.rollingClip : null;

        if (rollingClip == null)
        {
            FadeOutRolling();
            return;
        }

        if (rollingSource.clip != rollingClip)
        {
            rollingSource.clip = rollingClip;
            rollingSource.Play();
        }
        else if (!rollingSource.isPlaying)
        {
            rollingSource.Play();
        }

        float speedRatio = GetSpeedRatio();
        bool isSilenced = isMuted ||
                          !IsMoving ||
                          (muteRollingWhenAirborne && bicycleController.isAirborne);

        float targetVolume = 0f;

        if (!isSilenced)
        {
            targetVolume =
                Mathf.Clamp01(rollingVolumeBySpeed.Evaluate(speedRatio)) *
                activeSurface.volumeScale *
                masterVolume;

            // Coasting is quieter than pedalling.
            if (bicycleController.rawCustomAccelerationAxis <= 0.001f)
            {
                targetVolume *= coastingVolumeMultiplier;
            }
        }

        currentRollingVolume = Mathf.MoveTowards(
            currentRollingVolume,
            targetVolume,
            Mathf.Max(0.01f, rollingFadeSpeed) * Time.deltaTime);

        rollingSource.volume = currentRollingVolume;

        rollingSource.pitch = Mathf.Max(
            0.01f,
            rollingPitchBySpeed.Evaluate(speedRatio) * activeSurface.pitchScale);
    }

    private void FadeOutRolling()
    {
        if (rollingSource == null)
        {
            return;
        }

        currentRollingVolume = Mathf.MoveTowards(
            currentRollingVolume,
            0f,
            Mathf.Max(0.01f, rollingFadeSpeed) * Time.deltaTime);

        rollingSource.volume = currentRollingVolume;
    }

    private float GetSpeedRatio()
    {
        float topSpeed = maximumSpeed > 0f
            ? maximumSpeed
            : Mathf.Max(1f, bicycleController.topSpeed);

        return Mathf.Clamp01(CurrentSpeed / topSpeed);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || isMuted || oneShotSource == null)
        {
            return;
        }

        oneShotSource.volume = 1f;
        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume);
    }

    /// <summary>
    /// Plays the crash sound. Wire this to the On Accident event of
    /// BicycleAccidentReset, or call it from any other script.
    /// </summary>
    public void PlayAccident()
    {
        PlayOneShot(accidentClip, accidentVolume);
    }

    /// <summary>Sounds the bicycle horn.</summary>
    public void PlayHorn()
    {
        PlayOneShot(hornClip, hornVolume);
    }

    /// <summary>Silences every cycling sound without stopping the simulation.</summary>
    public void SetMuted(bool muted)
    {
        isMuted = muted;

        if (muted && oneShotSource != null)
        {
            oneShotSource.Stop();
        }
    }

    /// <summary>Sets the master volume for every sound this script plays.</summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Switches the rolling sound to a named surface entry, for cases where the
    /// ground cannot be identified by a tag.
    /// </summary>
    public void SetSurface(string surfaceName)
    {
        if (surfaces == null)
        {
            return;
        }

        foreach (SurfaceSound surface in surfaces)
        {
            if (surface != null && surface.name == surfaceName)
            {
                activeSurface = surface;
                return;
            }
        }

        Debug.LogWarning($"No surface sound named '{surfaceName}' is configured.", this);
    }

    private void FindBicycleController()
    {
        if (bicycleController != null)
        {
            return;
        }

        bicycleController = FindObjectOfType<BicycleController>();
    }

    private void OnValidate()
    {
        // The stop threshold must stay below the start threshold or the state flickers.
        stopMovingSpeed = Mathf.Min(stopMovingSpeed, startMovingSpeed * 0.9f);

        if (rollingSource != null)
        {
            rollingSource.spatialBlend = spatialBlend;
            rollingSource.outputAudioMixerGroup = outputMixerGroup;
        }

        if (oneShotSource != null)
        {
            oneShotSource.spatialBlend = spatialBlend;
            oneShotSource.outputAudioMixerGroup = outputMixerGroup;
        }
    }

    // Rigidbody.velocity was renamed to linearVelocity in Unity 6.
    private static Vector3 GetVelocity(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity;
#else
        return body.velocity;
#endif
    }
}
