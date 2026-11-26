using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The start screen (Figma 11-26 "타이틀", 88:2269): logo, the gear, and a menu of four words — 가게 문 열기 / 처음부터 / 설정 /
    /// 게임 종료. The chosen word sits on a light-blue pill with a pink heart before it; the mouse (hover) or the arrow keys choose,
    /// Enter / Space or a click presses. Everything here is the prefab's (Assets/_UI수정/1_타이틀/Title.prefab): the game only
    /// listens to the buttons (BootstrapController).
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [Header("메뉴 (위에서부터)")]
        [Tooltip("가게 문 열기: 이어서 하기 (저장이 없으면 새로 시작)")] public Button openButton;
        [Tooltip("처음부터: 새로 시작 (저장이 있으면 먼저 물어봐요)")] public Button newButton;
        [Tooltip("설정 창 열기")] public Button settingsButton;
        [Tooltip("게임 끄기")] public Button quitButton;
        [Header("그 밖")]
        [Tooltip("오른쪽 위 톱니: 설정 창 열기")] public Button gearButton;
        [Tooltip("메뉴 묶음 (로고가 보인 뒤에 나타나요)")] public GameObject menu;
        [Tooltip("고른 메뉴 뒤의 하늘색 알약")] public RectTransform highlight;
        [Tooltip("고른 메뉴 앞의 하트")] public RectTransform cursor;
        [Tooltip("알약: 글자 왼쪽 여백")] public float padLeft = 45.5f;
        [Tooltip("알약: 글자 오른쪽 여백")] public float padRight = 43f;
        [Tooltip("하트 왼쪽 끝 ~ 알약 왼쪽 끝")] public float cursorGap = 79f;

        Button[] items = new Button[0];
        int selected;

        /// <summary>The menu buttons, top to bottom.</summary>
        public Button[] Items { get { if (items.Length == 0) items = Array.FindAll(new[] { openButton, newButton, settingsButton, quitButton }, b => b != null); return items; } }
        public int Selected => selected;

        void Awake()
        {
            var items = Items;
            for (int i = 0; i < items.Length; i++)
            {
                int k = i;
                var trigger = items[i].gameObject.GetComponent<EventTrigger>() ?? items[i].gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => Select(k));
                trigger.triggers.Add(enter);
            }
        }

        void OnEnable() { Select(0); }

        /// <summary>The menu shows only after the logo has been seen a moment.</summary>
        public void ShowMenu(bool on)
        {
            if (menu != null && menu.activeSelf != on) menu.SetActive(on);
            if (on) Select(selected);
        }

        public bool MenuShown => menu == null || menu.activeInHierarchy;

        /// <summary>Puts the pill and the heart on menu word <paramref name="i"/>.</summary>
        public void Select(int i)
        {
            if (Items.Length == 0) return;
            selected = Mathf.Clamp(i, 0, items.Length - 1);
            Place();
        }

        void Place()
        {
            if (items.Length == 0 || highlight == null) return;
            var item = (RectTransform)items[selected].transform;
            var label = items[selected].GetComponentInChildren<TMP_Text>(true);
            float words = label != null ? label.preferredWidth : item.rect.width - padLeft - padRight;
            // the pill hugs the word and sits where the item sits; pill, heart and items share the menu column (laid out from its
            // top-left corner), so the item's left middle is its anchored place
            var hp = new Vector2(item.anchoredPosition.x - item.pivot.x * item.rect.width, item.anchoredPosition.y + (0.5f - item.pivot.y) * item.rect.height);
            highlight.pivot = new Vector2(0f, 0.5f);
            highlight.anchorMin = highlight.anchorMax = new Vector2(0f, 1f);
            highlight.anchoredPosition = hp;
            highlight.sizeDelta = new Vector2(padLeft + words + padRight, highlight.sizeDelta.y);
            if (cursor != null)
            {
                cursor.pivot = new Vector2(0f, 0.5f);
                cursor.anchorMin = cursor.anchorMax = highlight.anchorMin;
                cursor.anchoredPosition = hp - new Vector2(cursorGap, 0f);
            }
        }

        void LateUpdate()
        {
            if (!MenuShown || items.Length == 0) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select((selected + 1) % items.Length);
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Select((selected + items.Length - 1) % items.Length);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    var b = items[selected];
                    if (b != null && b.interactable) b.onClick.Invoke();
                }
            }
            Place();   // the words can change (language) and the column can move (screen size)
        }
    }
}
