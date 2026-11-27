using System;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>Where a request stands: waiting for a yes, under way, waiting for its reward, done — or a plain note (내 이야기).</summary>
    public enum AskState { Open, Doing, Ready, Done, Note }

    /// <summary>One request row's contents (a keeper's request in the shop, a friend's or a keeper's in the 퀘스트 window).</summary>
    public struct Ask
    {
        public string id, title, giver, line, progress;
        public int reward;
        public AskState state;
        /// <summary>What it is about (the chair, the watering can); none = a letter on the round icon.</summary>
        public Sprite picture;
        /// <summary>The letter on the icon (none = the giver's first letter).</summary>
        public string initial;
        /// <summary>The icon's colour (clear = one picked from the giver's name).</summary>
        public Color iconColor;
        /// <summary>The button's words (null = 수락할게요 / 찾으러 가기 / 보상 받기 by state, "" = no button).</summary>
        public string action;
        /// <summary>What the button does (null = the view's own OnAccept / OnGo / OnClaim).</summary>
        public Action onPress;
    }

    /// <summary>The colours and pictures every request row shares (Figma 88:2130).</summary>
    [Serializable]
    public class AskStyle
    {
        public Color rowColor = Color.white, rowDoneColor = new Color(0.965f, 0.941f, 0.906f, 1f);
        [Tooltip("끝낸 부탁의 동그라미")] public Color doneIconColor = new Color(0.925f, 0.89f, 0.839f, 1f);
        [Tooltip("부탁하는 이의 동그라미 색 (이름으로 하나 골라요)")] public Color[] giverColors = new Color[0];
        [Tooltip("수락할게요 · 보상 받기 (하늘색) / 찾으러 가기 (노란색)")] public Sprite openButton, goButton;
        [Tooltip("진행 정도 글자 색")] public string progressColor = "#B39484";

        public Color GiverColor(string giver)
        {
            if (giverColors == null || giverColors.Length == 0) return doneIconColor;
            int h = 0; foreach (var ch in giver ?? "") h = h * 31 + ch;
            return giverColors[(h & 0x7fffffff) % giverColors.Length];
        }
    }

    /// <summary>
    /// One request row (Figma 88:2206 …): the stitched white plate, the round icon with the giver's letter or a picture of the
    /// thing, the title (and how far it got), "giver : what they asked", the coin pill and the button; a done one is cream with
    /// ♥ 완료. The pieces are found by name in a row made by AskRowBuild.
    /// </summary>
    [Serializable]
    public class AskRow
    {
        public GameObject root;
        [Tooltip("줄 바탕 (색만 바뀌어요: 흰색 / 끝낸 부탁)")] public Graphic plate;
        public Graphic icon;
        [Tooltip("부탁에 나오는 물건 그림 (동그라미 위)")] public Image picture;
        public TMP_Text initial, title, line, reward;
        public GameObject rewardPill, done;
        public Button button;
        public TMP_Text buttonText;

        /// <summary>The pieces of a row by their names (a copy of the template row).</summary>
        public static AskRow Find(GameObject go)
        {
            var t = go.transform;
            T F<T>(string path) where T : Component { var c = t.Find(path); return c != null ? c.GetComponent<T>() : null; }
            return new AskRow
            {
                root = go, plate = F<Graphic>("Plate"), icon = F<Graphic>("Icon"), picture = F<Image>("Icon/Picture"), initial = F<TMP_Text>("Icon/Initial"),
                title = F<TMP_Text>("Title"), line = F<TMP_Text>("Line"), reward = F<TMP_Text>("Reward/Amount"),
                rewardPill = t.Find("Reward") != null ? t.Find("Reward").gameObject : null, done = t.Find("Done") != null ? t.Find("Done").gameObject : null,
                button = F<Button>("Btn"), buttonText = F<TMP_Text>("Btn/Label"),
            };
        }

        /// <summary>The words a row's button shows (null = none).</summary>
        public static string ButtonWords(Ask a)
        {
            if (a.state == AskState.Done || a.state == AskState.Note) return null;
            if (a.action != null) return a.action.Length > 0 ? a.action : a.state == AskState.Doing ? Loc.Get("quest.doing") : null;
            return Loc.Get(a.state == AskState.Open ? "quest.accept" : a.state == AskState.Ready ? "quest.claim" : "quest.go");
        }

        public void Draw(Ask a, AskStyle s)
        {
            bool isDone = a.state == AskState.Done;
            root.name = "Quest_" + a.id;
            plate.color = isDone ? s.rowDoneColor : s.rowColor;
            icon.color = isDone ? s.doneIconColor : a.iconColor.a > 0f ? a.iconColor : s.GiverColor(a.giver);
            initial.text = !string.IsNullOrEmpty(a.initial) ? a.initial : string.IsNullOrEmpty(a.giver) ? "!" : a.giver.Substring(0, 1);
            if (picture != null) { picture.sprite = a.picture; picture.enabled = a.picture != null; }
            initial.gameObject.SetActive(a.picture == null || picture == null);
            title.text = string.IsNullOrEmpty(a.progress) ? a.title : a.title + "  <color=" + s.progressColor + ">" + a.progress + "</color>";
            line.text = (string.IsNullOrEmpty(a.giver) ? "" : a.giver + " : ") + a.line;
            rewardPill.SetActive(!isDone && a.state != AskState.Note && a.reward > 0); reward.text = "+" + a.reward;
            done.SetActive(isDone);
            var words = ButtonWords(a);
            button.gameObject.SetActive(words != null);
            if (words == null) return;
            // accepted, nothing to do from here: the button's place says 진행중 and does not press (user 2026-11-27)
            bool underWay = a.state == AskState.Doing && a.action != null && a.action.Length == 0;
            button.interactable = !underWay;
            var cb = button.colors; cb.disabledColor = cb.normalColor; button.colors = cb;
            button.name = (underWay ? "Quest_Doing_" : a.state == AskState.Open ? "Quest_Accept_" : a.state == AskState.Ready ? "Quest_Claim_" : "Quest_Go_") + a.id;
            ((Image)button.targetGraphic).sprite = a.state == AskState.Doing ? s.goButton : s.openButton;
            buttonText.text = words;
        }
    }
}
