using UnityEngine;

namespace Mongle.Data
{
    [CreateAssetMenu(menuName = "Mongle/Toy Definition")]
    public class ToyDefinition : ScriptableObject
    {
        public string kindId = "TeddyBear_001";
        public string displayName = "몽이";
        public ToyCategory category;
        public Personality personality;
        public InteractionTag favoriteTags;
        [TextArea] public string shortBackstory;
        public GameObject prefab;
        public float heightMeters = 0.45f;
        /// <summary>Repair order is data (D-001-5). Order of this array is the repair order.</summary>
        public DamageDefinition[] damageSequence;
        public AccessoryDefinition[] accessories;
        /// <summary>Mongi's free gifts for its first dress-up (before the village has a shop); empty = <see cref="accessories"/>.</summary>
        public AccessoryDefinition[] giftAccessories;
        public int stampRewardCoins = 30; // D-002
        /// <summary>Optional per-toy wording for shared damage definitions ("안테나의 가시" on the robot, D-005).</summary>
        public DamageWording[] wording;
    }

    [System.Serializable]
    public class DamageWording { public string id, displayName, noteText; }
}
