using System.Collections.Generic;
using Mongle.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-040: ties the Figma screens (Assets/_UI수정) to the game's own screens: every button, word and list of a Figma screen gets a
    /// <see cref="UiLink"/> / <see cref="UiLinkList"/> naming its twin on the code-built screen, and the root a <see cref="UiFront"/>
    /// listing what it covers. Only these link components are (re)made — the look of the prefabs is left as it is, so a designer's
    /// edits survive. Run after "프리팹 다시 만들기" (it does so itself) or from the menu.
    /// </summary>
    public static class UiV2Wire
    {
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color Cream = C("#F9EDDD"), Cream2 = C("#FAF0DF"), Sky = C("#A0DCF8"), Pink = C("#FF8FAB"),
            PinkPale = C("#FFE1E6"), Pink2 = C("#FFA9C2"), Yellow = C("#FFEC76"), Mint = C("#A8DBC7"), Gray = C("#DCD3CC");

        [MenuItem("Mongle/UI v2/게임 화면과 다시 연결하기")]
        public static void WireAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[UiV2Wire] stop play mode first"); return; }
            int n = 0;
            foreach (var name in new[] { "Title", "Intake", "RepairHud", "VillageHud", "Quest", "Storage", "RideInvite", "Photo" })
            {
                var path = UiEditFolder.FrontPath(name);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[UiV2Wire] no prefab " + path); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    UiV2Layout.Apply(name, root.transform);   // rows / columns first, so a rebuilt screen gets them too
                    Clear(root.transform);
                    Wire(name, root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root, path); n++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("[UiV2Wire] linked " + n + " screens");
        }

        static void Clear(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<UiLink>(true)) Object.DestroyImmediate(c, true);
            foreach (var c in root.GetComponentsInChildren<UiLinkList>(true)) Object.DestroyImmediate(c, true);
            foreach (var c in root.GetComponentsInChildren<UiTabFilter>(true)) Object.DestroyImmediate(c, true);
            foreach (var c in root.GetComponentsInChildren<UiFront>(true)) Object.DestroyImmediate(c, true);
        }

        // ---------------- helpers ----------------

        static Transform T(Transform root, string at)
        {
            var t = F(root, at);
            if (t == null) Debug.LogWarning("[UiV2Wire] " + root.name + ": no " + at);
            return t;
        }

        /// <summary>A piece by its path; when pieces were put into rows / columns since (<see cref="UiV2Layout"/>), the path's names
        /// still have to appear in order, groups may sit between them ("Window/Tab_Active" finds "Window/TabRow/Tab_Active",
        /// "Btn_Home" finds "LeftColumn/TopLeftRow/Btn_Home").</summary>
        static Transform F(Transform root, string at)
        {
            if (string.IsNullOrEmpty(at)) return root;
            var t = root.Find(at); if (t != null) return t;
            var want = at.Split('/');
            foreach (var c in root.GetComponentsInChildren<Transform>(true))
            {
                if (c == root || c.name != want[want.Length - 1]) continue;
                int k = want.Length - 2;
                for (var p = c.parent; p != null && p != root && k >= 0; p = p.parent) if (p.name == want[k]) k--;
                if (k < 0) return c;
            }
            return null;
        }

        static UiLink L(Transform root, string at, string path, bool click = false, bool active = false)
        {
            var t = T(root, at); if (t == null) return Dummy;
            var l = t.gameObject.AddComponent<UiLink>(); l.path = path; l.click = click; l.active = active;
            return l;
        }

        /// <summary>A throwaway link for a missing object (warned about above), so the wiring code below never has to check.</summary>
        static UiLink dummy;
        static UiLink Dummy
        {
            get
            {
                if (dummy == null) dummy = new GameObject("UiV2Wire spare") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<UiLink>();
                return dummy;
            }
        }

        static UiLink Text(Transform root, string at, string textPath, bool fit = false)
        {
            var l = L(root, at, textPath); l.text = true; l.textPath = textPath; l.fitWidth = fit; return l;
        }

        static UiLink.ColorRule R(string when, Color c) => new UiLink.ColorRule { when = when, color = c };

        static UiFront Front(Transform root, params string[] hide)
        {
            var f = root.gameObject.AddComponent<UiFront>(); f.hideOld = hide; return f;
        }

        static UiLinkList List(Transform root, string at, string path, string prefix, string template, string[] samples, Vector2 step, int pageSize = 0)
        {
            var t = T(root, at); if (t == null) return null;
            var li = t.gameObject.AddComponent<UiLinkList>();
            li.path = path; li.prefix = prefix; li.step = step; li.pageSize = pageSize;
            li.template = (RectTransform)T(root, template);
            var s = new List<Transform>(); foreach (var x in samples) { var st = F(root, x); if (st != null) s.Add(st); }
            li.samples = s.ToArray();
            return li;
        }

        static Vector2 Pos(Transform root, string at) => ((RectTransform)F(root, at)).anchoredPosition;

        static void Tab(Transform root, string at, UiLinkList list, string filter, Color on, Color off)
        {
            var t = T(root, at); if (t == null) return;
            if (t.GetComponent<Button>() == null) { var b = t.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; }
            UiLink.Clickable(t.GetComponent<Button>());   // a tab plate drawn with Raycast Target off takes no clicks
            var f = t.gameObject.AddComponent<UiTabFilter>(); f.list = list; f.filter = filter; f.on = on; f.off = off;
        }

        static void EnsureButton(Transform root, string at)
        {
            var t = T(root, at); if (t == null) return;
            var b = t.GetComponent<Button>();
            if (b == null) { b = t.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; }
            UiLink.Clickable(b);   // a plate drawn with Raycast Target off takes no clicks
        }

        // ---------------- the screens ----------------

        static void Wire(string name, Transform r)
        {
            switch (name)
            {
                case "Title":
                    Front(r, "BG", "Cloud", "Deco", "Logo", "Btn_설정", "Menu");
                    L(r, "Btn_New", "Menu/Btn_처음부터", true, true);
                    L(r, "Btn_Continue", "Menu/Btn_가게 문 열기", true, true);
                    L(r, "Btn_Settings", "Btn_설정", false, true).openSettings = true;
                    break;

                case "Intake":
                    Front(r, "Dialog").onTopWhen = "Dialog";
                    L(r, "Btn_Settings", "").hideInGame = true;
                    L(r, "Dialog", "Dialog", false, true);
                    Text(r, "Dialog/Title", "Dialog/Label#0");
                    Text(r, "Dialog/Body", "Dialog/Label#1");
                    L(r, "Dialog/Btn_Look", "Dialog/Btn_ok", true, true);
                    Text(r, "Dialog/Btn_Look/Label", "Dialog/Btn_ok/Label");
                    L(r, "Dialog/Btn_Sorry", "Dialog/Btn_second", true, true);
                    Text(r, "Dialog/Btn_Sorry/Label", "Dialog/Btn_second/Label");
                    break;

                case "RepairHud":
                {
                    Front(r, "Banner", "Notebook", "Shelf", "Btn_＋", "Btn_－", "Btn_돌아가기", "Btn_설정");
                    L(r, "Btn_Back", "Btn_돌아가기", true, true);
                    L(r, "Btn_Settings", "Btn_설정", false, true).openSettings = true;
                    L(r, "Btn_ZoomIn", "Btn_＋", true, true);
                    L(r, "Btn_ZoomOut", "Btn_－", true, true);
                    var done = Text(r, "Done", "Banner/Label", true); done.path = "Banner"; done.active = true;
                    L(r, "Reward", "").hideInGame = true;
                    L(r, "Notes", "Notebook", false, true);
                    // one row of the care notes: done (✓, blue), being mended (pink) or still to find (? / !)
                    var item0 = r.Find("Notes/Item0");
                    if (item0 != null && item0.Find("Unknown") == null && r.Find("Notes/Item1/Unknown") != null)
                    {
                        var u = Object.Instantiate(r.Find("Notes/Item1/Unknown").gameObject, item0, false); u.name = "Unknown"; u.SetActive(false);
                    }
                    var step = (Pos(r, "Notes/Item3") - Pos(r, "Notes/Item0")) / 3f;
                    List(r, "Notes", "Notebook", "Row", "Notes/Item0", new[] { "Notes/Item1", "Notes/Item2", "Notes/Item3" }, step);
                    var row = L(r, "Notes/Item0", "");
                    row.colors = new[] { R("on:Icon/Check", Sky), R("sprite:=row_on", PinkPale), R("", Cream2) };
                    L(r, "Notes/Item0/Check", "").showIf = "on:Icon/Check";
                    var lab = Text(r, "Notes/Item0/Label", "Label"); lab.showIf = "!text:Label=";
                    L(r, "Notes/Item0/Unknown", "").showIf = "!text:Icon/Label=";
                    Text(r, "Notes/Item0/Unknown/Q", "Icon/Label");
                    // the tool bar: four tools a page, ◀ ▶ for the rest
                    L(r, "ToolBar", "Shelf", false, true);
                    var slotStep = new Vector2(Pos(r, "ToolBar/Slots/Slot_sponge").x - Pos(r, "ToolBar/Slots/Slot_tweezers").x, 0f);
                    var tools = List(r, "ToolBar/Slots", "Shelf", "Btn_", "ToolBar/Slots/Slot_tweezers",
                        new[] { "ToolBar/Slots/Slot_sponge", "ToolBar/Slots/Slot_needle", "ToolBar/Slots/Slot_injector" }, slotStep, 4);
                    if (tools != null) { tools.prev = F(r, "Btn_ToolsLeft")?.GetComponent<Button>(); tools.next = F(r, "Btn_ToolsRight")?.GetComponent<Button>(); tools.centre = true; }
                    var slot = L(r, "ToolBar/Slots/Slot_tweezers", "", true); slot.scale = true;
                    var icon = L(r, "ToolBar/Slots/Slot_tweezers/Icon", ""); icon.picture = true; icon.picturePath = "Icon";
                    var irt = (RectTransform)F(r, "ToolBar/Slots/Slot_tweezers/Icon");
                    if (irt != null) { irt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f); irt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f); irt.GetComponent<Image>().preserveAspect = true; }
                    Text(r, "ToolBar/Slots/Slot_tweezers/Label", "Label");
                    break;
                }

                case "VillageHud":
                {
                    Front(r, "Btn_＋", "Btn_－", "Btn_설정", "Status", "Hud/TileBoard", "Hud/Btn_가구 옮기기", "Hud/Btn_보관함", "Hud/Btn_앨범",
                        "Hud/Btn_퀘스트", "Hud/Coins", "RideBar", "Btn_돌아가기");
                    L(r, "Btn_Home", "Btn_돌아가기", true, true);
                    L(r, "Btn_Quest", "Hud/Btn_퀘스트", true, true);
                    L(r, "Btn_ZoomIn", "Btn_＋", true, true);
                    L(r, "Btn_ZoomOut", "Btn_－", true, true);
                    L(r, "Coins", "Hud/Coins", false, true);
                    Text(r, "Coins/Amount", "Hud/Coins/Label");
                    L(r, "Btn_Settings", "Btn_설정", false, true).openSettings = true;
                    L(r, "Toolbar", "Hud", false, true);
                    L(r, "Toolbar/Btn_furniture", "Hud/Btn_가구 옮기기", true, true);
                    L(r, "Toolbar/Btn_album", "Hud/Btn_앨범", true, true);
                    L(r, "Toolbar/Btn_storage", "Hud/Btn_보관함", true, true);
                    var banner = Text(r, "Banner", "Status/Label", true); banner.path = "Status"; banner.active = true; banner.fade = 0.4f;   // greets on arrival, then fades
                    L(r, "Toast", "RideBar", false, true);
                    Text(r, "Toast/Text", "RideBar/Label");
                    L(r, "Toast/Btn_Watch", "RideBar/Btn_밖에서 보기", true, true);
                    Text(r, "Toast/Btn_Watch/Label", "RideBar/Btn_밖에서 보기/Label");
                    L(r, "Hint", "").hideInGame = true;
                    if (F(r, "QuestTrackerSlot") != null) L(r, "QuestTrackerSlot", "Hud/QuestTracker", false, true).placeOld = true;
                    break;
                }

                case "Quest":
                {
                    { var qf = Front(r, "QuestBoard"); qf.onTopWhen = "QuestBoard"; qf.dimBehind = true; }
                    if (F(r, "Window/Panel/Empty") != null) { var qe = Text(r, "Window/Panel/Empty", "QuestBoard/Label#1"); qe.showIf = "!text:QuestBoard/Label#1="; }
                    L(r, "Window", "QuestBoard", false, true);
                    string[] tabs = { "Tab_Active", "Tab_Done", "Tab_Story" };
                    for (int i = 0; i < 3; i++)
                    {
                        EnsureButton(r, "Window/" + tabs[i]);
                        var tab = L(r, "Window/" + tabs[i], "QuestBoard/QuestTab" + i, true);
                        tab.colors = new[] { R("sprite:QuestBoard/QuestTab" + i + "=q_tab_on", Pink), R("", UiV2Layout.TabOff) };
                        Text(r, "Window/" + tabs[i] + "/Label", "QuestBoard/QuestTab" + i + "/Label");
                    }
                    L(r, "Window/Btn_Close", "QuestBoard/Btn_close", true);
                    const string cards = "Window/Panel/List/Content/";
                    var list = List(r, "Window/Panel/List", "QuestBoard/Viewport/Rows", "QuestRow_", cards + "Card0",
                        new[] { cards + "Card1", cards + "Card2", cards + "Card3" }, Pos(r, cards + "Card1") - Pos(r, cards + "Card0"));
                    if (list != null) list.growContent = true;
                    var title = Text(r, cards + "Card0/Title", "Label#0"); title.textPath2 = "Label#2";
                    Text(r, cards + "Card0/Desc", "Label#1");
                    L(r, cards + "Card0/Coin", "").showIf = "on:Reward";
                    var reward = Text(r, cards + "Card0/Reward", "Reward/Label"); reward.showIf = "on:Reward";
                    L(r, cards + "Card0/Btn_Action", "QuestAction_*", true, true);
                    if (r.Find(cards + "Card0/Btn_Action/Label") != null) Text(r, cards + "Card0/Btn_Action/Label", "QuestAction_*/Label");
                    L(r, cards + "Card0/Avatar", "").colors = new[]
                    {
                        R("sprite:Icon=q_ic_pink", Pink2), R("sprite:Icon=q_ic_mint", Mint), R("sprite:Icon=q_ic_yellow", Yellow), R("sprite:Icon=q_ic_gray", Gray),
                    };
                    break;
                }

                case "Storage":
                {
                    { var sf = Front(r, "StoragePanel/Storage"); sf.onTopWhen = "StoragePanel/Storage"; sf.dimBehind = true; }
                    if (F(r, "Window/Panel/Empty") != null) { var se = Text(r, "Window/Panel/Empty", "StoragePanel/Storage/Empty"); se.showIf = "on:StoragePanel/Storage/Empty"; }
                    L(r, "Window", "StoragePanel/Storage", false, true);
                    L(r, "Window/Btn_Close", "StoragePanel/Storage/Btn_close", true);
                    const string grid = "Window/Panel/Grid/Content/";
                    var list = List(r, "Window/Panel/Grid", "StoragePanel/Storage/Viewport/Cards", "Card_", grid + "Item0",
                        new[] { grid + "Item1", grid + "Item2", grid + "Item3", grid + "Item4" }, Vector2.zero);
                    Text(r, grid + "Item0/Name", "Label#0");
                    Text(r, grid + "Item0/Count/Text", "Label#2");
                    L(r, grid + "Item0/PicBack/Pic", "").idPicture = "UI2/Items/";
                    L(r, grid + "Item0/Btn_Use", "<button>", true, true);
                    Text(r, grid + "Item0/Btn_Use/Label", "<button>/Label");
                    Tab(r, "Window/Tab_All", list, "", Pink, UiV2Layout.TabOff);
                    Tab(r, "Window/Tab_Furniture", list, "Fur_,Plant_", Pink, UiV2Layout.TabOff);
                    Tab(r, "Window/Tab_Accessory", list, "Acc_", Pink, UiV2Layout.TabOff);
                    break;
                }

                case "RideInvite":
                {
                    Front(r, "FriendDialog").onTopWhen = "FriendDialog";
                    L(r, "Bubble", "FriendDialog", false, true);
                    Text(r, "Bubble/Text", "FriendDialog/Label#0");
                    var sub = Text(r, "Bubble/Sub", "FriendDialog/Label#1"); sub.showIf = "!text:FriendDialog/Label#1=";
                    L(r, "NameTag", "FriendDialog/NameTag", false, true);
                    Text(r, "NameTag/Name", "FriendDialog/NameTag/Label");
                    var yes = (RectTransform)F(r, "Btn_Yes"); var later = (RectTransform)r.Find("Btn_Later");
                    float gap = yes != null ? yes.rect.height + 18f : 100f;
                    // the choices: on their own grid holder when the screen has one (UiV2Layout: Answers/Choices)
                    var choiceList = List(r, F(r, "Choices") != null ? "Choices" : "", "FriendDialog/Choices", "Choice_", "Btn_Yes", new string[0], new Vector2(0f, gap));
                    if (choiceList != null) { choiceList.gridWidth = 440f; choiceList.narrowHide = "Heart"; }
                    // a second list on the same object is not possible: the footer buttons get their own holder
                    var holder = F(r, "Footer");
                    if (holder == null && later != null)
                    {
                        holder = new GameObject("Footer", typeof(RectTransform)).transform; holder.SetParent(r, false);
                        var hr = (RectTransform)holder; hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.offsetMin = hr.offsetMax = Vector2.zero;
                        holder.SetSiblingIndex(later.GetSiblingIndex()); later.SetParent(holder, true);
                    }
                    List(r, "Footer", "FriendDialog/Footer", "DialogBtn_", "Footer/Btn_Later", new string[0], new Vector2(0f, -gap));
                    L(r, "Btn_Yes", "", true);
                    Text(r, "Btn_Yes/Label", "Label");
                    L(r, "Footer/Btn_Later", "", true);
                    Text(r, "Footer/Btn_Later/Label", "Label");
                    break;
                }

                case "Photo":
                {
                    Front(r, "Btn_설정", "Controls");
                    L(r, "Btn_Settings", "Btn_설정", false, true).openSettings = true;
                    L(r, "Pose_smile", "Controls/Btn_동작", true, true);
                    Text(r, "Pose_smile/Label", "Controls/Btn_동작/Label#0");
                    L(r, "Pose_basic", "Controls/Btn_프레임", true, true);
                    Text(r, "Pose_basic/Label", "Controls/Btn_프레임/Label#0");
                    L(r, "Btn_Shoot", "Controls/Btn_찰칵", true, true);
                    L(r, "Zoom", "").hideInGame = true;
                    for (int i = 0; i < r.childCount; i++)
                    {
                        var c = r.GetChild(i);
                        if ((c.name == "Corner" || c.name == "Center" || c.name == "Pin") && c.GetComponent<UiLink>() == null)
                        { var l = c.gameObject.AddComponent<UiLink>(); l.path = "Controls"; l.active = true; }
                    }
                    break;
                }
            }
        }
    }
}
