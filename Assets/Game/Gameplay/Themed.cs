using System;
using System.Collections.Generic;
using RichCoast.Core;
using TMPro;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Binds baked colours to <see cref="ThemeKey"/>s so the milestone palette cross-fade can restyle
    /// them: <see cref="RestyleAll"/> (wired to <c>GameEvents.ThemeChanged</c> by the composition root)
    /// re-reads the active <see cref="Theme"/> into every live binding. The target's CURRENT alpha is
    /// preserved — pulses and fades that tween alpha keep working through a restyle. One component per
    /// GameObject holds a binding per bound Component (an Image and its Outline can both be bound);
    /// re-binding the same Component swaps its key. Zones bind their own surfaces; nothing here knows
    /// which zone owns what.
    /// </summary>
    public sealed class Themed : MonoBehaviour
    {
        sealed class Binding
        {
            public ThemeKey Key;
            public Func<Color> Get;
            public Action<Color> Set;
        }

        static readonly HashSet<Themed> live = new HashSet<Themed>();

        readonly Dictionary<Component, Binding> bindings = new Dictionary<Component, Binding>();

        /// <summary>Bind a component's colour through explicit accessors (for types without a typed overload).</summary>
        public static Themed Bind(Component target, ThemeKey key, Func<Color> get, Action<Color> set)
        {
            var themed = target.GetComponent<Themed>();
            if (themed == null) themed = target.gameObject.AddComponent<Themed>();
            if (!themed.bindings.TryGetValue(target, out var binding))
            {
                binding = new Binding();
                themed.bindings[target] = binding;
            }
            binding.Key = key;
            binding.Get = get;
            binding.Set = set;
            live.Add(themed);
            Restyle(binding);
            return themed;
        }

        public static Themed Bind(SpriteRenderer sr, ThemeKey key) => Bind(sr, key, () => sr.color, c => sr.color = c);
        public static Themed Bind(TMP_Text text, ThemeKey key) => Bind(text, key, () => text.color, c => text.color = c);
        /// <summary>A camera's clear colour (always opaque — the default background alpha is 0).</summary>
        public static Themed Bind(Camera cam, ThemeKey key) => Bind(cam, key, () => Color.white, c => cam.backgroundColor = c);
        /// <summary>A MeshRenderer's instanced material colour (the WorldArt quads own their material).</summary>
        public static Themed Bind(MeshRenderer mr, ThemeKey key) => Bind(mr, key, () => mr.material.color, c => mr.material.color = c);
        public static Themed Bind(LineRenderer line, ThemeKey key) => Bind(line, key, () => line.startColor, c => line.startColor = line.endColor = c);

        /// <summary>Re-read the active palette into every live binding (one cross-fade tick).</summary>
        public static void RestyleAll()
        {
            foreach (var themed in live)
                foreach (var binding in themed.bindings.Values)
                    Restyle(binding);
        }

        static void Restyle(Binding binding)
        {
            var target = Theme.Get(binding.Key);
            target.a = binding.Get().a;
            binding.Set(target);
        }

        void OnDestroy() => live.Remove(this);
    }
}
