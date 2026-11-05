using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Ground the playground (D-011) blocks, as boxes in its own space (filled in when the prefab is built): the scene cuts
    /// them out of the walkable ground, and the furniture grid keeps them free. The rides' boarding spots stay outside them.
    /// </summary>
    public class PlaygroundLayout : MonoBehaviour
    {
        public Vector3[] boxCenters = new Vector3[0];
        public Vector3[] boxSizes = new Vector3[0];
    }
}
