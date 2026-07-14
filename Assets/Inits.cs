using UnityEngine;
using GameAnalyticsSDK;

public class Inits : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameAnalytics.Initialize();

        GameAnalytics.SetCustomId("myCustomUserId");

        #if (UNITY_ANDROID)
        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "World1");
        #endif
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
