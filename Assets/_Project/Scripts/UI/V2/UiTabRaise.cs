using UnityEngine;

namespace Mongle.UI2
{
    /// <summary>
    /// A window tab (퀘스트 / 보관함): the chosen one stands a little taller than the others and takes its own edge colour, like a
    /// folder tab pulled up. The tabs sit bottom-aligned in their row with their feet under the sheet, so a taller tab rises.
    /// Which tab is chosen comes from its <see cref="UiTabFilter"/> (storage) or its <see cref="UiLink"/>'s first colour rule
    /// (quests). The sizes are set by "줄·열로 정리하기" (UiV2Layout) and can be changed here.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiTabRaise : MonoBehaviour
    {
        [Tooltip("고른 탭의 높이")] public float onHeight = 110f;
        [Tooltip("다른 탭의 높이")] public float offHeight = 100f;
        [Tooltip("고른 탭: 글자를 위에서 얼마나 내릴지")] public float onLabelTop = 13f;
        [Tooltip("다른 탭: 글자를 위에서 얼마나 내릴지")] public float offLabelTop = 8f;
        [Tooltip("고른 탭의 테두리 색")] public Color onEdge = new Color(1f, 0.78f, 0.84f, 1f);
        [Tooltip("다른 탭의 테두리 색")] public Color offEdge = new Color(0.769f, 0.875f, 0.906f, 1f);

        UiTabFilter filter; UiLink link; SoftRect shape; RectTransform label; int shown = -1;

        bool Chosen => filter != null ? filter.IsOn : link != null && link.MatchedRule == 0;

        void Awake() { filter = GetComponent<UiTabFilter>(); link = GetComponent<UiLink>(); }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            int now = Chosen ? 1 : 0;
            if (now == shown) return;
            shown = now;
            Apply(now == 1);
        }

        /// <summary>Puts the tab in its chosen (true) or resting look.</summary>
        public void Apply(bool on)
        {
            var rt = (RectTransform)transform;
            float h = on ? onHeight : offHeight;
            if (!Mathf.Approximately(rt.sizeDelta.y, h)) rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            if (label == null) label = transform.Find("Label") as RectTransform;
            if (label != null) label.anchoredPosition = new Vector2(label.anchoredPosition.x, -(on ? onLabelTop : offLabelTop));
            if (shape == null) shape = GetComponent<SoftRect>();
            if (shape != null && shape.borderColor != (on ? onEdge : offEdge)) { shape.borderColor = on ? onEdge : offEdge; shape.SetAll(); }
        }
    }
}
