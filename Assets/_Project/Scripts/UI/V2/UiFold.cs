using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// A panel that folds up to its header and opens again (the care notebook). Pressing one of <see cref="toggles"/> (its
    /// button, its title) swaps. Folded, only the <see cref="keep"/> pieces show and the list in the panel is hidden; the panel's
    /// top edge stays where it is. Works in the game only (in the editor the panel is shown open).
    /// </summary>
    [DisallowMultipleComponent]
    public class UiFold : MonoBehaviour
    {
        [Tooltip("누르면 접었다 폈다 하는 버튼들 (단추, 제목)")] public Button[] toggles = new Button[0];
        [Tooltip("접혔을 때도 보이는 것 (단추, 별, 제목)")] public RectTransform[] keep = new RectTransform[0];
        [Tooltip("접혔을 때 남는 높이")] public float foldedHeight = 100f;
        [Tooltip("처음에 접힌 채로 시작해요")] public bool startFolded;
        [Tooltip("접히고 펴지는 시간 (초)")] public float time = 0.18f;

        public bool Folded { get; private set; }

        RectTransform rt; UiLinkList list; bool ready;
        float openHeight, top, shown;   // shown: 0 folded … 1 open
        readonly List<GameObject> tucked = new List<GameObject>();

        void Awake() { Ready(); }

        void Ready()
        {
            if (ready) return; ready = true;
            rt = (RectTransform)transform; list = GetComponent<UiLinkList>();
            openHeight = rt.sizeDelta.y;
            top = rt.anchoredPosition.y + (1f - rt.pivot.y) * openHeight;
            foreach (var b in toggles) if (b != null) b.onClick.AddListener(Toggle);
            Folded = startFolded; shown = Folded ? 0f : 1f;
            Apply();
        }

        public void Toggle() { Ready(); Folded = !Folded; }

        /// <summary>Folds or opens; <paramref name="instant"/> skips the movement.</summary>
        public void SetFolded(bool folded, bool instant = false)
        {
            Ready(); Folded = folded;
            if (instant) { shown = folded ? 0f : 1f; Apply(); }
        }

        void LateUpdate()
        {
            Ready();
            float target = Folded ? 0f : 1f;
            if (Mathf.Approximately(shown, target)) { Hide(shown < 0.999f); return; }
            shown = Mathf.MoveTowards(shown, target, time > 0f ? Time.unscaledDeltaTime / time : 1f);
            Apply();
        }

        void Apply()
        {
            float t = shown * shown * (3f - 2f * shown);   // ease in and out
            float h = Mathf.Lerp(foldedHeight, openHeight, t);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            var p = rt.anchoredPosition; p.y = top - (1f - rt.pivot.y) * h; rt.anchoredPosition = p;
            Hide(shown < 0.999f);   // the rows come back once the panel is fully open
        }

        /// <summary>Hides (or shows again) everything but the header while the panel is not fully open.</summary>
        void Hide(bool hide)
        {
            if (list != null) list.hidden = hide;
            if (hide)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var c = transform.GetChild(i);
                    if (!c.gameObject.activeSelf || Kept(c) || (list != null && list.Owns(c))) continue;
                    c.gameObject.SetActive(false); tucked.Add(c.gameObject);
                }
            }
            else if (tucked.Count > 0)
            {
                foreach (var g in tucked) if (g != null) g.SetActive(true);
                tucked.Clear();
            }
        }

        bool Kept(Transform c)
        {
            foreach (var k in keep) if (k == c) return true;
            return false;
        }
    }
}
