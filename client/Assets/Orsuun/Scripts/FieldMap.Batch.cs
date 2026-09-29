using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// A big map's scenery in few draws (owner, 29 Sep 2026: "Phone speed pass"). Models, grass cards, reeds and bridge planks
    /// never become objects of their own: each goes into the batch of its material, tint and 40 m cell, and every batch
    /// becomes one mesh when the map is built. A map then draws a few dozen meshes instead of over a thousand, and the
    /// cells still let the camera skip what is behind it or past the haze.
    /// </summary>
    public sealed partial class FieldMap
    {
        private const float CellSize = 40f;

        private sealed class Batch
        {
            public Material Material;
            public Color? Tint;
            public readonly List<CombineInstance> Parts = new List<CombineInstance>();
            /// <summary>Cards, written straight in (two quads each).</summary>
            public readonly List<Vector3> Verts = new List<Vector3>(), Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Tris = new List<int>();
        }

        private readonly Dictionary<(Material, Color?, int), Batch> _batches = new Dictionary<(Material, Color?, int), Batch>();
        /// <summary>Meshes and materials made for the map, freed with it.</summary>
        private readonly List<Object> _made = new List<Object>();
        private static Mesh _cube;
        private static MaterialPropertyBlock _batchTint;

        private static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cube = box.GetComponent<MeshFilter>().sharedMesh;
                Destroy(box);
                return _cube;
            }
        }

        private Batch BatchFor(Material material, Color? tint, Vector3 at)
        {
            int cell = Mathf.FloorToInt(at.x / CellSize) * 1000 + Mathf.FloorToInt(at.z / CellSize);
            var key = (material, tint, cell);
            if (!_batches.TryGetValue(key, out Batch b)) _batches[key] = b = new Batch { Material = material, Tint = tint };
            return b;
        }

        /// <summary>A scenery model (a metre tall at scale 1) into its cell's batch; one that cannot be read (an old import)
        /// stands on its own instead.</summary>
        private void BatchModel(string name, Vector3 at, float yaw, float height, string tint)
        {
            var prefab = Art.Load<GameObject>("Scenery/Models/" + name);
            if (prefab == null) return;
            MeshFilter[] parts = prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in parts)
                if (mf.sharedMesh != null && !mf.sharedMesh.isReadable) { PlaceModel(_root, name, at, yaw, height, tint); return; }
            var material = Art.Load<Material>("Scenery/Models/" + name);
            Color? shade = !string.IsNullOrEmpty(tint) && ColorUtility.TryParseHtmlString(tint, out Color c) ? c : (Color?)null;
            Matrix4x4 place = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one * height) * prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in parts)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Material m = material != null ? material : mf.GetComponent<Renderer>()?.sharedMaterial;
                Batch b = BatchFor(m, shade, at);
                Matrix4x4 local = place * mf.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) b.Parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = local });
            }
        }

        /// <summary>Every batch becomes one mesh under the map's root.</summary>
        private void FlushBatches()
        {
            foreach (Batch b in _batches.Values)
            {
                var parts = new List<CombineInstance>(b.Parts);
                Mesh cards = null;
                if (b.Verts.Count > 0)
                {
                    cards = new Mesh { name = "Cards", indexFormat = IndexFormat.UInt32 };
                    cards.SetVertices(b.Verts);
                    cards.SetNormals(b.Normals);
                    cards.SetUVs(0, b.Uvs);
                    cards.SetTriangles(b.Tris, 0);
                    parts.Add(new CombineInstance { mesh = cards, transform = Matrix4x4.identity });
                }
                if (parts.Count == 0) continue;
                var mesh = new Mesh { name = "Batch", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(), true, true);
                mesh.RecalculateBounds();
                if (cards != null) Destroy(cards);
                Part(b.Material != null ? b.Material.name : "Batch", mesh, b.Material);
                if (b.Tint.HasValue)
                {
                    Renderer r = _root.GetChild(_root.childCount - 1).GetComponent<Renderer>();
                    _batchTint ??= new MaterialPropertyBlock();
                    r.GetPropertyBlock(_batchTint);
                    _batchTint.SetColor("_BaseColor", b.Tint.Value);
                    r.SetPropertyBlock(_batchTint);
                }
            }
            _batches.Clear();
            // -perflog: how many meshes the map draws in all.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perflog") >= 0)
                Debug.Log($"PERF map {_layout?.Name}: {_root.GetComponentsInChildren<Renderer>(true).Length} renderers");
        }
    }
}
