using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// Responsive canvas scaling for the v2 UI (D-038): reference 1920x1080. On screens at least as wide as 16:9 the UI
    /// follows the height (extra width spreads the corner buttons apart); on narrower screens (16:10, 4:3, small windows)
    /// it follows the width, so nothing is ever cut off at the sides.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteAlways]
    public class AdaptiveScaler : MonoBehaviour
    {
        CanvasScaler cs;
        void OnEnable() { cs = GetComponent<CanvasScaler>(); Apply(); }
        void Update() { Apply(); }
        void Apply()
        {
            if (cs == null) return;
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            var canvas = GetComponent<Canvas>();
            Vector2 size = canvas != null && canvas.renderMode != RenderMode.WorldSpace ? canvas.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
            if (size.x <= 0 || size.y <= 0) size = new Vector2(Screen.width, Screen.height);
            float aspect = size.y > 0 ? size.x / size.y : 16f / 9f;
            cs.matchWidthOrHeight = aspect >= 16f / 9f - 0.001f ? 1f : 0f;
        }
    }
}
