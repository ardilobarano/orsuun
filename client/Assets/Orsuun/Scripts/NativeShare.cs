using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// The phone's share sheet for a line of text (INVITE A FRIEND): Android's chooser (ACTION_SEND), iOS's activity sheet
    /// (Plugins/iOS/OrsuunShare.mm). False where there is none (the Mac, the editor): the caller copies the text instead.
    /// </summary>
    public static class NativeShare
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void OrsuunShare_Text(string text);
#endif

        public static bool Share(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Orsuun"))
                        activity.Call("startActivity", chooser);
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Share failed: " + ex.Message);
                return false;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            OrsuunShare_Text(text);
            return true;
#else
            return false;
#endif
        }
    }
}
