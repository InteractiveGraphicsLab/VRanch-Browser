using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Window : MonoBehaviour
{
    [SerializeField] BrowserView browserView;
    [SerializeField] WindowManager windowManager;
    [SerializeField] RawImage rawImage;
    [SerializeField] Transform edgeStartPoint, edgeEndPoint;
    [SerializeField] LineRenderer edge;
    [SerializeField] GameObject closeButton;
    public RawImage RawImage
    {
        get => rawImage;
    }
    private int id;
    public int Id
    {
        get { return id; }
    }
    private string url;
    private int parentId;
    public int ParentId
    {
        get => parentId;
        set => parentId = value;
    }
    private List<int> childIds = new List<int>();
    public List<int> ChildIds
    {
        get { return childIds; }
    }
    private bool active;
    
    private int depth;
    public int Depth
    {
        set => depth = value;
        get => depth;
    }
    private int index;
    public int Index
    {
        set => index = value;
        get => index;
    }

    void Start()
    {
        browserView.GazePointer = ExperimentManager.instance.GazePointer;
    }
    
    public void Init(int id, string url, int parentId, bool active = true)
    {
        Debug.Log("Init" + id);
        this.id = id;
        this.url = url;
        this.parentId = parentId;
        this.active = active;
        browserView.enabled = true;
        windowManager.OnSelectWindow(id, true);
        closeButton.SetActive(id != 0);
    }

    public void OnGeckoViewReady()
    {
        browserView.CallAjc("OpenNewSession", new object[] { id, url, true });
    }

    public void OnLoadRequest(string url)
    {
        Debug.Log("OnLoadRequest" + url);
        windowManager.OpenNewWindow(url, this.id, true);
        // ExperimentManager.instance.LogManager.IncrementPageCount();
    }

    public void AddChildId(int id)
    {
        childIds.Add(id);
    }

    public void InsertChildIds(int index, List<int> ids)
    {
        childIds.InsertRange(index, ids);
    }

    public Transform GetEdgeStartPoint()
    {
        return edgeStartPoint;
    }

    public void DrawEdge()
    {
        var parentWindow = windowManager.GetWindow(parentId);
        edge.useWorldSpace = true;
        if (parentWindow == null)
        {
            edge.SetPosition(0, edgeEndPoint.position);
            edge.SetPosition(1, edgeEndPoint.position);
            return;
        }
        var parentEdgeStart = parentWindow.GetEdgeStartPoint();
        
        Vector3 p0 = edgeEndPoint.position;
        Vector3 p3 = parentEdgeStart.position;
        Vector3 p1 = p0 - transform.right * 0.4f;
        Vector3 p2 = p3 + parentEdgeStart.right * 0.4f;
        int n = 30;
        edge.positionCount = n;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (float)(n - 1);

            Vector3 b01 = t * p0 + (1 - t) * p1;
            Vector3 b12 = t * p1 + (1 - t) * p2;
            Vector3 b23 = t * p2 + (1 - t) * p3;

            Vector3 b012 = t * b01 + (1 - t) * b12;
            Vector3 b123 = t * b12 + (1 - t) * b23;

            Vector3 b1234 = t * b012 + (1 - t) * b123;

            edge.SetPosition(i, b1234);
        }

    }

    public void Scroll(float multiplier)
    {
        if(!active) windowManager.OnSelectWindow(id);
        if (active && browserView.IsReady) browserView.InvokeScrollUp(multiplier);
    }

    public void OnClick()
    {
        if (!active) windowManager.OnSelectWindow(id);
        if (active && browserView.IsReady) browserView.OnClick();
    }

    public void Activate(bool active)
    {
        if (this.active == active) return;

        this.active = active;
        Debug.Log("activate " + this.id + " " + this.active);
        if (this.active)
        {
            GameObject browserViewObj = Instantiate(windowManager.windowPrefab.browserView.gameObject, this.transform.position, this.transform.rotation, this.transform);
            browserView = browserViewObj.GetComponent<BrowserView>();
            rawImage.texture = null;
            windowManager.OnSelectWindow(id, true);
            //edge.SetColors(Color.green, Color.white);
        }
        else
        {
            ShowPreview();
            this.url = browserView.CurrentUrl;
            browserView.ExitWebview();
            browserView.enabled = false;
            Destroy(browserView.gameObject);
            //edge.SetColors(Color.red, Color.white);
        }
    }

    public void ShowPreview()
    {
        byte[] pageImage = browserView.TakePngScreenShot(1920, 1080, 100);
        Texture2D texture = new Texture2D(1920, 1080);
        texture.LoadImage(pageImage);
        rawImage.texture = texture;
    }

    public void SetOverlayDepth(int depth)
    {
        browserView.SetOverlayDepth(depth);
    }

    public void Close()
    {
        if (id == 0) return;
        ExitWebview();
        windowManager.CloseWindow(id);
    }

    public void ExitWebview()
    {
        browserView.ExitWebview();
    }
}
