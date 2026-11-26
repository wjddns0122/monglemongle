using System.Collections.Generic;
using UnityEngine;

namespace Mongle.UI2
{
    /// <summary>
    /// While this full-screen window is on, the screen canvases drawn under it are switched off, so their buttons and
    /// labels don't show through its see-through backdrop (the settings title sat right on the village 퀘스트 pill).
    /// They come back when the window closes, however it is closed.
    /// </summary>
    public class HideBehind : MonoBehaviour
    {
        readonly List<Canvas> hidden = new List<Canvas>();

        void OnEnable()
        {
            var own = GetComponent<Canvas>();
            int order = own != null ? own.sortingOrder : 0;
            hidden.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (c == own || !c.isRootCanvas || !c.enabled || c.renderMode == RenderMode.WorldSpace || c.sortingOrder >= order) continue;
                c.enabled = false; hidden.Add(c);
            }
        }

        void OnDisable()
        {
            foreach (var c in hidden) if (c != null) c.enabled = true;
            hidden.Clear();
        }
    }
}
