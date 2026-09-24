using UnityEditor;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// Screen scenes (Resources/Scenes, painted 752x1344) keep their own size: Unity's default import would scale a
    /// non-power-of-two texture to the nearest power of two and squash it. No mipmaps, clamped, full quality.
    /// </summary>
    public sealed class SceneArtImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Scenes/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
