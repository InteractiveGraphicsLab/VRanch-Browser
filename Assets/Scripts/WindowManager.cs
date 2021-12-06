using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindowManager : MonoBehaviour
{
    [SerializeField] public Window windowPrefab;
    [SerializeField] WindowColumn windowColumnPrefab;
    private List<Window> windows;
    private List<int> selectedWindows = new List<int>();
    private List<WindowColumn> windowColumns;
    private List<int> maxIndexes;
    [SerializeField] private float windowWidth = 2.5f;
    [SerializeField] private float windowHeight = 1.2f;
    [SerializeField] private float windowScrollSpeedX = -0.1f;
    [SerializeField] private float windowScrollSpeedY = -0.1f;
    [SerializeField] private float windowScrollSpeedYDecay = 0.5f;
    [SerializeField] private float pageScrollSpeed = 1f;

    [SerializeField] Vector3 curveCenter;
    [SerializeField] private float curveX, curveZ;
    private Vector3 p0, p1, p2;

    private float scrollOnCurve = 0.5f;
    private List<float> scrollOnColumn;
    Transform player;
    private Transform handAnchor;


    void Update()
    {
        if (ExperimentManager.instance.IsRight && OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger))
            ExperimentManager.instance.SetMainHand(false);
        if (!ExperimentManager.instance.IsRight && OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
            ExperimentManager.instance.SetMainHand(true);

        Vector2 stick = ExperimentManager.instance.IsRight ?
            OVRInput.Get(OVRInput.RawAxis2D.RThumbstick) :
            OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);

        if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
        {
            scrollOnCurve += windowScrollSpeedX * stick.x;
            ScrollWindows();
        }
        else if (Mathf.Abs(stick.y) > Mathf.Abs(stick.x))
        {
            bool isHandTrigger = ExperimentManager.instance.IsRight ?
                OVRInput.Get(OVRInput.RawButton.RHandTrigger) :
                OVRInput.Get(OVRInput.RawButton.LHandTrigger);
            Ray pointerRay = new Ray(handAnchor.position, handAnchor.forward);
            RaycastHit[] hits = Physics.RaycastAll(pointerRay, 10);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hitInfo = hits[i];

                // Scroll Column
                if (!isHandTrigger && hitInfo.collider.tag == "WindowColumn")
                {
                    WindowColumn windowColumn = hitInfo.collider.gameObject.GetComponent<WindowColumn>();

                    for (int depth = 0; depth < scrollOnColumn.Count; depth++)
                    {
                        scrollOnColumn[depth] += windowScrollSpeedY * stick.y * Mathf.Pow(windowScrollSpeedYDecay, Mathf.Abs(windowColumn.Depth - depth));
                    }

                    ScrollWindows();
                }
                //Scroll Page
                if (isHandTrigger && hitInfo.collider.tag == "Window")
                {
                    Window window = hitInfo.collider.gameObject.GetComponent<Window>();
                    window.Scroll(stick.y * pageScrollSpeed);
                }
            }

        }

        // Reset scene
        if (OVRInput.GetDown(OVRInput.Button.Start)) ExperimentManager.instance.ChangeScene("VRanchBrowser", "VRanchBrowser");
    }

    void Start()
    {
        handAnchor = ExperimentManager.instance.HandAnchor;
        player = ExperimentManager.instance.Player;
        windows = new List<Window>();
        windowColumns = new List<WindowColumn>();
        scrollOnColumn = new List<float>();
        windowPrefab.Init(0, "https://www.google.com", -1);
        windowPrefab.DrawEdge();
        windows.Add(windowPrefab);
        windowColumns.Add(windowColumnPrefab);
        scrollOnColumn.Add(0);

        p0 = curveCenter + new Vector3(-curveX, 0, -curveZ);
        p1 = curveCenter;
        p2 = curveCenter + new Vector3(curveX, 0, -curveZ);
        //DrawCurve();

        /* // test
        OpenNewWindow("https://ja.wikipedia.org", 0, true);
        OpenNewWindow("https://ja.wikipedia.org", 0, true);
        OpenNewWindow("https://ja.wikipedia.org", 1, true);
        */

        SolveWindowPositions();
    }

    public Vector3 CalcPosition(float t)
    {
        Vector3 b01 = (1 - t) * p0 + t * p1;
        Vector3 b12 = (1 - t) * p1 + t * p2;

        Vector3 b012 = (1 - t) * b01 + t * b12;
        return b012;
    }

    private void DrawCurve()
    {
        int n = 100;

        var line = new GameObject("Line");
        var lineRenderer = line.AddComponent<LineRenderer>();
        lineRenderer.positionCount = n;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (float)(n - 1);
            Vector3 p = CalcPosition(t);
            lineRenderer.SetPosition(i, p);
        }
    }

    private void SetPosition(int parentId, int depth)
    {
        if (windows[parentId] == null) return;
        if (maxIndexes.Count - 1 < depth) maxIndexes.Add(-1);
        maxIndexes[depth] += 1;
        windows[parentId].Depth = depth;
        windows[parentId].Index = maxIndexes[depth];

        foreach (var id in windows[parentId].ChildIds) SetPosition(id, depth + 1);
    }

    private void SolveWindowPositions()
    {
        maxIndexes = new List<int>();
        SetPosition(0, 0);


        for (int depth = windowColumns.Count; depth < maxIndexes.Count; depth++)
        {
            WindowColumn windowColumn = Instantiate(windowColumnPrefab, Vector3.zero, Quaternion.identity);
            windowColumn.Depth = depth;
            windowColumns.Add(windowColumn);
            scrollOnColumn.Add(0);
        }

        ScrollWindows();
    }

    private void ScrollWindows()
    {
        List<float> depthPositionsOnCurve = new List<float>();
        List<Vector3> depthPositions = new List<Vector3>();

        depthPositionsOnCurve.Add(scrollOnCurve);
        depthPositions.Add(CalcPosition(scrollOnCurve));

        int depthCount = maxIndexes.Count;
        for (int depth = 1; depth < depthCount; depth++)
        {
            float dt = 0.0001f;
            for (int i = 0; i < 10000; i++)
            {
                var pOnCurve = depthPositionsOnCurve[depth - 1] + (float)i * dt;
                var p = CalcPosition(pOnCurve);
                var distance = Vector3.Distance(depthPositions[depth - 1], p);
                if (distance > windowWidth)
                {
                    depthPositionsOnCurve.Add(pOnCurve);
                    depthPositions.Add(p);
                    break;
                }
            }
        }

        foreach (var window in windows)
        {
            if (window == null) continue;
            var newPosition = depthPositions[window.Depth];
            newPosition.y = curveCenter.y + ((float)window.Index - (float)maxIndexes[window.Depth] / 2) * windowHeight + scrollOnColumn[window.Depth];
            window.transform.position = newPosition;
            window.transform.rotation = Quaternion.LookRotation(window.transform.position - player.position);
            window.DrawEdge();
        }

        foreach (var windowColumn in windowColumns)
        {
            if (windowColumn.Depth > depthPositions.Count - 1) continue;
            windowColumn.transform.position = depthPositions[windowColumn.Depth];
            windowColumn.transform.rotation = Quaternion.LookRotation(windowColumn.transform.position - player.position);
        }

        SortWindowsDepth();
    }

    public void OpenNewWindow(string url, int parentId, bool active)
    {
        Debug.Log("OpenNewWindow, " + url + ", " + parentId);

        int id = windows.Count;
        Window window = Instantiate(windowPrefab, Vector3.zero, Quaternion.identity);
        window.gameObject.transform.SetParent(transform, false);
        windows.Add(window);
        windows[parentId].AddChildId(id);
        window.Init(id, url, parentId);
        SolveWindowPositions();
    }

    public Window GetWindow(int id)
    {
        if (id < 0 || id >= windows.Count) return null;
        return windows[id];
    }

    public void CloseWindow(int id)
    {
        var parent = windows[windows[id].ParentId];
        foreach (var childId in windows[id].ChildIds)
        {
            if (windows[childId] != null) windows[childId].ParentId = parent.Id;
        }
        parent.InsertChildIds(parent.ChildIds.IndexOf(id), windows[id].ChildIds);

        foreach (Transform child in windows[id].transform)
        {
            Destroy(child.gameObject);
        }
        Destroy(windows[id].gameObject);
        windows[id] = null;

        SolveWindowPositions();
        selectedWindows.Remove(id);
    }

    public void OnSelectWindow(int id, bool checkMemory=false)
    {
        if (id == 0) return;

        selectedWindows.Remove(id);
        selectedWindows.Add(id);


        if (checkMemory) {
            int maxUsedMemory = 1800000; //1.8GB
            if (GetMemorySize() > maxUsedMemory) DeactivateOldWindow();
        }

        int maxCount = 13;
        if (selectedWindows.Count > maxCount) DeactivateOldWindow();
        windows[id].Activate(true);

        SortWindowsDepth();
    }

    public void DeactivateOldWindow()
    {
        windows[selectedWindows[0]].Activate(false);
        selectedWindows.RemoveAt(0);
    }

    public void SortWindowsDepth()
    {
        List<int> overlayWindows = new List<int>(selectedWindows);
        overlayWindows.Add(0);
        overlayWindows.Sort(CompareDistance);

        int depth = 0;
        foreach (int win in overlayWindows)
        {
            if (windows[win] == null) continue;
            windows[win].SetOverlayDepth(depth);
            depth++;
        }
    }

    private int CompareDistance(int win1, int win2)
    {
        if(windows[win1]==null || windows[win2]==null) return 0;

        float distance1 = Vector3.Distance(windows[win1].transform.position, player.position);
        float distance2 = Vector3.Distance(windows[win2].transform.position, player.position);
        if (distance1 > distance2) return 1;
        else return -1;
    }

    public void OnDestroy()
    {
        foreach (Window win in windows)
        {
            if (win != null) win.ExitWebview();
        }
    }

    public static long GetMemorySize()
    {
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var application = activity.Call<AndroidJavaObject>("getApplication");
        var context = activity.Call<AndroidJavaObject>("getApplicationContext");
        var staticContext = new AndroidJavaClass("android.content.Context");
        var service = staticContext.GetStatic<AndroidJavaObject>("ACTIVITY_SERVICE");
        var activityManager = activity.Call<AndroidJavaObject>("getSystemService", service);
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var pidList = new int[] { process.Id };
        var memoryInfoList = activityManager.Call<AndroidJavaObject[]>("getProcessMemoryInfo", pidList);

        long total = 0;
        foreach (var memoryInfo in memoryInfoList)
        {
            total += memoryInfo.Call<int>("getTotalPss"); //KB
        }
        return total;
    }
}
