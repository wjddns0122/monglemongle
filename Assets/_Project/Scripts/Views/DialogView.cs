using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The message box of the 11-26 design (Figma 88:1683 "몽이가 왔어요", also 88:510 "몽이의 새집"): a cream sheet with a blue
    /// band for the title, the words under it and one or two pill buttons — the second (peach, e.g. 죄송합니다) on the left, the
    /// main one (blue, e.g. 살펴볼게요) on the right. The sheet grows with long words. One prefab
    /// (Assets/_UI수정/5_공통/Dialog.prefab) serves every scene: <see cref="Open"/> brings it up.
    /// </summary>
    public class DialogView : MonoBehaviour
    {
        [Header("부품")]
        public RectTransform panel;
        public TMP_Text title, body;
        [Tooltip("오른쪽 하늘색 버튼 (확인·살펴볼게요 …)")] public Button okButton;
        [Tooltip("왼쪽 복숭아색 버튼 (죄송합니다·취소 …), 없으면 숨어요")] public Button secondButton;
        [Header("크기 (피그마)")]
        [Tooltip("판의 가장 작은 폭")] public float minWidth = 1182.03f;
        [Tooltip("판의 가장 큰 폭")] public float maxWidth = 1760f;
        [Tooltip("글자 양옆 여백")] public float sidePad = 120f;
        [Tooltip("판 위 끝 ~ 하늘색 띠 아래 끝")] public float headerHeight = 113.47f;
        [Tooltip("띠 ~ 글 사이")] public float bodyTop = 70.36f;
        [Tooltip("글 ~ 버튼 사이")] public float bodyBottom = 40.36f;
        [Tooltip("버튼 높이")] public float buttonHeight = 86.57f;
        [Tooltip("버튼 ~ 판 아래 끝")] public float bottomPad = 39.76f;
        [Tooltip("버튼의 가장 작은 폭")] public float buttonMinWidth = 344.78f;
        [Tooltip("버튼 글자 양옆 여백")] public float buttonPad = 40f;

        Action onOk, onSecond;
        static DialogView current;

        /// <summary>Whether a message box is up (in this scene).</summary>
        public static bool IsOpen => current != null && current.isActiveAndEnabled;

        /// <summary>Shows the message box: <paramref name="ok"/> is the main (right, blue) button, <paramref name="second"/> the optional left one.</summary>
        public static DialogView Open(string title, string body, string ok, Action onOk, string second = null, Action onSecond = null)
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Dialog");
                if (prefab == null) { onOk?.Invoke(); return null; }   // no box: go on as if confirmed rather than getting stuck
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<DialogView>();
                current.name = "Dialog";
            }
            current.Show(title, body, ok, onOk, second, onSecond);
            return current;
        }

        public static void CloseCurrent() { if (current != null) current.Hide(); }

        void Awake()
        {
            if (okButton != null) okButton.onClick.AddListener(() => Answer(true));
            if (secondButton != null) secondButton.onClick.AddListener(() => Answer(false));
        }

        void OnDestroy() { if (current == this) current = null; }

        void Answer(bool ok)
        {
            var a = ok ? onOk : onSecond;
            onOk = onSecond = null;
            Hide();
            a?.Invoke();
        }

        public void Hide() { gameObject.SetActive(false); }

        public void Show(string titleText, string bodyText, string ok, Action okAction, string second = null, Action secondAction = null)
        {
            onOk = okAction; onSecond = secondAction;
            if (title != null) title.text = titleText ?? "";
            if (body != null) body.text = bodyText ?? "";
            SetButton(okButton, ok);
            bool two = !string.IsNullOrEmpty(second);
            if (secondButton != null) { secondButton.gameObject.SetActive(two); if (two) SetButton(secondButton, second); }
            gameObject.SetActive(true);
            Fit();
        }

        void SetButton(Button b, string words)
        {
            if (b == null) return;
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = words ?? "";
            var rt = (RectTransform)b.transform;
            float w = Mathf.Max(buttonMinWidth, (label != null ? label.preferredWidth : 0f) + buttonPad * 2f);
            rt.sizeDelta = new Vector2(w, buttonHeight);
        }

        /// <summary>The sheet: the design size, wider for a long title / line / pair of buttons, taller for more lines.</summary>
        public void Fit()
        {
            if (panel == null) return;
            float buttons = Width(okButton) + (secondButton != null && secondButton.gameObject.activeSelf ? Width(secondButton) + Gap() : 0f);
            float words = Mathf.Max(title != null ? title.preferredWidth : 0f, body != null ? Unwrapped(body) : 0f);
            float w = Mathf.Clamp(Mathf.Max(minWidth, words + sidePad * 2f, buttons + sidePad * 2f), minWidth, maxWidth);
            float bodyH = 0f;
            if (body != null)
            {
                bodyH = string.IsNullOrEmpty(body.text) ? 0f : body.GetPreferredValues(body.text, w - sidePad * 2f, 0f).y;
                body.rectTransform.sizeDelta = new Vector2(w - sidePad * 2f, bodyH);   // anchored at the band's foot, centred
                body.rectTransform.anchoredPosition = new Vector2(body.rectTransform.anchoredPosition.x, -(headerHeight + bodyTop));
            }
            if (okButton != null && okButton.transform.parent is RectTransform row)
                row.anchoredPosition = new Vector2(row.anchoredPosition.x, bottomPad);   // the buttons' row sits bottomPad over the foot
            float h = headerHeight + bodyTop + bodyH + bodyBottom + buttonHeight + bottomPad;
            panel.sizeDelta = new Vector2(w, h);
        }

        static float Width(Button b) => b != null ? ((RectTransform)b.transform).sizeDelta.x : 0f;
        float Gap() { var row = okButton != null ? okButton.transform.parent.GetComponent<HorizontalLayoutGroup>() : null; return row != null ? row.spacing : 27.83f; }

        static float Unwrapped(TMP_Text t)
        {
            float widest = 0f;
            foreach (var line in (t.text ?? "").Split('\n')) widest = Mathf.Max(widest, t.GetPreferredValues(line, 99999f, 0f).x);
            return widest;
        }
    }
}
