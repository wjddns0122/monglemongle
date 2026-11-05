using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Scenery standing inside the park (D-006: shops, the hospital, the castle, trees, bushes). The scene builder cuts its
    /// footprint out of the walkable area; at runtime the furniture grid keeps it free as well.
    /// </summary>
    public class ParkObstacle : MonoBehaviour
    {
        public Vector3 center;     // world, on the ground
        public Vector2 halfSize;   // x / z half extents of the axis-aligned footprint
    }
}
