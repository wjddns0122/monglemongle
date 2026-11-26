using TMPro;
using UnityEngine;

namespace Mongle.Views
{
    /// <summary>
    /// The notice that comes and goes (Figma 11-26 "인게임3" 88:1767, the "몽글 코인 + 50" pill 88:1848–88:1850): a cream pill
    /// (#FAF0DF at 60 %) that hugs its words, the Figma coin in front of a coin notice. Prefab Assets/_UI수정/5_공통/Notice.prefab,
    /// listed as "Notice"; GameRoot keeps one across scenes (GameRoot.Say) and moves the pill out of the way of windows.
    /// </summary>
    public class NoticeView : MonoBehaviour
    {
        [Tooltip("알약 (글자에 맞춰 늘어나요)")] public RectTransform pill;
        public TMP_Text text;
        [Tooltip("코인 알림에만 보이는 코인")] public GameObject coin;
        [Tooltip("알약 양끝 여백")] public float pad = 51.46f;
        [Tooltip("코인과 글자 사이")] public float coinGap = 25.8f;
        [Tooltip("코인 너비")] public float coinSize = 56.28f;
        public float minWidth = 200f, maxWidth = 1700f;

        public bool Shown => pill != null && pill.gameObject.activeSelf;
        public string TextShown => Shown ? text.text : "";
        public bool CoinShown => Shown && coin != null && coin.activeSelf;

        /// <summary>Shows <paramref name="msg"/> (a "몽글 코인" one with the coin in front).</summary>
        public void Show(string msg)
        {
            bool withCoin = msg != null && msg.Contains("몽글 코인");
            text.text = msg ?? "";
            if (coin != null) coin.SetActive(withCoin);
            float lead = withCoin ? coinSize + coinGap : 0f;
            var size = text.GetPreferredValues(text.text, maxWidth - 2f * pad - lead, 0f);
            float w = Mathf.Clamp(size.x + 2f * pad + lead, minWidth, maxWidth);
            pill.sizeDelta = new Vector2(w, pill.sizeDelta.y);
            var rt = text.rectTransform;
            rt.offsetMin = new Vector2(pad + lead, rt.offsetMin.y); rt.offsetMax = new Vector2(-pad, rt.offsetMax.y);
            pill.gameObject.SetActive(true);
        }

        public void Hide() { if (pill != null) pill.gameObject.SetActive(false); }
    }
}
