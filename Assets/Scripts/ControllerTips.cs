using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllerTips : MonoBehaviour
{
    [SerializeField] private bool isRight = true;
    private bool enabledTips = true;
    private bool prevShouldShow = true;

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Two)) enabledTips = !enabledTips;

        bool shouldShow = enabledTips && ExperimentManager.instance.IsRight == isRight;
        if (shouldShow != prevShouldShow) ToggleTips(shouldShow);
    }

    private void ToggleTips(bool shouldShow)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(shouldShow);
        }
        prevShouldShow = shouldShow;
    }
}
