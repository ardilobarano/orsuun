using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Orsuun.Client.EditorTools
{
    /// <summary>
    /// Packs Assets/Orsuun/Content (models, their materials, backdrops, floors) into asset bundles for one platform, one
    /// bundle a folder (Models/Classes, Models/Mobs, ... Backdrops), each asset named by its old Resources path so
    /// Orsuun.Client.Art finds it, and writes manifest.json (name, size, md5, keys) beside them. Phones download them from
    /// the server's /downloads/content/&lt;platform&gt;/ (tools/build-mobile.sh uploads them); the Mac build carries its
    /// own in StreamingAssets/Content.
    /// </summary>
    public static class ContentBundles
    {
        [System.Serializable] private sealed class Entry { public string name; public long size; public string md5; public string[] keys; }
        [System.Serializable] private sealed class Manifest { public string version; public Entry[] bundles; }

        public static string Build(BuildTarget target, string outDir)
        {
            string folder = Orsuun.Client.Art.Folder;
            var groups = new SortedDictionary<string, List<(string path, string key)>>();
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                string path = file.Replace('\\', '/');
                string rel = path.Substring(folder.Length);
                int dot = rel.LastIndexOf('.');
                string key = dot > 0 ? rel.Substring(0, dot) : rel;
                string[] parts = rel.Split('/');
                // Models/Classes/..., Models/Mobs/...; loose files under Models join "models"; other folders one each.
                string group = (parts.Length > 2 ? parts[0] + "-" + parts[1] : parts[0]).ToLowerInvariant();
                if (!groups.TryGetValue(group, out var list)) groups[group] = list = new List<(string, string)>();
                list.Add((path, key));
            }
            var builds = new List<AssetBundleBuild>();
            foreach (var g in groups)
            {
                var paths = new List<string>();
                var keys = new List<string>();
                foreach (var (path, key) in g.Value) { paths.Add(path); keys.Add(key); }
                builds.Add(new AssetBundleBuild { assetBundleName = g.Key, assetNames = paths.ToArray(), addressableNames = keys.ToArray() });
            }
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            AssetBundleManifest built = BuildPipeline.BuildAssetBundles(outDir, builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, target);
            if (built == null) throw new System.Exception("Content bundles failed to build for " + target);

            var entries = new List<Entry>();
            foreach (AssetBundleBuild b in builds)
            {
                string file = Path.Combine(outDir, b.assetBundleName);
                entries.Add(new Entry { name = b.assetBundleName, size = new FileInfo(file).Length, md5 = Md5(file), keys = b.addressableNames });
            }
            // Only the bundles and the manifest travel: Unity's own manifests are left behind.
            foreach (string file in Directory.GetFiles(outDir))
            {
                string name = Path.GetFileName(file);
                if (!entries.Exists(e => e.name == name)) File.Delete(file);
            }
            var manifest = new Manifest { version = System.DateTime.UtcNow.ToString("yyMMddHHmm"), bundles = entries.ToArray() };
            File.WriteAllText(Path.Combine(outDir, "manifest.json"), JsonUtility.ToJson(manifest, true));
            long total = 0;
            foreach (Entry e in entries) total += e.size;
            Debug.Log($"Content bundles for {target}: {entries.Count}, {total / 1048576f:0.0} MB in {outDir}");
            return outDir;
        }

        /// <summary>Copies a platform's bundles into a built player's StreamingAssets/Content.</summary>
        public static void CopyInto(string bundles, string streamingAssets)
        {
            string target = Path.Combine(streamingAssets, "Content");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(bundles)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        private static string Md5(string file)
        {
            using (var md5 = MD5.Create())
            using (FileStream stream = File.OpenRead(file))
                return System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
