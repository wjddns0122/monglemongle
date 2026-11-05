using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Mongle.Core
{
    public enum PointerMode { None, Orbit, Tool }

    public struct PointerFrame
    {
        public Vector2 position;
        public Vector2 delta;
        public bool pressed;   // went down this frame
        public bool held;
        public bool released;  // went up this frame
        public float scroll;
        public bool overUI;
    }

    public interface IPointerSource
    {
        Vector2 Position { get; }
        bool Held { get; }
        float Scroll { get; }
    }

    public class MouseSource : IPointerSource
    {
        public Vector2 Position => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public bool Held => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
    }

    /// <summary>Scripted pointer used by automated QA/PlayMode tests; goes through the same code paths as a real mouse.</summary>
    public class ScriptedSource : IPointerSource
    {
        public Vector2 Position { get; set; }
        public bool Held { get; set; }
        public float Scroll { get; set; }
    }

    /// <summary>
    /// Single owner of pointer input. Gameplay reads <see cref="Frame"/>; a press that began over UI is never
    /// delivered to the world until it is released (prevents UI clicks acting on the toy).
    /// </summary>
    public class PointerInput : MonoBehaviour
    {
        public IPointerSource Source = new MouseSource();
        [System.NonSerialized] public PointerFrame Frame;
        public PointerMode Mode = PointerMode.None;
        /// <summary>World-input frame: same as Frame but suppressed when the press began over UI.</summary>
        [System.NonSerialized] public PointerFrame World;
        bool prevHeld;
        bool captureByUI;
        Vector2 prevPos;
        bool hasPrev;
        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        public void UseScripted(ScriptedSource s) { Source = s; }
        public void UseMouse() { Source = new MouseSource(); }

        void Update() { Poll(); }

        public void Poll()
        {
            var f = new PointerFrame();
            f.position = Source.Position;
            f.held = Source.Held;
            f.pressed = f.held && !prevHeld;
            f.released = !f.held && prevHeld;
            f.scroll = Source.Scroll;
            f.delta = hasPrev ? f.position - prevPos : Vector2.zero;
            f.overUI = IsOverUI(f.position);
            if (f.pressed) captureByUI = f.overUI;
            Frame = f;
            var w = f;
            if (captureByUI && (f.held || f.released)) { w.held = false; w.pressed = false; w.released = false; }
            if (f.overUI) w.scroll = 0f;
            World = w;
            if (f.released) captureByUI = false;
            prevHeld = f.held; prevPos = f.position; hasPrev = true;
        }

        public static bool IsOverUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = pos };
            hits.Clear();
            es.RaycastAll(ped, hits);
            return hits.Count > 0;
        }
    }
}
