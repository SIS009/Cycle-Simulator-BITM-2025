using UnityEngine;
using System.IO.Ports;

public class CubeMovement : MonoBehaviour
{
    SerialPort serial = new SerialPort("COM18", 9600);

    public float speed = 5f;
    public float minSpeed = 1f;
    public float maxSpeed = 15f;

    public Animator animator; // Drag animator here

    void Start()
    {
        try
        {
            serial.ReadTimeout = 50;
            serial.Open();
            Debug.Log("Serial Port Opened");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Serial Port Error: " + e.Message);
        }
    }

    void Update()
    {
        if (serial != null && serial.IsOpen)
        {
            try
            {
                string data = serial.ReadLine();
                float value = float.Parse(data);

                Debug.Log("Value: " + value);

                // Check range (1 to 10)
                if (value >= 1f && value <= 10f)
                {
                    // Animation ON
                    animator.SetBool("isMoving", true);

                    // Speed control
                    speed = Mathf.Clamp(value, minSpeed, maxSpeed);

                    // Move object
                    transform.Translate(Vector3.forward * speed * Time.deltaTime);
                }
                else
                {
                    // Animation OFF
                    animator.SetBool("isMoving", false);
                }
            }
            catch (System.TimeoutException)
            {
            }
        }
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen)
        {
            serial.Close();
        }
    }
}