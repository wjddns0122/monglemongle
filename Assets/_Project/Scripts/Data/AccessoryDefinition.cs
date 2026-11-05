using UnityEngine;

namespace Mongle.Data
{
    [CreateAssetMenu(menuName = "Mongle/Accessory Definition")]
    public class AccessoryDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public AccessorySlot slot;
        public string attachPointName; // AP_HEAD / AP_FACE / AP_NECK
        public GameObject prefab;
        public Color uiColor = Color.white;
    }
}
