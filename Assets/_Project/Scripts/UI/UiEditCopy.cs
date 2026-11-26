using UnityEngine;

namespace Mongle.UI
{
    /// <summary>
    /// D-039: holds a copy of a screen's prefab inside its scene, so the screen is edited right there (Scene / Game view, without
    /// pressing Play). Saving the scene, or pressing Play, writes the edits into the prefab file (editor: UiEditFolder). In the game
    /// the copy hides itself: the code builds the real screen and lays the prefab over it (<see cref="UiSkin"/>). The holder is
    /// tagged EditorOnly, so builds leave it out.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public class UiEditCopy : MonoBehaviour
    {
        [TextArea(2, 6)]
        public string note = "이 아래 화면을 고치고 Cmd+S(윈도우 Ctrl+S)로 저장하면 UI 파일에 바로 저장돼요. 게임(▶)에서는 이 사본이 숨겨지고, 고친 모양이 진짜 화면에 입혀져요.";

        void Awake() { if (Application.isPlaying) gameObject.SetActive(false); }
    }
}
