using UnityEngine;
using System.IO.Ports;
using System;

public class ArduinoSerial : MonoBehaviour
{
    private SerialPort sp;
    private string data;
    public static bool isPaused = false;

    public float targetTimeScale = 5f;
    public float speed = 1f; // jitni fast increase chahiye

    [Header("Serial Settings")]
    public string portName = "COM18";
    public int baudRate = 9600;

    [Header("Data")]
    public bool cycleToMove = false;
    public int sensorData;

    public static ArduinoSerial instance;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        try
        {
            sp = new SerialPort(portName, baudRate);
            sp.ReadTimeout = 100;
            sp.Open();
            Debug.Log("✅ Serial port opened");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Failed to open serial port: " + e.Message);
        }
    }

    void Update()
    {
        if (sp == null || !sp.IsOpen) return;

        bool dataReceived = false; // ✅ check flag

        try
        {
            data = sp.ReadLine();

            if (!string.IsNullOrEmpty(data))
            {
                dataReceived = true;

                Debug.Log("RAW: [" + data + "]");

                data = data.Trim();

                string numbersOnly = System.Text.RegularExpressions.Regex.Match(data, @"\d+").Value;

                if (int.TryParse(numbersOnly, out int arduinoValue))
                {
                    Debug.Log("✅ Arduino Value: " + arduinoValue);

                    if (arduinoValue > 5 && arduinoValue >=10 && CyclingManager.Instance != null)
                    {
                        cycleToMove = true;
                        isPaused = false;
                        Time.timeScale = 6;
                        CyclingManager.Instance.boardValueForMovement = true;
                        CyclingManager.Instance.isCycling = false;
                        CyclingManager.Instance._cyclingSpeed = 10f;



                        /*if (CyclingManager.Instance != null)
                        {
                            CyclingManager.Instance.boardValueForMovement = true;
                            CyclingManager.Instance.isCycling = false;
                            CyclingManager.Instance._cyclingSpeed = 10f;
                            isPaused = false;
                            Time.timeScale = 3f;
                            //Time.timeScale = Mathf.MoveTowards(Time.timeScale, targetTimeScale, speed * Time.unscaledDeltaTime);

                        }*/

                        if (arduinoValue >= 20)
                        {
                            cycleToMove = true;
                            isPaused = false;
                            Time.timeScale = 2;
                            CyclingManager.Instance.boardValueForMovement = true;
                            CyclingManager.Instance.isCycling = false;
                            CyclingManager.Instance._cyclingSpeed = 10f;
                        }
                    if(arduinoValue >= 30)
                        {
                            cycleToMove = true;
                            isPaused = false;
                            Time.timeScale = 3;
                            CyclingManager.Instance.boardValueForMovement = true;
                            CyclingManager.Instance.isCycling = false;
                            CyclingManager.Instance._cyclingSpeed = 10f;
                        }

                        if (arduinoValue < 5)
                        {
                            cycleToMove = false;
                            isPaused = true;
                            Time.timeScale = 0f;
                        }


                    }
                    
                    else
                    {
                        cycleToMove = false;
                        isPaused = true;
                        Time.timeScale = 0f;

                        if (CyclingManager.Instance != null)
                        {
                            CyclingManager.Instance.boardValueForMovement = false;
                            CyclingManager.Instance.isCycling = true;
                            CyclingManager.Instance._cyclingSpeed = 0f;
                        }
                        
                    }
                }
            }
        }
        catch (Exception)
        {
            // ❌ No data received → handled below
        }

        // ❗ If NO data in this frame → STOP immediately
        if (!dataReceived)
        {
            cycleToMove = true;
            Time.timeScale = 0;


            if (CyclingManager.Instance != null)
            {
                CyclingManager.Instance.boardValueForMovement = false;
                CyclingManager.Instance.isCycling = true;
                CyclingManager.Instance._cyclingSpeed = 0f;
            }

            Debug.LogWarning("⚠️ No Data → Instant STOP");
        }
    }

    public void stop()
    {
        if(isPaused == true)
        {
            Time.timeScale = 0f;
        }
       
    }



    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen)
        {
            sp.Close();
            Debug.Log("🔌 Serial port closed");
        }
    }
}