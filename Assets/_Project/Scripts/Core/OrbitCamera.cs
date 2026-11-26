using UnityEngine;

namespace Mongle.Core
{
    /// <summary>
    /// Orbit/zoom camera around a target (D-001-2: all values are tunable fields). A focus shot temporarily
    /// overrides the orbit (used for repair close-ups); ReleaseFocus returns smoothly to the orbit pose.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 target = new Vector3(0, 0.22f, 0);
        public float distance = 1.15f, minDistance = 0.6f, maxDistance = 2.0f;
        public float yaw = 0f, pitch = 14f, minPitch = -4f, maxPitch = 60f;
        /// <summary>Dragging the pitch below this keeps the camera near the ground and tilts the view up instead (to look at the sky).</summary>
        public float lookUpBelow = -90f;
        public float degreesPerPixel = 0.22f;
        public float zoomPerScroll = 0.004f;
        public float smooth = 0.12f;
        public float dragThresholdPixels = 6f;
        public bool allowRotate = true;
        public bool allowZoom = true;
        /// <summary>Right/middle mouse drag slides the target over the ground, kept inside panMin..panMax (x, z).</summary>
        public bool allowPan = false;
        public Vector2 panMin = new Vector2(-10f, -10f), panMax = new Vector2(10f, 10f);
        public bool useWorldInput = true;
        [Range(0f, 1f)] public float focusFov = 0f;

        public bool Focusing { get; private set; }
        public bool WasDragging { get; private set; }
        public float FocusBlend { get; private set; }

        Vector3 focusPos, focusLook;
        Vector3 velPos; Vector3 curLook;
        Vector2 pressPos; bool pressing;
        float targetDistance; float targetYaw, targetPitch;
        Camera cam;
        float baseFov;
        Quaternion rotVel;
        bool initialized;

        void Awake() { cam = GetComponent<Camera>(); baseFov = cam.fieldOfView; Reset(); }

        public void Reset()
        {
            targetDistance = distance; targetYaw = yaw; targetPitch = pitch;
            var p = OrbitPosition(yaw, pitch, distance);
            transform.position = p; curLook = target; transform.rotation = Quaternion.LookRotation(target - p, Vector3.up);
            initialized = true;
        }

        public Vector3 OrbitPosition(float y, float p, float d)
        {
            var rot = Quaternion.Euler(-p, y, 0); // positive pitch = camera above the target
            return target + rot * new Vector3(0, 0, d);
        }

        Transform focusLookTransform; Vector3 focusLookOffset;
        public void FocusOn(Vector3 cameraPosition, Vector3 lookAt) { focusPos = cameraPosition; focusLook = lookAt; focusLookTransform = null; followPos = null; Focusing = true; }
        /// <summary>Focus that travels with something moving (a ride gondola): both points are asked for every frame.</summary>
        public void FocusFollow(System.Func<Vector3> cameraPosition, System.Func<Vector3> lookAt) { followPos = cameraPosition; followLook = lookAt; focusLookTransform = null; Focusing = true; }
        System.Func<Vector3> followPos, followLook;
        /// <summary>Focus that keeps looking at a moving node (animated ear etc.), keeping the initial offset from it.</summary>
        public void FocusOn(Vector3 cameraPosition, Transform lookAt, Vector3 lookAtWorldNow) { followPos = null; focusPos = cameraPosition; focusLookTransform = lookAt; focusLookOffset = lookAt != null ? lookAt.InverseTransformPoint(lookAtWorldNow) : Vector3.zero; focusLook = lookAtWorldNow; Focusing = true; }
        public void ReleaseFocus() { Focusing = false; followPos = null; followLook = null; }

        public void SetOrbit(float y, float p, float d) { targetYaw = yaw = y; targetPitch = pitch = p; targetDistance = distance = d; }
        public void ZoomStep(float factor) { targetDistance = Mathf.Clamp(targetDistance * factor, minDistance, maxDistance); }

        void LateUpdate()
        {
            if (!initialized) Reset();
            var root = GameRoot.Instance;
            var ptr = root != null ? root.Pointer : null;
            WasDragging = false;
            if (ptr != null && ptr.Mode == PointerMode.Orbit && !Focusing)
            {
                var f = useWorldInput ? ptr.World : ptr.Frame;
                if (f.pressed) { pressPos = f.position; pressing = true; }
                if (pressing && f.held && allowRotate)
                {
                    if ((f.position - pressPos).magnitude > dragThresholdPixels)
                    {
                        WasDragging = true;
                        float dpp = degreesPerPixel * GameSettings.MouseScale;   // settings: mouse sensitivity (D-038)
                        targetYaw -= f.delta.x * dpp;
                        targetPitch = Mathf.Clamp(targetPitch - f.delta.y * dpp, minPitch, maxPitch);
                    }
                }
                if (f.released || !f.held) pressing = false;
                if (allowZoom && Mathf.Abs(f.scroll) > 0.001f)
                    targetDistance = Mathf.Clamp(targetDistance * (1f - Mathf.Clamp(f.scroll * zoomPerScroll, -0.15f, 0.15f)), minDistance, maxDistance);
                if (allowPan) Pan();
                PadOrbit();
            }
            if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov * GameSettings.FovScale;   // settings: field of view (D-038)

            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, smooth));
            yaw = Mathf.LerpAngle(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            distance = Mathf.Lerp(distance, targetDistance, k);

            Vector3 wantPos; Vector3 wantLook;
            if (Focusing && followPos != null) { wantPos = followPos(); wantLook = followLook(); }
            else if (Focusing) { wantPos = focusPos; wantLook = focusLookTransform != null ? focusLookTransform.TransformPoint(focusLookOffset) : focusLook; }
            else
            {
                wantPos = OrbitPosition(yaw, Mathf.Max(pitch, lookUpBelow), distance); wantLook = target;
                if (pitch < lookUpBelow) wantLook += Vector3.up * (distance * Mathf.Tan((lookUpBelow - pitch) * Mathf.Deg2Rad));
            }
            transform.position = Vector3.SmoothDamp(transform.position, wantPos, ref velPos, smooth * 1.4f, Mathf.Infinity, Time.unscaledDeltaTime);
            curLook = Vector3.Lerp(curLook, wantLook, k);
            var dir = curLook - transform.position;
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            FocusBlend = Mathf.MoveTowards(FocusBlend, Focusing ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }

        /// <summary>Slides the target over the ground (keyboard panning), kept inside the pan bounds.</summary>
        public void Slide(Vector3 delta)
        {
            target += new Vector3(delta.x, 0f, delta.z);
            if (allowPan) { target.x = Mathf.Clamp(target.x, panMin.x, panMax.x); target.z = Mathf.Clamp(target.z, panMin.y, panMax.y); }
        }

        /// <summary>Controller: right stick orbits, left stick slides (where panning is allowed), shoulders zoom; speed = controller sensitivity (D-038).</summary>
        void PadOrbit()
        {
            var pad = UnityEngine.InputSystem.Gamepad.current; if (pad == null) return;
            float s = GameSettings.PadScale * Time.unscaledDeltaTime;
            var r = pad.rightStick.ReadValue();
            if (allowRotate && r.sqrMagnitude > 0.02f) { targetYaw += r.x * 120f * s; targetPitch = Mathf.Clamp(targetPitch - r.y * 90f * s, minPitch, maxPitch); }
            float z = (pad.rightShoulder.isPressed ? 1f : 0f) - (pad.leftShoulder.isPressed ? 1f : 0f);
            if (allowZoom && z != 0f) targetDistance = Mathf.Clamp(targetDistance * (1f - z * 1.2f * s), minDistance, maxDistance);
            var l = pad.leftStick.ReadValue();
            if (allowPan && l.sqrMagnitude > 0.02f)
            {
                var right = transform.right; right.y = 0; right.Normalize(); var fwd = transform.forward; fwd.y = 0; fwd.Normalize();
                Slide((right * l.x + fwd * l.y) * (distance * 0.9f * s));
            }
        }

        void Pan()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current; if (mouse == null) return;
            if (!(mouse.rightButton.isPressed || mouse.middleButton.isPressed)) return;
            var d = mouse.delta.ReadValue(); if (d.sqrMagnitude < 0.01f) return;
            // move the ground under the pointer: scale by how far away the ground is
            var right = transform.right; right.y = 0; right.Normalize();
            var fwd = transform.forward; fwd.y = 0; fwd.Normalize();
            float k = distance * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            target -= (right * d.x + fwd * d.y * 1.4f) * k;
            target.x = Mathf.Clamp(target.x, panMin.x, panMax.x); target.z = Mathf.Clamp(target.z, panMin.y, panMax.y);
        }

        /// <summary>Small camera punch used by completion feedback.</summary>
        public void Punch(float amount = 0.02f) { if (isActiveAndEnabled) StartCoroutine(PunchRoutine(amount)); }

        System.Collections.IEnumerator PunchRoutine(float amount)
        {
            float t = 0;
            var baseP = Vector3.zero;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Sin(t * 60f) * Mathf.Exp(-t * 14f) * amount;
                transform.position += transform.up * s;
                yield return null;
            }
        }
    }
}
