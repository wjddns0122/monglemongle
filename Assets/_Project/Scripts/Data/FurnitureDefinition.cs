using UnityEngine;

namespace Mongle.Data
{
    /// <summary>Accessory = something a friend wears (sold at the accessory shop; it is never placed in the village).</summary>
    public enum ItemCategory { Furniture, Plant, Accessory }

    [CreateAssetMenu(menuName = "Mongle/Furniture Definition")]
    public class FurnitureDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public GameObject prefab;
        public Vector2Int footprint = Vector2Int.one; // in grid cells at rotation 0 (x = width, y = depth)
        public InteractionTag tags;
        public int capacity = 1;
        public Vector2Int defaultCell;

        [Header("Shop (D-002)")]
        public ItemCategory category = ItemCategory.Furniture;
        public int price;                 // 0 = not for sale (starter piece)
        [TextArea] public string description;
        public Color uiColor = Color.white;
        /// <summary>Starter pieces come with the village and cannot be put back into the storage box.</summary>
        public bool starter;

        [Header("Plant growth (category = Plant)")]
        public int[] stageWaters = { 2, 5 };      // cumulative waterings needed to reach stage 1, 2, ...
        public float waterCooldownSeconds = 25f;  // real seconds before the plant is thirsty again
        public int bloomReward = 15;              // coins, once per plant, when fully grown

        public bool IsPlant => category == ItemCategory.Plant;
        public bool IsAccessory => category == ItemCategory.Accessory;
        public bool Placeable => !IsAccessory;
        public bool Storable => !IsPlant && !starter && !IsAccessory;
        public int MaxStage => stageWaters != null ? stageWaters.Length : 0;
    }
}
