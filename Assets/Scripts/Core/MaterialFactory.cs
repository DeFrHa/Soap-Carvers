using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Core
{
    /// <summary>
    /// Creates simple URP materials from code. Falls back to built-in shaders if
    /// URP shaders cannot be found (e.g. stripped from a build; see SETUP.md).
    /// </summary>
    public static class MaterialFactory
    {
        public const string UrpLit = "Universal Render Pipeline/Lit";
        public const string UrpUnlit = "Universal Render Pipeline/Unlit";

        static Shader FindShader(string preferred, string fallback)
        {
            Shader s = Shader.Find(preferred);
            if (s == null) s = Shader.Find(fallback);
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        public static Material Lit(string name, Color color, float smoothness = 0.15f, float metallic = 0f)
        {
            var m = new Material(FindShader(UrpLit, "Standard")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material Emissive(string name, Color color, Color emission, float smoothness = 0.3f)
        {
            Material m = Lit(name, color, smoothness);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        public static Material Unlit(string name, Color color)
        {
            var m = new Material(FindShader(UrpUnlit, "Unlit/Color")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            return m;
        }

        public static Material UnlitTexture(string name, Texture texture)
        {
            var m = new Material(FindShader(UrpUnlit, "Unlit/Texture")) { name = name };
            m.SetColor("_BaseColor", Color.white);
            SetTexture(m, texture);
            return m;
        }

        public static void SetTexture(Material m, Texture texture)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
        }

        /// <summary>Alpha-blended lit material (glass). Same URP property setup as the unlit variant.</summary>
        public static Material LitTransparent(string name, Color color, float smoothness = 0.9f)
        {
            Material m = Lit(name, color, smoothness);
            MakeTransparent(m, false);
            return m;
        }

        /// <summary>Alpha-blended, double-sided unlit material (URP property setup).</summary>
        public static Material UnlitTransparent(string name, Color color)
        {
            Material m = Unlit(name, color);
            MakeTransparent(m, true);
            return m;
        }

        static void MakeTransparent(Material m, bool doubleSided)
        {
            m.SetFloat("_Surface", 1f);          // 0 = Opaque, 1 = Transparent
            m.SetFloat("_Blend", 0f);            // Alpha
            if (doubleSided) m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
