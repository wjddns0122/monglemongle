using UnityEngine;

namespace Mongle.Data
{
    [CreateAssetMenu(menuName = "Mongle/Tool Definition")]
    public class ToolDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public string shortHint;     // one-line intent of the tool
        public GameObject prefab;
        public Color uiColor = Color.white;
    }
}
