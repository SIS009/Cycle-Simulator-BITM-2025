using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TppFpvViewSwitcher : MonoBehaviour
{
    public Button BtTppView;
    public Button BtFppView;
    [Space]
    public GameObject TppCamera;
    public GameObject FppCamera;

    // Start is called before the first frame update
    void Start()
    {
        BtFppView.onClick.AddListener(OnFppView);
        BtTppView.onClick.AddListener(OnTppView);
    }

    void OnTppView()
    {
        TppCamera.SetActive(true);
    }

    void OnFppView()
    {
        TppCamera.SetActive(false);
        FppCamera.transform.localPosition = new Vector3(0, -0.00199f, 0.00112f);
        FppCamera.transform.localRotation = Quaternion.Euler(13.938f, -0.009f, -0.108f);
        FppCamera.GetComponent<Camera>().fieldOfView = 60f;
    }
}
