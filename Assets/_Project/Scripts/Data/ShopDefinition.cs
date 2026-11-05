using UnityEngine;

namespace Mongle.Data
{
    /// <summary>A village shop: what it sells and what its keeper says. Static content only.</summary>
    [CreateAssetMenu(menuName = "Mongle/Shop Definition")]
    public class ShopDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public string keeperName;
        public FurnitureDefinition[] items;
        public string[] greetings;   // every visit
        public string[] buyLines;    // after a purchase
        public string[] poorLines;   // not enough coins
        public string firstVisitLine;
        public string pokeLine;      // clicking the keeper again while the shop is closed
        public Vector3 position;
        public float yaw;
        public Color wallColor = Color.white;
        public Color roofColor = Color.white;
        public Color keeperColor = Color.white;
    }
}
