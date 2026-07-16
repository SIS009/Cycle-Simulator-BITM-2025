using UnityEngine;
using UnityEngine.Events;

public class OnTwoObjectCollision : MonoBehaviour
{
    private GameObject Cycle;
    [SerializeField] UnityEvent onCollisionTrigger;
    [SerializeField] UnityEvent onCollisionExit;

    public static OnTwoObjectCollision Instance { get; private set; }

    private bool isColliding = false; // To track collision state

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        Cycle = GameObject.FindGameObjectWithTag("Cycle");
    }

    void Update()
    {
        if (Cycle != null)
        {
            // Check if self object and Cycle are colliding
            bool currentlyColliding = gameObject.GetComponent<Collider>().bounds.Intersects(Cycle.GetComponent<Collider>().bounds);

            if (currentlyColliding && !isColliding)
            {
                // Collision just started
                isColliding = true;
                Debug.Log("Collision Triggered with: " + Cycle.name);
                onCollisionTrigger.Invoke();
            }
            else if (!currentlyColliding && isColliding)
            {
                // Collision just ended
                isColliding = false;
                Debug.Log("Collision Ended with: " + Cycle.name);
                onCollisionExit.Invoke();
            }
        }
    }
}
