using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Tiny haptics util: Android <c>VibrationEffect.createOneShot(ms, amplitude)</c> through JNI
    /// (API 26+, with the legacy <c>vibrate(ms)</c> fallback), <c>Handheld.Vibrate</c> elsewhere on
    /// handhelds, and a no-op in the editor / desktop. Never throws — feel must not crash a build.
    /// </summary>
    public static class Haptics
    {
        static bool initialised;
        static bool supported;
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdkInt;
#endif

        public static void Pulse(int ms, int amplitude)
        {
            if (ms <= 0) return;
            Init();
            if (!supported) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (sdkInt >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, Mathf.Clamp(amplitude, 1, 255));
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", (long)ms);
                }
            }
            catch { supported = false; }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        static void Init()
        {
            if (initialised) return;
            initialised = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                sdkInt = version.GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                supported = vibrator != null && vibrator.Call<bool>("hasVibrator");
            }
            catch { supported = false; }
#elif UNITY_IOS && !UNITY_EDITOR
            supported = true;
#else
            supported = false;
#endif
        }
    }
}
