using UnityEngine;

namespace Mongle.Village
{
    /// <summary>The toy hospital in the village. Clicking it brings the next toy friend in for repairs (D-005).</summary>
    public class HospitalBuilding : MonoBehaviour
    {
        /// <summary>Where a patient stands when greeting the player in front of the door (local space).</summary>
        public Vector3 doorLocal = new Vector3(0f, 0f, 0.55f);
        public Vector3 DoorWorld => transform.TransformPoint(doorLocal);
        float wobble;

        /// <summary>A small "boing" so the click feels answered.</summary>
        public void Poke() { wobble = 1f; }

        void Update()
        {
            if (wobble <= 0f) return;
            wobble = Mathf.Max(0f, wobble - Time.deltaTime * 2.5f);
            float s = 1f + Mathf.Sin(wobble * Mathf.PI * 3f) * 0.025f * wobble;
            transform.localScale = baseScale * s;
        }

        Vector3 baseScale;
        void Awake() { baseScale = transform.localScale; }
    }
}
