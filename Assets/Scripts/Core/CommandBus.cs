using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// The single entry point for game-state changes. Players (and physics
    /// events on the host) build <see cref="GameCommand"/>s and call
    /// <see cref="Execute"/>; managers register one handler per command type.
    /// Executed commands are logged in order. A network layer slots in here:
    /// clients send commands to the host, the host executes them.
    /// </summary>
    public class CommandBus : MonoBehaviour
    {
        // Initialized inline so managers may register before our Awake.
        readonly Dictionary<Type, Func<GameCommand, bool>> _handlers = new Dictionary<Type, Func<GameCommand, bool>>();
        readonly List<GameCommand> _log = new List<GameCommand>(1024);

        public IReadOnlyList<GameCommand> Log => _log;

        /// <summary>Raised after a command was executed successfully.</summary>
        public event Action<GameCommand> Executed;

        public void Register<T>(Func<T, bool> handler) where T : GameCommand
        {
            _handlers[typeof(T)] = c => handler((T)c);
        }

        public bool Execute(GameCommand command)
        {
            if (command == null) return false;
            if (!_handlers.TryGetValue(command.GetType(), out Func<GameCommand, bool> handler))
            {
                Debug.LogWarning($"[Build Crew] No handler for {command.GetType().Name}");
                return false;
            }
            if (!handler(command)) return false;
            command.Time = UnityEngine.Time.time;
            _log.Add(command);
            Executed?.Invoke(command);
            return true;
        }

        public void ClearLog() => _log.Clear();
    }
}
