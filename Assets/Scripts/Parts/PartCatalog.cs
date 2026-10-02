using System.Collections.Generic;

namespace BuildCrew.Parts
{
    /// <summary>
    /// Registry of part types by id. Built-in types are registered on first
    /// use; mods/community code can <see cref="Register"/> more (or replace one)
    /// before the kit is loaded. Static because it holds type data, not state.
    /// </summary>
    public static class PartCatalog
    {
        static Dictionary<string, PartDefinition> _types;

        static Dictionary<string, PartDefinition> Types
        {
            get
            {
                if (_types == null)
                {
                    _types = new Dictionary<string, PartDefinition>();
                    foreach (PartDefinition def in BuiltInParts.All()) _types[def.Id] = def;
                }
                return _types;
            }
        }

        public static IEnumerable<PartDefinition> All => Types.Values;

        public static void Register(PartDefinition definition) => Types[definition.Id] = definition;

        public static PartDefinition Get(string id) =>
            id != null && Types.TryGetValue(id, out PartDefinition def) ? def : null;

        public static bool IsKnown(string id) => Get(id) != null;
    }
}
