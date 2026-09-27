using UnityEditor;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// Screen scenes (Resources/Scenes, painted 752x1344), card pictures (Resources/Thumbs) and the painted UI kit
    /// (Resources/UI) keep their own size:
    /// Unity's default import would scale a non-power-of-two texture to the nearest power of two, squashing a scene and
    /// breaking the kit's nine-slice borders. Clamped, full quality; the kit keeps mipmaps for small buttons.
    /// A smaller download (owner, 26 Sep 2026, the APK was 410 MB against Google Play's 200 MB): phones get ASTC for these
    /// (a non-power-of-two picture otherwise stayed uncompressed), and the 3D models (Content/Models, downloaded by the app) import their
    /// textures at 1024 in ASTC 6x6 (enemies at 512) and their meshes compressed without tangents (no shader here uses
    /// normal maps). The player data is packed with LZ4HC (ProjectSetup).
    /// Raising GetVersion reimports every texture and model under these rules.
    /// </summary>
    public sealed class SceneArtImport : AssetPostprocessor
    {
        public override uint GetVersion() => 3;

        /// <summary>The map music (Content/Music, downloaded, 27 Sep 2026): the long themes stream from their bundle (only the
        /// one playing and the one fading are read); the short stings load whole. Vorbis for both.</summary>
        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Content/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            bool sting = System.IO.Path.GetFileName(path).StartsWith("Sting");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = sting ? UnityEngine.AudioClipLoadType.DecompressOnLoad : UnityEngine.AudioClipLoadType.Streaming;
            settings.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = !sting;
        }

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            var importer = (TextureImporter)assetImporter;
            if (path.Contains("/Content/Models/"))
            {
                // Enemies stand small on a phone's lane: 512 is plenty; heroes, shown large on their stages, keep 1024.
                int size = path.Contains("/Content/Models/Mobs/") ? 512 : 1024;
                importer.maxTextureSize = size;
                Phones(importer, TextureImporterFormat.ASTC_6x6, size);
                return;
            }
            bool scene = path.Contains("/Resources/Scenes/") || path.Contains("/Resources/Thumbs/"), kit = path.Contains("/Resources/UI/");
            if (!scene && !kit) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = kit;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaIsTransparency = kit;
            // The kit's borders stay crisp at 4x4; painted scenes read the same at 6x6.
            Phones(importer, kit ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6, 2048);
        }

        private void OnPreprocessModel()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Content/Models/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
        }

        private static void Phones(TextureImporter importer, TextureImporterFormat format, int maxSize)
        {
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.format = format;
                settings.maxTextureSize = maxSize;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
