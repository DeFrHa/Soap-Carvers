using System;
using System.Collections.Generic;
using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Parts
{
    /// <summary>The part types the game ships with. Low-poly, colorful, made of primitives.</summary>
    public static class BuiltInParts
    {
        public static IEnumerable<PartDefinition> All()
        {
            yield return new PostPart();
            yield return new PlankPart();
            yield return new BeamPart();
            yield return new StonePart();
            yield return new BrickStackPart();
            yield return new RoofTilePart();
            yield return new MetalSheetPart();
            yield return new PanePart();
            yield return new WindowFramePart();
            yield return new DoorPart();
            yield return new FixingBoxPart("nails", "Box of nails", "nailBox");
            yield return new FixingBoxPart("screws", "Box of screws", "screwBox");
            yield return new ToiletPart();
            yield return new GnomePart();
        }
    }

    // ------------------------------------------------------------------ wood

    public class PostPart : PartDefinition
    {
        public override string Id => "post";
        public override string DisplayName => "Post";
        public override PartCategory Category => PartCategory.Wood;
        public override float Density => 480f;
        public override float[] StandardLengths => new[] { 2.5f };
        public override int SawStrokes(GameSettings s) => s.postStrokes;
        public override Vector3 DefaultSize(float length) => new Vector3(length, 0.12f, 0.12f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, "post");
            // Darker end caps so the length reads at a glance.
            v.Box(new Vector3(size.x * 0.5f - 0.01f, 0f, 0f), new Vector3(0.025f, size.y + 0.004f, size.z + 0.004f), "darkWood");
            v.Box(new Vector3(-size.x * 0.5f + 0.01f, 0f, 0f), new Vector3(0.025f, size.y + 0.004f, size.z + 0.004f), "darkWood");
        }
    }

    public class PlankPart : PartDefinition
    {
        public override string Id => "plank";
        public override string DisplayName => "Plank";
        public override PartCategory Category => PartCategory.Wood;
        public override float Density => 420f;
        public override float[] StandardLengths => new[] { 2f, 3f };
        public override Vector3 DefaultSize(float length) => new Vector3(length, 0.4f, 0.03f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, "wood");
            // Two grooves so it reads as boards.
            for (int i = -1; i <= 1; i += 2)
                v.Box(new Vector3(0f, i * size.y * 0.17f, 0f), new Vector3(size.x - 0.02f, 0.012f, size.z + 0.004f), "post");
        }
    }

    public class BeamPart : PartDefinition
    {
        public override string Id => "beam";
        public override string DisplayName => "Beam";
        public override PartCategory Category => PartCategory.Wood;
        public override float Density => 450f; // 4 m beam = 36 kg: one player, slowly
        public override float[] StandardLengths => new[] { 3f, 4f };
        public override bool NeedsTwoPersonSaw => true;
        public override int SawStrokes(GameSettings s) => s.beamStrokes;
        public override Vector3 DefaultSize(float length) => new Vector3(length, 0.2f, 0.1f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, "beam");
            v.Box(new Vector3(size.x * 0.5f - 0.012f, 0f, 0f), new Vector3(0.03f, size.y + 0.004f, size.z + 0.004f), "darkWood");
            v.Box(new Vector3(-size.x * 0.5f + 0.012f, 0f, 0f), new Vector3(0.03f, size.y + 0.004f, size.z + 0.004f), "darkWood");
        }
    }

    // ----------------------------------------------------------------- stone

    public class StonePart : PartDefinition
    {
        public override string Id => "stone";
        public override string DisplayName => "Foundation stone";
        public override PartCategory Category => PartCategory.Stone;
        public override float Density => 640f; // hollow-core block: 1.5 x 0.25 x 0.25 m = 60 kg
        public override Vector3 DefaultSize(float length) => new Vector3(1.5f, 0.25f, 0.25f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, "stone");
            v.Box(new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x * 0.92f, 0.01f, size.z * 0.8f), "cement");
        }
    }

    /// <summary>Ten bricks (5 long x 2 high) handled as one object and one slot: a wall row section.</summary>
    public class BrickStackPart : PartDefinition
    {
        public override string Id => "brickStack";
        public override string DisplayName => "Brick stack (10)";
        public override string PluralName => "Brick stacks";
        public override PartCategory Category => PartCategory.Stone;
        public override float Mass(Vector3 size) => 25f; // 10 x 2.5 kg
        public override Vector3 DefaultSize(float length) => new Vector3(1.5f, 0.4f, 0.2f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            const float gap = 0.012f;
            float bw = size.x / 5f, bh = size.y / 2f;
            for (int row = 0; row < 2; row++)
            for (int i = 0; i < 5; i++)
            {
                var c = new Vector3(-size.x * 0.5f + bw * (i + 0.5f), -size.y * 0.5f + bh * (row + 0.5f), 0f);
                v.Box(c, new Vector3(bw - gap, bh - gap, size.z), "brick");
            }
            // Mortar-colored core so the gaps don't show through.
            v.Box(Vector3.zero, new Vector3(size.x - 0.02f, size.y - 0.02f, size.z * 0.8f), "cement");
        }
    }

    // ------------------------------------------------------------------ roof

    /// <summary>A panel of tiles (1.5 wide, 1.0 down the slope): local x along the eave, z down the slope.</summary>
    public class RoofTilePart : PartDefinition
    {
        public override string Id => "roofTile";
        public override string DisplayName => "Roof tile panel";
        public override PartCategory Category => PartCategory.Roof;
        public override float Density => 200f;
        public override Vector3 DefaultSize(float length) => new Vector3(1.5f, 0.04f, 1f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(new Vector3(0f, -size.y * 0.25f, 0f), new Vector3(size.x, size.y * 0.5f, size.z), "tile");
            int n = Mathf.Max(1, Mathf.RoundToInt(size.x / 0.25f));
            float step = size.x / n;
            for (int i = 0; i < n; i++)
                v.Cylinder(new Vector3(-size.x * 0.5f + step * (i + 0.5f), 0f, 0f), step * 0.9f, size.z, "tile", new Vector3(90f, 0f, 0f));
        }

        public override void BuildColliders(GameObject root, Vector3 size) =>
            AddBox(root, Vector3.zero, new Vector3(size.x - 0.01f, Mathf.Max(0.03f, size.y), size.z - 0.01f));
    }

    /// <summary>Corrugated sheet: local x = length, y = thickness, z = width.</summary>
    public class MetalSheetPart : PartDefinition
    {
        public override string Id => "metalSheet";
        public override string DisplayName => "Corrugated sheet";
        public override PartCategory Category => PartCategory.Roof;
        public override float Mass(Vector3 size) => Mathf.Max(1f, size.x * size.z * 3.5f); // ~3.5 kg/m^2
        public override Vector3 DefaultSize(float length) => new Vector3(3f, 0.03f, 1.2f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(new Vector3(0f, -size.y * 0.3f, 0f), new Vector3(size.x, size.y * 0.3f, size.z), "metalSheet");
            int n = Mathf.Max(2, Mathf.RoundToInt(size.z / 0.15f));
            float step = size.z / n;
            for (int i = 0; i < n; i++)
                v.Box(new Vector3(0f, 0f, -size.z * 0.5f + step * (i + 0.5f)), new Vector3(size.x, size.y * 0.7f, step * 0.4f), "metalSheet");
        }
    }

    // ----------------------------------------------------------------- glass

    /// <summary>Window pane: x = width, y = height, z = thickness. Comes 1 x 1 m; cut to size with the glass cutter.</summary>
    public class PanePart : PartDefinition
    {
        public override string Id => "pane";
        public override string DisplayName => "Window pane";
        public override PartCategory Category => PartCategory.Glass;
        public override float Density => 2500f;
        public override bool Fragile => true;
        public override CutTool CutTool => CutTool.GlassCutter;
        public override Vector3 DefaultSize(float length) => new Vector3(1f, 1f, 0.006f);

        public override Vector3 StockSize(Vector3 needed) =>
            needed.x <= 1.04f && needed.y <= 1.04f ? new Vector3(1f, 1f, needed.z) : needed;

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, "glass");
            // Thin white edge so a pane on the ground is visible at all.
            v.Box(new Vector3(0f, size.y * 0.5f - 0.005f, 0f), new Vector3(size.x, 0.01f, size.z + 0.002f), "white");
            v.Box(new Vector3(0f, -size.y * 0.5f + 0.005f, 0f), new Vector3(size.x, 0.01f, size.z + 0.002f), "white");
        }

        public override void BuildColliders(GameObject root, Vector3 size) =>
            AddBox(root, Vector3.zero, new Vector3(size.x - 0.01f, size.y - 0.01f, Mathf.Max(0.02f, size.z)));
    }

    // ------------------------------------------------------------ ready-made

    /// <summary>Window frame: four bars around a 1.0 x 0.8 opening (for a 1.5 x 1.2 frame), so a pane sits inside.</summary>
    public class WindowFramePart : PartDefinition
    {
        public override string Id => "windowFrame";
        public override string DisplayName => "Window frame";
        public override PartCategory Category => PartCategory.ReadyMade;
        public override float Mass(Vector3 size) => 9f;
        public override Vector3 DefaultSize(float length) => new Vector3(1.5f, 1.2f, 0.1f);

        static void Bars(Vector3 size, out float side, out float top)
        {
            side = Mathf.Max(0.06f, (size.x - 1f) * 0.5f);
            top = Mathf.Max(0.06f, (size.y - 0.8f) * 0.5f);
            if (side > size.x * 0.3f) side = size.x * 0.17f;
            if (top > size.y * 0.3f) top = size.y * 0.17f;
        }

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            Bars(size, out float side, out float top);
            v.Box(new Vector3(-size.x * 0.5f + side * 0.5f, 0f, 0f), new Vector3(side, size.y, size.z), "frame");
            v.Box(new Vector3(size.x * 0.5f - side * 0.5f, 0f, 0f), new Vector3(side, size.y, size.z), "frame");
            v.Box(new Vector3(0f, size.y * 0.5f - top * 0.5f, 0f), new Vector3(size.x - side * 2f, top, size.z), "frame");
            v.Box(new Vector3(0f, -size.y * 0.5f + top * 0.5f, 0f), new Vector3(size.x - side * 2f, top, size.z), "frame");
            // Sill
            v.Box(new Vector3(0f, -size.y * 0.5f + 0.02f, -size.z * 0.6f), new Vector3(size.x * 0.9f, 0.04f, size.z * 0.5f), "frame");
        }

        public override void BuildColliders(GameObject root, Vector3 size)
        {
            Bars(size, out float side, out float top);
            AddBox(root, new Vector3(-size.x * 0.5f + side * 0.5f, 0f, 0f), new Vector3(side - 0.005f, size.y - 0.01f, size.z));
            AddBox(root, new Vector3(size.x * 0.5f - side * 0.5f, 0f, 0f), new Vector3(side - 0.005f, size.y - 0.01f, size.z));
            AddBox(root, new Vector3(0f, size.y * 0.5f - top * 0.5f, 0f), new Vector3(size.x - side * 2f, top - 0.005f, size.z));
            AddBox(root, new Vector3(0f, -size.y * 0.5f + top * 0.5f, 0f), new Vector3(size.x - side * 2f, top - 0.005f, size.z));
        }
    }

    /// <summary>Door: x = width, y = height, z = thickness. The handle side faces -z (outside).</summary>
    public class DoorPart : PartDefinition
    {
        public override string Id => "door";
        public override string DisplayName => "Door";
        public override PartCategory Category => PartCategory.ReadyMade;
        public override float Density => 200f;
        public override Vector3 DefaultSize(float length) => new Vector3(0.9f, 2f, 0.04f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, "door");
            float pw = size.x * 0.35f, ph = size.y * 0.35f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                v.Box(new Vector3(sx * size.x * 0.22f, sy * size.y * 0.22f, -size.z * 0.5f), new Vector3(pw, ph, 0.012f), "green");
            v.Sphere(new Vector3(size.x * 0.38f, -0.05f, -size.z * 0.5f - 0.03f), Vector3.one * 0.07f, "brass");
            v.Sphere(new Vector3(size.x * 0.38f, -0.05f, size.z * 0.5f + 0.03f), Vector3.one * 0.07f, "brass");
        }
    }

    // --------------------------------------------------------------- fixings

    /// <summary>Box of nails or screws: a consumable. E with a hammer/screwdriver in hand grabs a handful.</summary>
    public class FixingBoxPart : PartDefinition
    {
        readonly string _id, _name, _material;

        public FixingBoxPart(string id, string name, string material)
        {
            _id = id;
            _name = name;
            _material = material;
        }

        public override string Id => _id;
        public override string DisplayName => _name;
        public override string PluralName => _name.Replace("Box", "Boxes");
        public override PartCategory Category => PartCategory.Fixings;
        public override float Mass(Vector3 size) => 1.5f;
        public override Vector3 DefaultSize(float length) => new Vector3(0.22f, 0.12f, 0.14f);
        public override Type ComponentType => typeof(FixingBox);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            v.Box(Vector3.zero, size, _material);
            v.Box(new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x * 1.04f, 0.015f, size.z * 1.04f), "white");
            v.Box(new Vector3(0f, 0f, -size.z * 0.5f), new Vector3(size.x * 0.6f, size.y * 0.45f, 0.004f), "white");
        }
    }

    // ------------------------------------------------------------------ junk

    public class ToiletPart : PartDefinition
    {
        public override string Id => "toilet";
        public override string DisplayName => "Toilet (?!)";
        public override string PluralName => "Toilets";
        public override PartCategory Category => PartCategory.Junk;
        public override float Mass(Vector3 size) => 22f;
        public override Vector3 DefaultSize(float length) => new Vector3(0.42f, 0.78f, 0.68f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            float h = size.y;
            v.Cylinder(new Vector3(0f, -h * 0.5f + 0.2f, 0.05f), size.x * 0.85f, 0.4f, "porcelain");
            v.Cylinder(new Vector3(0f, -h * 0.5f + 0.42f, 0.05f), size.x, 0.05f, "porcelain");
            v.Cylinder(new Vector3(0f, -h * 0.5f + 0.45f, 0.05f), size.x * 0.95f, 0.02f, "white");
            v.Box(new Vector3(0f, h * 0.5f - 0.18f, size.z * 0.5f - 0.1f), new Vector3(size.x, 0.36f, 0.18f), "porcelain");
            v.Box(new Vector3(size.x * 0.3f, h * 0.5f - 0.02f, size.z * 0.5f - 0.1f), new Vector3(0.06f, 0.03f, 0.05f), "metal");
        }

        public override void BuildColliders(GameObject root, Vector3 size)
        {
            AddBox(root, new Vector3(0f, -size.y * 0.5f + 0.225f, 0.05f), new Vector3(size.x, 0.45f, size.z * 0.62f));
            AddBox(root, new Vector3(0f, size.y * 0.5f - 0.18f, size.z * 0.5f - 0.1f), new Vector3(size.x, 0.36f, 0.18f));
        }
    }

    public class GnomePart : PartDefinition
    {
        public override string Id => "gnome";
        public override string DisplayName => "Garden gnome";
        public override PartCategory Category => PartCategory.Junk;
        public override float Mass(Vector3 size) => 3.5f;
        public override Vector3 DefaultSize(float length) => new Vector3(0.28f, 0.55f, 0.28f);

        public override void BuildVisual(PartVisualBuilder v, Vector3 size)
        {
            float h = size.y, w = size.x;
            v.Cylinder(new Vector3(0f, -h * 0.5f + 0.02f, 0f), w, 0.04f, "green");
            v.Sphere(new Vector3(0f, -h * 0.5f + 0.15f, 0f), new Vector3(w * 0.9f, 0.24f, w * 0.9f), "blue");
            v.Sphere(new Vector3(0f, -h * 0.5f + 0.3f, 0f), Vector3.one * w * 0.6f, "skin");
            v.Box(new Vector3(0f, -h * 0.5f + 0.25f, -w * 0.22f), new Vector3(w * 0.4f, 0.12f, 0.05f), "white", new Vector3(-15f, 0f, 0f));
            v.Cylinder(new Vector3(0f, -h * 0.5f + 0.39f, 0f), w * 0.55f, 0.08f, "red");
            v.Cylinder(new Vector3(0f, -h * 0.5f + 0.46f, 0f), w * 0.35f, 0.08f, "red");
            v.Sphere(new Vector3(0f, h * 0.5f - 0.03f, 0f), Vector3.one * 0.08f, "red");
        }

        public override void BuildColliders(GameObject root, Vector3 size)
        {
            var c = root.AddComponent<CapsuleCollider>();
            c.height = size.y;
            c.radius = size.x * 0.45f;
        }
    }
}
