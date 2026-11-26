using System.Collections;
using Mongle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Core
{
    /// <summary>First scene: shows the title, resolves save problems with the player, then enters the right scene.</summary>
    public class BootstrapController : MonoBehaviour
    {
        IEnumerator Start()
        {
            GameRoot root = null;
            while ((root = GameRoot.Instance) == null) yield return null;
            // D-030: the start screen is an object of the Bootstrap scene (edit it there); a scene without one gets the same screen from code
            var title = FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
            if (title == null) title = TitleScreen.Build(transform);
            title.gameObject.SetActive(true); title.ShowMenu(false);
            UiKit.EnsureEventSystem();
            if (title.settingsButton != null) title.settingsButton.onClick.AddListener(Mongle.UI2.UiFront.OpenSettings);
            if (Application.isPlaying) Mongle.UI2.UiFront.Attach(title.Canvas.transform, "Title");   // D-040: the Figma start screen in front
            yield return new WaitForSeconds(1.2f);

            if (root.NeedsSaveDecision)
            {
                bool decided = false;
                var ui = new GameObject("BootDialogUI").AddComponent<ShopUI>(); ui.Build(); ui.ShowSettingsButton(false); Mongle.UI2.UiFront.Attach(ui.Canvas.transform, "Intake");
                string why = root.LoadResult.status == SaveStatus.UnsupportedVersion
                    ? "더 새로운 버전에서 만든 저장이에요. 이 버전에서는 열 수 없어요."
                    : "저장 파일이 손상되어 읽을 수 없어요.";
                ui.ShowDialog("저장 파일 확인", why + "\n새로 시작해도 기존 파일은 지우지 않고 따로 보관해요.", "새로 시작", () =>
                {
                    root.StartNewGame(true); decided = true;
                }, "종료", () => Application.Quit());
                while (!decided) yield return null;
                Destroy(ui.gameObject);
            }
            else if (!SkipMenu()) yield return TitleMenu(root, title);
            root.GoToCurrentPhase();
        }

        /// <summary>The scripted QA run and the tests (which point the save at a temp file) go straight on without the menu.</summary>
        static bool SkipMenu()
        {
            if (GameRoot.SavePathOverride != null) return true;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-mongleQa" || a == "-mongleAutoStart") return true;
            return false;
        }

        /// <summary>
        /// Start screen (D-017, Figma D-028): "가게 문 열기" goes on with the saved game (or begins a new one when there is none), "처음부터" begins a new game
        /// (asks first when there is a save, then moves the old save aside instead of deleting it). Exactly the Figma screen: no extra lines on it.
        /// </summary>
        IEnumerator TitleMenu(GameRoot root, TitleScreen title)
        {
            bool hasSave = root.LoadResult.status == SaveStatus.Ok;
            bool chosen = false, fresh = false;
            // the buttons are the scene's (TitleScreen); only what they do is set here
            title.openButton.onClick.AddListener(() => { if (!chosen) { chosen = true; fresh = !hasSave; } });
            title.againButton.onClick.AddListener(() => { if (!chosen) { chosen = true; fresh = true; } });
            title.ShowMenu(true);
            // "처음부터" stays blue and clickable: without a save it simply starts a new game like the first button
            while (true)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (!chosen && kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) { chosen = true; fresh = !hasSave; }   // Enter: same as the first button
                if (!chosen) { yield return null; continue; }
                if (!fresh) yield break;                     // continue
                if (!hasSave) { root.StartNewGame(false); yield break; }
                // a save exists: ask first
                bool answered = false, yes = false;
                var ui = new GameObject("BootDialogUI").AddComponent<ShopUI>(); ui.Build(); ui.ShowSettingsButton(false); Mongle.UI2.UiFront.Attach(ui.Canvas.transform, "Intake");
                ui.ShowDialog("새로 시작할까요?", "지금 저장된 게임이 있어요.\n새로 시작해도 기존 저장은 지우지 않고 따로 보관해요.", "새로 시작", () => { yes = true; answered = true; }, "취소", () => { answered = true; });
                while (!answered) yield return null;
                Destroy(ui.gameObject);
                if (yes) { root.StartNewGame(true); yield break; }
                chosen = false; fresh = false;               // back to the menu
            }
        }
    }
}
