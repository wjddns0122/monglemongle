using UnityEngine;

namespace Mongle.UI
{
    /// <summary>
    /// D-039: where the editable screen prefabs are. They live in the UI folder (Assets/_UI수정, one subfolder per scene) instead
    /// of Resources, so a designer finds them next to their scene; this list (Resources/UI/SkinIndex.asset) is what
    /// <see cref="UiSkin.Find"/> reads in the game. The editor keeps it filled (UiEditFolder).
    /// </summary>
    public class UiSkinIndex : ScriptableObject
    {
        public const string ResourcePath = "UI/SkinIndex";
        public GameObject[] skins = new GameObject[0];
        /// <summary>D-040: the Figma screens shown in front of the code's screens (UiFront), and the settings window.</summary>
        public GameObject[] fronts = new GameObject[0];

        public GameObject GetFront(string name)
        {
            if (fronts == null) return null;
            foreach (var s in fronts) if (s != null && s.name == name) return s;
            return null;
        }

        public GameObject Get(string name)
        {
            if (skins == null) return null;
            foreach (var s in skins) if (s != null && s.name == name) return s;
            return null;
        }
    }
}
