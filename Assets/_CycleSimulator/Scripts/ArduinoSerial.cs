using UnityEngine;
using System.IO.Ports;
using System;

public class ArduinoSerial : MonoBehaviour
{
    private SerialPort sp;
    private string data;
    private string portName = "COM11";       // Make this configurable from Inspector
    public int baudRate = 9600;            // Match with Arduino's baud rate

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
            sp.ReadTimeout = 100; // Optional: prevent freeze if nothing is sent
            sp.Open();
            Debug.Log("Serial port opened.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to open serial port: " + e.Message);
        }
    }

   void Update()
    {
        if (sp != null && sp.IsOpen)
        {
            try
            {
                data = sp.ReadLine();
                if (!string.IsNullOrEmpty(data))
                {
                    Debug.Log("Received: " + data);

                    // Expecting data like: sensor_status=0
                    if (data.StartsWith("sensor_status="))
                    {
                        string valueStr = data.Substring("sensor_status=".Length).Trim();

                        if (int.TryParse(valueStr, out int sensorValue))
                        {
                            sensorData = sensorValue;
                            cycleToMove = sensorValue != 0;
                            Debug.Log("cycleToMove set to: " + cycleToMove);
                        }
                        else
                        {
                            Debug.LogWarning("Failed to parse sensor value: " + valueStr);
                        }
                    }
                }
            }
            catch (TimeoutException) { } // Ignore timeout
            catch (Exception e)
            {
                Debug.LogWarning("Serial read error: " + e.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen)
        {
            sp.Close();
            Debug.Log("Serial port closed.");
        }
    }
}
