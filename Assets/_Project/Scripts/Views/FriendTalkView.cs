using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// A friend talking to the player (Figma 11-26 "대화1" 88:231, "대화2" 88:367): the white bubble with the friend's name tag,
    /// its line and a small peach line under it, and on the right either the two pills of a request (♥ 좋아! / 다음에 할게), a
    /// drop-down of answers to a question, or the white date pill with its 월 / 일 drop-downs for the birthday question. Prefab
    /// Assets/_UI수정/3_마을/FriendTalk.prefab, opened with <see cref="Get"/>; the village only hands it words and what to do.
    /// </summary>
    public class FriendTalkView : MonoBehaviour
    {
        [Header("말풍선")]
        public RectTransform bubble;
        public SoftRect nameTag;
        public TMP_Text nameText, lineText, subText;
        public RectTransform next;
        [Tooltip("이름표 글자 양옆 여백")] public float namePad = 34f;
        [Header("부탁 (좋아! / 다음에 할게)")]
        public Button yesButton, laterButton;
        public TMP_Text yesLabel, laterLabel;
        [Tooltip("고른 쪽의 하늘색 테두리 (좋아! · 다음에 할게 · 질문 칸)")] public Graphic yesRing, laterRing, answerRing;
        [Tooltip("고른 쪽의 하트")] public GameObject yesHeart, laterHeart;
        [Header("질문 (고르기)")]
        public TMP_Dropdown answer;
        [Header("생일 (월 / 일)")]
        public GameObject datePanel;
        public TMP_Dropdown month, day;
        [Tooltip("열린 목록일 때 위로 뒤집히는 화살표")] public RectTransform[] arrows = new RectTransform[0];

        Action onYes, onLater;
        Action<string> onAnswer;
        Action<int, int> onDate;
        Func<int, int> daysIn;
        List<string> answers = new List<string>();
        static FriendTalkView current;

        public bool Visible => isActiveAndEnabled;
        public string LineShown => lineText != null ? lineText.text : "";
        public string NameShown => nameText != null ? nameText.text : "";

        /// <summary>The window (the prefab listed as "FriendTalk"), made once per scene.</summary>
        public static FriendTalkView Get()
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("FriendTalk");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<FriendTalkView>(); current.name = "FriendTalk";
                current.gameObject.SetActive(false);
            }
            return current;
        }

        bool hooked;
        void Awake() => Hook();

        /// <summary>The buttons and drop-downs call back here (once; also on the first line shown, for the tests in edit mode).</summary>
        void Hook()
        {
            if (hooked) return; hooked = true;
            if (yesButton != null) yesButton.onClick.AddListener(() => onYes?.Invoke());
            if (laterButton != null) laterButton.onClick.AddListener(() => onLater?.Invoke());
            if (answer != null) answer.onValueChanged.AddListener(i => { if (i >= 0 && i < answers.Count) onAnswer?.Invoke(answers[i]); });
            if (month != null) month.onValueChanged.AddListener(_ => { FillDays(); TryDate(); });
            if (day != null) day.onValueChanged.AddListener(_ => TryDate());
            if (next != null) nextY = next.anchoredPosition.y;
            // the pointer chooses: the pill under it wears the edge and the heart (the top one at first)
            Hover(yesButton != null ? yesButton.gameObject : null, 0);
            Hover(answer != null ? answer.gameObject : null, 0);
            Hover(laterButton != null ? laterButton.gameObject : null, 1);
        }

        void Hover(GameObject go, int i)
        {
            if (go == null) return;
            var t = go.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Light(i));
            t.triggers.Add(enter);
        }

        /// <summary>Which pill wears the sky-blue edge and the heart: 0 the top one (좋아! or the answers), 1 다음에 할게.</summary>
        public void Light(int i)
        {
            Lit = i;
            if (yesRing != null) yesRing.gameObject.SetActive(i == 0);
            if (yesHeart != null) yesHeart.SetActive(i == 0);
            if (answerRing != null) answerRing.gameObject.SetActive(i == 0);
            if (laterRing != null) laterRing.gameObject.SetActive(i == 1);
            if (laterHeart != null) laterHeart.SetActive(i == 1);
        }
        public int Lit { get; private set; }

        bool AnyListOpen => (answer != null && answer.IsExpanded) || (month != null && month.IsExpanded) || (day != null && day.IsExpanded);
        bool LaterShown => laterButton != null && laterButton.gameObject.activeSelf;
        bool TopShown => (yesButton != null && yesButton.gameObject.activeSelf) || (answer != null && answer.gameObject.activeSelf) || (datePanel != null && datePanel.activeSelf);

        /// <summary>↑ (-1) / ↓ (+1): the other pill, if there is one.</summary>
        public void Move(int step)
        {
            int to = Mathf.Clamp(Lit + step, 0, 1);
            if (to == 1 && !LaterShown) return;
            if (to == 0 && !TopShown) return;
            Light(to);
        }

        /// <summary>Enter / Space: 좋아!, the answers' list (opened), the month's list, or 다음에 할게 — whichever wears the edge.</summary>
        public void PressLit()
        {
            if (!Visible) return;
            if (Lit == 1) { if (LaterShown) laterButton.onClick.Invoke(); return; }
            if (yesButton != null && yesButton.gameObject.activeSelf) yesButton.onClick.Invoke();
            else if (answer != null && answer.gameObject.activeSelf) answer.Show();
            else if (datePanel != null && datePanel.activeSelf && month != null) month.Show();
        }

        void OnDestroy() { if (current == this) current = null; }

        void Update()
        {
            // the talk has the keys while it is up (user 2026-11-27: "질문에 집중 … 방향키로는 질문에 답할 수 있게만"):
            // ↑ / ↓ choose the pill, Enter or Space answers with it (a drop-down's open list keeps its own keys)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && !AnyListOpen)
            {
                if (kb.upArrowKey.wasPressedThisFrame) Move(-1);
                if (kb.downArrowKey.wasPressedThisFrame) Move(1);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) PressLit();
            }
            // the little ▼ of a drop-down points up while its list is open (Figma 88:367)
            var downs = new[] { answer, month, day };
            for (int i = 0; i < arrows.Length && i < downs.Length; i++)
                if (arrows[i] != null && downs[i] != null) arrows[i].localEulerAngles = new Vector3(0f, 0f, downs[i].IsExpanded ? 180f : 0f);
            if (next != null) next.anchoredPosition = new Vector2(next.anchoredPosition.x, nextY + Mathf.Sin(Time.unscaledTime * 4f) * 3f);
        }
        float nextY;

        // ---------------- what it shows ----------------
        /// <summary>A request: "좋아!" (with the heart) and "다음에 할게".</summary>
        public void Ask(string speaker, Color tag, string line, string sub, string yes, Action onYes, string later, Action onLater)
        {
            Say(speaker, tag, line, sub, later, onLater);
            this.onYes = onYes;
            if (yesLabel != null) yesLabel.text = yes;
            yesButton.gameObject.SetActive(true);
        }

        /// <summary>A question: its answers in a drop-down (choosing one answers), "다음에 할게" under it.</summary>
        public void Question(string speaker, Color tag, string line, string sub, IList<string> choices, Action<string> onAnswer, string later, Action onLater)
        {
            Say(speaker, tag, line, sub, later, onLater);
            this.onAnswer = onAnswer;
            answers = choices != null ? choices.ToList() : new List<string>();
            answer.ClearOptions(); answer.AddOptions(answers); Bar(answer, answers.Count);
            answer.SetValueWithoutNotify(-1); answer.RefreshShownValue();
            answer.gameObject.SetActive(true);
        }

        /// <summary>The birthday: Figma's white date pill, [ ] 월 [ ] 일, the size of the 다음에 할게 pill right above it (the days
        /// follow the month); both chosen answers.</summary>
        public void Birthday(string speaker, Color tag, string line, string sub, Func<int, int> daysInMonth, Action<int, int> onDate, string later, Action onLater)
        {
            Say(speaker, tag, line, sub, later, onLater);
            this.onDate = onDate; daysIn = daysInMonth;
            month.ClearOptions(); month.AddOptions(Enumerable.Range(1, 12).Select(m => m.ToString()).ToList()); Bar(month, 12);
            month.SetValueWithoutNotify(-1); month.RefreshShownValue();
            FillDays();
            day.SetValueWithoutNotify(-1); day.RefreshShownValue();
            datePanel.SetActive(true);
        }

        public void Hide()
        {
            foreach (var d in new[] { answer, month, day }) if (d != null && d.IsExpanded) d.Hide();
            gameObject.SetActive(false);
            onYes = onLater = null; onAnswer = null; onDate = null;
        }

        void Say(string speaker, Color tag, string line, string sub, string later, Action onLater)
        {
            Hook();
            gameObject.SetActive(true);
            nameText.text = speaker; nameTag.color = tag;
            var size = nameText.GetPreferredValues(speaker, 9999f, 0f);
            ((RectTransform)nameTag.transform).sizeDelta = new Vector2(Mathf.Max(204.09f, size.x + 2f * namePad), ((RectTransform)nameTag.transform).sizeDelta.y);
            lineText.text = line; subText.text = sub ?? "";
            this.onLater = onLater;
            if (laterLabel != null) laterLabel.text = later;
            laterButton.gameObject.SetActive(!string.IsNullOrEmpty(later));
            yesButton.gameObject.SetActive(false); answer.gameObject.SetActive(false); datePanel.SetActive(false);
            onYes = null; onAnswer = null; onDate = null;
            Light(0);
            transform.SetAsLastSibling();
        }

        void FillDays()
        {
            int keep = day.value, m = month.value + 1;
            int n = month.value >= 0 && daysIn != null ? daysIn(m) : 31;
            day.ClearOptions(); day.AddOptions(Enumerable.Range(1, n).Select(d => d.ToString()).ToList()); Bar(day, n);
            day.SetValueWithoutNotify(keep >= 0 && keep < n ? keep : -1); day.RefreshShownValue();
        }

        /// <summary>The thin scrollbar of a list shows only when there are more rows than the 3 it shows (Figma 88:464).</summary>
        public const int Rows = 3;
        static void Bar(TMP_Dropdown d, int count)
        {
            var sr = d.template != null ? d.template.GetComponent<ScrollRect>() : null;
            if (sr != null && sr.verticalScrollbar != null) sr.verticalScrollbar.gameObject.SetActive(count > Rows);
        }

        void TryDate() { if (month.value >= 0 && day.value >= 0) onDate?.Invoke(month.value + 1, day.value + 1); }

        // ---------------- test hooks (the QA run and the tests pick like a player would) ----------------
        public bool QaYes() { if (!Visible || !yesButton.gameObject.activeSelf) return false; yesButton.onClick.Invoke(); return true; }
        public bool QaLater() { if (!Visible || !laterButton.gameObject.activeSelf) return false; laterButton.onClick.Invoke(); return true; }
        public bool QaAnswer(string value)
        {
            int i = answers.IndexOf(value);
            if (!Visible || !answer.gameObject.activeSelf || i < 0) return false;
            answer.value = i; return true;
        }
        public bool QaDate(int m, int d)
        {
            if (!Visible || !datePanel.activeSelf) return false;
            month.value = m - 1; if (d - 1 >= day.options.Count) return false;
            day.value = d - 1; return true;
        }
        public IEnumerable<string> Answers => answers;
    }
}
