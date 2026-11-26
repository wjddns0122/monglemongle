using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>D-040: a tab that narrows a <see cref="UiLinkList"/> to items whose names contain <see cref="filter"/> ("" = all).</summary>
    public class UiTabFilter : MonoBehaviour
    {
        public UiLinkList list;
        [Tooltip("이 글자가 이름에 든 항목만 보여요 (',' 로 여러 개, 비우면 전부)")] public string filter;
        public Color on = Color.white, off = Color.white;
        Graphic g; bool ready;

        /// <summary>Whether this tab is the one chosen now.</summary>
        public bool IsOn => list != null && (list.filter ?? "") == (filter ?? "");

        void Prepare()
        {
            if (ready) return; ready = true;
            g = GetComponent<Graphic>();
            var b = GetComponent<Button>(); if (b == null) b = gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            UiLink.Clickable(b);   // the Figma tab plates came with Raycast Target off: no clicks reached them
            b.onClick.AddListener(() => { if (list != null) { list.filter = filter ?? ""; list.page = 0; } });
        }

        public void Sync()
        {
            Prepare();
            if (g == null || list == null) return;
            var c = (list.filter ?? "") == (filter ?? "") ? on : off;
            if (g.color != c) g.color = c;
        }
    }
}
