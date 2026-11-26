using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>D-039: a link in the UI folder (Assets/_UI수정) that opens the scene whose screen the folder holds.</summary>
    public class UiSceneLink : ScriptableObject
    {
        public SceneAsset scene;
    }

    [CustomEditor(typeof(UiSceneLink))]
    class UiSceneLinkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var link = (UiSceneLink)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(link.scene == null))
                if (GUILayout.Button(link.scene != null ? "▶  " + link.scene.name + " 씬 열기" : "씬이 없어요", GUILayout.Height(44)))
                    UiEditFolder.OpenScene(link.scene);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("씬이 열리면 Hierarchy에서 \"UI 수정 · …\" 아래 화면을 클릭해서 고치고 Cmd+S(윈도우 Ctrl+S)로 저장하세요.\n저장하면 이 폴더의 UI 파일에 바로 반영돼요. 화면은 Game 창에서 보면 제일 잘 보여요.", MessageType.Info);
        }
    }
}
