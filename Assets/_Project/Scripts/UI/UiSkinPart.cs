using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mongle.UI
{
    /// <summary>
    /// D-035: marks an object of a screen prefab (Resources/UI/Skins) that the game's code makes. It remembers how the object looked
    /// when the prefab was made (<see cref="baked"/>), so only what the designer changed afterwards is laid over the game's screen —
    /// everything left as it was stays the code's (texts it writes, colours it switches, layouts it computes).
    /// Objects without this component are the designer's own and appear in the game as they are.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiSkinPart : MonoBehaviour
    {
        [Tooltip("체크하면 게임에서 이 오브젝트가 보이지 않아요")] public bool hideInGame;
        [HideInInspector] public string bakedPath;
        /// <summary>A list the code fills (cards, rows, choice buttons): children whose names start with one of these
        /// ("," between, "*" for any) are made by the code, and the first such child here is the model for all of them.</summary>
        [HideInInspector] public string listOf;
        [HideInInspector] public UiSkinSnapshot baked;
    }

    /// <summary>How a part looked when the prefab was made: its place and the settings of its look components.</summary>
    [Serializable]
    public class UiSkinSnapshot
    {
        public bool valid;
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition;
        public Vector3 scale, euler;
        public List<UiSkinLook> looks = new List<UiSkinLook>();

        public UiSkinLook Find(string type, int index)
        {
            foreach (var l in looks) if (l.type == type && l.index == index) return l;
            return null;
        }
    }

    /// <summary>One look component (Image, Text …) as it was: its settings as JSON without object references, plus the references
    /// that matter (the picture and the font) kept as real links.</summary>
    [Serializable]
    public class UiSkinLook
    {
        public string type; public int index; public bool enabled;
        public string json;
        public UnityEngine.Object picture, font;
    }
}
