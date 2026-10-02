using System.Collections.Generic;
using BuildCrew.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Parts
{
    /// <summary>
    /// Collects primitive shapes (box, sphere, cylinder) for a part's look and
    /// merges them into ONE mesh (a submesh per material) on a "Visual" child,
    /// so a stack of 10 bricks is one renderer, not ten.
    /// </summary>
    public class PartVisualBuilder
    {
        struct Item
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public Material Material;
        }

        readonly Transform _root;
        readonly MaterialPalette _palette;
        readonly List<Item> _items = new List<Item>();

        public PartVisualBuilder(Transform root, MaterialPalette palette)
        {
            _root = root;
            _palette = palette;
        }

        public Material Mat(string key) => _palette != null ? _palette.Get(key) : MaterialPalette.Create(key);

        public void Box(Vector3 center, Vector3 size, string material, Vector3 euler = default) =>
            Add(PrimitiveType.Cube, center, size, material, euler);

        public void Sphere(Vector3 center, Vector3 size, string material) =>
            Add(PrimitiveType.Sphere, center, size, material, Vector3.zero);

        /// <summary>Cylinder of the given diameter and length along its local y (rotate with euler).</summary>
        public void Cylinder(Vector3 center, float diameter, float length, string material, Vector3 euler = default) =>
            Add(PrimitiveType.Cylinder, center, new Vector3(diameter, length * 0.5f, diameter), material, euler);

        void Add(PrimitiveType type, Vector3 center, Vector3 scale, string material, Vector3 euler)
        {
            _items.Add(new Item
            {
                Mesh = PrimitiveMeshes.Get(type),
                Matrix = Matrix4x4.TRS(center, Quaternion.Euler(euler), scale),
                Material = Mat(material),
            });
        }

        /// <summary>Builds the combined mesh child. Returns its renderer.</summary>
        public MeshRenderer Finish(string name = "Visual")
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            foreach (Item it in _items)
            {
                if (!byMaterial.TryGetValue(it.Material, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    byMaterial[it.Material] = list;
                    order.Add(it.Material);
                }
                list.Add(new CombineInstance { mesh = it.Mesh, transform = it.Matrix });
            }

            // One merged mesh per material, then those as submeshes of the final mesh.
            var subMeshes = new CombineInstance[order.Count];
            var temp = new List<Mesh>();
            for (int i = 0; i < order.Count; i++)
            {
                var m = new Mesh();
                m.CombineMeshes(byMaterial[order[i]].ToArray(), true, true);
                temp.Add(m);
                subMeshes[i] = new CombineInstance { mesh = m, transform = Matrix4x4.identity };
            }
            var mesh = new Mesh { name = _root.name + "_Mesh" };
            mesh.CombineMeshes(subMeshes, false, true);
            mesh.RecalculateBounds();
            foreach (Mesh m in temp) Object.Destroy(m);

            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = order.ToArray();
            r.shadowCastingMode = ShadowCastingMode.On;
            _items.Clear();
            return r;
        }
    }

    /// <summary>Unity's built-in primitive meshes, fetched once.</summary>
    public static class PrimitiveMeshes
    {
        static readonly Dictionary<PrimitiveType, Mesh> Cache = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh Get(PrimitiveType type)
        {
            if (Cache.TryGetValue(type, out Mesh m) && m != null) return m;
            GameObject temp = GameObject.CreatePrimitive(type);
            m = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            Cache[type] = m;
            return m;
        }
    }
}
