using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Keeps a hanging part (a Ferris-wheel gondola) upright while the part it hangs from turns: it holds its rotation
    /// relative to <see cref="frame"/> (the wheel's standing frame), so only its hanging point travels around the rim.
    /// </summary>
    public class KeepUpright : MonoBehaviour
    {
        public Transform frame;
        Quaternion rest; bool ready;

        void Start()
        {
            if (frame == null) frame = transform.root;
            rest = Quaternion.Inverse(frame.rotation) * transform.rotation; ready = true;
        }

        void LateUpdate()
        {
            if (ready) transform.rotation = frame.rotation * rest;
        }
    }
}
