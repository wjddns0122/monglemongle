using UnityEngine;

namespace Mongle.Village
{
    /// <summary>Slowly turns a part (e.g. a Ferris wheel); Boost() speeds it up for a moment when someone plays with it.</summary>
    public class SpinPart : MonoBehaviour
    {
        public float degreesPerSecond = 14f;
        float boostLeft;
        public void Boost() { boostLeft = 3f; }
        void Update()
        {
            float k = boostLeft > 0f ? 6f : 1f; boostLeft -= Time.deltaTime;
            transform.Rotate(0f, 0f, -degreesPerSecond * k * Time.deltaTime, Space.Self);
        }
    }
}
