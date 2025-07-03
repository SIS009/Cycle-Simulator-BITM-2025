using System.Collections;
using UnityEngine;
using Cinemachine;

public class CyclingManager : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] private Animator _cyclist;
    [SerializeField] private Animator _cycle;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineDollyCart cycleSpeed;

    [Header("Cycling State")]
    public bool isCycling = false;
    public bool isRunningFirstTime = true;
    public bool boardValueForMovement = false;

    [Header("Speed Control")]
    public float currentValue = 0f;
    public float duration = 3f;
    private float timer = 0f;
    private float startSpeed = 0f;
    private float targetSpeed = 0f;

    private bool isTransitioning = false;

    void Start()
    {
        cycleSpeed.m_Speed = 0f;
        currentValue = 0f;
        StartCoroutine(StartCycling());
    }

    void Update()
    {
        if (boardValueForMovement && !isCycling)
        {
            // Start cycling
            StartSpeedTransition(0f, 5f);
            _cycle.SetBool("IsCycling", true);
            _cyclist.SetBool("IsCycling", true);
            isCycling = true;
            isRunningFirstTime = false;
        }
        else if (!boardValueForMovement && isCycling)
        {
            // Stop cycling
            StartSpeedTransition(5f, 0f);
            _cycle.SetBool("IsCycling", false);
            _cyclist.SetBool("IsCycling", false);
            isCycling = false;
        }

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
}
