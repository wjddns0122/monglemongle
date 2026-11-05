using UnityEngine;

namespace Mongle.Data
{
    /// <summary>A shopkeeper's request (D-003). Condition = an event name (+ optional parameter) counted up to targetCount.</summary>
    [CreateAssetMenu(menuName = "Mongle/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string id;
        public string giverShopId;        // the keeper who asks and pays
        public string title;              // short, shown in the tracker
        [TextArea] public string request; // what the keeper says when asking
        public string acceptLine;         // keeper's answer after accepting
        public string doneLine;           // keeper's line when paying out
        public string eventId;            // e.g. PlantBloomed, ItemPlaced, MongiSat, MongiPlayed, PlantWatered, MongiReacted, ItemBought
        public string eventParam;         // optional: must match the event parameter (usually an item id); empty = any
        public int targetCount = 1;
        public int rewardCoins;
        public string requires;           // quest id that must be done first (empty = available from the start)
    }
}
