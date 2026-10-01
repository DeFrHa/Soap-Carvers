using System;
using System.Collections.Generic;

namespace SoapCarvers.Targets
{
    /// <summary>Registry of all code-defined target shapes.</summary>
    public static class TargetLibrary
    {
        static readonly List<Func<TargetShape>> Factories = new List<Func<TargetShape>>
        {
            () => new SwanShape(),
            () => new MushroomShape(),
        };

        public static IEnumerable<TargetShape> All()
        {
            foreach (var f in Factories) yield return f();
        }

        /// <summary>Case-insensitive lookup; falls back to the first shape (Swan).</summary>
        public static TargetShape Get(string name)
        {
            foreach (var f in Factories)
            {
                TargetShape s = f();
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            }
            return Factories[0]();
        }
    }
}
