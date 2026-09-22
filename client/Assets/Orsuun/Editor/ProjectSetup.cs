using System.IO;
using UnityEditor;
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
        private const string MaterialPath = "Assets/Orsuun/Resources/GreyBox.mat";

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

            // Runtime-only primitives reference no material asset, so the build would strip their shader
            // and draw them magenta. A material in Resources keeps the shader in the player.
            if (!File.Exists(MaterialPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), MaterialPath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Orsuun project setup complete.");
        }

        /// <summary>Windows playtest build: -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildWindows</summary>
        public static void BuildWindows()
        {
            Run();
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
