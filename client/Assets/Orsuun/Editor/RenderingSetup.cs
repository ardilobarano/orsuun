using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// URP setup (idempotent, run from ProjectSetup.Run): pipeline asset with HDR, the bloom profile, the ember-glow
    /// materials the lane loads from Resources, and the Korstone model import settings.
    /// Headless preview of the glow steps and the Korstone:
    /// Unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.RenderingSetup.RenderPreview
    /// </summary>
    public static class RenderingSetup
    {
        private const string Dir = "Assets/Orsuun/Rendering";
        private const string PipelinePath = Dir + "/OrsuunURP.asset";
        private const string RendererPath = Dir + "/OrsuunURP_Renderer.asset";
        private const string Res = "Assets/Orsuun/Resources/";
        /// <summary>The downloaded art (models, their materials, backdrops, floors; Orsuun.Client.Art), outside Resources.</summary>
        private const string Con = "Assets/Orsuun/Content/";
        private const string GlowShader = "Orsuun/EmberGlow";

        public static readonly Color HeroColor = new Color(0.25f, 0.55f, 0.95f);
        public static readonly Color SteelColor = new Color(0.62f, 0.62f, 0.66f);

        [MenuItem("Orsuun/Rendering Setup")]
        public static void Ensure()
        {
            Directory.CreateDirectory(Dir);
            EnsurePipeline();
            EnsureGreyBox();
            EnsureGlowMaterial("EmberGear", HeroColor, crackScale: 5f, crackWidth: 0.025f, intensity: 1.3f, rim: 2.5f);
            Material weapon = EnsureGlowMaterial("EmberWeapon", SteelColor, crackScale: 7f, crackWidth: 0.04f, intensity: 2.0f, rim: 2f);
            weapon.SetFloat("_BodyGlow", 1f);
            EditorUtility.SetDirty(weapon);
            Material korstone = EnsureGlowMaterial("KorstoneEmber", new Color(0.035f, 0.03f, 0.03f), crackScale: 2.4f, crackWidth: 0.02f, intensity: 2.2f, rim: 3f);
            korstone.SetFloat("_CrackAlways", 1f);
            korstone.SetFloat("_CrackFadeTop", 2.4f);
            korstone.SetFloat("_Roughness", 0.3f);
            EditorUtility.SetDirty(korstone);
            EnsurePostFx();
            EnsureBackdrops();
            EnsureFloors();
            EnsureLooks();
            EnsureClassLooks();
            EnsureKorstones();
            EnsureMobs();
            EnsureRiver();
            EnsureScenery();
            EnsureTown();
            EnsureFx();
            EnsureAudioImport();
            EnsureKorstoneImport();
            AssetDatabase.SaveAssets();
            Debug.Log("Orsuun rendering setup complete (URP " + (GraphicsSettings.defaultRenderPipeline != null) + ").");
        }

        private static void EnsurePipeline()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, RendererPath);
            }
            if (data.postProcessData == null)
            {
                data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                EditorUtility.SetDirty(data);
            }
            Debug.Log("Renderer post-process data: " + (data.postProcessData != null ? data.postProcessData.name : "MISSING"));

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = 2;
            asset.shadowDistance = 30f;
            // SRP Batcher off (23 Sep 2026): with it on, EmberGlow materials on Metal all drew the last-set base colour
            // (verified with RenderPreview's albedo debug pass). The lane draws a few dozen objects, so the batcher's
            // CPU saving is negligible here; revisit when real scenes have hundreds of materials.
            asset.useSRPBatcher = false;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static void EnsureGreyBox()
        {
            string path = Res + "GreyBox.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
            if (mat.shader != lit) mat.shader = lit;
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
        }

        private static Material EnsureGlowMaterial(string name, Color baseColor, float crackScale, float crackWidth, float intensity, float rim)
        {
            // The looks' and Korstone shapes' materials ride with their models in the downloaded Content; the rest stay in Resources.
            string path = (name.StartsWith("Looks/") || name.StartsWith("Korstones/") ? Con : Res) + name + ".mat";
            Shader shader = Shader.Find(GlowShader);
            if (shader == null) throw new System.Exception("Shader " + GlowShader + " not found or failed to compile.");
            foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
                Debug.Log("EmberGlow " + m.severity + ": " + m.message + " (line " + m.line + ", " + m.platform + ")");
            if (!shader.isSupported) Debug.LogError("EmberGlow is not supported on this platform/pipeline.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_Tint", baseColor);
            mat.SetFloat("_CrackScale", crackScale);
            mat.SetFloat("_CrackWidth", crackWidth);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_RimPower", rim);
            mat.SetFloat("_Debug", 0f);
            mat.SetFloat("_CrackAlways", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// One glow material per item look (Resources/Models/Looks/*.fbx from art/blender/looks.py) in Resources/Looks,
        /// carrying that look's base-colour texture. Weapons run hotter and carry a whole-surface glow share.
        /// </summary>
        private static void EnsureLooks()
        {
            const string models = Con + "Models/Looks/";
            if (!Directory.Exists(models)) return;
            Directory.CreateDirectory(Con + "Looks");
            foreach (string fbx in Directory.GetFiles(models, "*.fbx"))
            {
                string id = Path.GetFileNameWithoutExtension(fbx);
                bool weapon = id.StartsWith("Weapon");
                if (weapon) EnsureModelImport(models + id + ".fbx");
                else EnsureAnimatedImport(models + id + ".fbx");
                Material mat = EnsureGlowMaterial("Looks/" + id, Color.white, crackScale: 5f, crackWidth: 0.025f,
                    intensity: weapon ? 1.4f : 1.15f, rim: weapon ? 3f : 2.5f);   // a thin glaive is nearly all rim: kept lower
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(models + id + "BaseColor.png"));
                mat.SetFloat("_BodyGlow", weapon ? 1f : 0f);
                // Armour meshes are many small fragments: a wide aura hull splits into shards, so armour keeps it thin.
                mat.SetFloat("_AuraWidth", weapon ? 0.035f : 0.01f);
                // A glaive is modelled one unit long and scaled to the armour's hands: its glitter cells and sheen bands are
                // set finer so they come out the size of the armour's (26 Sep 2026).
                mat.SetFloat("_GlitterScale", weapon ? 50f : 20f);
                mat.SetFloat("_SheenScale", weapon ? 6f : 3f);
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>
        /// Other classes' bodies (Resources/Models/Classes/*.fbx, rigged by art/blender/rig.py): legacy clips like the
        /// Vanguard's armour looks, and a glow material in Resources/Looks named after the class.
        /// </summary>
        private static void EnsureClassLooks()
        {
            const string models = Con + "Models/Classes/";
            if (!Directory.Exists(models)) return;
            foreach (string fbx in Directory.GetFiles(models, "*.fbx"))
            {
                string id = Path.GetFileNameWithoutExtension(fbx);
                EnsureAnimatedImport(models + id + ".fbx");
                Material mat = EnsureGlowMaterial("Looks/" + id, Color.white, crackScale: 5f, crackWidth: 0.025f, intensity: 1.15f, rim: 2.5f);
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(models + id + "BaseColor.png"));
                mat.SetFloat("_BodyGlow", 0f);
                mat.SetFloat("_AuraWidth", 0.01f);
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>
        /// Korstone shapes (Resources/Models/Korstones/A|B|C.fbx from looks.mob_model): one EmberGlow material each in
        /// Resources/Korstones with the shape's texture. The lane copies it per stone and sets the tier's colours
        /// (KorstoneLook); _CrackRemap repaints the painted cracks, a light layer of procedural cracks runs over the top.
        /// </summary>
        private static void EnsureKorstones()
        {
            const string models = Con + "Models/Korstones/";
            if (!Directory.Exists(models)) return;
            Directory.CreateDirectory(Con + "Korstones");
            foreach (string fbx in Directory.GetFiles(models, "*.fbx"))
            {
                string id = Path.GetFileNameWithoutExtension(fbx);
                EnsureModelImport(models + id + ".fbx");
                Material mat = EnsureGlowMaterial("Korstones/" + id, new Color(0.78f, 0.64f, 0.56f), crackScale: 2.2f, crackWidth: 0.018f, intensity: 2.4f, rim: 3f);
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(models + id + "BaseColor.png"));
                mat.SetFloat("_CrackRemap", 1f);
                mat.SetFloat("_CrackAlways", 0.2f);
                mat.SetFloat("_CrackFadeTop", 0f);
                mat.SetFloat("_Roughness", 0.35f);
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>
        /// One URP Lit material per enemy model (Resources/Models/Mobs/*.fbx from looks.py mob_model) in Resources/Mobs.
        /// The Hollowed are ember-corrupted: their base colour doubles as a faint emission, so the bright orange veins
        /// cross the bloom threshold while dark fur barely lifts. Deserters are plain men and get none.
        /// </summary>
        private static void EnsureMobs()
        {
            const string models = Con + "Models/Mobs/";
            if (!Directory.Exists(models)) return;
            Directory.CreateDirectory(Con + "Mobs");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string fbx in Directory.GetFiles(models, "*.fbx"))
            {
                string id = Path.GetFileNameWithoutExtension(fbx);
                // Mobs are rigged with their own clips since 24 Sep 2026 (art/blender/mobrig.py).
                EnsureAnimatedImport(models + id + ".fbx");
                // Mounts stay readable: the lane measures each one's saddle and barrel from its mesh (LaneView.Seat).
                if (id.StartsWith("Mount")) EnsureReadable(models + id + ".fbx");
                string matPath = Con + "Mobs/" + id + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = lit;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(models + id + "BaseColor.png");
                mat.SetTexture("_BaseMap", tex);
                bool ember = id == "Wolf" || id == "Boar" || id == "Greyjaw";   // the Hollowed; people and other beasts are not ember-veined
                // The Cinder Marches burn from within; Whisperwood's dead glow cold (25 Sep 2026).
                bool lava = id == "AshFiend" || id == "MagmaHound" || id == "Azhdar" || id == "SapHorror";   // and the Bloodbirch's glowing sap
                bool ghost = id == "HangingSpirit" || id == "LanternWisp" || id == "LanternWidow" || id == "StoneSentinel" || id == "LastCarver"   // and the Archive's glowing runes
                             || id == "Hurm" || id == "DebtWraith";   // Hurm's pale cracks and the debt wraiths (25 Sep 2026)
                // The Thousand Markers' risen army burns with violet oath-light; the Hollow Throne's court with molten gold (26 Sep 2026).
                bool oath = id == "RisenTrooper" || id == "RisenRider" || id == "RisenCaptain" || id == "Varkesh";
                bool court = id == "ThroneGuard" || id == "KhanHound" || id == "OathChanter" || id == "KhanShadow";
                // The Hollowed read darker than their bright sheet textures: corrupted beasts, not farm animals.
                mat.SetColor("_BaseColor", ember ? new Color(0.72f, 0.68f, 0.68f) : Color.white);
                mat.SetFloat("_Smoothness", 0.2f);
                // Wardrobe pieces (owner, 25 Sep 2026): the Hollow Steed glows cold, the Ember Fox's tail and the Amber Road Courser warm.
                Color? glow = ember ? new Color(0.32f, 0.26f, 0.2f)
                    : lava ? new Color(0.55f, 0.32f, 0.16f)
                    : ghost ? new Color(0.22f, 0.3f, 0.38f)
                    : oath ? new Color(0.26f, 0.2f, 0.34f)
                    : court ? new Color(0.3f, 0.22f, 0.1f)
                    : id == "MountWarhorseHollow" ? new Color(0.3f, 0.4f, 0.55f)
                    : id == "MountWarhorseAmber" ? new Color(0.26f, 0.2f, 0.12f)     // the Trail's courser, warm on its bronze
                    : id == "MountWarhorseWhite" ? new Color(0.05f, 0.07f, 0.1f)     // the second season's courser, a breath of frost on its scales
                    : id == "PetFox" ? new Color(0.22f, 0.16f, 0.1f)
                    : id == "PetFalcon" || id == "PetEagle" ? new Color(0.3f, 0.28f, 0.25f) : (Color?)null;   // birds fly in the shade side
                if (glow.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", tex);
                    mat.SetColor("_EmissionColor", glow.Value);
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                }
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>
        /// Music streams as Vorbis (a minute of stereo PCM would sit in memory on the phone); short effects are
        /// mono ADPCM, decompressed on load so they fire without delay; the river's long loops (water, birds, fire) stay
        /// Vorbis in memory, mono.
        /// </summary>
        private static void EnsureAudioImport()
        {
            const string dir = Res + "Audio/";
            if (!Directory.Exists(dir)) return;
            foreach (string wav in Directory.GetFiles(dir, "*.wav"))
            {
                string path = dir + Path.GetFileName(wav);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                bool music = Path.GetFileName(wav).StartsWith("Music");
                bool loop = System.Array.IndexOf(new[] { "RiverWater.wav", "RiverBirds.wav", "RiverFire.wav", "TownMarket.wav" }, Path.GetFileName(wav)) >= 0;
                AudioImporterSampleSettings want = importer.defaultSampleSettings;
                want.loadType = music ? AudioClipLoadType.Streaming : loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                want.compressionFormat = music || loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                want.quality = music ? 0.55f : loop ? 0.5f : 1f;
                AudioImporterSampleSettings have = importer.defaultSampleSettings;
                bool same = have.loadType == want.loadType && have.compressionFormat == want.compressionFormat
                    && Mathf.Approximately(have.quality, want.quality) && importer.forceToMono == !music;
                if (same) continue;
                importer.defaultSampleSettings = want;
                importer.forceToMono = !music;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Additive spark material for hit effects (Resources/FxSpark.mat, URP Particles/Unlit) with a soft round dot
        /// texture generated here. Kept in Resources so the build keeps the particle shader.
        /// </summary>
        private static void EnsureFx()
        {
            string texPath = Res + "FxSparkDot.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                const int size = 64;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxSparkDot" };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.6f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                AssetDatabase.CreateAsset(tex, texPath);
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) { Debug.LogWarning("URP particle shader missing"); return; }
            string matPath = Res + "FxSpark.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1.6f, 1.3f, 1f, 1f));   // HDR-bright so the bloom picks the sparks up
            // Transparent, additive: the URP particle shader reads these instead of a blend-mode keyword per pass.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Unlit materials for the zone environment keys behind the lane (docs/concept/env-*.jpg, variant 1 of each).</summary>
        private static void EnsureBackdrops()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (string zone in new[] { "HuntingGround", "KorstoneField", "CommanderGround", "SaltFlats", "FrostPasture", "HollowSpire", "CinderMarches", "Whisperwood", "SilkWarren", "CarversArchive", "Bloodbirch", "DrownedSteppe", "ColossusGraves", "SunkenBazaar", "ThousandMarkers", "HollowThrone" })
            {
                string texPath = Con + "Backdrops/" + zone + ".jpg";
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (ti.wrapMode != TextureWrapMode.Clamp || ti.maxTextureSize != 2048))
                {
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) { Debug.LogWarning("Backdrop texture missing: " + texPath); continue; }
                string matPath = Con + "Backdrops/Backdrop" + zone + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(unlit); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = unlit;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>
        /// The hunt's scenery (LaneScenery, 28 Sep 2026): one atlas of six painted props per set (Content/Scenery), each a
        /// Lit material cut by its alpha and seen from both sides (a mirrored card shows its back).
        /// </summary>
        private static void EnsureScenery()
        {
            const string dir = Con + "Scenery/";
            if (!Directory.Exists(dir)) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string file in Directory.GetFiles(dir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string texPath = dir + name + ".png";
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti
                    && (ti.wrapMode != TextureWrapMode.Clamp || !ti.alphaIsTransparency || ti.maxTextureSize != 2048 || !ti.mipmapEnabled))
                {
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.alphaIsTransparency = true;
                    ti.maxTextureSize = 2048;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
                string matPath = dir + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = lit;
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.05f);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.SetFloat("_Cull", 0f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                mat.doubleSidedGI = true;
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>
        /// The town square (TownScene, 28 Sep 2026), in Content/Town beside its textures (tools/art/town_atlas.py): the
        /// painted far side (Unlit, clamped, its own size), the paving (Lit, repeating), the townsfolk and props (Unlit cards
        /// keep their painted light; alpha cut, both sides), the rugs (Lit, so the lanterns warm them) and the shade under
        /// figures (Unlit, see-through).
        /// </summary>
        private static void EnsureTown()
        {
            const string dir = Con + "Town/";
            if (!Directory.Exists(dir)) return;
            Texture2D Tex(string file, TextureWrapMode wrap, int max, bool alpha)
            {
                string path = dir + file;
                if (AssetImporter.GetAtPath(path) is TextureImporter ti
                    && (ti.wrapMode != wrap || ti.maxTextureSize != max || !ti.mipmapEnabled || ti.alphaIsTransparency != alpha
                        || ti.npotScale != TextureImporterNPOTScale.None || ti.anisoLevel != 8))
                {
                    ti.wrapMode = wrap;
                    ti.maxTextureSize = max;
                    ti.mipmapEnabled = true;
                    ti.alphaIsTransparency = alpha;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.anisoLevel = 8;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Material Mat(string name, Shader shader, Texture2D tex, Color tint)
            {
                string path = dir + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", tint);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(mat);
                return mat;
            }
            void Cut(Material mat)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.SetFloat("_Cull", 0f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                mat.doubleSidedGI = true;
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit"), unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Mat("Square", unlit, Tex("Square.jpg", TextureWrapMode.Clamp, 2048, false), Color.white);
            Material paving = Mat("Paving", lit, Tex("Paving.jpg", TextureWrapMode.Repeat, 1024, false), Color.white);
            paving.SetFloat("_Smoothness", 0.08f);
            // The evening's warmth on the painted folk, a touch under full so the bloom leaves them be.
            Cut(Mat("Folk", unlit, Tex("Folk.png", TextureWrapMode.Clamp, 2048, true), new Color(0.93f, 0.86f, 0.78f)));
            Cut(Mat("Props", unlit, Tex("Props.png", TextureWrapMode.Clamp, 2048, true), new Color(0.9f, 0.83f, 0.76f)));
            Material rugs = Mat("Rugs", lit, Tex("Rugs.png", TextureWrapMode.Clamp, 1024, true), Color.white);
            rugs.SetFloat("_Smoothness", 0.02f);
            Cut(rugs);
            Material shade = Mat("Shade", unlit, Tex("Shade.png", TextureWrapMode.Clamp, 128, true), Color.white);
            shade.SetFloat("_Surface", 1f);
            shade.SetFloat("_Blend", 0f);
            shade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            shade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            shade.SetFloat("_ZWrite", 0f);
            shade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shade.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// Old Nergui's river (RiverScene, 28 Sep 2026): the painted horizon (Unlit, clamped), the water (Orsuun/Water,
        /// reflecting the horizon), wood, ground and stone (URP Lit over repeating textures) and the reed cards (Lit, alpha
        /// cut, both sides), all in Content/River beside their textures.
        /// </summary>
        private static void EnsureRiver()
        {
            const string dir = Con + "River/";
            if (!Directory.Exists(dir)) return;
            Texture2D Tex(string file, TextureWrapMode wrap, int max, bool alpha = false)
            {
                string path = dir + file;
                if (AssetImporter.GetAtPath(path) is TextureImporter ti
                    && (ti.wrapMode != wrap || ti.maxTextureSize != max || ti.anisoLevel != 8 || !ti.mipmapEnabled || ti.alphaIsTransparency != alpha))
                {
                    ti.wrapMode = wrap;
                    ti.maxTextureSize = max;
                    ti.anisoLevel = 8;
                    ti.mipmapEnabled = true;
                    ti.alphaIsTransparency = alpha;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Material Mat(string name, Shader shader)
            {
                string path = dir + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                return mat;
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit"), unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Texture2D horizon = Tex("Horizon.jpg", TextureWrapMode.Clamp, 2048);
            Material sky = Mat("Horizon", unlit);
            sky.SetTexture("_BaseMap", horizon);
            sky.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(sky);

            Shader waterShader = Shader.Find("Orsuun/Water");
            if (waterShader != null)
            {
                Material water = Mat("Water", waterShader);
                water.SetTexture("_SkyMap", horizon);
                EditorUtility.SetDirty(water);
            }
            else Debug.LogWarning("Orsuun/Water shader missing");

            foreach ((string name, float smooth) in new[] { ("Wood", 0.12f), ("Ground", 0.05f), ("Stone", 0.18f) })
            {
                Material m = Mat(name, lit);
                m.SetTexture("_BaseMap", Tex(name + ".jpg", TextureWrapMode.Repeat, 1024));
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", smooth);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
            }

            Material reeds = Mat("Reeds", lit);
            reeds.SetTexture("_BaseMap", Tex("Reeds.png", TextureWrapMode.Clamp, 1024, alpha: true));
            reeds.SetColor("_BaseColor", Color.white);
            reeds.SetFloat("_Smoothness", 0.05f);
            reeds.SetFloat("_AlphaClip", 1f);
            reeds.SetFloat("_Cutoff", 0.45f);
            reeds.SetFloat("_Cull", 0f);
            reeds.EnableKeyword("_ALPHATEST_ON");
            reeds.renderQueue = (int)RenderQueue.AlphaTest;
            reeds.doubleSidedGI = true;
            reeds.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(reeds);
        }

        /// <summary>
        /// The lane floors (Resources/Floors): repeating, mipmapped and anisotropic, since the camera sees them at a low
        /// angle, at most 1024 px.
        /// </summary>
        private static void EnsureFloors()
        {
            string dir = Con + "Floors/";
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.GetFiles(dir))
            {
                if (file.EndsWith(".meta")) continue;
                if (!(AssetImporter.GetAtPath(file) is TextureImporter ti)) continue;
                if (ti.wrapMode == TextureWrapMode.Repeat && ti.anisoLevel == 8 && ti.maxTextureSize == 1024 && ti.mipmapEnabled) continue;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
        }

        /// <summary>
        /// Every backdrop with each candidate floor (Resources/Floors/&lt;key&gt;_A and _B) to artifacts/floor-&lt;key&gt;-&lt;v&gt;.png,
        /// for choosing. Unity -batchmode -executeMethod Orsuun.Client.EditorTools.RenderingSetup.RenderFloors
        /// </summary>
        public static void RenderFloors()
        {
            ShaderUtil.allowAsyncCompilation = false;
            Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("FloorPreview");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.transform.SetParent(root.transform);
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Res + "PostFX.asset");
            var cam = new GameObject("LaneCamera").AddComponent<Camera>();
            cam.transform.SetParent(root.transform);
            cam.fieldOfView = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            cam.transform.position = new Vector3(1.5f, 5.4f, -19.5f);
            cam.transform.LookAt(new Vector3(1.5f, 1.9f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var rig = new GameObject("Lane"); rig.transform.SetParent(root.transform);
            var view = rig.AddComponent<Orsuun.Client.LaneView>();
            view.BuildScenery();
            view.BuildHero();
            view.SetLooks("Armor_T3", "Weapon_T3");
            var places = new (string key, Orsuun.Rules.Combat.ZoneType zone, int stage)[]
            {
                ("HuntingGround", Orsuun.Rules.Combat.ZoneType.Campaign, 5), ("KorstoneField", Orsuun.Rules.Combat.ZoneType.KorstoneField, Orsuun.Rules.Content.KorstoneFieldI),
                ("CommanderGround", Orsuun.Rules.Combat.ZoneType.Campaign, 15), ("SaltFlats", Orsuun.Rules.Combat.ZoneType.HuntingGround, Orsuun.Rules.Content.SaltFlats),
                ("FrostPasture", Orsuun.Rules.Combat.ZoneType.HuntingGround, Orsuun.Rules.Content.FrostPasture), ("HollowSpire", Orsuun.Rules.Combat.ZoneType.Campaign, 311),
                ("CinderMarches", Orsuun.Rules.Combat.ZoneType.Campaign, 45), ("Whisperwood", Orsuun.Rules.Combat.ZoneType.Campaign, 55),
                ("SilkWarren", Orsuun.Rules.Combat.ZoneType.Campaign, 321), ("CarversArchive", Orsuun.Rules.Combat.ZoneType.Campaign, 331),
                ("Bloodbirch", Orsuun.Rules.Combat.ZoneType.Campaign, 65), ("DrownedSteppe", Orsuun.Rules.Combat.ZoneType.Campaign, 75),
                ("ColossusGraves", Orsuun.Rules.Combat.ZoneType.Campaign, 85), ("SunkenBazaar", Orsuun.Rules.Combat.ZoneType.Campaign, 95),
                ("ThousandMarkers", Orsuun.Rules.Combat.ZoneType.Campaign, 105), ("HollowThrone", Orsuun.Rules.Combat.ZoneType.Campaign, 115),
            };
            Directory.CreateDirectory("../artifacts");
            // ORSUUN_FLOOR_KEYS limits the maps ("HuntingGround,SaltFlats"), ORSUUN_FLOOR_VARIANTS the candidates ("_C,_D").
            string keys = System.Environment.GetEnvironmentVariable("ORSUUN_FLOOR_KEYS");
            string variants = System.Environment.GetEnvironmentVariable("ORSUUN_FLOOR_VARIANTS");
            // "chosen" is each map's own floor (Resources/Floors/<key>).
            string[] vs = string.IsNullOrEmpty(variants) ? new[] { "", "_A", "_B" }
                : System.Array.ConvertAll(variants.Split(','), v => v == "chosen" ? "" : v);
            foreach (var (key, zone, stage) in places)
                foreach (string v in vs)
                {
                    if (!string.IsNullOrEmpty(keys) && System.Array.IndexOf(keys.Split(','), key) < 0) continue;
                    Orsuun.Client.LaneView.FloorVariant = v;
                    view.SetZone(zone, stage);
                    view.RefreshFloor();
                    Capture(cam, "../artifacts/floor-" + key + (v.Length == 0 ? (variants == "chosen" ? "-chosen" : "-none") : "-" + v.Substring(1)) + ".png", 1080, 1056);
                }
            Orsuun.Client.LaneView.FloorVariant = "";
        }

        private static void EnsurePostFx()
        {
            string path = Res + "PostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureKorstoneImport()
        {
            EnsureModelImport(Con + "Models/Korstone.fbx");
            // Class models for later (Kestrel, Wraithsworn, Drumcaller) live in Assets/Orsuun/Models/Classes, outside the build.
            EnsureModelImport("Assets/Orsuun/Models/Classes/Kestrel.fbx");
            EnsureModelImport("Assets/Orsuun/Models/Classes/Wraithsworn.fbx");
            EnsureModelImport("Assets/Orsuun/Models/Classes/Drumcaller.fbx");
        }

        /// <summary>Clip names from art/blender/rig.py and how each one wraps.</summary>
        private static readonly (string name, WrapMode wrap)[] HeroClips =
        {
            ("Idle", WrapMode.Loop), ("Run", WrapMode.Loop), ("Attack", WrapMode.Once), ("Hit", WrapMode.Once), ("Death", WrapMode.ClampForever),
        };

        /// <summary>
        /// Rigged armour looks: legacy clips (LaneView plays them by name on the Animation component), one per Blender
        /// action. The FBX takes are called "Armature|Idle" and so on; they are renamed to the bare action name.
        /// </summary>
        private static void EnsureAnimatedImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.animationType != ModelImporterAnimationType.Legacy || !importer.importAnimation
                || importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                // The takes are only listed once the file has been imported with animation on.
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            var clips = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation take in importer.defaultClipAnimations)
            {
                string bare = take.takeName.Substring(take.takeName.LastIndexOf('|') + 1);
                foreach ((string name, WrapMode wrap) in HeroClips)
                {
                    if (bare != name) continue;
                    take.name = name;
                    take.wrapMode = wrap;
                    take.loopTime = wrap == WrapMode.Loop;
                    clips.Add(take);
                }
            }
            if (clips.Count == 0)
            {
                var takes = new System.Collections.Generic.List<string>();
                foreach (ModelImporterClipAnimation take in importer.defaultClipAnimations) takes.Add(take.takeName);
                Debug.LogWarning("No hero clips found in " + path + " (takes: " + string.Join(", ", takes) + ")");
                return;
            }
            bool same = importer.clipAnimations.Length == clips.Count;
            for (int i = 0; same && i < clips.Count; i++)
                same = importer.clipAnimations[i].name == clips[i].name && importer.clipAnimations[i].wrapMode == clips[i].wrapMode;
            if (same && importer.animationType == ModelImporterAnimationType.Legacy && importer.importAnimation
                && importer.materialImportMode == ModelImporterMaterialImportMode.None) return;
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static void EnsureReadable(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        /// <summary>A still model (the weapon looks): no materials, no clips, and readable, since GearSparkle's particles are
        /// emitted from its surface (an unreadable mesh sends them all to its origin).</summary>
        private static void EnsureModelImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || !importer.isReadable;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.isReadable = true;
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>
        /// References for the item icons (owner, 26 Sep 2026: "we need different images for all levels different items"):
        /// every class's look for every band standing whole, and the Vanguard's glaive of each band alone, in
        /// artifacts/iconref/. Unity -batchmode -executeMethod Orsuun.Client.EditorTools.RenderingSetup.RenderIconRefs
        /// </summary>
        public static void RenderIconRefs()
        {
            ShaderUtil.allowAsyncCompilation = false;
            Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;
            var cam = new GameObject("IconRefCamera").AddComponent<Camera>();
            cam.fieldOfView = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.41f, 0.39f);
            var rig = new GameObject("Lane");
            var view = rig.AddComponent<Orsuun.Client.LaneView>();
            view.BuildHero();
            // Frames whatever stands on the rig, tall side filling nine tenths of the shot.
            void Frame(GameObject root, float aspect)
            {
                bool any = false;
                Bounds b = default;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any) return;
                float half = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float dist = Mathf.Max(b.extents.y / half, b.extents.x / (half * aspect)) / 0.9f + b.extents.z;
                cam.transform.position = b.center + new Vector3(0f, 0f, -dist);
                cam.transform.LookAt(b.center);
            }
            Directory.CreateDirectory("../artifacts/iconref");
            float[] none = new float[8];
            for (int band = 0; band <= Orsuun.Rules.ItemLooks.MaxTier; band++)
            {
                view.SetHeroClass(Orsuun.Rules.Combat.HeroClass.Vanguard);
                view.SetLooks("Armor_T" + band, "Weapon_T" + band);
                view.SetGear(none);
                Frame(rig, 0.7f);
                CaptureSkinned(cam, "../artifacts/iconref/Vanguard-T" + band + ".png", rig.transform);
                foreach (var cls in new[] { Orsuun.Rules.Combat.HeroClass.Kestrel, Orsuun.Rules.Combat.HeroClass.Wraithsworn, Orsuun.Rules.Combat.HeroClass.Drumcaller })
                {
                    view.SetHeroClass(cls, band);
                    view.PoseHero("Idle", 0f);
                    Frame(rig, 0.7f);
                    CaptureSkinned(cam, "../artifacts/iconref/" + cls + "-T" + band + ".png", rig.transform);
                }
            }
            // The second looks (owner, 26 Sep 2026: a man and a woman of every class), band by band.
            Directory.CreateDirectory("../artifacts/iconref/alt");
            for (int band = 0; band <= Orsuun.Rules.ItemLooks.MaxTier; band++)
                foreach (var cls in new[] { Orsuun.Rules.Combat.HeroClass.Vanguard, Orsuun.Rules.Combat.HeroClass.Kestrel, Orsuun.Rules.Combat.HeroClass.Wraithsworn, Orsuun.Rules.Combat.HeroClass.Drumcaller })
                {
                    view.SetHeroClass(cls, band, null, secondLook: true);
                    if (cls == Orsuun.Rules.Combat.HeroClass.Vanguard)
                    {
                        view.SetLooks("Armor_T" + band, "Weapon_T" + band);
                        view.SetGear(none);
                    }
                    else view.PoseHero("Idle", 0f);
                    Frame(rig, 0.7f);
                    CaptureSkinned(cam, "../artifacts/iconref/alt/" + cls + "-T" + band + ".png", rig.transform);
                }
            view.SetHeroClass(Orsuun.Rules.Combat.HeroClass.Vanguard);

            // Each wardrobe skin's own models, one per class, to check a new costume (26 Sep 2026).
            Directory.CreateDirectory("../artifacts/iconref/skins");
            foreach (Orsuun.Rules.WardrobeDef def in Orsuun.Rules.Wardrobe.All)
            {
                if (def.Kind != Orsuun.Rules.WardrobeKind.Skin) continue;
                foreach (var cls in new[] { Orsuun.Rules.Combat.HeroClass.Vanguard, Orsuun.Rules.Combat.HeroClass.Kestrel, Orsuun.Rules.Combat.HeroClass.Wraithsworn, Orsuun.Rules.Combat.HeroClass.Drumcaller })
                {
                    foreach (bool second in new[] { false, true })
                    {
                        string model = Orsuun.Client.LaneView.SkinModel(cls, def.Look, second);
                        if (model == null) continue;
                        if (cls == Orsuun.Rules.Combat.HeroClass.Vanguard)
                        {
                            view.SetHeroClass(cls, 0, null, second);
                            view.SetLooks(model, "Weapon_T5");
                            view.SetGear(none);
                        }
                        else
                        {
                            view.SetHeroClass(cls, 5, model, second);
                            view.PoseHero("Idle", 0f);
                        }
                        Frame(rig, 0.7f);
                        CaptureSkinned(cam, "../artifacts/iconref/skins/" + cls + "-" + def.Look + (second ? "-alt" : "") + ".png", rig.transform);
                    }
                }
            }

            // And each mount under the Vanguard (standing where the lane's Update would lift him into the saddle).
            view.SetHeroClass(Orsuun.Rules.Combat.HeroClass.Vanguard);
            view.SetLooks("Armor_T5", "Weapon_T5");
            foreach (Orsuun.Rules.WardrobeDef def in Orsuun.Rules.Wardrobe.All)
            {
                if (def.Kind != Orsuun.Rules.WardrobeKind.Mount) continue;
                view.SetWardrobe(def.Look, null, Color.white);
                Frame(rig, 1.2f);
                CaptureSkinned(cam, "../artifacts/iconref/skins/Mount-" + def.Look + ".png", rig.transform);
            }
            view.SetWardrobe(null, null, Color.white);

            // The glaives alone, lying across the frame.
            view.SetHeroClass(Orsuun.Rules.Combat.HeroClass.Vanguard);
            rig.SetActive(false);
            for (int band = 0; band <= Orsuun.Rules.ItemLooks.MaxTier; band++)
            {
                var prefab = Orsuun.Client.Art.Load<GameObject>("Models/Looks/Weapon_T" + band);
                if (prefab == null) continue;
                var glaive = (GameObject)Object.Instantiate(prefab);
                var mat = Orsuun.Client.Art.Load<Material>("Looks/Weapon_T" + band);
                if (mat != null) foreach (Renderer r in glaive.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                glaive.transform.position = new Vector3(100f, 0f, 0f);
                // Lying along x, turned about its length so the flat of the blade faces the camera (thinnest in z).
                float bestTurn = 0f, thinnest = float.MaxValue;
                for (float turn = 0f; turn < 180f; turn += 15f)
                {
                    glaive.transform.rotation = Quaternion.Euler(turn, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
                    Bounds tb = default; bool f = true;
                    foreach (Renderer r in glaive.GetComponentsInChildren<Renderer>()) { if (f) { tb = r.bounds; f = false; } else tb.Encapsulate(r.bounds); }
                    if (tb.size.z < thinnest) { thinnest = tb.size.z; bestTurn = turn; }
                }
                glaive.transform.rotation = Quaternion.Euler(bestTurn, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
                Frame(glaive, 2.8f);
                Capture(cam, "../artifacts/iconref/Glaive-T" + band + ".png", 1400, 500);
                Object.DestroyImmediate(glaive);
            }
        }

        /// <summary>Renders the four glow steps and the Korstone to artifacts/glow-preview.png, no Play mode needed.</summary>
        public static void RenderPreview()
        {
            // The editor compiles shader variants asynchronously and skips draws until they are ready;
            // a one-shot headless render must wait for them instead.
            ShaderUtil.allowAsyncCompilation = false;
            Ensure();
            RenderLanePreview();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.25f, 1f);
            ground.transform.localScale = new Vector3(30f, 0.5f, 8f);
            var groundMat = new Material(AssetDatabase.LoadAssetAtPath<Material>(Res + "GreyBox.mat")) { color = new Color(0.30f, 0.34f, 0.26f) };
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            float[] glows = { 0f, 0.35f, 0.65f, 1f };
            for (int i = 0; i < glows.Length; i++)
            {
                var hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hero.transform.position = new Vector3(-5.2f + i * 2.2f, 1f, 0f);
                var body = new Material(AssetDatabase.LoadAssetAtPath<Material>(Res + "EmberGear.mat"));
                body.SetFloat("_Glow", glows[i]);
                hero.GetComponent<Renderer>().sharedMaterial = body;

                var glaive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glaive.transform.SetParent(hero.transform, false);
                glaive.transform.localPosition = new Vector3(0.62f, 0.35f, -0.25f);
                glaive.transform.localScale = new Vector3(0.07f, 2.1f, 0.07f);
                glaive.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                var blade = new Material(AssetDatabase.LoadAssetAtPath<Material>(Res + "EmberWeapon.mat"));
                blade.SetFloat("_Glow", glows[i]);
                glaive.GetComponent<Renderer>().sharedMaterial = blade;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Con + "Models/Korstone.fbx");
            if (model != null)
            {
                var kor = (GameObject)Object.Instantiate(model);
                kor.transform.position = new Vector3(4.6f, 0f, 1.2f);
                kor.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Res + "KorstoneEmber.mat");
                foreach (Renderer r in kor.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                Bounds b = new Bounds(kor.transform.position, Vector3.zero);
                foreach (Renderer r in kor.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                Debug.Log("Korstone bounds size " + b.size + " min y " + b.min.y);
                foreach (MeshFilter mf in kor.GetComponentsInChildren<MeshFilter>())
                    Debug.Log("Korstone part " + mf.name + " local bounds " + mf.sharedMesh.bounds.size + " lossyScale " + mf.transform.lossyScale + " rot " + mf.transform.localEulerAngles);
            }
            else Debug.LogWarning("Korstone model missing");

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Res + "PostFX.asset");

            var cam = new GameObject("PreviewCamera").AddComponent<Camera>();
            cam.fieldOfView = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            cam.transform.position = new Vector3(0f, 3.4f, -19f);
            cam.transform.LookAt(new Vector3(0f, 1.2f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            Directory.CreateDirectory("../artifacts");
            string debugModes = System.Environment.GetEnvironmentVariable("ORSUUN_PREVIEW_DEBUG");
            if (string.IsNullOrEmpty(debugModes)) Capture(cam, "../artifacts/glow-preview.png");
            else
                foreach (string mode in debugModes.Split(','))
                {
                    foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Debug")) r.sharedMaterial.SetFloat("_Debug", float.Parse(mode));
                    Capture(cam, "../artifacts/glow-debug-" + mode + ".png");
                }
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Debug")) r.sharedMaterial.SetFloat("_Debug", 0f);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The lane as the phone frames it (same camera as GameRoot), one shot per zone backdrop.</summary>
        private static void RenderLanePreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("LanePreview");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.transform.SetParent(root.transform);
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Res + "PostFX.asset");
            var cam = new GameObject("LaneCamera").AddComponent<Camera>();
            cam.transform.SetParent(root.transform);
            cam.fieldOfView = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            cam.transform.position = new Vector3(1.5f, 5.4f, -19.5f);
            cam.transform.LookAt(new Vector3(1.5f, 1.9f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var rig = new GameObject("Lane"); rig.transform.SetParent(root.transform);
            var view = rig.AddComponent<Orsuun.Client.LaneView>();
            view.BuildScenery();
            view.BuildHero();
            foreach (string zone in new[] { "HuntingGround", "KorstoneField", "CommanderGround" })
            {
                view.SetZone((Orsuun.Rules.Combat.ZoneType)System.Enum.Parse(typeof(Orsuun.Rules.Combat.ZoneType), zone));
                Capture(cam, "../artifacts/lane-" + zone + ".png", 1080, 1056);
            }
            view.SetZone(Orsuun.Rules.Combat.ZoneType.HuntingGround, Orsuun.Rules.Content.SaltFlats);
            Capture(cam, "../artifacts/lane-SaltFlats.png", 1080, 1056);
            view.SetZone(Orsuun.Rules.Combat.ZoneType.HuntingGround, Orsuun.Rules.Content.FrostPasture);
            Capture(cam, "../artifacts/lane-FrostPasture.png", 1080, 1056);
            view.SetZone(Orsuun.Rules.Combat.ZoneType.HuntingGround, 1);
            view.SetGear(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f });
            Capture(cam, "../artifacts/lane-glow9.png", 1080, 1056);
            // Each piece at its own level: weapon +9, armour +7; the stat-only slots at +9 must not glow.
            view.SetGear(new[] { 1f, 0.35f, 1f, 1f, 1f, 1f, 1f, 1f });
            Capture(cam, "../artifacts/lane-glow-mixed.png", 1080, 1056);

            // Close-ups of the hero for judging the upgrade glow: plain, each piece at its own level, full +9.
            cam.transform.position = new Vector3(-0.2f, 1.6f, -5.2f);
            cam.transform.LookAt(new Vector3(-1.6f, 1.05f, 0f));
            float[] none = new float[8];
            float[] nine = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            var steps = new (string name, string armor, string weapon, float[] glow)[]
            {
                ("T0", "Armor_T0", "Weapon_T0", none),
                ("T1", "Armor_T1", "Weapon_T1", none),
                ("T2", "Armor_T2", "Weapon_T2", none),
                ("mixed", "Armor_T2", "Weapon_T0", none),
                ("T2-plus9", "Armor_T2", "Weapon_T2", nine),
                ("T0-weapon9", "Armor_T0", "Weapon_T2", new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }),
            };
            foreach (var (name, armor, weapon, glow) in steps)
            {
                view.SetLooks(armor, weapon);
                view.SetGear(glow);
                Capture(cam, "../artifacts/hero-" + name + ".png", 700, 1000);
            }
            // The later bands, 30 to 105: armour and weapon of the same band.
            for (int t = 3; t <= Orsuun.Rules.ItemLooks.MaxTier; t++)
            {
                view.SetLooks("Armor_T" + t, "Weapon_T" + t);
                view.SetGear(none);
                Capture(cam, "../artifacts/hero-T" + t + ".png", 700, 1000);
            }

            // Animation poses on the rigged T1 look (legacy clips sampled directly; the editor runs no Animation update).
            view.SetLooks("Armor_T1", "Weapon_T1");
            view.SetGear(none);
            foreach ((string clip, float at) in new[] { ("Attack", 0.30f), ("Attack", 0.57f), ("Run", 0.0f), ("Run", 0.5f), ("Hit", 0.33f), ("Death", 1f) })
            {
                if (!view.PoseHero(clip, at)) { Debug.LogWarning("No clip " + clip + " on the hero look"); break; }
                CaptureSkinned(cam, "../artifacts/pose-" + clip + "-" + (int)(at * 100) + ".png", view.transform);
            }
            view.PoseHero("Idle", 0f);

            // The other classes, each armour band drawn so far: idle, wind-up and strike, and a run stride on band 0.
            foreach (var cls in new[] { Orsuun.Rules.Combat.HeroClass.Kestrel, Orsuun.Rules.Combat.HeroClass.Wraithsworn, Orsuun.Rules.Combat.HeroClass.Drumcaller })
                for (int band = 0; band <= Orsuun.Rules.ItemLooks.MaxTier; band++)
                {
                    view.SetHeroClass(cls, band);
                    foreach ((string clip, float at) in new[] { ("Idle", 0f), ("Attack", 0.4f), ("Attack", 0.57f), ("Run", 0.25f) })
                    {
                        if (clip == "Run" && band > 0) continue;
                        if (!view.PoseHero(clip, at)) { Debug.LogWarning("No clip " + clip + " on " + cls); break; }
                        CaptureSkinned(cam, "../artifacts/" + cls.ToString().ToLowerInvariant() + "-T" + band + "-" + clip + (clip == "Attack" ? Mathf.RoundToInt(at * 100).ToString() : "") + ".png", view.transform);
                    }
                    view.PoseHero("Idle", 0f);
                }
            view.SetHeroClass(Orsuun.Rules.Combat.HeroClass.Vanguard);
            view.SetLooks("Armor_T1", "Weapon_T1");

            // Korstones by level: the five tiers side by side, then the Elder of each shape.
            {
                cam.transform.position = new Vector3(4.5f, 3.2f, -17f);
                cam.transform.LookAt(new Vector3(4.5f, 1.6f, 2f));
                var stones = new System.Collections.Generic.List<Transform>();
                int[] levels = { 10, 30, 50, 70, 95 };
                for (int k = 0; k < levels.Length; k++)
                {
                    Transform t = view.PreviewKorstone(levels[k], false, new Vector3(-1.5f + k * 3f, 0f, 2f));
                    if (t != null) stones.Add(t);
                }
                Capture(cam, "../artifacts/korstone-tiers.png", 1600, 700);
                foreach (Transform t in stones) Object.DestroyImmediate(t.gameObject);
                stones.Clear();
                int[] elders = { 10, 50, 95 };
                for (int k = 0; k < elders.Length; k++)
                {
                    Transform t = view.PreviewKorstone(elders[k], true, new Vector3(0.5f + k * 4f, 0f, 3f));
                    if (t != null) stones.Add(t);
                }
                Capture(cam, "../artifacts/korstone-elders.png", 1600, 700);
                foreach (Transform t in stones) Object.DestroyImmediate(t.gameObject);
            }

            // Enemies from a live lane: a stage-1 pack, then each Commander (the boss stage has no packs).
            cam.transform.position = new Vector3(1.5f, 5.4f, -19.5f);
            cam.transform.LookAt(new Vector3(1.5f, 1.9f, 0f));
            view.SetLooks("Armor_T1", "Weapon_T1");
            var heroStats = Orsuun.Rules.HeroFactory.FromWeapon(new Orsuun.Rules.ItemState(10, Orsuun.Rules.Rarity.Rare));
            var skills = Orsuun.Rules.Combat.SkillDef.VanguardWrath();
            var pack = Orsuun.Rules.Combat.ActivePlay.NewLoop(Orsuun.Rules.Content.Stage(1), heroStats, skills, new Orsuun.Rules.Inventory { Potions = 5 }, 7UL, 0);
            for (int i = 0; i < 4000 && !(pack.Phase == Orsuun.Rules.Combat.LanePhase.Fighting && pack.Enemies.Count >= 6); i++) { pack.Tick(); pack.DrainEvents(); }
            view.Bind(pack);
            view.PlaceEnemies(1f);
            Capture(cam, "../artifacts/lane-mobs.png", 1080, 1056);
            // The Salt Flats and Frost Pasture packs on their own grounds.
            foreach ((int zone, string label) in new[] { (Orsuun.Rules.Content.SaltFlats, "SaltFlats"), (Orsuun.Rules.Content.FrostPasture, "FrostPasture") })
            {
                var hunt = Orsuun.Rules.Combat.ActivePlay.NewLoop(Orsuun.Rules.Content.Stage(zone), heroStats, skills, new Orsuun.Rules.Inventory { Potions = 5 }, 7UL, 0);
                for (int i = 0; i < 4000 && !(hunt.Phase == Orsuun.Rules.Combat.LanePhase.Fighting && hunt.Enemies.Count >= 6); i++) { hunt.Tick(); hunt.DrainEvents(); }
                view.Bind(hunt);
                view.PlaceEnemies(1f);
                Capture(cam, "../artifacts/lane-mobs-" + label + ".png", 1080, 1056);
            }
            foreach (Orsuun.Rules.BossDef boss in Orsuun.Rules.Content.Bosses)
            {
                var fight = Orsuun.Rules.Combat.ActivePlay.NewLoop(Orsuun.Rules.Content.BossStage(boss), heroStats, skills, new Orsuun.Rules.Inventory { Potions = 5 }, 7UL, 0);
                for (int i = 0; i < 4000 && fight.Phase != Orsuun.Rules.Combat.LanePhase.Fighting; i++) { fight.Tick(); fight.DrainEvents(); }
                view.Bind(fight);
                view.PlaceEnemies(1f);
                Capture(cam, "../artifacts/lane-boss-" + boss.Id + ".png", 1080, 1056);
            }
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Skinned meshes deform in the player loop, which a one-shot editor render never runs: bake each one at the
        /// sampled pose into a stand-in mesh for the capture, then put the skinned renderers back.
        /// </summary>
        private static void CaptureSkinned(Camera cam, string path, Transform root)
        {
            var standIns = new System.Collections.Generic.List<GameObject>();
            var hidden = new System.Collections.Generic.List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var go = new GameObject(smr.name + "Posed");
                go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = baked;
                go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                smr.enabled = false;
                hidden.Add(smr);
                standIns.Add(go);
            }
            Capture(cam, path, 700, 1000);
            foreach (GameObject go in standIns) Object.DestroyImmediate(go);
            foreach (SkinnedMeshRenderer smr in hidden) smr.enabled = true;
        }

        private static void Capture(Camera cam, string path, int width = 1600, int height = 800)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            bool viaRequest = RenderPipeline.SupportsRenderRequest(cam, request);
            if (viaRequest) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("Preview written to " + path + (viaRequest ? " (render request)" : " (Camera.Render)"));
        }
    }
}
