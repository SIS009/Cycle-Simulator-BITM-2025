using UnityEngine;
using System.IO.Ports;
using System;

public class ArduinoAnimationController : MonoBehaviour
{
    SerialPort sp = new SerialPort("COM18", 9600);

   // public Animator animator;

    void Start()
    {
        try
        {
            sp.ReadTimeout = 100;
            sp.Open();
            Debug.Log("Serial Port Opened");
        }
        catch (Exception e)
        {
            Debug.LogError("Serial Error: " + e.Message);
        }
    }

    void Update()
    {
        if (sp != null && sp.IsOpen)
        {
            try
            {
                string data = sp.ReadLine(); // Read from Arduino
                int value = int.Parse(data); // Example: 0, 5, 20

                Debug.Log("Value: " + value);

                // CONDITION:
                // 0–10 → STOP animation
                // >10 → PLAY animation

                if (value > 10)
                {
                    CyclingManager.Instance.isCycling = false;
                    //animator.SetBool("isMoving", true);  // PLAY
                }
                else
                {
                    CyclingManager.Instance.isCycling = true;
                   // animator.SetBool("isMoving", false); // STOP
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen)
        {
            sp.Close();
        }
    }
}