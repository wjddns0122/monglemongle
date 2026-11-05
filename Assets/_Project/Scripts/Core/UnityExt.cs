using UnityEngine;

namespace Mongle.Core
{
    public static class UnityExt
    {
        /// <summary>GetComponent or AddComponent. Avoids '??' which does not work with Unity's null semantics in the editor.</summary>
        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
        public static T GetOrAdd<T>(this Component c) where T : Component => c.gameObject.GetOrAdd<T>();
    }
}
