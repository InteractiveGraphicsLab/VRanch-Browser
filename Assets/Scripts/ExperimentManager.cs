using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager instance = null;

    [SerializeField] private Transform player;
    public Transform Player { get => player; }
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform leftHandAnchor;
    public Transform HandAnchor { get; private set; }
    public Transform HandAnchorSub { get; private set; }
    [SerializeField] private Transform gazePointer;
    public Transform GazePointer { get => gazePointer; }
    [SerializeField] private OVRGazePointer oVRGazePointer;
    [SerializeField] private UnityEngine.EventSystems.OVRInputModule oVRInputModule;
    

    private bool isRight = true;
    public bool IsRight { get => isRight; }
    
    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(this.gameObject);
        OVRManager.InputFocusLost += HidePointer;
        OVRManager.InputFocusAcquired += ShowPointer;
    }

    public void ShowPointer(){
        oVRGazePointer.gameObject.SetActive(true);

    }

    public void HidePointer(){
        oVRGazePointer.gameObject.SetActive(false);
    }

    void Start()
    {
        SetMainHand(true);
        ChangeScene("VRanchBrowser");
    }

    public void ChangeScene(string loadSceneName, string unloadSceneName = "")
    {
        if (unloadSceneName != "")
        {
            SceneManager.UnloadScene(unloadSceneName);
            Resources.UnloadUnusedAssets();
        }

        if (loadSceneName != "") SceneManager.LoadScene(loadSceneName, LoadSceneMode.Additive);
    }

    public void SetMainHand(bool isRight)
    {
        this.isRight = isRight;
        HandAnchor = isRight ? rightHandAnchor : leftHandAnchor;
        HandAnchorSub = isRight ? leftHandAnchor : rightHandAnchor;
        oVRGazePointer.rayTransform = HandAnchor;
        oVRInputModule.rayTransform = HandAnchor.FindChildRecursive("OVRControllerPrefab");
        oVRInputModule.joyPadClickButton = isRight ?
            OVRInput.Button.One | OVRInput.Button.SecondaryIndexTrigger :
            OVRInput.Button.Three | OVRInput.Button.PrimaryIndexTrigger;
    }
}
