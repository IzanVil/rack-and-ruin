using UnityEngine;

namespace ServerGame.Utils
{
    public static class Viewport
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void SgWatchViewport();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int SgViewportCssWidth();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int SgViewportCssHeight();

#endif

        public static void Watch()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SgWatchViewport();
#endif
        }

        public static Vector2 CssSize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            float w = SgViewportCssWidth();
            float h = SgViewportCssHeight();
            if (w > 0f && h > 0f) return new Vector2(w, h);
#endif
            return new Vector2(Screen.width, Screen.height);
        }
    }
}
