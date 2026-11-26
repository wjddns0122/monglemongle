using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Mongle.Views
{
    /// <summary>Small things every 11-26 screen needs (the new UI does not use the old UiKit).</summary>
    public static class ViewUtil
    {
        /// <summary>Buttons need one EventSystem (Input System module); it lives across scenes.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (Application.isPlaying) Object.DontDestroyOnLoad(es);
        }
    }
}
