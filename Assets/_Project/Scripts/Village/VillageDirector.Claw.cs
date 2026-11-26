using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.UI;
using UnityEngine;
using UnityEngine.InputSystem;
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
        ClawMachine claw;
        /// <summary>The claw screen (11-26 "인형뽑기" 88:1866, <see cref="Mongle.Views.ClawView"/>).</summary>
        public Mongle.Views.ClawView ClawUi { get; private set; }
        public bool ClawOpen { get; private set; }
        readonly System.Random clawRng = new System.Random();
        const string ClawTip = "방향키 이동 · SPACE 내리기";

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
            if (Application.isPlaying) ClawUi = Mongle.Views.ClawView.Get();
            if (ClawUi != null) { ClawUi.OnBack = CloseClaw; ClawUi.OnStart = ClawPress; }
        }

        public bool OpenClaw(ClawMachine m)
        {
            if (m == null || ClawOpen) return false;
            if (PlacingMode) { root.Say("가구 옮기기 중이에요. '나가기'를 누르고 열어요"); return false; }
            if (AnyPanelOpen || ViewingRide != null || QuestUiOpen || ClawUi == null) return false;
            CloseDress(); Mongle.Views.AlbumView.Close();
            ClawOpen = true;
            m.CameraPose(out var cam, out var look);
            Orbit.FocusOn(cam, look);
            SetHud(false); Hud.Hint(null);
            ClawUi.Show(); ClawUi.SetHint(ClawTip); RefreshClawUi();
            root.Audio.Play("hello", 0.7f, 1.2f);
            return true;
        }

        public void CloseClaw()
        {
            if (!ClawOpen) return;
            if (claw != null && claw.Busy && claw.Round != null && claw.Round.Phase == ClawPhase.Drop) return; // let the claw finish
            claw.Abort(); ClawOpen = false;
            ClawUi.Hide();
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
                ClawUi.ShowResult(null);
                if (claw.Available <= 0) { ClawUi.SetHint("캡슐이 다 나갔어요. 30분마다 하나씩 채워져요"); root.Audio.Play("pop", 0.5f, 0.8f); RefreshClawUi(); return; }
                if (!World.Claw.TryUse()) { ClawUi.SetHint("하트가 없어요. 30분마다 하나씩 채워져요"); root.Audio.Play("pop", 0.5f, 0.8f); RefreshClawUi(); return; }
                root.Persist("claw-try");
                claw.Begin(new ClawRound(claw.RestUv.x, claw.RestUv.y));
                ClawUi.SetHint(ClawTip);
                RefreshClawUi(); root.Audio.Play("pop", 0.5f, 1.3f);
                return;
            }
            r.Release();
            root.Audio.Play("click", 0.5f, 1.2f);
            ClawUi.SetHint("내려가요...");
            RefreshClawUi();
        }

        void RefreshClawUi()
        {
            if (ClawUi == null) return;
            World.Claw.Refresh();
            ClawUi.SetHearts(World.Claw.Count);
            long sec = World.Claw.SecondsToNext;
            ClawUi.SetTimer(World.Claw.Count >= ClawHearts.Max ? null : "다음 하트 " + (sec / 60).ToString("D2") + ":" + (sec % 60).ToString("D2"));
            var r = claw != null ? claw.Round : null;
            bool idle = r == null || r.Phase == ClawPhase.Done;
            string label = idle ? (World.Claw.Count > 0 ? "시작하기" : "하트 없음") : r.Phase == ClawPhase.Move ? "내리기!" : "...";
            ClawUi.SetStart(label, idle ? World.Claw.Count > 0 : r.Phase != ClawPhase.Drop);
            ClawUi.SetArrows(r != null && r.Phase == ClawPhase.Move);
            ClawUi.backButton.interactable = r == null || r.Phase != ClawPhase.Drop;
        }

        void UpdateClaw()
        {
            if (!ClawOpen) return;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) { CloseClaw(); return; }
                var kb = Keyboard.current;   // the keys pressed show on the screen's key caps and the big button
                ClawUi.ShowKeys(kb.leftArrowKey.isPressed || kb.aKey.isPressed, kb.rightArrowKey.isPressed || kb.dKey.isPressed,
                    kb.upArrowKey.isPressed || kb.wKey.isPressed, kb.downArrowKey.isPressed || kb.sKey.isPressed, kb.spaceKey.isPressed || kb.enterKey.isPressed);
                if ((Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) && ClawUi.startButton.interactable) ClawPress();
                var r = claw != null ? claw.Round : null;
                if (r != null && r.Phase == ClawPhase.Move)
                {
                    var k = Keyboard.current;
                    float dx = (k.rightArrowKey.isPressed || k.dKey.isPressed || ClawUi.Held(1) ? 1f : 0f) - (k.leftArrowKey.isPressed || k.aKey.isPressed || ClawUi.Held(0) ? 1f : 0f);
                    float dy = (k.downArrowKey.isPressed || k.sKey.isPressed || ClawUi.Held(3) ? 1f : 0f) - (k.upArrowKey.isPressed || k.wKey.isPressed || ClawUi.Held(2) ? 1f : 0f);
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
                    ClawUi.ShowResult("와! " + Josa.IGa(def.displayName) + " 나왔어요!\n보관함에 넣어 뒀어요");
                    ClawUi.SetHint(def.IsAccessory ? "보관함에서 친구에게 입혀 봐요" : "보관함에서 마을에 놓아 봐요");
                    root.Audio.Play("success", 0.9f, 1.1f);
                    Fx.Burst(Catalog, claw.chuteOut != null ? claw.chuteOut.position + Vector3.up * 0.15f : claw.transform.position, new Color(1f, 0.9f, 0.5f), 30, 0.02f, 0.5f, 1.1f, 0.04f);
                }
                else { ClawUi.ShowResult("캡슐이 비어 있었어요!"); ClawUi.SetHint("다시 도전해요"); }
            }
            else
            {
                ClawUi.ShowResult("아쉬워요! 조금만 더 맞춰요");
                ClawUi.SetHint(World.Claw.Count > 0 ? "하트가 남았어요. 다시 해 봐요" : "하트가 다 떨어졌어요. 30분마다 하나씩 채워져요");
                root.Audio.Play("pop", 0.6f, 0.8f);
            }
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
}
