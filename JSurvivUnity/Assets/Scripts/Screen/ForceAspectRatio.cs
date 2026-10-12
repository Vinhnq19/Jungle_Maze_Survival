using UnityEngine;

public class ForceAspectRatio : MonoBehaviour
{
    private const float TargetAspect = 1920.0f / 1080.0f;

    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    void Update()
    {
        if (UnityEngine.Screen.width != lastScreenWidth || UnityEngine.Screen.height != lastScreenHeight)
        {
            int newWidth = (int)(UnityEngine.Screen.height * TargetAspect);

            UnityEngine.Screen.SetResolution(newWidth, UnityEngine.Screen.height, UnityEngine.Screen.fullScreen);
            
            lastScreenWidth = UnityEngine.Screen.width;
            lastScreenHeight = UnityEngine.Screen.height;
        }
    }
}