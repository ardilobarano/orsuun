using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Orsuun.Client
{
    /// <summary>
    /// The game's heavy art (owner, 26 Sep 2026: "most of the games make user download files when the user first opens the
    /// game, cant we do sth like it"): the 3D models and their materials, the lane backdrops and floors live in
    /// Assets/Orsuun/Content, packed by platform into asset bundles (Editor/ContentBundles) that the app downloads from the
    /// server's /downloads/content/&lt;platform&gt;/ on first launch and keeps (ArtLoader); only changed bundles download again.
    /// Art.Load finds an asset by its old Resources path ("Models/Looks/Armor_T3"): in a loaded bundle, else in Resources
    /// (what the app itself carries); in the editor straight from the project.
    /// </summary>
    public static class Art
    {
        public const string Folder = "Assets/Orsuun/Content/";

        private static readonly Dictionary<string, AssetBundle> Owners = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Object> Loaded = new Dictionary<string, Object>();
#if UNITY_EDITOR
        private static Dictionary<string, string> _project;
#endif

        /// <summary>A loaded bundle and the keys it holds (the manifest's list, as built).</summary>
        public static void Register(AssetBundle bundle, IEnumerable<string> keys)
        {
            foreach (string key in keys) Owners[key] = bundle;
        }

        public static T Load<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;
            string cacheKey = typeof(T).Name + ":" + key;
            if (Loaded.TryGetValue(cacheKey, out Object found)) return found as T;
            T asset = null;
#if UNITY_EDITOR
            if (_project == null)
            {
                _project = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(Folder))
                    foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                    {
                        if (file.EndsWith(".meta", StringComparison.Ordinal)) continue;
                        string path = file.Replace('\\', '/');
                        string rel = path.Substring(Folder.Length);
                        int dot = rel.LastIndexOf('.');
                        _project[dot > 0 ? rel.Substring(0, dot) : rel] = path;
                    }
            }
            if (_project.TryGetValue(key, out string assetPath)) asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
#endif
            if (asset == null && Owners.TryGetValue(key, out AssetBundle bundle)) asset = bundle.LoadAsset<T>(key);
            if (asset == null) asset = Resources.Load<T>(key);
            // Misses are remembered too (the lane asks for backdrops it may not have, every refresh).
            Loaded[cacheKey] = asset;
            return asset;
        }
    }

    /// <summary>
    /// Fetches and opens the art bundles before the game starts (GameRoot.Boot). The Mac screenshot build carries its
    /// bundles in StreamingAssets/Content; phones download theirs into persistentDataPath/content under a progress bar on
    /// the title art, check the server's manifest on every start (a missing server falls back to what is kept), and only
    /// fetch bundles whose hash changed. The editor loads straight from the project.
    /// </summary>
    public sealed class ArtLoader : MonoBehaviour
    {
        [Serializable] public sealed class Entry { public string name; public long size; public string md5; public string[] keys; }
        [Serializable] public sealed class Manifest { public string version; public Entry[] bundles; }

        private const string ManifestFile = "manifest.json";
        private Action _then;
        private GameObject _canvas;
        private Text _status;
        private RectTransform _fill;
        private GameObject _retry;
#pragma warning disable CS0414   // read only in players (the editor loads straight from the project)
        private bool _retryPressed;
#pragma warning restore CS0414

        public static void Begin(Action then)
        {
            var loader = new GameObject("ArtLoader").AddComponent<ArtLoader>();
            loader._then = then;
            loader.StartCoroutine(loader.Run());
        }

        private static string Platform =>
            Application.platform == RuntimePlatform.Android ? "Android"
            : Application.platform == RuntimePlatform.IPhonePlayer ? "iOS"
            : Application.platform == RuntimePlatform.OSXPlayer ? "OSX" : "Windows";

        private IEnumerator Run()
        {
#if UNITY_EDITOR
            Finish();
            yield break;
#else
            // The Mac screenshot build: its bundles ride inside the app.
            string carried = Path.Combine(Application.streamingAssetsPath, "Content");
            Manifest inside = Read(Path.Combine(carried, ManifestFile));
            if (inside != null)
            {
                Open(carried, inside);
                Finish();
                yield break;
            }

            string cache = Path.Combine(Application.persistentDataPath, "content");
            Directory.CreateDirectory(cache);
            Manifest kept = Read(Path.Combine(cache, ManifestFile));
            string baseUrl = Net.ServerLink.ResolveBaseUrl() + "/downloads/content/" + Platform + "/";

            while (true)
            {
                Manifest latest = null;
                using (var request = UnityWebRequest.Get(baseUrl + ManifestFile))
                {
                    request.timeout = 10;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                        try { latest = JsonUtility.FromJson<Manifest>(request.downloadHandler.text); } catch (Exception) { latest = null; }
                }
                if (latest == null || latest.bundles == null)
                {
                    // No server: play with what is kept, if all of it is there.
                    if (kept != null && Complete(cache, kept)) { Open(cache, kept); Finish(); yield break; }
                    Show("Could not reach the server to download the world.\nCheck the connection and try again.", 0f, retry: true);
                    _retryPressed = false;
                    while (!_retryPressed) yield return null;
                    continue;
                }

                var needed = new List<Entry>();
                long total = 0;
                foreach (Entry e in latest.bundles)
                {
                    Entry old = Find(kept, e.name);
                    string file = Path.Combine(cache, e.name);
                    if (old == null || old.md5 != e.md5 || !File.Exists(file) || new FileInfo(file).Length != e.size)
                    {
                        needed.Add(e);
                        total += e.size;
                    }
                }

                bool failed = false;
                long done = 0;
                foreach (Entry e in needed)
                {
                    string file = Path.Combine(cache, e.name), part = file + ".part";
                    using (var request = new UnityWebRequest(baseUrl + e.name, UnityWebRequest.kHttpVerbGET))
                    {
                        request.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
                        UnityWebRequestAsyncOperation op = request.SendWebRequest();
                        while (!op.isDone)
                        {
                            ShotWhileLoading();
                            Show("Downloading the world", total > 0 ? (done + (long)request.downloadedBytes) / (float)total : 1f, retry: false,
                                $"{Mb(done + (long)request.downloadedBytes)} / {Mb(total)} MB");
                            yield return null;
                        }
                        if (request.result != UnityWebRequest.Result.Success || !File.Exists(part) || new FileInfo(part).Length != e.size)
                        {
                            failed = true;
                            break;
                        }
                    }
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(part, file);
                    done += e.size;
                    // What is fetched counts at once, so a broken download resumes from the next bundle.
                    kept = Merge(kept, latest, e);
                    File.WriteAllText(Path.Combine(cache, ManifestFile), JsonUtility.ToJson(kept));
                }
                if (failed)
                {
                    Show("The download stopped.\nCheck the connection and try again.", total > 0 ? done / (float)total : 0f, retry: true);
                    _retryPressed = false;
                    while (!_retryPressed) yield return null;
                    continue;
                }

                File.WriteAllText(Path.Combine(cache, ManifestFile), JsonUtility.ToJson(latest));
                // Bundles the server no longer lists are dropped.
                foreach (string file in Directory.GetFiles(cache))
                {
                    string name = Path.GetFileName(file);
                    if (name != ManifestFile && Find(latest, name) == null) File.Delete(file);
                }
                Open(cache, latest);
                Finish();
                yield break;
            }
#endif
        }

        /// <summary>Screenshots of the download screen: -shotloader &lt;png&gt; saves it once, three seconds in.</summary>
        private float _started = -1f;
        private bool _shot;

        private void ShotWhileLoading()
        {
            if (_shot) return;
            if (_started < 0f) _started = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _started < 3f) return;
            _shot = true;
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-shotloader");
            if (i >= 0 && i + 1 < args.Length) ScreenCapture.CaptureScreenshot(args[i + 1]);
        }

        private static string Mb(long bytes) => (bytes / 1048576f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        private static Manifest Read(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<Manifest>(File.ReadAllText(path)); } catch (Exception) { return null; }
        }

        private static Entry Find(Manifest manifest, string name)
        {
            if (manifest?.bundles == null) return null;
            foreach (Entry e in manifest.bundles) if (e.name == name) return e;
            return null;
        }

        private static bool Complete(string folder, Manifest manifest)
        {
            if (manifest.bundles == null) return false;
            foreach (Entry e in manifest.bundles)
            {
                string file = Path.Combine(folder, e.name);
                if (!File.Exists(file) || new FileInfo(file).Length != e.size) return false;
            }
            return true;
        }

        /// <summary>The kept manifest with one freshly fetched bundle recorded as the latest has it.</summary>
        private static Manifest Merge(Manifest kept, Manifest latest, Entry fetched)
        {
            var list = new List<Entry>();
            if (kept?.bundles != null) foreach (Entry e in kept.bundles) if (e.name != fetched.name) list.Add(e);
            list.Add(fetched);
            return new Manifest { version = kept?.version ?? latest.version, bundles = list.ToArray() };
        }

        private static void Open(string folder, Manifest manifest)
        {
            foreach (Entry e in manifest.bundles)
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(folder, e.name));
                if (bundle != null) Art.Register(bundle, e.keys ?? Array.Empty<string>());
                else Debug.LogWarning("Art bundle failed to open: " + e.name);
            }
        }

        private void Show(string status, float progress, bool retry, string detail = null)
        {
            if (_canvas == null)
            {
                Canvas canvas = Ui.Canvas("ArtLoaderCanvas", 60);
                _canvas = canvas.gameObject;
                Ui.Panel("Black", canvas.transform, 0f, 0f, 1f, 1f, Color.black);
                var art = Resources.Load<Texture2D>("Art/Title");
                if (art != null)
                {
                    var raw = Ui.Rect("Title", canvas.transform, 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
                    raw.texture = art;
                    var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    fit.aspectRatio = art.width / (float)art.height;
                }
                Ui.Panel("Shade", canvas.transform, 0f, 0f, 1f, 0.34f, new Color(0f, 0f, 0f, 0.55f));
                _status = Ui.Label("Status", canvas.transform, 0.06f, 0.19f, 0.94f, 0.3f, "", 42, TextAnchor.MiddleCenter, Palette.Parchment);
                Ui.Bar("Progress", canvas.transform, 0.1f, 0.14f, 0.9f, 0.18f, Palette.Sorn, out Image fill);
                _fill = fill.rectTransform;
                _retry = Ui.Button("Retry", canvas.transform, 0.3f, 0.05f, 0.7f, 0.11f, "TRY AGAIN", 30, Palette.ButtonForge, () => _retryPressed = true, out _).gameObject;
            }
            _status.text = detail == null ? status : status + "\n<size=32>" + detail + "</size>";
            _fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            _retry.SetActive(retry);
        }

        private void Finish()
        {
            if (_canvas != null) Destroy(_canvas);
            _then?.Invoke();
            Destroy(gameObject);
        }
    }
}
