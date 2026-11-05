using UnityEngine;

namespace Mongle.Village
{
    /// <summary>Lazy floating for sky decorations (hot-air balloons): slow bob, a small wandering drift and a gentle turn.</summary>
    public class FloatDrift : MonoBehaviour
    {
        public float bob = 0.3f, bobPeriod = 7f, drift = 0.5f, degreesPerSecond = 3f;
        Vector3 basePos; float phase;

        void Start()
        {
            basePos = transform.localPosition;
            phase = basePos.x * 1.7f + basePos.z * 0.9f; // different balloons never move in step
        }

        void Update()
        {
            float t = Time.time;
            transform.localPosition = basePos + new Vector3(Mathf.Sin(t * 0.13f + phase) * drift,
                Mathf.Sin(t * Mathf.PI * 2f / bobPeriod + phase) * bob, Mathf.Cos(t * 0.11f + phase) * drift);
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
