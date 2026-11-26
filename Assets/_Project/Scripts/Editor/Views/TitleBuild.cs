using Mongle.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 start screen (Figma 88:2269 "타이틀") as a prefab: Assets/_UI수정/1_타이틀/Title.prefab, placed in the Bootstrap
    /// scene. Once made, the prefab is the designer's: <see cref="Build"/> only writes it when asked to (it replaces edits).
    /// </summary>
    public static class TitleBuild
    {
        public const string PrefabPath = UiEditFolder.TitleDir + "/Title.prefab";
        const string ScenePath = "Assets/_Project/Scenes/Bootstrap.unity";

        // Figma places (frame px): the menu column starts where the first word's pill is; rows are 115.35 apart
        const float MenuX = 300.5f, MenuY = 591.98f, RowH = 94f, RowPitch = 115.345f, ItemW = 520f;

        public static GameObject Make()
        {
            var root = Root("Title", 5);
            var view = root.gameObject.AddComponent<TitleView>();
            // the Figma frame is see-through: behind it goes the start screen's picture the game used before (patchwork clouds)
            Backdrop(root, "Background", "Title/title_bg.jpg");
            Picture(root, "Logo", "Title/title_logo.png", 112f, 68.5f, 753.5f, 443f, TL);
            view.gearButton = PictureButton(root, "Btn_Gear", "Common/gear.png", 1789f, 35f, 103f, 103f, TR);

            float menuH = 4 * RowH + 3 * (RowPitch - RowH);
            var menu = Node(root, "Menu", MenuX, MenuY, ItemW, menuH, TL);
            var col = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = RowPitch - RowH; col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = col.childControlHeight = false; col.childForceExpandWidth = col.childForceExpandHeight = false;
            var size = new Vector2(ItemW, menuH);
            // the pill behind the chosen word (drawn first), then the words, then the heart in front
            var pill = Picture(menu, "Highlight", "Title/title_pill.png", 0f, 0f, 436.5f, RowH, TL, size, sliced: true);
            IgnoreLayout(pill);
            view.openButton = Item(menu, "Item_가게 문 열기", "가게 문 열기", "title.continue");
            view.newButton = Item(menu, "Item_처음부터", "처음부터", "title.new");
            view.settingsButton = Item(menu, "Item_설정", "설정", "common.settings");
            view.quitButton = Item(menu, "Item_게임 종료", "게임 종료", "title.quit");
            var heart = Picture(menu, "Cursor", "Title/title_heart.png", -79f, 16.5f, 69.5f, 62.5f, TL, size);
            IgnoreLayout(heart);
            view.menu = menu.gameObject; view.highlight = pill.rectTransform; view.cursor = heart.rectTransform;
            return root.gameObject;
        }

        /// <summary>One menu word: a clear row that takes the click, the word 45.5 in from the pill's left edge (Figma).</summary>
        static Button Item(Transform menu, string name, string text, string key)
        {
            var row = Node(menu, name, 0f, 0f, ItemW, RowH, TL, new Vector2(ItemW, RowH));
            var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = HitArea(row.gameObject);
            // the Figma text box (85 tall) sits 3.65 above the pill's middle
            Words(row, "Label", text, 65f, Brown, 45.5f, (RowH - 85f) * 0.5f - 3.65f, ItemW - 45.5f, 85f, TL, TMPro.HorizontalAlignmentOptions.Left, key, new Vector2(ItemW, RowH));
            return b;
        }

        /// <summary>Writes the prefab (replacing what is there) and returns it.</summary>
        public static GameObject Build()
        {
            var go = Make();
            try { return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath); }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>One instance of the prefab at the root of <paramref name="scene"/> (SceneBuilder calls this for a fresh Bootstrap scene).</summary>
        public static void AddTo(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "Title";
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
