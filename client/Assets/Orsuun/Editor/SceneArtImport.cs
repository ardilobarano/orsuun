using UnityEditor;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// Screen scenes (Resources/Scenes, painted 752x1344), card pictures (Resources/Thumbs) and the painted UI kit
    /// (Resources/UI) keep their own size:
    /// Unity's default import would scale a non-power-of-two texture to the nearest power of two, squashing a scene and
    /// breaking the kit's nine-slice borders. Clamped, full quality; the kit keeps mipmaps for small buttons.
    /// </summary>
    public sealed class SceneArtImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool scene = path.Contains("/Resources/Scenes/") || path.Contains("/Resources/Thumbs/"), kit = path.Contains("/Resources/UI/");
            if (!scene && !kit) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = kit;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaIsTransparency = kit;
        }
    }
}
