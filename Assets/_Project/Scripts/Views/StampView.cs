using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The "수선 완료!" stamp card (Figma 11-26 88:1315): a grid sheet with eight round slots — one paw stamp per friend that
    /// has moved in — and the pink "도장 찍기!" button that stamps the next slot (the paw lands big and settles, the card
    /// shakes). Prefab Assets/_UI수정/2_수선점/Stamp.prefab, opened by the repair shop with <see cref="Open"/>.
    /// </summary>
    public class StampView : MonoBehaviour
    {
        public RectTransform card;
        [Tooltip("발바닥 도장 (칸마다 하나, 왼쪽 위부터)")] public GameObject[] marks = new GameObject[0];
        public Button stampButton;

        int next;
        Action onStamp;
        static StampView current;

        public static bool IsOpen => current != null && current.isActiveAndEnabled;

        /// <summary>Shows the card with <paramref name="filled"/> stamps already in it; the button calls <paramref name="stamp"/>.</summary>
        public static StampView Open(int filled, Action stamp)
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Stamp");
                if (prefab == null) { stamp?.Invoke(); return null; }
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<StampView>(); current.name = "Stamp";
            }
            current.Show(filled, stamp);
            return current;
        }

        public static void Close() { if (current != null) current.gameObject.SetActive(false); }

        void Awake() { if (stampButton != null) stampButton.onClick.AddListener(() => onStamp?.Invoke()); }
        void OnDestroy() { if (current == this) current = null; }

        public void Show(int filled, Action stamp)
        {
            onStamp = stamp;
            next = Mathf.Clamp(filled, 0, Mathf.Max(0, marks.Length - 1));
            for (int i = 0; i < marks.Length; i++) { marks[i].SetActive(i < next); marks[i].transform.localScale = Vector3.one; }
            if (stampButton != null) stampButton.interactable = true;
            gameObject.SetActive(true);
        }

        /// <summary>The new paw lands in the next slot (big, then settles) and the card gives a little shake.</summary>
        public void PlayStamp(Action onDone)
        {
            if (stampButton != null) stampButton.interactable = false;
            StartCoroutine(Stamp(onDone));
        }

        IEnumerator Stamp(Action onDone)
        {
            if (next < marks.Length)
            {
                var mark = marks[next]; mark.SetActive(true);
                float t = 0;
                while (t < 0.18f) { t += Time.unscaledDeltaTime; mark.transform.localScale = Vector3.one * Mathf.Lerp(3.2f, 1f, t / 0.18f); yield return null; }
                mark.transform.localScale = Vector3.one;
            }
            if (card != null)
            {
                float t = 0; var p0 = card.anchoredPosition;
                while (t < 0.3f) { t += Time.unscaledDeltaTime; card.anchoredPosition = p0 + new Vector2(0, Mathf.Sin(t * 70) * 10 * (1 - t / 0.3f)); yield return null; }
                card.anchoredPosition = p0;
            }
            onDone?.Invoke();
        }
    }
}
