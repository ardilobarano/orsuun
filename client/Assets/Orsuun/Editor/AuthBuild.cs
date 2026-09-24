using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// Sign in with Google / Apple wiring the builds need: iOS links AuthenticationServices (the in-app sign-in sheet)
    /// and registers the orsuun:// scheme; Android gets an intent filter so an orsuun://auth link opens the game.
    /// </summary>
    public static class AuthBuild
    {
        public const string Scheme = "orsuun";

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
#if UNITY_IOS
            if (target != BuildTarget.iOS) return;
            string projectPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(path);
            var project = new UnityEditor.iOS.Xcode.PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", false);
            project.WriteToFile(projectPath);
#endif
        }

        /// <summary>Adds VIEW / BROWSABLE orsuun://auth to the launcher activity of a Unity manifest; true if it changed.</summary>
        public static bool AddDeepLink(string manifestPath)
        {
            const string android = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            bool changed = false;
            foreach (XmlElement activity in doc.GetElementsByTagName("activity"))
            {
                bool launcher = false, linked = false;
                foreach (XmlNode filter in activity.ChildNodes)
                {
                    if (filter.Name != "intent-filter") continue;
                    foreach (XmlNode child in filter.ChildNodes)
                    {
                        if (child is not XmlElement e) continue;
                        string name = e.GetAttribute("name", android);
                        if (e.Name == "category" && name == "android.intent.category.LAUNCHER") launcher = true;
                        if (e.Name == "data" && e.GetAttribute("scheme", android) == Scheme) linked = true;
                    }
                }
                if (!launcher || linked) continue;
                XmlElement link = doc.CreateElement("intent-filter");
                XmlElement Child(string tag, string attribute, string value)
                {
                    XmlElement c = doc.CreateElement(tag);
                    c.SetAttribute(attribute, android, value);
                    link.AppendChild(c);
                    return c;
                }
                Child("action", "name", "android.intent.action.VIEW");
                Child("category", "name", "android.intent.category.DEFAULT");
                Child("category", "name", "android.intent.category.BROWSABLE");
                XmlElement data = Child("data", "scheme", Scheme);
                data.SetAttribute("host", android, "auth");
                activity.AppendChild(link);
                changed = true;
            }
            if (changed) doc.Save(manifestPath);
            return changed;
        }
    }

#if UNITY_ANDROID
    /// <summary>After Unity writes the Gradle project: the orsuun://auth deep link on the launcher activity.</summary>
    public sealed class AndroidDeepLink : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifest)) AuthBuild.AddDeepLink(manifest);
        }
    }
#endif
}
