using System;
using System.Collections.Generic;
using Mongle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// D-040: finds objects of the game's own (code-built) screen by name path: "Notebook/Row0/Label", "Dialog/Label#1" (the second
    /// of that name), "QuestAction_*" (the first whose name starts so), "&lt;button&gt;" (the first button below), "" (the base itself).
    /// Conditions: "on:path" (shown), "sprite:path=name", "text:path=words" ("text:path=" is empty); "!" in front turns it round.
    /// </summary>
    public static class UiPath
    {
        public static Transform Find(Transform b, string path)
        {
            if (b == null) return null;
            if (string.IsNullOrEmpty(path)) return b;
            var t = b;
            foreach (var part in path.Split('/'))
            {
                if (part.Length == 0) continue;
                if (part == "<button>") { var btn = t.GetComponentInChildren<Button>(true); t = btn != null ? btn.transform : null; }
                else if (part.EndsWith("*"))
                {
                    var stem = part.Substring(0, part.Length - 1); Transform hit = null;
                    for (int i = 0; i < t.childCount; i++) { var c = t.GetChild(i); if (c.name.StartsWith(stem, StringComparison.Ordinal)) { hit = c; break; } }
                    t = hit;
                }
                else t = UiSkin.At(t, part);
                if (t == null) return null;
            }
            return t;
        }

        public static bool Test(Transform b, string cond)
        {
            if (string.IsNullOrEmpty(cond)) return true;
            bool neg = cond.StartsWith("!"); if (neg) cond = cond.Substring(1);
            int colon = cond.IndexOf(':');
            string kind = colon < 0 ? "on" : cond.Substring(0, colon), rest = colon < 0 ? cond : cond.Substring(colon + 1);
            string path = rest, val = null; int eq = rest.IndexOf('=');
            if (eq >= 0) { path = rest.Substring(0, eq); val = rest.Substring(eq + 1); }
            var t = Find(b, path); bool r = false;
            switch (kind)
            {
                case "on": r = t != null && t.gameObject.activeInHierarchy; break;
                case "sprite": var im = t != null ? t.GetComponent<Image>() : null; r = im != null && im.sprite != null && im.sprite.name == val; break;
                case "text": r = t != null && (TextOf(t) ?? "") == (val ?? ""); break;
            }
            return neg ? !r : r;
        }

        /// <summary>The words on an object: its own Text, or its "Label" child's (buttons).</summary>
        public static string TextOf(Transform t)
        {
            if (t == null) return null;
            if (t.TryGetComponent<Text>(out var tx)) return tx.text;
            if (t.TryGetComponent<TMP_Text>(out var tm)) return tm.text;
            var l = t.Find("Label");
            if (l != null && l.TryGetComponent<Text>(out tx)) return tx.text;
            return null;
        }
    }

    /// <summary>
    /// D-040: ties a part of a Figma screen to its twin on the game's own screen (found by <see cref="path"/>): pressing it presses
    /// the twin, and it can follow the twin's words, picture, size and whether it is shown. The game's code keeps working on its
    /// own (hidden) screen; this screen only shows it. Everything visual here is free to edit — only the path fields are the link.
    /// Kept up to date by <see cref="UiFront"/> (or <see cref="UiLinkList"/> for list items), so it also works while hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiLink : MonoBehaviour
    {
        [Serializable] public struct ColorRule { public string when; public Color color; }

        [Tooltip("게임 화면에서 짝이 되는 예전 오브젝트 (이름 경로). 바꾸지 마세요.")] public string path;
        [Tooltip("누르면 짝 버튼을 대신 눌러요")] public bool click;
        [Tooltip("짝이 보일 때만 보여요")] public bool active;
        [Tooltip("이 조건일 때만 보여요 (예: on:Icon/Check)")] public string showIf;
        [Tooltip("짝의 글자를 그대로 보여 줘요")] public bool text;
        [Tooltip("글자를 가져올 곳 (비우면 짝)")] public string textPath;
        [Tooltip("뒤에 붙일 글자를 가져올 곳 (예: 진행 1/3)")] public string textPath2;
        [Tooltip("글자 길이에 맞춰 바탕 폭이 늘어나요")] public bool fitWidth;
        [Tooltip("짝의 그림을 그대로 보여 줘요")] public bool picture;
        [Tooltip("그림을 가져올 곳 (비우면 짝, 없으면 짝의 Icon)")] public string picturePath;
        [Tooltip("항목 아이디로 그림 찾기 (Resources 안 폴더, 예: UI2/Items/)")] public string idPicture;
        [Tooltip("짝의 크기(강조·흔들림)를 따라가요")] public bool scale;
        [Tooltip("이 조건이면 이 색 (위에서부터 먼저 맞는 것)")] public ColorRule[] colors = new ColorRule[0];
        [Tooltip("누르면 설정 창을 열어요")] public bool openSettings;
        [Tooltip("게임에서는 늘 숨겨요")] public bool hideInGame;
        [Tooltip("짝(피그마에 없는 예전 화면 부품)을 이 자리로 옮겨요. 이 칸은 짝의 크기를 따라가요 (줄·열에 끼워 넣을 때)")] public bool placeOld;
        [Tooltip("나타나고 사라질 때 이 시간(초) 동안 서서히 (0이면 바로)")] public float fade;

        /// <summary>Which of <see cref="colors"/> holds now (-1: none); a tab whose first rule holds is the chosen one (<see cref="UiTabRaise"/>).</summary>
        public int MatchedRule { get; private set; } = -1;

        Transform baseT, target; float nextTry;
        Button myButton; TMP_Text myText; Image myImage; Graphic myGraphic;
        Vector3 baseScale; Color baseColor; string lastText, lastId; bool ready;
        float fitPad, fitMin, fitFont, fitCentre, fitW0, fitPadLast = -1f, fitSizes = -1f; Vector2 fitParent = -Vector2.one; int fitMask = -1; string fitText;
        bool fitByBox;   // the words' box is pinned to the plate's sides: its Left / Right are the padding
        const float FitEdge = 24f, FitGap = 16f;

        void Prepare()
        {
            if (ready) return; ready = true;
            myButton = GetComponent<Button>();
            if (myButton != null && (click || openSettings)) { myButton.onClick.AddListener(Press); Clickable(myButton); }
            if (text)
            {
                myText = GetComponent<TMP_Text>(); if (myText == null) myText = GetComponentInChildren<TMP_Text>(true);
                if (myText != null && myText.TryGetComponent<LocText>(out var lt)) lt.enabled = false;   // the game's words, not the table's
                if (fitWidth && myText != null)
                {
                    fitFont = myText.enableAutoSizing ? myText.fontSizeMax : myText.fontSize;
                    myText.enableAutoSizing = false; myText.fontSize = fitFont;
                    var rt = (RectTransform)transform; var tr = myText.rectTransform;
                    fitPad = rt.rect.width - myText.preferredWidth; fitMin = rt.rect.width * 0.5f;
                    fitW0 = rt.rect.width; fitCentre = rt.anchoredPosition.x + (0.5f - rt.pivot.x) * rt.rect.width;
                    fitByBox = tr.parent == rt && Mathf.Approximately(tr.anchorMin.x, 0f) && Mathf.Approximately(tr.anchorMax.x, 1f);
                    if (tr.parent == rt && !fitByBox)
                    {
                        // pin the words' box to the plate's sides, so it widens with it (the words stay centred)
                        var wc = new Vector3[4]; tr.GetWorldCorners(wc);
                        float l = rt.InverseTransformPoint(wc[0]).x - rt.rect.xMin, r = rt.rect.xMax - rt.InverseTransformPoint(wc[2]).x;
                        float y0 = tr.offsetMin.y, y1 = tr.offsetMax.y;
                        tr.anchorMin = new Vector2(0f, tr.anchorMin.y); tr.anchorMax = new Vector2(1f, tr.anchorMax.y);
                        tr.offsetMin = new Vector2(l, y0); tr.offsetMax = new Vector2(-r, y1);
                        myText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    }
                }
            }
            if (picture || !string.IsNullOrEmpty(idPicture))
            {
                myImage = GetComponent<Image>();
                if (myImage == null) foreach (var n in new[] { "Icon", "Pic" }) { var c = FindDeep(transform, n); if (c != null && c.TryGetComponent(out myImage)) break; }
            }
            myGraphic = GetComponent<Graphic>();
            baseScale = transform.localScale;
            if (myGraphic != null) baseColor = myGraphic.color;
        }

        /// <summary>
        /// A button only gets clicks through a picture that takes them (Raycast Target). Figma tabs were drawn with that off, so
        /// the 퀘스트 / 보관함 tabs could not be pressed: a button with no such picture gets its own picture turned on.
        /// </summary>
        public static void Clickable(Button b)
        {
            if (b == null) return;
            foreach (var g in b.GetComponentsInChildren<Graphic>(true)) if (g.raycastTarget) return;
            var own = b.targetGraphic != null ? b.targetGraphic : b.GetComponent<Graphic>();
            if (own != null) own.raycastTarget = true;
        }

        CanvasGroup fadeGroup;

        /// <summary>Shows or hides this part over <see cref="fade"/> seconds. False when it is (now) hidden.</summary>
        bool FadeTo(bool on)
        {
            if (fadeGroup == null && !TryGetComponent(out fadeGroup)) fadeGroup = gameObject.AddComponent<CanvasGroup>();
            if (on && !gameObject.activeSelf) { fadeGroup.alpha = 0f; gameObject.SetActive(true); }
            if (!gameObject.activeSelf) return false;
            float a = Mathf.MoveTowards(fadeGroup.alpha, on ? 1f : 0f, Time.unscaledDeltaTime / fade);
            if (!Mathf.Approximately(a, fadeGroup.alpha)) fadeGroup.alpha = a;
            if (!on && a <= 0f) { gameObject.SetActive(false); fadeGroup.alpha = 1f; return false; }
            return true;   // still shown (fading out keeps its words up to date)
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c != t && c.name == name) return c;
            return null;
        }

        /// <summary>Which object of the game's screen the paths start from (its canvas, or one list item).</summary>
        public void Bind(Transform b)
        {
            Prepare();
            if (b == baseT) return;
            baseT = b; target = null; nextTry = 0f; lastText = null; lastId = null;
        }

        Transform Target()
        {
            if (target != null) return target;
            if (Time.unscaledTime < nextTry) return null;
            target = UiPath.Find(baseT, path);
            if (target == null) nextTry = Time.unscaledTime + 0.25f;
            return target;
        }

        void Press()
        {
            if (openSettings) { UiFront.OpenSettings(); return; }
            var t = Target(); if (t == null) return;
            var b = t.GetComponent<Button>(); if (b == null) b = t.GetComponentInChildren<Button>();
            if (b != null && b.interactable && b.gameObject.activeInHierarchy) b.onClick.Invoke();
        }

        public void Sync()
        {
            Prepare();
            if (hideInGame) { if (gameObject.activeSelf) gameObject.SetActive(false); return; }
            var t = Target();
            if (active || !string.IsNullOrEmpty(showIf))
            {
                bool on = (!active || (t != null && t.gameObject.activeInHierarchy)) && UiPath.Test(baseT, showIf);
                if (fade > 0f && Application.isPlaying) { if (!FadeTo(on)) return; }
                else
                {
                    if (gameObject.activeSelf != on) gameObject.SetActive(on);
                    if (!on) return;
                }
            }
            if (text && myText != null)
            {
                var s = UiPath.TextOf(string.IsNullOrEmpty(textPath) ? t : UiPath.Find(baseT, textPath));
                if (!string.IsNullOrEmpty(textPath2))
                {
                    var s2 = UiPath.TextOf(UiPath.Find(baseT, textPath2));
                    if (!string.IsNullOrEmpty(s2)) s = string.IsNullOrEmpty(s) ? s2 : s + "  " + s2;
                }
                if (s != null && s != lastText) { lastText = s; myText.text = s; }
            }
            if (fitWidth && myText != null) Fit();
            if (picture && myImage != null)
            {
                var src = string.IsNullOrEmpty(picturePath) ? t : UiPath.Find(baseT, picturePath);
                Image im = null;
                if (src != null && !src.TryGetComponent(out im)) { var ic = src.Find("Icon"); if (ic != null) ic.TryGetComponent(out im); }
                if (im != null && im.sprite != null && myImage.sprite != im.sprite) { myImage.sprite = im.sprite; myImage.preserveAspect = true; }
            }
            if (!string.IsNullOrEmpty(idPicture) && myImage != null && baseT != null)
            {
                int u = baseT.name.IndexOf('_'); var id = u >= 0 ? baseT.name.Substring(u + 1) : baseT.name;
                if (id != lastId)
                {
                    lastId = id;
                    var sp = Resources.Load<Sprite>(idPicture + id);
                    if (sp == null) { var tex = Resources.Load<Texture2D>(idPicture + id); if (tex != null) sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); }
                    if (sp != null) { myImage.sprite = sp; myImage.preserveAspect = true; myImage.enabled = true; }
                    else myImage.enabled = false;
                }
            }
            if (placeOld && t is RectTransform oldPiece) Place(oldPiece);
            if (scale && t != null) transform.localScale = baseScale * t.localScale.x;
            if (myButton != null && click && t != null && t.TryGetComponent<Button>(out var ob)) myButton.interactable = ob.interactable;
            if (myGraphic != null && colors != null && colors.Length > 0)
            {
                var c = baseColor; MatchedRule = -1;
                for (int i = 0; i < colors.Length; i++) if (UiPath.Test(baseT, colors[i].when)) { c = colors[i].color; MatchedRule = i; break; }
                if (myGraphic.color != c) myGraphic.color = c;
            }
        }

        /// <summary>
        /// The plate grows or shrinks with its words, but only into the free room of its row: between the screen's sides and the
        /// buttons beside it (settings, back, coins …). That room changes with the screen's shape and with what the game shows, so
        /// this runs again when either does. The plate keeps its design place while it fits and moves aside only as far as needed;
        /// words that still do not fit get smaller (down to 60%).
        /// </summary>
        void Fit()
        {
            var rt = (RectTransform)transform; var parent = rt.parent as RectTransform;
            if (parent == null || !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x)) return;   // a stretched plate is sized by its parent
            var pr = parent.rect;
            int mask = 0; float sizes = 0f;   // which neighbours are shown, and how big (rows / columns shrink when a button hides)
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i); if (!c.gameObject.activeSelf) continue;
                if (i < 31) mask |= 1 << i;
                if (c is RectTransform crt) sizes += crt.sizeDelta.x + crt.sizeDelta.y * 3.1f;
            }
            var words = myText.text;
            var tr = myText.rectTransform;
            float pad = fitByBox ? tr.offsetMin.x - tr.offsetMax.x : fitPad;   // Left + Right of the words' box
            if (mask == fitMask && Mathf.Abs(sizes - fitSizes) < 0.5f && (pr.size - fitParent).sqrMagnitude < 0.25f && words == fitText && Mathf.Abs(pad - fitPadLast) < 0.5f) return;
            fitMask = mask; fitSizes = sizes; fitParent = pr.size; fitText = words; fitPadLast = pad;

            myText.enableAutoSizing = false; myText.fontSize = fitFont;
            float want = Mathf.Max(myText.preferredWidth + pad, fitMin);

            float anchorX = pr.xMin + rt.anchorMin.x * pr.width;   // where the anchor sits in the parent
            float centre = anchorX + fitCentre;                     // the design's centre of the plate
            var band = BoxIn(pr, rt);
            float lo = pr.xMin + FitEdge, hi = pr.xMax - FitEdge;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i) as RectTransform;
                if (c == null || c == rt || !c.gameObject.activeSelf) continue;
                var r = BoxIn(pr, c);
                if (r.width <= 1f || r.width > pr.width * 0.6f) continue;                 // backgrounds, full-width bars
                if (r.yMin >= band.yMax - 1f || r.yMax <= band.yMin + 1f) continue;        // not in this row
                if (Mathf.Abs(r.center.x - centre) < fitW0 * 0.5f) continue;               // drawn on the plate in the design
                if (r.center.x < centre) lo = Mathf.Max(lo, r.xMax + FitGap); else hi = Mathf.Min(hi, r.xMin - FitGap);
            }
            // a plate the design puts in the middle stays in the middle: it grows the same both ways
            bool centred = Mathf.Abs(fitCentre) < 1f && Mathf.Approximately(rt.anchorMin.x, 0.5f);
            float room = centred ? 2f * Mathf.Min(centre - lo, hi - centre) : hi - lo;
            float w = Mathf.Max(fitMin, Mathf.Min(want, room));
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            float mid = centred ? centre : w <= room ? Mathf.Clamp(centre, lo + w * 0.5f, hi - w * 0.5f) : (lo + hi) * 0.5f;
            var p = rt.anchoredPosition; p.x = mid - anchorX - (0.5f - rt.pivot.x) * w; rt.anchoredPosition = p;
            if (want > w + 0.5f)
            {
                myText.fontSizeMax = fitFont; myText.fontSizeMin = Mathf.Max(8f, fitFont * 0.6f);
                myText.enableAutoSizing = true;
            }
        }

        /// <summary>
        /// A piece of the game's own screen that Figma has no design for (the quest tracker) is moved onto this spot, so it takes its
        /// place in a row / column; this spot takes the piece's size, so the row makes room for it (and closes up when it is hidden).
        /// </summary>
        void Place(RectTransform old)
        {
            var me = (RectTransform)transform;
            var ps = me.parent != null ? me.parent.lossyScale : Vector3.one;
            var size = new Vector2(old.rect.width * old.lossyScale.x / Mathf.Max(1e-5f, ps.x), old.rect.height * old.lossyScale.y / Mathf.Max(1e-5f, ps.y));
            if (Mathf.Approximately(me.anchorMin.x, me.anchorMax.x) && Mathf.Approximately(me.anchorMin.y, me.anchorMax.y) && (me.sizeDelta - size).sqrMagnitude > 0.25f)
                me.sizeDelta = size;
            var mine = new Vector3[4]; me.GetWorldCorners(mine);
            var its = new Vector3[4]; old.GetWorldCorners(its);
            var move = mine[1] - its[1];   // top-left corners together
            if (move.sqrMagnitude > 1e-8f) old.position += move;
        }

        /// <summary>A child's box in its parent's space, worked out from its anchors and the parent's size (so it is right even
        /// before the canvas has laid things out, on the frame a screen is put in).</summary>
        static Rect BoxIn(Rect parent, RectTransform c)
        {
            Vector2 min = parent.min + Vector2.Scale(c.anchorMin, parent.size) + c.offsetMin;
            Vector2 max = parent.min + Vector2.Scale(c.anchorMax, parent.size) + c.offsetMax;
            var s = c.localScale;
            if (!Mathf.Approximately(s.x, 1f) || !Mathf.Approximately(s.y, 1f))
            {
                var piv = new Vector2(Mathf.Lerp(min.x, max.x, c.pivot.x), Mathf.Lerp(min.y, max.y, c.pivot.y));
                min = piv + Vector2.Scale(min - piv, s); max = piv + Vector2.Scale(max - piv, s);
            }
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }
    }
}
