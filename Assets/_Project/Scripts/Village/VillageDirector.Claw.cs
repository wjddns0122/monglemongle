using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>
    /// The capsule claw machine next to the shops (D-014): five heart tries that come back one per 30 minutes; the claw sweeps
    /// left-right and then front-back and the player stops it each time; a catch gives one random item that the shops sell
    /// (it goes into the storage box like a purchase).
    /// </summary>
    public partial class VillageDirector
    {
        ClawMachine claw; GameObject clawBar; Image[] clawHearts; Text clawTimer, clawTip, clawResult; Button clawGo, clawQuit;
        ClawHold[] clawArrows;   // on-screen arrows: left, right, back, front
        public bool ClawOpen { get; private set; }
        readonly System.Random clawRng = new System.Random();
        // the tries are the Figma hearts (D-027): the deep pink one of the quest list for a heart you have, the pale one for a used heart
        const string HeartFull = "q_heart", HeartEmpty = "dlg_choice_heart";

        static Sprite arrowSprite;

        /// <summary>A round-cornered triangle pointing up (the title font has no ◀ ▶ ▲ ▼, D-027); rotated for the other ways.</summary>
        static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            const int N = 64; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // signed distance to an equilateral triangle (side 34), rounded by 5
                    var p = new Vector2(x + 0.5f - N / 2f, y + 0.5f - N / 2f + 2f);
                    const float k = 1.7320508f, r = 17f;
                    p.x = Mathf.Abs(p.x) - r; p.y += r / k;
                    if (p.x + k * p.y > 0f) p = new Vector2(p.x - k * p.y, -k * p.x - p.y) / 2f;
                    p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
                    float d = -p.magnitude * Mathf.Sign(p.y) - 5f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
                }
            tex.Apply();
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return arrowSprite;
        }

        void SetupClaw()
        {
            claw = FindAnyObjectByType<ClawMachine>();
            if (claw == null) return;
            Grid.BlockBox(claw.transform.position, new Vector2(0.75f, 0.6f), claw.transform.eulerAngles.y);
            claw.Stock = World.ClawCaps;
            claw.Finished += OnClawFinished;
            BuildClawUi();
        }

        void BuildClawUi()
        {
            // D-027: three rows that never overlap — hearts + timer on top, the tip line, then the d-pad / 시작 / 그만하기 row
            var bar = UiKit.Panel(canvas.transform, "ClawBar", UiKit.Paper, new Vector2(0.5f, 0f), new Vector2(0, 165), new Vector2(1120, 290), true);
            clawBar = bar.gameObject;
            clawHearts = new Image[ClawHearts.Max];
            const float HeartPitch = 74f, HeartSize = 64f, Group = 4 * HeartPitch + HeartSize + 24f + 250f;   // hearts, gap, timer
            float hx = -Group / 2f + HeartSize / 2f;
            for (int i = 0; i < clawHearts.Length; i++)
            {
                var rt = UiKit.Fixed(bar.transform, "Heart" + i, new Vector2(0.5f, 1f), new Vector2(hx + i * HeartPitch, -54f), new Vector2(HeartSize, HeartSize));
                var img = rt.gameObject.AddComponent<Image>(); img.sprite = UiKit.UiSprite(HeartFull); img.preserveAspect = true; img.raycastTarget = false; clawHearts[i] = img;
            }
            clawTimer = UiKit.Label(bar.transform, "", 30, UiKit.Brown, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(250, 56), TextAnchor.MiddleLeft);
            clawTimer.rectTransform.pivot = new Vector2(0f, 0.5f); clawTimer.rectTransform.anchoredPosition = new Vector2(hx + 4 * HeartPitch + HeartSize / 2f + 24f, -54f);
            clawTip = UiKit.Label(bar.transform, "", 30, UiKit.Brown, new Vector2(0.5f, 1f), new Vector2(0, -116), new Vector2(1040, 46));
            clawTip.verticalOverflow = VerticalWrapMode.Truncate; clawTip.resizeTextForBestFit = true; clawTip.resizeTextMinSize = 20; clawTip.resizeTextMaxSize = 30;   // one line, shrinks if long
            clawGo = UiKit.MakeButton(bar.transform, "시작!", UiKit.Pink, new Vector2(0.5f, 0f), new Vector2(-10, 80), new Vector2(330, 96), ClawPress, 42);
            clawQuit = UiKit.MakeButton(bar.transform, "그만하기", UiKit.Mint, new Vector2(0.5f, 0f), new Vector2(340, 80), new Vector2(230, 96), CloseClaw, 32);
            // arrows (hold to move), a d-pad on the left of the bottom row: left, right, back (up), front (down)
            clawArrows = new ClawHold[4];
            Vector2[] at = { new Vector2(-494, 80), new Vector2(-346, 80), new Vector2(-420, 110), new Vector2(-420, 50) };
            float[] turn = { 90f, -90f, 0f, 180f };
            for (int i = 0; i < 4; i++)
            {
                var b = UiKit.MakeButton(bar.transform, "", UiKit.Sky, new Vector2(0.5f, 0f), at[i], new Vector2(64, 54), null, 30);
                b.name = "Btn_" + new[] { "왼쪽", "오른쪽", "위", "아래" }[i];
                var arrow = UiKit.Fixed(b.transform, "Arrow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
                var ai = arrow.gameObject.AddComponent<Image>(); ai.sprite = ArrowSprite(); ai.color = UiKit.FigBrown; ai.raycastTarget = false;
                arrow.localEulerAngles = new Vector3(0f, 0f, turn[i]);
                clawArrows[i] = b.gameObject.AddComponent<ClawHold>();
            }
            // plain pills on the stitched bar, like the other bars (D-026)
            foreach (var b in bar.GetComponentsInChildren<Button>(true)) UiKit.NoStitch(b.GetComponent<Image>());
            clawResult = UiKit.Label(canvas.transform, "", 54, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1000, 220));
            clawResult.name = "ClawResult"; clawResult.gameObject.SetActive(false);
            clawBar.SetActive(false);
            SkinPart(bar); SkinPart(clawResult);
        }

        public bool OpenClaw(ClawMachine m)
        {
            if (m == null || ClawOpen) return false;
            if (PlacingMode) { root.Say("가구 옮기기 중이에요. '나가기'를 누르고 열어요"); return false; }
            if (AnyPanelOpen || ViewingRide != null || QuestUiOpen) return false;
            CloseDress(); album.Hide();
            ClawOpen = true;
            m.CameraPose(out var cam, out var look);
            Orbit.FocusOn(cam, look);
            SetHud(false); baseUi.Banner(null);
            clawBar.SetActive(true); RefreshClawUi();
            clawTip.text = "방향키(또는 화살표 버튼)로 집게를 캡슐 위로 움직이고 '내리기!'";
            root.Audio.Play("hello", 0.7f, 1.2f);
            return true;
        }

        public void CloseClaw()
        {
            if (!ClawOpen) return;
            if (claw != null && claw.Busy && claw.Round != null && claw.Round.Phase == ClawPhase.Drop) return; // let the claw finish
            claw.Abort(); ClawOpen = false;
            clawBar.SetActive(false); clawResult.gameObject.SetActive(false);
            Orbit.ReleaseFocus(); SetHud(true);
            root.Audio.Play("click", 0.4f, 0.9f);
        }

        /// <summary>The big button: start a try (uses a heart), then it becomes "내리기!".</summary>
        void ClawPress()
        {
            if (claw == null) return;
            var r = claw.Round;
            if (r != null && r.Phase == ClawPhase.Drop) return;
            if (r == null || r.Phase == ClawPhase.Done)
            {
                clawResult.gameObject.SetActive(false);
                if (claw.Available <= 0) { clawTip.text = "캡슐이 다 나갔어요. 30분마다 하나씩 채워져요"; root.Audio.Play("pop", 0.5f, 0.8f); RefreshClawUi(); return; }
                if (!World.Claw.TryUse()) { clawTip.text = "하트가 없어요. 30분마다 하나씩 채워져요"; root.Audio.Play("pop", 0.5f, 0.8f); RefreshClawUi(); return; }
                root.Persist("claw-try");
                claw.Begin(new ClawRound(claw.RestUv.x, claw.RestUv.y));
                clawTip.text = "방향키로 집게를 캡슐 위로 움직이고 '내리기!' (Space)";
                RefreshClawUi(); root.Audio.Play("pop", 0.5f, 1.3f);
                return;
            }
            r.Release();
            root.Audio.Play("click", 0.5f, 1.2f);
            clawTip.text = "내려가요...";
            RefreshClawUi();
        }

        void RefreshClawUi()
        {
            if (clawBar == null) return;
            World.Claw.Refresh();
            for (int i = 0; i < clawHearts.Length; i++) clawHearts[i].sprite = UiKit.UiSprite(i < World.Claw.Count ? HeartFull : HeartEmpty);
            long s = World.Claw.SecondsToNext;
            clawTimer.text = World.Claw.Count >= ClawHearts.Max ? "하트 가득!" : "다음 하트 " + (s / 60).ToString("D2") + ":" + (s % 60).ToString("D2");
            var r = claw != null ? claw.Round : null;
            string label = r == null || r.Phase == ClawPhase.Done ? (World.Claw.Count > 0 ? "시작!" : "하트 없음") : r.Phase == ClawPhase.Move ? "내리기!" : "...";
            UiKit.SetButtonText(clawGo, label);
            clawGo.interactable = (r == null || r.Phase == ClawPhase.Done) ? World.Claw.Count > 0 : r.Phase != ClawPhase.Drop;
            foreach (var arrow in clawArrows) arrow.GetComponent<Button>().interactable = r != null && r.Phase == ClawPhase.Move;
            clawQuit.interactable = r == null || r.Phase != ClawPhase.Drop;
        }

        void UpdateClaw()
        {
            if (!ClawOpen) return;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) { CloseClaw(); return; }
                if ((Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) && clawGo.interactable) ClawPress();
                var r = claw != null ? claw.Round : null;
                if (r != null && r.Phase == ClawPhase.Move)
                {
                    var k = Keyboard.current;
                    float dx = (k.rightArrowKey.isPressed || k.dKey.isPressed || clawArrows[1].Held ? 1f : 0f) - (k.leftArrowKey.isPressed || k.aKey.isPressed || clawArrows[0].Held ? 1f : 0f);
                    float dy = (k.downArrowKey.isPressed || k.sKey.isPressed || clawArrows[3].Held ? 1f : 0f) - (k.upArrowKey.isPressed || k.wKey.isPressed || clawArrows[2].Held ? 1f : 0f);
                    r.Steer(dx, dy, Time.deltaTime);
                }
            }
            RefreshClawUi();
        }

        void OnClawFinished(bool won)
        {
            if (!ClawOpen) return;
            if (won)
            {
                var ids = ClawPrizePool();
                var id = ClawRewards.Pick(ids, clawRng);
                var def = id != null ? Catalog.furniture.FirstOrDefault(f => f != null && f.id == id) : null;
                if (def != null)
                {
                    World.AddToInventory(def.id); root.Persist("claw-prize:" + def.id);
                    clawResult.text = "와! " + Josa.IGa(def.displayName) + " 나왔어요!\n보관함에 넣어 뒀어요";
                    clawTip.text = def.IsAccessory ? "악세서리 상자에서 친구에게 입혀 봐요" : "보관함에서 마을에 놓아 봐요";
                    root.Audio.Play("success", 0.9f, 1.1f);
                    Fx.Burst(Catalog, claw.chuteOut != null ? claw.chuteOut.position + Vector3.up * 0.15f : claw.transform.position, new Color(1f, 0.9f, 0.5f), 30, 0.02f, 0.5f, 1.1f, 0.04f);
                }
                else { clawResult.text = "캡슐이 비어 있었어요!"; clawTip.text = "다시 도전해요"; }
            }
            else
            {
                clawResult.text = "아쉬워요! 조금만 더 맞춰요";
                clawTip.text = World.Claw.Count > 0 ? "하트가 남았어요. 다시 해 봐요" : "하트가 다 떨어졌어요. 30분마다 하나씩 채워져요";
                root.Audio.Play("pop", 0.6f, 0.8f);
            }
            clawResult.gameObject.SetActive(true);
            RefreshClawUi();
        }

        /// <summary>Every item the shops sell (price above zero), each once.</summary>
        List<string> ClawPrizePool()
        {
            var ids = new List<string>();
            if (Catalog.shops != null)
                foreach (var sh in Catalog.shops)
                    if (sh != null && sh.items != null)
                        foreach (var it in sh.items) if (it != null && it.price > 0 && !ids.Contains(it.id)) ids.Add(it.id);
            return ids;
        }

        public void ClickClaw(ClawMachine m)
        {
            if (!OpenClaw(m)) return;
        }
    }

    /// <summary>A button that reports whether it is being held (the on-screen arrows).</summary>
    public class ClawHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData e) { if (GetComponent<Button>() == null || GetComponent<Button>().interactable) Held = true; }
        public void OnPointerUp(PointerEventData e) { Held = false; }
        public void OnPointerExit(PointerEventData e) { Held = false; }
        void OnDisable() { Held = false; }
    }
}
