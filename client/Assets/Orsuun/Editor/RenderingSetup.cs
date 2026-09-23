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
            Material vanguard = EnsureGlowMaterial("VanguardEmber", Color.white, crackScale: 5f, crackWidth: 0.025f, intensity: 1.15f, rim: 2.5f);
            vanguard.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Models/VanguardBaseColor.png"));
            EditorUtility.SetDirty(vanguard);
            EnsurePostFx();
            EnsureBackdrops();
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
            string path = Res + name + ".mat";
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

        /// <summary>Unlit materials for the zone environment keys behind the lane (docs/concept/env-*.jpg, variant 1 of each).</summary>
        private static void EnsureBackdrops()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (string zone in new[] { "HuntingGround", "KorstoneField", "CommanderGround" })
            {
                string texPath = Res + "Backdrops/" + zone + ".jpg";
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (ti.wrapMode != TextureWrapMode.Clamp || ti.maxTextureSize != 2048))
                {
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) { Debug.LogWarning("Backdrop texture missing: " + texPath); continue; }
                string matPath = Res + "Backdrops/Backdrop" + zone + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(unlit); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = unlit;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(mat);
            }
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
            EnsureModelImport(Res + "Models/Korstone.fbx");
            EnsureModelImport(Res + "Models/Vanguard.fbx");
            EnsureModelImport(Res + "Models/VanguardModular.fbx");
            EnsureModelImport(Res + "Models/Kestrel.fbx");
            EnsureModelImport(Res + "Models/Wraithsworn.fbx");
            EnsureModelImport(Res + "Models/Drumcaller.fbx");
        }

        private static void EnsureModelImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            if (changed) importer.SaveAndReimport();
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

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Res + "Models/Korstone.fbx");
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
            cam.transform.position = new Vector3(1.5f, 4.6f, -19.5f);
            cam.transform.LookAt(new Vector3(1.5f, 1.1f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var rig = new GameObject("Lane"); rig.transform.SetParent(root.transform);
            var view = rig.AddComponent<Orsuun.Client.LaneView>();
            view.BuildScenery();
            view.BuildHero();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Res + "Models/Korstone.fbx");
            if (model != null)
            {
                var kor = (GameObject)Object.Instantiate(model, root.transform);
                kor.transform.position = new Vector3(3.4f, 0f, 2.2f);
                kor.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                var km = AssetDatabase.LoadAssetAtPath<Material>(Res + "KorstoneEmber.mat");
                foreach (Renderer r in kor.GetComponentsInChildren<Renderer>()) r.sharedMaterial = km;
            }
            foreach (string zone in new[] { "HuntingGround", "KorstoneField", "CommanderGround" })
            {
                view.SetZone((Orsuun.Rules.Combat.ZoneType)System.Enum.Parse(typeof(Orsuun.Rules.Combat.ZoneType), zone));
                Capture(cam, "../artifacts/lane-" + zone + ".png", 1080, 1056);
            }
            view.SetGear(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f });
            Capture(cam, "../artifacts/lane-glow9.png", 1080, 1056);
            // Each piece at its own level: weapon +9, armour +7; the stat-only slots at +9 must not glow.
            view.SetGear(new[] { 1f, 0.35f, 1f, 1f, 1f, 1f, 1f, 1f });
            Capture(cam, "../artifacts/lane-glow-mixed.png", 1080, 1056);

            // Close-ups of the hero for judging the upgrade glow: plain, each piece at its own level, full +9.
            cam.transform.position = new Vector3(-0.2f, 1.6f, -5.2f);
            cam.transform.LookAt(new Vector3(-1.6f, 1.05f, 0f));
            var steps = new (string name, float[] glow)[]
            {
                ("plain", new float[8]),
                ("mixed", new[] { 1f, 0f, 1f, 1f, 1f, 1f, 1f, 1f }),
                ("plus7", new[] { 0.35f, 0.35f, 0.35f, 0.35f, 0.35f, 0.35f, 0.35f, 0.35f }),
                ("plus9", new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }),
            };
            foreach (var (name, glow) in steps)
            {
                view.SetGear(glow);
                Capture(cam, "../artifacts/hero-" + name + ".png", 700, 1000);
            }
            Object.DestroyImmediate(root);
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
