using System.Collections;
using Mongle.UI;
using Mongle.Views;
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
            // the start screen (11-26 design) is the Bootstrap scene's own object: Assets/_UI수정/1_타이틀/Title.prefab
            var title = FindAnyObjectByType<TitleView>(FindObjectsInactive.Include);
            ViewUtil.EnsureEventSystem();
            if (title != null)
            {
                title.gameObject.SetActive(true); title.ShowMenu(false);
                if (title.gearButton != null) title.gearButton.onClick.AddListener(Mongle.UI2.UiFront.OpenSettings);
                if (title.settingsButton != null) title.settingsButton.onClick.AddListener(Mongle.UI2.UiFront.OpenSettings);
                if (title.quitButton != null) title.quitButton.onClick.AddListener(Application.Quit);
            }
            else Debug.LogWarning("[Bootstrap] no TitleView in the scene: starting without the menu");
            yield return new WaitForSeconds(1.2f);

            if (root.NeedsSaveDecision)
            {
                bool decided = false;
                string why = root.LoadResult.status == SaveStatus.UnsupportedVersion
                    ? "더 새로운 버전에서 만든 저장이에요. 이 버전에서는 열 수 없어요."
                    : "저장 파일이 손상되어 읽을 수 없어요.";
                DialogView.Open("저장 파일 확인", why + "\n새로 시작해도 기존 파일은 지우지 않고 따로 보관해요.", "새로 시작", () =>
                {
                    root.StartNewGame(true); decided = true;
                }, "종료", () => Application.Quit());
                while (!decided) yield return null;
            }
            else if (!SkipMenu() && title != null) yield return TitleMenu(root, title);
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
        /// Start screen (D-017; 11-26 design): "가게 문 열기" goes on with the saved game (or begins a new one when there is none), "처음부터" begins a
        /// new game (asks first when there is a save, then moves the old save aside instead of deleting it). The keys (↑ ↓, Enter) are the
        /// screen's own (<see cref="TitleView"/>): they press the same buttons.
        /// </summary>
        IEnumerator TitleMenu(GameRoot root, TitleView title)
        {
            bool hasSave = root.LoadResult.status == SaveStatus.Ok;
            bool chosen = false, fresh = false;
            // the buttons are the scene's (TitleView); only what they do is set here
            if (title.openButton != null) title.openButton.onClick.AddListener(() => { if (!chosen) { chosen = true; fresh = !hasSave; } });
            if (title.newButton != null) title.newButton.onClick.AddListener(() => { if (!chosen) { chosen = true; fresh = true; } });
            title.ShowMenu(true);
            // "처음부터" without a save simply starts a new game like the first button
            while (true)
            {
                if (!chosen) { yield return null; continue; }
                if (!fresh) yield break;                     // continue
                if (!hasSave) { root.StartNewGame(false); yield break; }
                // a save exists: ask first
                bool answered = false, yes = false;
                title.ShowMenu(false);   // the menu's keys rest while the question is up
                DialogView.Open("새로 시작할까요?", "지금 저장된 게임이 있어요.\n새로 시작해도 기존 저장은 지우지 않고 따로 보관해요.", "새로 시작", () => { yes = true; answered = true; }, "취소", () => { answered = true; });
                while (!answered) yield return null;
                if (yes) { root.StartNewGame(true); yield break; }
                chosen = false; fresh = false; title.ShowMenu(true);   // back to the menu
            }
        }
    }
}
