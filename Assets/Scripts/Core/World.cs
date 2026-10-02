using BuildCrew.Building;
using BuildCrew.Interaction;
using BuildCrew.Parts;
using BuildCrew.Tools;
using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// Cached lookups of the world managers (one of each per scene). Not a
    /// singleton holder: nothing player-specific lives here, and a destroyed
    /// manager (scene reload) is simply looked up again.
    /// </summary>
    public static class World
    {
        static GameManager _game;
        static CommandBus _bus;
        static EntityRegistry _registry;
        static GrabManager _grabs;
        static PartManager _parts;
        static BuildManager _build;
        static WorkshopManager _workshop;
        static MaterialPalette _palette;
        static GameSettings _fallbackSettings;

        public static GameManager Game => Find(ref _game);
        public static CommandBus Bus => Find(ref _bus);
        public static EntityRegistry Registry => Find(ref _registry);
        public static GrabManager Grabs => Find(ref _grabs);
        public static PartManager Parts => Find(ref _parts);
        public static BuildManager Build => Find(ref _build);
        public static WorkshopManager Workshop => Find(ref _workshop);
        public static MaterialPalette Palette => Find(ref _palette);

        public static GameSettings Settings
        {
            get
            {
                GameManager g = Game;
                if (g != null && g.Settings != null) return g.Settings;
                if (_fallbackSettings == null) _fallbackSettings = GameSettings.CreateDefault();
                return _fallbackSettings;
            }
        }

        /// <summary>Tools work only while the build timer runs.</summary>
        public static bool ToolsEnabled => Game != null && Game.State == GameState.Build;

        static T Find<T>(ref T cache) where T : Object
        {
            if (cache == null) cache = Object.FindFirstObjectByType<T>();
            return cache;
        }
    }
}
