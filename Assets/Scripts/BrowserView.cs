using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public class BrowserView : MonoBehaviour 
{
    
    public event  Action<int> OnProgressUpdate = delegate{  };
    public event Action<string> OnPageLoad = delegate {  };
    private Window window = null;
    private SingleWindow singleWindow;
    
    public Button BackButton;
    public Button ForwardButton;
    public TMP_InputField UrlInputField;
    public TMP_Text ProgressText;
    public Transform GazePointer;
    private RawImage RawImage;
    private UserAgent _currentUserAgent = UserAgent.mobile;
    private OVROverlay _overlay;
    private bool isReady = false;
    public bool IsReady
    {
        get => isReady;
    }

    /// <summary>
    /// The currently visited url.
    /// </summary>
    public string CurrentUrl = "";

    public bool CanGoForward { get; private set; }
    public bool CanGoBack { get; private set; }

    private int _surfaceWidth = Screen.width;
    private int _surfaceHeight = 1440; 
    private AndroidJavaObject _ajc;
    private Texture2D _imageTexture2D;
    private RectTransform _rawImageRect;
    private static string classString = "com.eyeflite.ian.geckoviewplugin.GeckoViewPLugin";
    private TouchScreenKeyboard overlayKeyboard;
    private string prevInputText = "";
    private bool prevKeyboardVisibility = false;

    void Update()
    {
        if (overlayKeyboard != null && overlayKeyboard.text != prevInputText)
        {
            Debug.Log(prevInputText + " " + overlayKeyboard.status);

            int matchLength = CountMatchLength(prevInputText, overlayKeyboard.text);
            Backspace(prevInputText.Length - matchLength);
            AppendText(overlayKeyboard.text.Substring(matchLength));

            prevInputText = overlayKeyboard.text;
        }
    }

    private int CountMatchLength(string str1, string str2)
    {
        int i;
        for (i = 1; i <= Math.Min(str1.Length, str2.Length); i++)
        {
            if (str1.Substring(0, i) != str2.Substring(0, i)) break;
        }
        return i - 1;
    }

    // CALL THIS TO ADD KEYS TO BROWSER    
    public void AppendText(string appendText)
    {
        //CallAjc("AddKeys", new object[] {appendText});
        CallAjc("AddText", new object[] {appendText});
    }
    
    // CHANGE PER YOUR INPUT MODULE SPECIFICS
    public void OnClick()
    {
        AddTap(GazePointer.transform.position);       
    }

    //TODO: show your keyboard here
    public void ChangeKeyboardVisiblity(bool show)
    {
        if (show && show != prevKeyboardVisibility)
            overlayKeyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
        prevKeyboardVisibility = show;
    }

    public void SetOverlayDepth(int depth)
    {
        _overlay.compositionDepth = depth;
    }

    
    /// <summary>
    /// Calls the plugin to get png bytes from the surface.
    /// </summary>
    /// <returns>PNG byte array</returns>
    public byte[] TakePngScreenShot(int width=0, int height=0, int quality=100)
    {
        if (width == 0 || width> _surfaceWidth) width = _surfaceWidth;
        if (height == 0 || height>_surfaceHeight) height = _surfaceHeight;
            
        AndroidJavaObject jo = _ajc.Call<AndroidJavaObject>("GetSurfaceBytesBuffer",
            new object[]{width, height, quality});
        AndroidJavaObject bufferObject = jo.Get<AndroidJavaObject>("Buffer");
        byte[] bytes = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(bufferObject.GetRawObject());
        return bytes;
    }

    
    #region Button Interactions
    

    public void InvokeLoadURL()
    {
        if (UrlInputField.text == "")
        {
            LoadURL("google.com");
        }

        string potentialUrl = UrlInputField.text;

        if (ValidHttpURL(potentialUrl, out var outUri))
        {
            LoadURL(outUri.AbsoluteUri);
        }
        else
        {
            string encodedSearchString = WebUtility.UrlEncode(potentialUrl);
            string searchUrl = "https://www.google.com/search?q=" + encodedSearchString;
            LoadURL(searchUrl);
        }

    }

        

    // PLUGIN METHODS:

    public void InvokeStopLoading()
    {
        CallAjc("StopWebview", new object[] { });
    }

    // NAVIGATION: 
    
    public void LoadURLIfNew(string url)
    {
        if (url!=CurrentUrl)
            LoadURL(url);
    }

        
    public void LoadURL(string url)
    {            
        SetInputFieldUrl(url);
        CallAjc("LoadURL", new object[] {url});

    }

    public void InvokeGoBack()
    {
        CallAjc("GoBack", new object[] { });
    }

    public void InvokeGoForward()
    {
        CallAjc("GoForward", new object[] { });
    }


    // SCROLLING:
    
    private int _scrollBy = 50;
    public void InvokeScrollUp(float multiplier = 1f)
    {
        int scrollBy = (int) (_scrollBy * multiplier);
        CallAjc("ScrollByXorY", new object[] { 0, -scrollBy });
    //            int javaCode = 19;
    }

    public void InvokeScrollDown(float multiplier = 1f)
    {
        int scrollBy = (int) (_scrollBy * multiplier);
        CallAjc("ScrollByXorY", new object[] { 0, scrollBy });
    //            int javaCode = 20;
    }

    public void InvokeScrollLeft()
    {
        CallAjc("ScrollByXorY", new object[] { -50, 0 });
    }

    public void InvokeScrollRight()
    {
        CallAjc("ScrollByXorY", new object[] { 50, 0 });
    }


    public void InvokeScrollPageUp()
    {
        int javaCode = 92;
        CallAjc("AddKeyCode", new object[] { javaCode });
    }

    public void InvokeScrollPageDown()
    {
        int javaCode = 93;
        CallAjc("AddKeyCode", new object[] { javaCode });
    }


    public void InvokeScrollAllTheWay(bool down)
    {
        int javaCode = 122; // home key
        
        if (down)
            javaCode =123; // end key
        
        CallAjc("AddKeyCode", new object[] { javaCode });

    }

    public enum UserAgent
    {
        mobile,
        desktop,
        vr
    }

    private void SetBrowserUserAgent(UserAgent type, bool reload)
    {
        CallAjc("SetBrowserUserAgent", new object[] { type.ToString("G"), reload});
    }


    /// <summary>
    /// Current hack to zoom out in desktop view.
    /// reloads in desktop view and then adds a double tap at 100% progress
    /// </summary>
    public void InvokeToggleUserAgentThenZoomOut()
    {
        InvokeToggleUserAgent();
        if (_currentUserAgent == UserAgent.desktop)
            OnProgressUpdate += DoubleTapAt100Progress;
    }

            
    private void DoubleTapAt100Progress(int progress)
    {
        if (progress< 100)
            return; 
        
        //   Debug.Log("adding double tap");
        OnProgressUpdate -= DoubleTapAt100Progress;
        Vector2 position = new Vector2(0,0);
        // TODO:
    //            AddTapDirectly(position);
    //            AddTapDirectly(position);
    }


    private void InvokeToggleUserAgent()
    {
        if (_currentUserAgent== UserAgent.mobile)
            _currentUserAgent = UserAgent.desktop;

        else if (_currentUserAgent == UserAgent.desktop)
            _currentUserAgent = UserAgent.mobile;

        SetBrowserUserAgent(_currentUserAgent, true);
    }

   
    public void InvokeRefresh()
    {
        CallAjc("Refresh", new object[] { });
    }

    
    // KEYBOARD INPUT:
    
    public void Backspace(int repeat=0)
    {
        int javaCode = 67;
        CallAjc("AddKeyCode", new object[] { javaCode, 0, repeat });
    }

    public void MoveCaretLeft()
    {
        int javaCode = 21;
        CallAjc("AddKeyCode", new object[] { javaCode });
    }

    public void MoveCaretRight()
    {
        int javaCode = 22;
        CallAjc("AddKeyCode", new object[] { javaCode });

    }

    public void MoveCaretUp()
    {
        int javaCode = 19;
        CallAjc("AddKeyCode", new object[] { javaCode });
    }

    public void MoveCaretDown()
    {
        int javaCode = 20;
        CallAjc("AddKeyCode", new object[] { javaCode });
    }


    public void Escape()
    {
        // youtube notes: b_key= 30, backspace = 67, media close = 128 ,menu=82, escape=111, back = 4, tv_contents_menu = 256
        int code = 111; 
        CallAjc("AddKeyCode", new object[] {code});
    }

    public void Enter()
    {
        // j,k,l all seem like they can be used to pause/play a video
        // youtube notes: numpad_enter=160, enter =66, space = 62
        int code = 66;
        CallAjc("AddKeyCode", new object[] {code});
    }

    public void AddKeyCodeWithModifier(int keyCode, int modifier)
    {
        CallAjc("AddKeyCode", new object[] {keyCode, modifier});
    }


    // SESSIONS:
    
    public void DeactivateGeckoSession()
    {
        CallAjc("ActivateSession", new object[] {false});
    }

    public void ActivateGeckoSession()
    {
        CallAjc("ActivateSession", new object[] {true});
    }

    public void ExitWebview()
    {
        CallAjc("ExitWebview", new object[] {});
    }
    
    
#endregion

    
    #region Initialization
    private void Awake()
    {
        UnityThread.initUnityThread();
        window = GetComponentInParent<Window>();
        if (window == null) singleWindow = GetComponentInParent<SingleWindow>();
        _overlay = GetComponent<OVROverlay>();
        if (window != null) RawImage = window.RawImage;
        else RawImage = singleWindow.RawImage;
        _rawImageRect = RawImage.GetComponent<RectTransform>();

        InitializeAndroidPlugin();
    }
    
    

    private void OnGeckoViewReady()
    {
        isReady = true;
    }

  
    /// <summary>
        /// Sets the surface for our GeckoView plugin when the OVROverlay is ready and the plugin is initialized
        /// Also we sign up for the sign in event so we can load the first webpage/initialize history
        /// when the user signs ins
        /// </summary>
        /// <returns>The start function</returns>
        private IEnumerator Start()
        {

#if UNITY_EDITOR
            yield break;
#endif
            // Wait for surface to be available.
            while (_overlay.externalSurfaceObject == IntPtr.Zero || _ajc == null)
            {
                yield return null;
            }
            Debug.Log("Browser Start!");
            var pluginClass = _ajc.GetRawClass();
            var pluginObject = _ajc.GetRawObject();
            var surfaceMethodId = AndroidJNI.GetMethodID(pluginClass, "PassSurface", "(Landroid/view/Surface;)V");
            
            AndroidJNI.CallVoidMethod(pluginObject, surfaceMethodId,
                new jvalue[] { new jvalue { l = _overlay.externalSurfaceObject } });

            AndroidJNI.DeleteLocalRef(pluginClass);
            AndroidJNI.DeleteLocalRef(pluginObject);

            OnGeckoViewReady();
        if (window != null) window.OnGeckoViewReady();
        else singleWindow.OnGeckoViewReady();
        }

       
        /// <summary>
        /// Initializes the android plugin.by creating our GeckoView instance
        /// and passing it the proper width/height and user agent
        /// also passes a reference to the callback object
        /// </summary>
        public void InitializeAndroidPlugin()
        {
#if UNITY_EDITOR
            return;
#endif
            // testing new values 
            _surfaceWidth = _overlay.externalSurfaceWidth; //(int) _rawImage.rectTransform.rect.width;
            _surfaceHeight = _overlay.externalSurfaceHeight; //(int) (_rawImage.rectTransform.rect.height);

            var tempAjc = new AndroidJavaClass(classString);
            _ajc = tempAjc.CallStatic<AndroidJavaObject>("CreateInstance",
                new object[] {_overlay.externalSurfaceWidth, _overlay.externalSurfaceHeight, UserAgent.mobile.ToString("G") });
            UnityInterface androidPluginCallback = new UnityInterface {BrowserView = this};
            _ajc.Call("SetUnityBitmapCallback", androidPluginCallback);
        }
    
    
    
    
//    // Browser view must have pivot point at (0.5,0.5)



    #endregion
     
    #region AndroidInterface
    
    // method to to tap in the right coords despite difference in scaling
    private void AddTap(Vector3 pos)
    {
        // get dimensions of rawimage
        Camera thisCamera = Camera.main;
        Debug.Assert(thisCamera.name == "CenterEyeAnchor");
        Vector2 positionInRect = new Vector2();

        Vector2 screenPoint = RectTransformUtility .WorldToScreenPoint(thisCamera, pos);
        //Debug.Log("screen point: " + screenPoint);
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rawImageRect,
            screenPoint, thisCamera, out positionInRect);
        //Debug.Log("main camera is: " + Camera.main.name);

        // take care of the pivots and their effect on position
        positionInRect.x += _rawImageRect.pivot.x * _rawImageRect.rect.width; 
        positionInRect.y += (_rawImageRect.pivot.y*_rawImageRect.rect.height);
        
        Debug.Assert(Math.Abs(_rawImageRect.pivot.y) > 0);
        // change coordinate system 
        positionInRect.y += -_rawImageRect.rect.height;
        positionInRect.y = Math.Abs(positionInRect.y);
        //Debug.Log(positionInRect);

        // get the screen dimensions and divide them by the rectangle's screen dimensions for scaling
        float screenWidth = _surfaceWidth; //rect.width;
        float screenHeight = _surfaceHeight; //rect.height;

        float xScale = screenWidth / _rawImageRect.rect.width; // rectWidthInScreen;
        float yScale = screenHeight / _rawImageRect.rect.height; // rectHeightInScreen;

        Vector2 positionInWebView = new Vector2(positionInRect.x * xScale, positionInRect.y * yScale);
//        Debug.Log("position in webview: " + positionInWebView);

//        Debug.Log("transformed pos:" + positionInWebView);
        // if we're within the bounds of the rectangle
        if (_ajc!= null)
        {
            CallAjc("AddLongTap", new object[]{positionInWebView.x, positionInWebView.y});
        }
    }
      
    
    // before calling anything to theplugin, make sure it has drawing enabled
    public void CallAjc(string methodName, object[] paramies)
    {
        if (_ajc != null)
        {
            //_ajc.Call("SetShouldDraw", true);
            _ajc.Call(methodName,paramies);
        }
    } 

        public void SetInputFieldUrl(string url)
    {

        // don't set the url to the same thing multiple times, it may overwrite what the user has typed
        if (url == CurrentUrl) 
            return;
        
        UrlInputField.text = url;
        CurrentUrl = url;
    }

        public void UpdateProgress(int progress)
        {
            OnProgressUpdate?.Invoke(progress);
            ProgressText.text = progress.ToString();
        }

        public void SetCanGoBack(bool canGoBack)
        {
            CanGoBack = canGoBack;
            BackButton.interactable = canGoBack;
        }

        public void SetCanGoForward(bool canGoForward)
        {
            CanGoForward = canGoForward;
            ForwardButton.interactable = canGoForward;
        }

    private string _filePathToReadWhenComplete = "";
    public void PrepareReadFile(string path, string directory, string fileName, string url)
    {
        Debug.Log("download file called in plugin");
        
        path = Path.Combine(path, directory);
        Debug.Log("abs path is: " + path);
        _filePathToReadWhenComplete = Path.Combine(path, fileName);
    }

    public void ReadFile()
    {
        Debug.Log("abs path with filename is: " + _filePathToReadWhenComplete);
        string fileContents = System.IO.File.ReadAllText(_filePathToReadWhenComplete);
        Debug.Log("file contents are: " + fileContents);

    }
    
    
    public void OnPageVisited(string url, string lastUrl)
    {
        Debug.Log("on page visited: " + url );
        Debug.Log("last page visited: " + lastUrl );
        // NOTE: seems like this method is used too often for random sites of no importance
        OnPageLoad?.Invoke(url);
    }

    public void OnLoadRequest(string url)
    {
        Debug.Log("On Load Request: " + url);
        if (window != null) window.OnLoadRequest(url);
        else singleWindow.OnLoadRequest(url);
    }
    
    


    // used for autofill text, if the input field target changes
    public void RestartInput()
    {
        
    }

    // reload the last url
    public void OnSessionCrash()
    {
        Debug.Log("Attempting browser restart" );
        //CallAjc("RestartBrowser", new object[]{("trash", true)});   
    }

    #endregion
 
    
    
    private bool ValidHttpURL(string s, out Uri resultURI)
    {        
        bool returnVal = false;
        
        if (!Regex.IsMatch(s, @"^https?:\/\/", RegexOptions.IgnoreCase))
            s = "http://" + s;
        
        if (Uri.TryCreate(s, UriKind.Absolute, out resultURI))
            returnVal = (resultURI.Scheme == Uri.UriSchemeHttp || 
                         resultURI.Scheme == Uri.UriSchemeHttps);
        
        if (!s.Contains(".") || s.Contains(" "))
        {
            returnVal = false;
        }
        
        if (!Uri.IsWellFormedUriString(s, UriKind.Absolute)) 
            returnVal = false;   
        


        return returnVal;
    }

}

  class UnityInterface : AndroidJavaProxy
    {
        public UnityInterface() : base("com.eyeflite.ian.geckoviewplugin.UnityInterface")
        {
        }

        public BrowserView BrowserView;


        public void updateProgress(int progress)
        {
//        Debug.Log("update progress called ");
            UnityThread.executeInUpdate(() => BrowserView.UpdateProgress(progress));

        }

        // TODO: implement different sessions for youtube vs. browser
        public void CanGoBack(bool able)
        {
//            Debug.Log("Gecko Says we can go back");
            UnityThread.executeInUpdate(() => BrowserView.SetCanGoBack(able));
        }
        
        // TODO: implement different sessions for youtube vs. browser
        public void CanGoForward(bool able)
        {
//            Debug.Log("Gecko Says we can go forward");
            UnityThread.executeInUpdate(() => BrowserView.SetCanGoForward(able));
        }

        public void updateURL(string url)
        {

//        Debug.Log("update url called! " + url);
            UnityThread.executeInUpdate(() => BrowserView.SetInputFieldUrl(url));
        }

        public void OnPageVisited(string url, string lastUrl)
        {
            UnityThread.executeInUpdate(() => BrowserView.OnPageVisited(url, lastUrl));

        }

        public void OnLoadRequest(string url)
        {
            UnityThread.executeInUpdate(() => BrowserView.OnLoadRequest(url));
        }


        public void ChangeKeyboardVisiblity(bool show)
        {
            UnityThread.executeInUpdate(() => BrowserView.ChangeKeyboardVisiblity(show));
        }

        public void RestartInput()
        {
            UnityThread.executeInUpdate(BrowserView.RestartInput);
        }

        public void OnSessionCrash()
        {
            UnityThread.executeInUpdate(BrowserView.OnSessionCrash);
        }

        public void OnFullScreenRequestChange(bool fullScreen){
        
//            UnityThread.executeInUpdate(() => BrowserView.OnFullScreenRequestChange(fullScreen));

        }

        //    public void DownloadFileRequestedAtURL(string path, string directory, string fileName, string url)
        //    {
        //        Debug.Log("message from android about download files: " + url);
        //        
        //        UnityThread.executeInUpdate(()=> BrowserView.PrepareReadFile(path,directory,fileName,url));
        //    }
        //
        //    public void FileDownloadComplete()
        //    {
        //        UnityThread.executeInUpdate(()=> BrowserView.ReadFile());
        //
        //    }




    }
