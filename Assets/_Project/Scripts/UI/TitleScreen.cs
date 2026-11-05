using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>
    /// The start screen as real objects (D-030): it lives in the Bootstrap scene (prefab Prefabs/UI/TitleScreen), so it can be moved,
    /// resized and re-skinned in the Scene view and the Inspector. The game only finds it and wires the buttons (BootstrapController);
    /// when a scene has none, <see cref="Build"/> draws the same screen from code (Figma D-028 layout).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public Canvas canvas;
        [Tooltip("가게 문 열기: 이어서 하기 (저장이 없으면 새로 시작)")] public Button openButton;
        [Tooltip("처음부터: 새로 시작 (저장이 있으면 먼저 물어봄)")] public Button againButton;
        [Tooltip("톱니 (설정 창은 아직)")] public Button settingsButton;
        [Tooltip("메뉴가 열릴 때 보이는 묶음 (두 버튼)")] public GameObject menu;

        public Canvas Canvas => canvas != null ? canvas : GetComponent<Canvas>();

        /// <summary>The two buttons appear only when the menu opens (after the logo has been seen a moment).</summary>
        public void ShowMenu(bool on) { if (menu != null) menu.SetActive(on); }

        /// <summary>The Figma start screen (D-031: Artboard 1, 9:1191), as a canvas of plain UI objects; used by the prefab maker and as the fallback.</summary>
        public static TitleScreen Build(Transform parent)
        {
            var go = new GameObject("TitleScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 5;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
            var t = go.AddComponent<TitleScreen>(); t.canvas = c;
            // the cream screen (the Figma frame has no fill); the patchwork-cloud illustration of D-028 stays in it, switched off
            var bg = UiKit.Rect(go.transform, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            bg.color = UiKit.Cream; bg.raycastTarget = false;
            var art = UiKit.Rect(bg.transform, "Art", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            art.sprite = UiKit.UiSprite("ttl2_bg"); art.raycastTarget = false;
            if (art.sprite != null)
            {
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = art.sprite.rect.width / art.sprite.rect.height;
            }
            art.gameObject.SetActive(false);
            // the stitched cloud, the button with its hanging stars, and the 몽글몽글 수선점 logo
            UiKit.Fig(go.transform, "ttl3_cloud", UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, false, "Cloud");
            UiKit.Fig(go.transform, "ttl3_deco", UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, false, "Deco");
            UiKit.Fig(go.transform, "ttl3_logo", UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, false, "Logo");
            t.settingsButton = UiKit.FigButton(go.transform, "ttl3_gear", null, UiKit.Edge.End, UiKit.Edge.Start, 0f, 0f, "Btn_설정");
            UiKit.HiddenLabel(t.settingsButton, "설정");
            // the two buttons; the words sit where the Figma text boxes are (62 pt Cafe24, centred on 758.0/1263.6, 860.5)
            var menu = UiKit.Rect(go.transform, "Menu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); t.menu = menu.gameObject;
            t.openButton = UiKit.FigButton(menu, "ttl3_btn_open", null, UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, "Btn_가게 문 열기");
            UiKit.FigTextAt(t.openButton.transform, "ttl3_btn_open", "가게 문 열기", 62, UiKit.FigBrown, 758.0f, 860.5f, 410f, 100f).name = "Label";
            t.againButton = UiKit.FigButton(menu, "ttl3_btn_again", null, UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, "Btn_처음부터");
            UiKit.FigTextAt(t.againButton.transform, "ttl3_btn_again", "처음부터", 62, UiKit.FigBrown, 1263.6f, 860.5f, 410f, 100f).name = "Label";
            return t;
        }
    }
}
