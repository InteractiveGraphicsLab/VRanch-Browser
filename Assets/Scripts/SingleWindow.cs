using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SingleWindow : MonoBehaviour
{
    [SerializeField] BrowserView browserView;
    [SerializeField] RawImage rawImage;
    [SerializeField] private float pageScrollSpeed = 1f;
    private Transform player;
    private Transform handAnchor;

    public RawImage RawImage
    {
        get => rawImage;
    }
    private int id = 0;
    private string url;
    private bool active = true;

    public void Start()
    {
        url = "https://www.google.com";
        browserView.GazePointer = ExperimentManager.instance.GazePointer;
        player = ExperimentManager.instance.Player;
        handAnchor = ExperimentManager.instance.HandAnchor;

        transform.rotation = Quaternion.LookRotation(transform.position - player.position);
    }


    void Update()
    {
        Vector2 stick = ExperimentManager.instance.IsRight ?
            OVRInput.Get(OVRInput.RawAxis2D.RThumbstick) :
            OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);

        if (Mathf.Abs(stick.y) > Mathf.Abs(stick.x))
        {
            bool isHandTrigger = ExperimentManager.instance.IsRight ?
                OVRInput.Get(OVRInput.RawButton.RHandTrigger) :
                OVRInput.Get(OVRInput.RawButton.LHandTrigger);
            Ray pointerRay = new Ray(handAnchor.position, handAnchor.forward);
            RaycastHit[] hits = Physics.RaycastAll(pointerRay, 10);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hitInfo = hits[i];

                //Scroll Page
                if (isHandTrigger && hitInfo.collider.tag == "Window")
                {
                    Scroll(stick.y * pageScrollSpeed);
                }
            }

        }
    }


    public void OnGeckoViewReady()
    {
        browserView.CallAjc("OpenNewSession", new object[] { id, url, true });
    }

    public void OnLoadRequest(string url)
    {
        Debug.Log("OnLoadRequest" + url);
        browserView.LoadURL(url);
    }

    public void GoForward()
    {
        browserView.InvokeGoForward();
    }

    public void GoBack()
    {
        browserView.InvokeGoBack();
    }
    
    public void Scroll(float multiplier)
    {
        if (active && browserView.IsReady) browserView.InvokeScrollUp(multiplier);
    }

    public void OnClick()
    {
        if (active && browserView.IsReady) browserView.OnClick();
    }

    public void OnDestroy()
    {
        browserView.ExitWebview();
    }
}
