using BuildCrew.Core;
using BuildCrew.Kits;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Parts
{
    /// <summary>Builds part GameObjects: rigidbody with real mass, colliders, combined visual, outline.</summary>
    public static class PartFactory
    {
        public static Part Create(PartSpec spec, Vector3 position, Quaternion rotation, int entityId, MaterialPalette palette)
        {
            PartDefinition def = PartCatalog.Get(spec.TypeId);
            if (def == null)
            {
                Debug.LogWarning($"[Build Crew] Unknown part type '{spec.TypeId}'");
                return null;
            }
            Vector3 size = spec.Size;

            var go = new GameObject($"{def.DisplayName} {def.FormatSize(size)}");
            // Inactive while assembling, so Awake runs once everything is set.
            go.SetActive(false);
            go.transform.SetPositionAndRotation(position, rotation);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = def.Mass(size);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.15f;

            def.BuildColliders(go, size);
            var visual = new PartVisualBuilder(go.transform, palette);
            def.BuildVisual(visual, size);
            visual.Finish();

            // "Placed but not fixed" outline: a slightly larger transparent yellow box.
            var outline = new GameObject("Outline");
            outline.transform.SetParent(go.transform, false);
            outline.transform.localScale = size + Vector3.one * 0.04f;
            outline.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Cube);
            var or = outline.AddComponent<MeshRenderer>();
            or.sharedMaterial = palette != null ? palette.Get("outline") : MaterialPalette.Create("outline");
            or.shadowCastingMode = ShadowCastingMode.Off;
            outline.SetActive(false);

            var part = (Part)go.AddComponent(def.ComponentType);
            part.Configure(entityId, def.DisplayName);
            part.Initialize(def, size, spec.Role, outline);
            part.MarkSpawnedAtRuntime();
            go.SetActive(true);
            return part;
        }
    }
}
