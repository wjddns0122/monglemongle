using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// Furniture placing (Figma 115:1398 "아트보드 12 copy"): ◀ where the home button stands leaves placing, 되돌리기 (Z) and
    /// 다시 하기 (Y) beside it, and the bar at the bottom left: 돌리기, 보관함에 넣기, 여기에 두기, 취소. A bar button shows its word in
    /// the little tip while the pointer is on it; 여기에 두기's tip also stands while a piece is picked (as in the frame).
    /// Assets/_UI수정/3_마을/PlaceBar.prefab, listed as "PlaceBar" in Resources/Views/ViewIndex; the village opens it.
    /// </summary>
    public class PlaceBarView : MonoBehaviour
    {
        [Header("왼쪽 위")]
        public Button backButton;
        public Button undoButton, redoButton;
        [Header("아래 막대")]
        public Button rotateButton;
        public Button storeButton, putButton, cancelButton;
        [Tooltip("돌리기 · 보관함에 넣기 · 여기에 두기 · 취소 말풍선 (버튼 순서)")] public GameObject[] tips = new GameObject[0];
        [Tooltip("되돌릴 것 / 다시 할 것이 없을 때 버튼의 진하기")] [Range(0f, 1f)] public float idleAlpha = 0.45f;
        [Tooltip("Z = 되돌리기, Y = 다시 하기")] public bool keys = true;
        [Tooltip("되돌리기 · 다시 하기의 Z · Y 키 표시 (집 놓을 때는 숨겨요)")] public GameObject[] undoKeys = new GameObject[0];

        public Action OnBack, OnUndo, OnRedo, OnRotate, OnStore, OnPut, OnCancel;

        /// <summary>Which bar button the tip of <see cref="tips"/> belongs to: 여기에 두기 is the third.</summary>
        public const int PutTip = 2;

        int hovered = -1;
        bool picked;

        void Awake()
        {
            Hook(backButton, () => OnBack?.Invoke());
            Hook(undoButton, () => OnUndo?.Invoke());
            Hook(redoButton, () => OnRedo?.Invoke());
            Hook(rotateButton, () => OnRotate?.Invoke());
            Hook(storeButton, () => OnStore?.Invoke());
            Hook(putButton, () => OnPut?.Invoke());
            Hook(cancelButton, () => OnCancel?.Invoke());
            var bar = new[] { rotateButton, storeButton, putButton, cancelButton };
            for (int i = 0; i < bar.Length; i++) if (bar[i] != null) Hover(bar[i].gameObject, i);
            ViewUtil.EnsureEventSystem();
            RefreshTips();
        }

        static void Hook(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        void Hover(GameObject go, int i)
        {
            var t = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; enter.callback.AddListener(_ => { hovered = i; RefreshTips(); });
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit }; exit.callback.AddListener(_ => { if (hovered == i) hovered = -1; RefreshTips(); });
            t.triggers.Add(enter); t.triggers.Add(exit);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!keys || kb == null) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
            if (kb.zKey.wasPressedThisFrame) OnUndo?.Invoke();
            if (kb.yKey.wasPressedThisFrame) OnRedo?.Invoke();
        }

        public void Show(bool on)
        {
            if (!on) { hovered = -1; picked = false; }
            gameObject.SetActive(on);
            RefreshTips();
        }
        public bool Shown => gameObject.activeSelf;

        /// <summary>Each frame while placing: is a piece picked (여기에 두기's tip), is there anything to take back / do again.</summary>
        public void Refresh(bool pickedPiece, bool canUndo, bool canRedo)
        {
            if (picked != pickedPiece) { picked = pickedPiece; RefreshTips(); }
            Dim(undoButton, canUndo); Dim(redoButton, canRedo);
        }

        void Dim(Button b, bool on)
        {
            var g = b != null ? b.targetGraphic : null; if (g == null) return;
            float a = on ? 1f : idleAlpha;
            if (!Mathf.Approximately(g.color.a, a)) { var c = g.color; c.a = a; g.color = c; }
        }

        void RefreshTips()
        {
            for (int i = 0; i < tips.Length; i++)
                if (tips[i] != null) tips[i].SetActive(hovered == i || (hovered < 0 && picked && i == PutTip));
        }

        /// <summary>Furniture: 되돌리기 / 다시 하기 with their keys; a house being set down has none.</summary>
        public void SetUndoShown(bool on)
        {
            if (undoButton != null) undoButton.gameObject.SetActive(on);
            if (redoButton != null) redoButton.gameObject.SetActive(on);
            foreach (var k in undoKeys) if (k != null) k.SetActive(on);
        }
        public bool UndoShown => undoButton != null && undoButton.gameObject.activeSelf;

        /// <summary>여기에 두기 is dimmed where the piece cannot go (a click still says why).</summary>
        public void SetPutReady(bool ok)
        {
            var g = putButton != null ? putButton.targetGraphic : null; if (g == null) return;
            var c = g.color; c.a = ok ? 1f : idleAlpha; g.color = c;
        }

        public bool TipShown(int i) => i >= 0 && i < tips.Length && tips[i] != null && tips[i].activeSelf;
        public bool UndoLit => undoButton != null && undoButton.targetGraphic != null && undoButton.targetGraphic.color.a > 0.99f;
        public bool RedoLit => redoButton != null && redoButton.targetGraphic != null && redoButton.targetGraphic.color.a > 0.99f;
    }
}
