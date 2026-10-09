using System;
using System.Collections.Generic;

namespace ChuchuGames.Core
{
    /// <summary>
    /// Enum-driven state machine (GDD §9: "start with an enum + switch"). Optionally restricts
    /// transitions; with no rules every transition is allowed.
    /// </summary>
    public sealed class StateMachine<T> where T : struct, Enum
    {
        readonly Dictionary<T, HashSet<T>> _allowed = new Dictionary<T, HashSet<T>>();
        readonly Dictionary<T, Action> _enter = new Dictionary<T, Action>();
        readonly Dictionary<T, Action> _exit = new Dictionary<T, Action>();

        public T Current { get; private set; }

        /// <summary>(from, to), raised after exit/enter callbacks.</summary>
        public event Action<T, T> Changed;

        public StateMachine(T initial) => Current = initial;

        /// <summary>Declares legal transitions from a state. Once any rule exists, undeclared transitions are refused.</summary>
        public StateMachine<T> Allow(T from, params T[] to)
        {
            if (!_allowed.TryGetValue(from, out var set)) _allowed[from] = set = new HashSet<T>();
            foreach (var t in to) set.Add(t);
            return this;
        }

        public StateMachine<T> OnEnter(T state, Action action)
        {
            _enter[state] = _enter.TryGetValue(state, out var a) ? a + action : action;
            return this;
        }

        public StateMachine<T> OnExit(T state, Action action)
        {
            _exit[state] = _exit.TryGetValue(state, out var a) ? a + action : action;
            return this;
        }

        public bool CanGo(T to) =>
            _allowed.Count == 0 || (_allowed.TryGetValue(Current, out var set) && set.Contains(to));

        public void Go(T to)
        {
            if (!CanGo(to)) throw new InvalidOperationException($"Transition {Current} → {to} is not allowed");
            var from = Current;
            if (_exit.TryGetValue(from, out var exit)) exit();
            Current = to;
            if (_enter.TryGetValue(to, out var enter)) enter();
            Changed?.Invoke(from, to);
        }
    }
}
