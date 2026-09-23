using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// One-time project setup, runnable headless:
    /// Unity.exe -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Orsuun/Scenes/Main.unity";
        /// <summary>The app icon: the blood-moon Korstone (24 Sep 2026; alternatives in docs/concept/app-icon-*.jpg).</summary>
        private const string IconPath = "Assets/Orsuun/Art/AppIcon.png";

        [MenuItem("Orsuun/Run Project Setup")]
        public static void Run()
        {
            PlayerSettings.companyName = "Orsuun";
            PlayerSettings.productName = "Orsuun: War of Banners";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // Deliberately empty: GameRoot builds the whole grey-box at runtime.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            // URP pipeline, bloom and the Resources materials. Runtime-only primitives reference no material asset,
            // so the build would strip their shaders; the materials in Resources keep them in the player.
            RenderingSetup.Ensure();
            EnsureAppIcon();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Orsuun project setup complete.");
        }

        /// <summary>
        /// One 1024 px opaque square as the default icon; Unity scales it to every iOS and Android size (the App Store
        /// icon must have no alpha). Android gets it as the legacy and round icon; no adaptive layers yet.
        /// </summary>
        private static void EnsureAppIcon()
        {
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer)
            {
                bool changed = importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.maxTextureSize != 1024 || importer.alphaSource != TextureImporterAlphaSource.None;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.alphaSource = TextureImporterAlphaSource.None;
                if (changed) importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning("App icon missing: " + IconPath);
                return;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        /// <summary>
        /// Android playtest build: ORSUUN_SERVER_URL=https://host Unity -batchmode -quit -projectPath client
        /// -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildAndroid. Needs the Android module installed.
        /// </summary>
        public static void BuildAndroid()
        {
            Run();
            WriteServerUrl();

            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.orsuun.warofbanners");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.bundleVersion = "0.1." + System.DateTime.UtcNow.ToString("yyMMdd");
            PlayerSettings.Android.bundleVersionCode = int.Parse(System.DateTime.UtcNow.ToString("yyMMddHH"));
            // Debug-signed APK for sideloading; a release keystore comes with the store build.
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/Orsuun.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log("Build result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// iOS build: exports an Xcode project to Builds/iOS; tools/build-mobile.sh then archives it with xcodebuild.
        /// Needs the iOS module and a Mac. Signing is done in Xcode / by the script, not here.
        /// </summary>
        public static void BuildIos()
        {
            Run();
            WriteServerUrl();

            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.orsuun.warofbanners");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            string team = System.Environment.GetEnvironmentVariable("ORSUUN_APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.bundleVersion = "0.1." + System.DateTime.UtcNow.ToString("yyMMdd");
            PlayerSettings.iOS.buildNumber = System.DateTime.UtcNow.ToString("yyMMddHH");
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/iOS",
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            Debug.Log("Build result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Bakes ORSUUN_SERVER_URL into Resources/server-url.txt so the player knows its server without arguments.</summary>
        private static void WriteServerUrl()
        {
            string url = System.Environment.GetEnvironmentVariable("ORSUUN_SERVER_URL");
            const string path = "Assets/Orsuun/Resources/server-url.txt";
            if (string.IsNullOrWhiteSpace(url))
            {
                if (File.Exists(path)) Debug.Log("Using existing " + path + ": " + File.ReadAllText(path).Trim());
                else Debug.LogWarning("ORSUUN_SERVER_URL not set and no " + path + "; the build will talk to localhost.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, url.Trim());
            AssetDatabase.ImportAsset(path);
            Debug.Log("Server URL baked: " + url);
        }

        /// <summary>Mac player for screenshots and desk tests: -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildMac</summary>
        public static void BuildMac()
        {
            Run();
            WriteServerUrl();
            // Keep ticking when the window is not frontmost, so unattended screenshots do not stall.
            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Mac/Orsuun.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log("Build result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Windows playtest build: -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildWindows</summary>
        public static void BuildWindows()
        {
            Run();
            WriteServerUrl();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/Orsuun.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("Build result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
