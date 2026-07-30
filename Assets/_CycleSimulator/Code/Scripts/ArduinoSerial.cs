using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

public class ArduinoSerial : MonoBehaviour
{
    [Header("Serial settings")]
    [SerializeField] private string portName = "COM11";
    [SerializeField] private int baudRate = 115200;

    public float Angle { get; private set; }
    public float Velocity { get; private set; }
    public int Scene { get; private set; }

    private SerialPort serialPort;
    private Thread readingThread;
    private volatile bool keepReading;

    private readonly ConcurrentQueue<string> receivedLines =
        new ConcurrentQueue<string>();

    private static readonly Regex DataPattern = new Regex(
        @"Angle:\s*(-?\d+(?:\.\d+)?)\s*,\s*" +
        @"Velocity:\s*(-?\d+(?:\.\d+)?)\s*,\s*" +
        @"Scene:\s*(-?\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private void Start()
    {
        OpenSerialPort();
    }

    private void OpenSerialPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 100,
                NewLine = "\n"
            };

            serialPort.Open();
            serialPort.DiscardInBuffer();

            keepReading = true;
            readingThread = new Thread(ReadSerialData)
            {
                IsBackground = true
            };

            readingThread.Start();

            Debug.Log($"Arduino connected on {portName} at {baudRate} baud.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not open {portName}: {exception.Message}");
        }
    }

    private void ReadSerialData()
    {
        while (keepReading)
        {
            try
            {
                string line = serialPort.ReadLine().Trim();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    receivedLines.Enqueue(line);
                }
            }
            catch (TimeoutException)
            {
                // Normal when no complete line is currently available.
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

    private void Update()
    {
        while (receivedLines.TryDequeue(out string line))
        {
            if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                Debug.LogError(line);
                continue;
            }

            ParseArduinoData(line);
        }
    }

    private void ParseArduinoData(string line)
    {
        Match match = DataPattern.Match(line);

        if (!match.Success)
        {
            Debug.LogWarning($"Unrecognised Arduino data: {line}");
            return;
        }

        bool angleParsed = float.TryParse(
            match.Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float angle);

        bool velocityParsed = float.TryParse(
            match.Groups[2].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float velocity);

        bool sceneParsed = int.TryParse(
            match.Groups[3].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int scene);

        if (!angleParsed || !velocityParsed || !sceneParsed)
        {
            Debug.LogWarning($"Could not parse Arduino data: {line}");
            return;
        }

        Angle = angle;
        Velocity = velocity;
        Scene = scene;

        Debug.Log(
            $"Angle: {Angle}, Velocity: {Velocity}, Scene: {Scene}"
        );
    }

    private void OnDestroy()
    {
        CloseSerialPort();
    }

    private void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    private void CloseSerialPort()
    {
        keepReading = false;

        if (readingThread != null && readingThread.IsAlive)
        {
            readingThread.Join(500);
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }

        serialPort?.Dispose();
        serialPort = null;
    }
}