using Orsuun.Rules;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>The three Banners on screen: their colours and their flags (Resources/Art/Banners, cut out of the flag art).</summary>
    public static class BannerLook
    {
        public static Color Color(Banner banner)
        {
            if (banner == Banner.None) return Palette.Muted;
            return ColorUtility.TryParseHtmlString(Banners.Def(banner).ColorHex, out Color c) ? c : Palette.Muted;
        }

        public static Texture2D Flag(Banner banner) => banner == Banner.None ? null : Resources.Load<Texture2D>("Art/Banners/" + banner);

        public static string Name(Banner banner) => banner == Banner.None ? "no Banner" : Banners.Def(banner).Name;

        public static Banner Parse(string text) => System.Enum.TryParse(text, out Banner b) ? b : Banner.None;

        /// <summary>A flag in its own box, kept to its own shape (the fitter fits the box, never the canvas).</summary>
        public static UnityEngine.UI.RawImage FlagImage(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
        {
            RectTransform box = Ui.Rect(name, parent, xMin, yMin, xMax, yMax);
            RectTransform inner = Ui.Rect("Image", box, 0f, 0f, 1f, 1f);
            var image = inner.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            var fit = inner.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.36f;
            return image;
        }

        public static void Show(UnityEngine.UI.RawImage image, Banner banner)
        {
            Texture2D flag = Flag(banner);
            if (image.texture != flag) image.texture = flag;
            image.enabled = flag != null;
            if (flag != null) image.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = flag.width / (float)flag.height;
        }
    }
}
