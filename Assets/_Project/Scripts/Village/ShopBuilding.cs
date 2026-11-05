using Mongle.Data;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>A shop in the village scene. Static content comes from the ShopDefinition with the same id.</summary>
    public class ShopBuilding : MonoBehaviour
    {
        public string shopId;
        public Transform CameraSpot;      // where the camera stands while the shop is open
        public KeeperActor Keeper;
        public ShopDefinition Def { get; set; }
        public Transform KeeperHead => Keeper != null && Keeper.Head != null ? Keeper.Head : transform;
        public Vector3 DoorWorld => CameraSpot != null ? new Vector3(CameraSpot.position.x, 0, CameraSpot.position.z) : transform.position;
    }
}
