using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using SBPScripts;

/// <summary>
/// Reads Arduino data such as:
/// Angle: 2.5,Velocity: 18,Scene: 3,Trigger: 1
///
/// This script does not modify BicycleController. It writes to the controller's
/// public input fields after BicycleController.Update() has processed keyboard input.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class ArduinoBicycleBridge : MonoBehaviour
{
    // private static ArduinoBicycleBridge instance;

    [Header("Serial Port")]
    [SerializeField] private string portName = "COM11";
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private int readTimeoutMilliseconds = 100;
    [SerializeField] private float dataTimeoutSeconds = 0.75f;

    [Header("Bicycle")]
    [Tooltip("Optional. Leave empty to find the BicycleController automatically in every scene.")]
    [SerializeField] private BicycleController bicycleController;
    [SerializeField] private bool findBicycleAutomatically = true;

    [Header("Arduino Mapping")]
    [SerializeField] private float minimumAngle = -15f;
    [SerializeField] private float maximumAngle = 15f;
    [SerializeField] private float angleDeadZone = 1f;
    [SerializeField] private bool invertSteering = false;

    [SerializeField] private float minimumVelocity = 0f;
    [SerializeField] private float maximumVelocity = 30f;
    [SerializeField] private float velocityDeadZone = 0.10f;

    [Tooltip("Larger values make steering respond faster.")]
    [SerializeField] private float steeringResponse = 10f;

    [Tooltip("Larger values make acceleration respond faster.")]
    [SerializeField] private float velocityResponse = 6f;

    [Header("Scene Switching")]
    [SerializeField] private bool enableSceneSwitching = true;

    [Tooltip("Enabled: Arduino Scene 1-4 maps to build indices 0-3. Disabled: Scene 0-3 maps directly.")]
    [SerializeField] private bool sceneValuesAreOneBased = true;

    [SerializeField, Range(1, 32)] private int controlledSceneCount = 4;

    [Header("Debug")]
    [SerializeField] private bool printReceivedData = false;

    public float Angle { get; private set; }
    public float Velocity { get; private set; }
    public int ArduinoSceneValue { get; private set; }
    public int TriggerValue { get; private set; }

    private SerialPort serialPort;
    private Thread serialThread;
    private volatile bool keepReading;

    private readonly ConcurrentQueue<string> receivedLines =
        new ConcurrentQueue<string>();

    private bool hasValidPacket;
    private float lastValidPacketTime;
    private float currentSteeringAxis;
    private float currentAccelerationAxis;
    private int lastHandledSceneValue = int.MinValue;
    private int lastTriggerValue = 1;
    private bool hasTriggerReading;
    private float nextBicycleSearchTime;

    private static readonly Regex PacketPattern = new Regex(
        @"Angle\s*:\s*(-?\d+(?:\.\d+)?)\s*,\s*" +
        @"Velocity\s*:\s*(-?\d+(?:\.\d+)?)\s*,\s*" +
        @"Scene\s*:\s*(-?\d+)\s*,\s*" +
        @"Trigger\s*:\s*(-?\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private void Awake()
    {
        // if (instance != null && instance != this)
        // {
        //     Destroy(gameObject);
        //     return;
        // }

        // instance = this;
        // DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        FindBicycleController();
        OpenSerialPort();
    }

    private void Update()
    {
        ProcessReceivedLines();

        if (findBicycleAutomatically && bicycleController == null &&
            Time.unscaledTime >= nextBicycleSearchTime)
        {
            FindBicycleController();
            nextBicycleSearchTime = Time.unscaledTime + 1f;
        }
    }

    /// <summary>
    /// BicycleController applies keyboard input in Update(). This bridge has a later
    /// execution order and applies Arduino input in LateUpdate(), so no controller
    /// source-code change is required.
    /// </summary>
    private void LateUpdate()
    {
        if (bicycleController == null)
        {
            return;
        }

        bool dataIsFresh = hasValidPacket &&
                           Time.realtimeSinceStartup - lastValidPacketTime <= dataTimeoutSeconds;

        float targetSteering = 0f;
        float targetAcceleration = 0f;

        if (dataIsFresh)
        {
            targetSteering = MapAngleToSteering(Angle);
            targetAcceleration = MapVelocityToAcceleration(Velocity);
        }

        currentSteeringAxis = Mathf.MoveTowards(
            currentSteeringAxis,
            targetSteering,
            steeringResponse * Time.unscaledDeltaTime);

        currentAccelerationAxis = Mathf.MoveTowards(
            currentAccelerationAxis,
            targetAcceleration,
            velocityResponse * Time.unscaledDeltaTime);

        // Horizontal input replacement: Arduino Angle replaces A and D.
        bicycleController.customSteerAxis = currentSteeringAxis;
        bicycleController.customLeanAxis = currentSteeringAxis;

        // Vertical input replacement: Arduino Velocity replaces W.
        bicycleController.customAccelerationAxis = currentAccelerationAxis;

        // BicycleController uses the raw value mainly as a forward/reverse gate.
        bicycleController.rawCustomAccelerationAxis =
            currentAccelerationAxis > 0.001f ? 1f : 0f;
    }

    private float MapAngleToSteering(float angle)
    {
        angle = Mathf.Clamp(angle, minimumAngle, maximumAngle);

        if (Mathf.Abs(angle) <= angleDeadZone)
        {
            return 0f;
        }

        float steering = Mathf.InverseLerp(minimumAngle, maximumAngle, angle) * 2f - 1f;

        if (invertSteering)
        {
            steering *= -1f;
        }

        return Mathf.Clamp(steering, -1f, 1f);
    }

    private float MapVelocityToAcceleration(float velocity)
    {
        velocity = Mathf.Clamp(velocity, minimumVelocity, maximumVelocity);

        if (velocity <= minimumVelocity + velocityDeadZone)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            Mathf.InverseLerp(minimumVelocity, maximumVelocity, velocity));
    }

    private void ProcessReceivedLines()
    {
        while (receivedLines.TryDequeue(out string line))
        {
            if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                Debug.LogError(line);
                continue;
            }

            if (!TryParsePacket(line, out float angle, out float velocity,
                    out int sceneValue, out int triggerValue))
            {
                if (printReceivedData)
                {
                    Debug.LogWarning($"Invalid Arduino packet: {line}");
                }

                continue;
            }

            Angle = Mathf.Clamp(angle, minimumAngle, maximumAngle);
            Velocity = Mathf.Clamp(velocity, minimumVelocity, maximumVelocity);
            ArduinoSceneValue = sceneValue;
            TriggerValue = triggerValue;
            hasValidPacket = true;
            lastValidPacketTime = Time.realtimeSinceStartup;

            if (printReceivedData)
            {
                Debug.Log(
                    $"Arduino -> Angle: {Angle:F2}, Velocity: {Velocity:F2}, " +
                    $"Scene: {ArduinoSceneValue}, Trigger: {TriggerValue}");
            }

            HandleSceneCommand(ArduinoSceneValue, TriggerValue);
        }
    }

    private static bool TryParsePacket(
        string line,
        out float angle,
        out float velocity,
        out int sceneValue,
        out int triggerValue)
    {
        angle = 0f;
        velocity = 0f;
        sceneValue = 0;
        triggerValue = 0;

        Match match = PacketPattern.Match(line.Trim());

        if (!match.Success)
        {
            return false;
        }

        return float.TryParse(
                   match.Groups[1].Value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out angle)
               && float.TryParse(
                   match.Groups[2].Value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out velocity)
               && int.TryParse(
                   match.Groups[3].Value,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out sceneValue)
               && int.TryParse(
                   match.Groups[4].Value,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out triggerValue);
    }

    /// <summary>
    /// The scene action fires only on the Trigger's falling edge (previous value
    /// was non-zero, current value is 0), so a single press acts once instead of
    /// every frame while Trigger stays at 0.
    ///  - Scene changed  + Trigger becomes 0 -> load the new scene.
    ///  - Scene unchanged + Trigger becomes 0 -> reload the current scene.
    /// </summary>
    private void HandleSceneCommand(int sceneValue, int triggerValue)
    {
        if (!enableSceneSwitching)
        {
            lastTriggerValue = triggerValue;
            hasTriggerReading = true;
            return;
        }

        bool triggerFallingEdge = hasTriggerReading && lastTriggerValue != 0 && triggerValue == 0;

        lastTriggerValue = triggerValue;
        hasTriggerReading = true;

        // Only act at the moment Trigger transitions to 0.
        if (!triggerFallingEdge)
        {
            return;
        }

        bool sceneChanged = sceneValue != lastHandledSceneValue;
        lastHandledSceneValue = sceneValue;

        int targetBuildIndex = sceneValuesAreOneBased
            ? sceneValue - 1
            : sceneValue;

        if (!sceneChanged)
        {
            // Same scene value: restart the current scene.
            LoadSceneWithLoader(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (targetBuildIndex < 0 || targetBuildIndex >= controlledSceneCount)
        {
            Debug.LogWarning(
                $"Arduino Scene {sceneValue} is outside the configured " +
                $"{controlledSceneCount}-scene range.");
            return;
        }

        if (targetBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError(
                $"Build index {targetBuildIndex} is not available. " +
                "Add all required scenes to Build Settings.");
            return;
        }

        LoadSceneWithLoader(targetBuildIndex);
    }

    /// <summary>
    /// Loads a scene through the LoadingScreen singleton (animated loader) when it
    /// is available, and falls back to a direct SceneManager load otherwise.
    /// </summary>
    private void LoadSceneWithLoader(int buildIndex)
    {
        if (LoadingScreen.instance != null)
        {
            LoadingScreen.instance.LoadScene(buildIndex);
        }
        else
        {
            SceneManager.LoadScene(buildIndex);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bicycleController = null;
        FindBicycleController();
    }

    private void FindBicycleController()
    {
        if (!findBicycleAutomatically && bicycleController != null)
        {
            return;
        }

        bicycleController = FindObjectOfType<BicycleController>();
    }

    private void OpenSerialPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = readTimeoutMilliseconds,
                NewLine = "\n"
            };

            serialPort.Open();
            serialPort.DiscardInBuffer();

            keepReading = true;
            serialThread = new Thread(ReadSerialLoop)
            {
                IsBackground = true,
                Name = "Arduino Serial Reader"
            };

            serialThread.Start();
            Debug.Log($"Arduino connected on {portName} at {baudRate} baud.");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Could not open Arduino serial port {portName}: {exception.Message}");
        }
    }

    private void ReadSerialLoop()
    {
        while (keepReading)
        {
            try
            {
                string line = serialPort.ReadLine();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    receivedLines.Enqueue(line.Trim());
                }
            }
            catch (TimeoutException)
            {
                // Expected when no complete line is currently available.
            }
            catch (Exception exception)
            {
                if (keepReading)
                {
                    receivedLines.Enqueue($"ERROR:{exception.Message}");
                }
            }
        }
    }

    private void CloseSerialPort()
    {
        keepReading = false;

        try
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Error while closing serial port: {exception.Message}");
        }

        if (serialThread != null && serialThread.IsAlive)
        {
            serialThread.Join(500);
        }

        serialPort?.Dispose();
        serialPort = null;
        serialThread = null;
    }

    private void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    private void OnDestroy()
    {
        // if (instance != this)
        // {
        //     return;
        // }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        CloseSerialPort();
        // instance = null;
    }
}