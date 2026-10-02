using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// Named materials for everything spawned at runtime (pile parts, cut
    /// pieces, shards, ghosts). SceneBuilder fills <see cref="entries"/> with
    /// persisted .mat assets so they ship with the scene; any missing key is
    /// created on demand from <see cref="Defaults"/>.
    /// </summary>
    public class MaterialPalette : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public Material material;
        }

        public enum Kind { Lit, Glossy, Metal, Glass, Unlit, Ghost, Emissive }

        public struct Spec
        {
            public string Key;
            public Color Color;
            public Kind Kind;
            public Spec(string key, Color color, Kind kind = Kind.Lit) { Key = key; Color = color; Kind = kind; }
        }

        /// <summary>Every material key the game uses, with its look.</summary>
        public static readonly Spec[] Defaults =
        {
            new Spec("ground", new Color(0.47f, 0.72f, 0.35f)),
            new Spec("dirt", new Color(0.55f, 0.43f, 0.3f)),
            new Spec("trunk", new Color(0.5f, 0.33f, 0.2f)),
            new Spec("leavesGreen", new Color(0.32f, 0.66f, 0.3f)),
            new Spec("leavesLime", new Color(0.55f, 0.8f, 0.3f)),
            new Spec("leavesPink", new Color(0.98f, 0.62f, 0.75f)),
            new Spec("leavesTeal", new Color(0.3f, 0.7f, 0.6f)),
            new Spec("wood", new Color(0.86f, 0.64f, 0.38f)),
            new Spec("post", new Color(0.62f, 0.42f, 0.24f)),
            new Spec("beam", new Color(0.5f, 0.32f, 0.2f)),
            new Spec("darkWood", new Color(0.4f, 0.26f, 0.16f)),
            new Spec("pallet", new Color(0.78f, 0.66f, 0.45f)),
            new Spec("stone", new Color(0.62f, 0.62f, 0.6f)),
            new Spec("brick", new Color(0.78f, 0.3f, 0.22f)),
            new Spec("tile", new Color(0.86f, 0.45f, 0.25f)),
            new Spec("metalSheet", new Color(0.7f, 0.76f, 0.8f), Kind.Metal),
            new Spec("metal", new Color(0.78f, 0.8f, 0.84f), Kind.Metal),
            new Spec("glass", new Color(0.6f, 0.85f, 1f, 0.45f), Kind.Glass),
            new Spec("frame", new Color(0.95f, 0.95f, 0.92f)),
            new Spec("door", new Color(0.2f, 0.55f, 0.45f)),
            new Spec("brass", new Color(0.95f, 0.75f, 0.3f), Kind.Metal),
            new Spec("nailBox", new Color(0.25f, 0.45f, 0.9f), Kind.Glossy),
            new Spec("screwBox", new Color(1f, 0.55f, 0.15f), Kind.Glossy),
            new Spec("porcelain", new Color(0.97f, 0.97f, 0.97f), Kind.Glossy),
            new Spec("red", new Color(0.9f, 0.2f, 0.18f), Kind.Glossy),
            new Spec("blue", new Color(0.2f, 0.4f, 0.85f), Kind.Glossy),
            new Spec("yellow", new Color(1f, 0.82f, 0.2f), Kind.Glossy),
            new Spec("orange", new Color(1f, 0.55f, 0.12f), Kind.Glossy),
            new Spec("green", new Color(0.3f, 0.7f, 0.35f), Kind.Glossy),
            new Spec("skin", new Color(1f, 0.8f, 0.65f)),
            new Spec("white", new Color(0.96f, 0.96f, 0.96f)),
            new Spec("black", new Color(0.08f, 0.08f, 0.09f), Kind.Glossy),
            new Spec("rubber", new Color(0.12f, 0.12f, 0.12f)),
            new Spec("cement", new Color(0.72f, 0.72f, 0.7f)),
            new Spec("sand", new Color(0.93f, 0.8f, 0.55f)),
            new Spec("water", new Color(0.3f, 0.6f, 0.95f), Kind.Glossy),
            new Spec("mortarWet", new Color(0.45f, 0.45f, 0.47f), Kind.Glossy),
            new Spec("mortarFresh", new Color(0.6f, 0.6f, 0.58f), Kind.Glossy),
            new Spec("mortarSet", new Color(0.82f, 0.82f, 0.8f)),
            new Spec("mortarDry", new Color(0.78f, 0.74f, 0.62f)),
            new Spec("outline", new Color(1f, 0.9f, 0.1f, 0.35f), Kind.Ghost),
            new Spec("ghostFuture", new Color(1f, 1f, 1f, 0.06f), Kind.Ghost),
            new Spec("ghostStage", new Color(0.3f, 0.85f, 1f, 0.16f), Kind.Ghost),
            new Spec("ghostActive", new Color(0.35f, 0.95f, 1f, 0.38f), Kind.Ghost),
            new Spec("ghostTarget", new Color(0.3f, 1f, 0.4f, 0.55f), Kind.Ghost),
            new Spec("cutMarker", new Color(1f, 0.15f, 0.1f, 0.85f), Kind.Ghost),
            new Spec("scoreMarker", new Color(1f, 1f, 1f, 0.9f), Kind.Ghost),
            new Spec("rain", new Color(0.7f, 0.8f, 1f, 0.5f), Kind.Ghost),
            new Spec("hologram", new Color(0.35f, 0.85f, 1f), Kind.Emissive),
            new Spec("hologramFixed", new Color(0.4f, 1f, 0.5f), Kind.Emissive),
            new Spec("hologramStage", new Color(1f, 0.85f, 0.3f), Kind.Emissive),
            new Spec("hologramFaint", new Color(0.15f, 0.3f, 0.45f, 0.5f), Kind.Ghost),
        };

        [SerializeField] List<Entry> entries = new List<Entry>();

        readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public void SetEntries(List<Entry> list) => entries = list;

        public Material Get(string key)
        {
            if (_cache.TryGetValue(key, out Material m) && m != null) return m;
            foreach (Entry e in entries)
            {
                if (e.key != key || e.material == null) continue;
                _cache[key] = e.material;
                return e.material;
            }
            m = Create(key);
            _cache[key] = m;
            return m;
        }

        /// <summary>Creates the default material for a key (unknown keys become magenta).</summary>
        public static Material Create(string key)
        {
            foreach (Spec s in Defaults)
                if (s.Key == key) return Create(s);
            return MaterialFactory.Lit(key, Color.magenta);
        }

        public static Material Create(Spec s)
        {
            switch (s.Kind)
            {
                case Kind.Glossy: return MaterialFactory.Lit(s.Key, s.Color, 0.55f);
                case Kind.Metal: return MaterialFactory.Lit(s.Key, s.Color, 0.7f, 0.65f);
                case Kind.Glass: return MaterialFactory.LitTransparent(s.Key, s.Color, 0.95f);
                case Kind.Unlit: return MaterialFactory.Unlit(s.Key, s.Color);
                case Kind.Ghost: return MaterialFactory.UnlitTransparent(s.Key, s.Color);
                case Kind.Emissive: return MaterialFactory.Emissive(s.Key, s.Color, s.Color * 0.6f, 0.4f);
                default: return MaterialFactory.Lit(s.Key, s.Color, 0.15f);
            }
        }
    }
}
