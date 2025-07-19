using System.Collections;
using UnityEngine;
using Cinemachine;


public class CyclingManager : MonoBehaviour
{
    [Header("Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startCyclingAudio;
    [SerializeField] private AudioClip stopCyclingAudio;
    [SerializeField] private AudioClip cyclingAudio;

    [Header("Animators")]
    [SerializeField] private Animator _cyclist;
    [SerializeField] private Animator _cycle;

    [Header("Colliders")]
    [SerializeField] private Collider endPoint;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineDollyCart cycleSpeed;

    [Header("Cycling State")]
    public bool isCycling = false;
    public bool isRunningFirstTime = true;
    public bool boardValueForMovement = false;

    [Header("Speed Control")]
    public float currentValue = 0f;
    public float duration = 3f;
    public float _cyclingSpeed = 20f;
    private float timer = 0f;
    private float startSpeed = 0f;
    private float targetSpeed = 0f;

    private bool isTransitioning = false;

    public static CyclingManager Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        cycleSpeed.m_Speed = 0f;
        currentValue = 0f;
        StartCoroutine(StartCycling());
    }

    void Update()
    {
        // 'boardValueForMovement' will control cycle directly from editor data
        // 'ArduinoSerial.instance.cycleToMove' will control cycle from Arduino's data
        // 'boardValueForMovement' or 'ArduinoSerial.instance.cycleToMove' anyone of these will control the cycle
        if ((boardValueForMovement || !ArduinoSerial.instance.cycleToMove) && !isCycling)
        {
            // Start cycling
            OnStartCycling();

            isRunningFirstTime = false;
        }
        else if (!(boardValueForMovement || !ArduinoSerial.instance.cycleToMove) && isCycling)
        {
            // Stop cycling
            OnStopCycling();
        }
        // --------------------------------------------------------------------------------------------------------

        // If transitioning, update speed over time
        if (isTransitioning)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            cycleSpeed.m_Speed = Mathf.Lerp(startSpeed, targetSpeed, t);

            if (t >= 1f)
            {
                isTransitioning = false;
                timer = 0f;
            }
        }
        // --------------------------------------------------------------------------------------------------------

        if (_cycle != null)
        {
            // Check if self object and Cycle are colliding
            bool currentlyColliding = endPoint.bounds.Intersects(_cycle.GetComponent<Collider>().bounds);

            if (currentlyColliding)
            {
                Debug.Log("Reached to the end...");

                // Call when the cycle reachs the end point
                OnStopCycling();
            }
        }
    }

    void StartSpeedTransition(float from, float to)
    {
        startSpeed = from;
        targetSpeed = to;
        timer = 0f;
        isTransitioning = true;
    }

    IEnumerator StartCycling()
    {
        yield return new WaitUntil(() => isCycling);
        // Can add additional logic here if needed
    }

    // Stop cycling
    private void OnStopCycling()
    {
        StartSpeedTransition(_cyclingSpeed, 0f);
        _cycle.SetBool("IsCycling", false);
        _cyclist.SetBool("IsCycling", false);
        isCycling = false;

        // Play cycleing stop audio
        audioSource.clip = stopCyclingAudio;
        audioSource.loop = false;
        audioSource.Play();
    }

    // Start cycling
    public void OnStartCycling()
    {
        StartSpeedTransition(0f, _cyclingSpeed);
        _cycle.SetBool("IsCycling", true);
        _cyclist.SetBool("IsCycling", true);
        isCycling = true;

        // Play cycleing stop audio
        audioSource.clip = startCyclingAudio;
        audioSource.Play();

        StartCoroutine(PlayCyclingAudioWithDelay());
    }

    IEnumerator PlayCyclingAudioWithDelay()
    {
        yield return new WaitForSeconds(duration);

        // Play cycleing stop audio
        audioSource.clip = cyclingAudio;
        audioSource.loop = true;
        audioSource.Play();
    }
}
