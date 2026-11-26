using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mongle.UI
{
    /// <summary>
    /// D-035: every screen the code draws has an editable prefab (made by the Mongle > UI menu); since D-039 they live in the UI folder
    /// Assets/_UI수정 (one subfolder per scene, listed in Resources/UI/SkinIndex) and each scene holds an editable copy of its screen.
    /// Right after the code builds (a part of) the screen, <see cref="Apply(Transform, string, string)"/> lays the prefab over it,
    /// object by object (matched by name path). Only what the designer changed in the prefab since it was made is laid over
    /// (each part remembers how it was made, <see cref="UiSkinPart.baked"/>): position / size / anchors / scale / rotation,
    /// the Image / RawImage / Text / Button / FitToText / Shadow / Outline settings (picture, colour, words, font, size …).
    /// Everything else stays the code's, so laying the prefab again later never undoes what the game has changed since.
    /// Objects the designer added (no <see cref="UiSkinPart"/>, or a copy of one) appear as decorations; a part ticked
    /// <see cref="UiSkinPart.hideInGame"/> is kept invisible; in a list (<see cref="UiSkinPart.listOf"/>) the one sample item is the
    /// model for every item the code makes.
    /// </summary>
    public static class UiSkin
    {
        public const string Folder = "UI/Skins";

        /// <summary>The look components whose settings the prefab can change (Outline is a Shadow, so both are listed).</summary>
        static readonly Type[] Kinds = { typeof(Image), typeof(RawImage), typeof(Text), typeof(Button), typeof(FitToText), typeof(Shadow), typeof(Outline) };
        /// <summary>Settings that hold events or links to other objects: never copied from the prefab.</summary>
        static readonly HashSet<string> Skip = new HashSet<string> { "m_OnClick", "m_OnCullStateChanged", "m_Navigation", "m_SpriteState", "text" };

        static UiSkinIndex index;

        /// <summary>The screen prefab named <paramref name="skin"/>: from the UI folder's list (D-039, Resources/UI/SkinIndex),
        /// else from the old place Resources/UI/Skins.</summary>
        public static GameObject Find(string skin)
        {
            if (string.IsNullOrEmpty(skin)) return null;
            if (index == null) index = Resources.Load<UiSkinIndex>(UiSkinIndex.ResourcePath);
            var found = index != null ? index.Get(skin) : null;
            return found != null ? found : Resources.Load<GameObject>(Folder + "/" + skin);
        }

        /// <summary>D-040: the Figma screen named <paramref name="name"/> (listed in Resources/UI/SkinIndex).</summary>
        public static GameObject FindFront(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (index == null) index = Resources.Load<UiSkinIndex>(UiSkinIndex.ResourcePath);
            return index != null ? index.GetFront(name) : null;
        }

        /// <summary>Lays skin <paramref name="skin"/> (or its part at <paramref name="path"/>, e.g. "Shelf") over <paramref name="built"/>.</summary>
        public static bool Apply(Transform built, string skin, string path = null)
        {
            if (Off) return false;
            var src = Find(skin); if (src == null || built == null) return false;
            var s = src.transform;
            if (!string.IsNullOrEmpty(path)) { s = Walk(s, path); if (s == null) return false; }
            Apply(built, s);
            return true;
        }

        /// <summary>Lays the skin over <paramref name="part"/>, a part of the screen <paramref name="root"/> made later (a panel opened for the
        /// first time, a list filled again): the skin's object at the same place is used.</summary>
        public static bool ApplyUnder(Transform root, Transform part, string skin)
        {
            if (root == null || part == null) return false;
            if (part == root) return Apply(root, skin);
            var path = PathOf(part, root);
            return path != null && Apply(part, skin, path);
        }

        /// <summary>The same with the skin's object itself (tests, editors).</summary>
        public static void Apply(Transform built, Transform skin)
        {
            if (Off || built == null || skin == null) return;
            Copy(skin, built, PathOf(skin), skin.parent == null || IsItem(skin), InItem(skin));
        }

        /// <summary>While true nothing is laid over (the baker builds the screens as the code alone draws them).</summary>
        public static bool Off;

        // ---------------- paths ----------------

        /// <summary>The object at a key path under <paramref name="t"/> ("PlaceBar/Btn_돌리기"), or null.</summary>
        public static Transform At(Transform t, string path) => Walk(t, path);

        static Transform Walk(Transform t, string path)
        {
            foreach (var part in path.Split('/')) { t = ChildByKey(t, part); if (t == null) return null; }
            return t;
        }

        /// <summary>A child's key: its name, plus "#n" when siblings share the name (n = its place among them).</summary>
        public static string KeyOf(Transform c)
        {
            var p = c.parent; if (p == null) return c.name;
            int same = 0, index = 0;
            for (int i = 0; i < p.childCount; i++)
            {
                var o = p.GetChild(i); if (o.name != c.name) continue;
                if (o == c) index = same;
                same++;
            }
            return same > 1 ? c.name + "#" + index : c.name;
        }

        static Transform ChildByKey(Transform p, string key)
        {
            for (int i = 0; i < p.childCount; i++) { var c = p.GetChild(i); if (KeyOf(c) == key) return c; }
            return null;
        }

        /// <summary>The key path of an object from <paramref name="root"/> (or from the top of its hierarchy): "Shelf/Btn_핀셋/Icon".
        /// Null when it is not under <paramref name="root"/>.</summary>
        public static string PathOf(Transform t, Transform root = null)
        {
            var parts = new List<string>();
            var c = t;
            for (; c != null && c != root && c.parent != null; c = c.parent) parts.Insert(0, KeyOf(c));
            if (root != null && c != root) return null;
            return string.Join("/", parts);
        }

        static string Join(string path, string key) => string.IsNullOrEmpty(path) ? key : path + "/" + key;

        // ---------------- lists ----------------

        static string[] Prefixes(UiSkinPart p) => p == null || string.IsNullOrEmpty(p.listOf) ? null : p.listOf.Split(',');

        static string PrefixOf(string name, string[] list)
        {
            foreach (var x in list) if (x == "*" || name.StartsWith(x, StringComparison.Ordinal)) return x;
            return null;
        }

        /// <summary>An item of a list the code fills (its place is the code's to choose).</summary>
        static bool IsItem(Transform t)
        {
            if (t.parent == null) return false;
            var list = Prefixes(t.parent.GetComponent<UiSkinPart>());
            return list != null && PrefixOf(t.name, list) != null;
        }

        static bool InItem(Transform t)
        {
            for (var c = t; c != null; c = c.parent) if (IsItem(c)) return true;
            return false;
        }

        /// <summary>Inside a list item, names often end in the item's id ("QuestAction_q12"): the part before the id.</summary>
        static string Stem(string name) { int i = name.IndexOf('_'); return i < 0 ? null : name.Substring(0, i + 1); }

        static Transform ByStem(Transform p, string name)
        {
            var stem = Stem(name); if (stem == null) return null;
            for (int i = 0; i < p.childCount; i++) { var c = p.GetChild(i); if (c.name.StartsWith(stem, StringComparison.Ordinal)) return c; }
            return null;
        }

        /// <summary>The list's sample item that is the model for a code-made item named <paramref name="name"/>.</summary>
        static Transform Model(Transform src, string name, string[] list)
        {
            var prefix = PrefixOf(name, list); if (prefix == null) return null;
            for (int i = 0; i < src.childCount; i++)
            {
                var c = src.GetChild(i);
                if (c.GetComponent<UiSkinPart>() != null && (prefix == "*" || c.name.StartsWith(prefix, StringComparison.Ordinal))) return c;
            }
            return null;
        }

        // ---------------- laying over ----------------

        static void Copy(Transform src, Transform dst, string path, bool keepPlace, bool loose)
        {
            src.TryGetComponent<UiSkinPart>(out var part);
            var was = part != null && part.baked != null && part.baked.valid ? part.baked : null;
            if (!keepPlace && src is RectTransform sr && dst is RectTransform dr) CopyRect(sr, dr, was);
            CopyLooks(src.gameObject, dst.gameObject, was);
            if (part != null && part.hideInGame) Hide(dst.gameObject);
            var list = Prefixes(part);
            if (list != null)
                for (int i = 0; i < dst.childCount; i++)
                {
                    var dc = dst.GetChild(i); if (dc.GetComponent<UiSkinAdded>() != null) continue;
                    var model = Model(src, dc.name, list);
                    if (model != null) Copy(model, dc, Join(path, KeyOf(model)), true, true);
                }
            for (int i = 0; i < src.childCount; i++)
            {
                var sc = src.GetChild(i);
                if (list != null && PrefixOf(sc.name, list) != null) continue;   // list models: laid over the code's items above
                string key = KeyOf(sc), childPath = Join(path, key);
                var dc = ChildByKey(dst, key);
                if (dc == null && loose) dc = ByStem(dst, sc.name);
                bool made = sc.TryGetComponent<UiSkinPart>(out var p) && p.bakedPath == childPath;
                if (dc != null && (made || dc.GetComponent<UiSkinAdded>() != null)) { Copy(sc, dc, childPath, false, loose); continue; }
                if (made) continue;   // a part the code has not made (yet): the code decides when it exists
                // the designer's own object (or a copy of a part): shown as it is in the prefab
                var go = Object.Instantiate(sc.gameObject, dst, false); go.name = sc.name;
                foreach (var m in go.GetComponentsInChildren<UiSkinPart>(true)) { if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m); }
                go.AddComponent<UiSkinAdded>();
                go.transform.SetSiblingIndex(Mathf.Min(i, dst.childCount - 1));
            }
        }

        static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-6f;
        static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

        static void CopyRect(RectTransform s, RectTransform d, UiSkinSnapshot was)
        {
            if (was == null)
            {
                d.anchorMin = s.anchorMin; d.anchorMax = s.anchorMax; d.pivot = s.pivot;
                d.sizeDelta = s.sizeDelta; d.anchoredPosition = s.anchoredPosition;
                d.localScale = s.localScale; d.localRotation = s.localRotation;
                return;
            }
            if (!Near(s.anchorMin, was.anchorMin)) d.anchorMin = s.anchorMin;
            if (!Near(s.anchorMax, was.anchorMax)) d.anchorMax = s.anchorMax;
            if (!Near(s.pivot, was.pivot)) d.pivot = s.pivot;
            if (!Near(s.sizeDelta, was.sizeDelta)) d.sizeDelta = s.sizeDelta;
            if (!Near(s.anchoredPosition, was.anchoredPosition)) d.anchoredPosition = s.anchoredPosition;
            if (!Near(s.localScale, was.scale)) d.localScale = s.localScale;
            if (Quaternion.Angle(s.localRotation, Quaternion.Euler(was.euler)) > 0.01f) d.localRotation = s.localRotation;
        }

        static List<Component> Comps(GameObject g, Type kind)
        {
            var all = new List<Component>();
            foreach (var c in g.GetComponents(kind)) if (c != null && c.GetType() == kind) all.Add(c);
            return all;
        }

        static void CopyLooks(GameObject s, GameObject d, UiSkinSnapshot was)
        {
            foreach (var kind in Kinds)
            {
                var from = Comps(s, kind); if (from.Count == 0) continue;
                var to = Comps(d, kind);
                for (int i = 0; i < from.Count; i++)
                {
                    var look = was?.Find(kind.Name, i);
                    var dst = i < to.Count ? to[i] : null;
                    if (dst == null)
                    {
                        if (look != null || !typeof(Shadow).IsAssignableFrom(kind)) continue;
                        dst = d.AddComponent(kind);   // a shadow or outline the designer added
                    }
                    Lay(from[i], dst, look);
                }
            }
        }

        static void Lay(Component src, Component dst, UiSkinLook look)
        {
            bool changed = false;
            if (look == null)
            {
                // not in the prefab when it was made (or a designer object): everything as the prefab says,
                // except pictures and fonts the prefab cannot hold (made while playing)
                var pic = Picture(dst); var font = dst is Text dt ? dt.font : null;
                var all = Partial(Strip(JsonUtility.ToJson(src)), "{}");   // every setting but the links and events
                if (all != null) JsonUtility.FromJsonOverwrite(all, dst);
                SetRefs(src, dst, pic, font, true);
                if (src is Behaviour sb && dst is Behaviour db) db.enabled = sb.enabled;
                changed = true;
            }
            else
            {
                var now = Strip(JsonUtility.ToJson(src));
                if (now != look.json)
                {
                    var partial = Partial(now, look.json);
                    if (partial != null)
                    {
                        // new words replace only the words the screen had when the prefab was made: what the game wrote itself
                        // (a name, each list item's own label, a live message) stays the game's (D-039)
                        string keep = dst is Text dt && dt.text != BakedWords(look.json) ? dt.text : null;
                        JsonUtility.FromJsonOverwrite(partial, dst);
                        if (keep != null) ((Text)dst).text = keep;
                        changed = true;
                    }
                }
                if (Picture(src) != look.picture) { SetPicture(dst, Picture(src)); changed = true; }
                if (src is Text st && st.font != look.font && dst is Text tt) { tt.font = st.font; changed = true; }
                if (src is Behaviour sb && sb.enabled != look.enabled && dst is Behaviour db) { db.enabled = sb.enabled; changed = true; }
            }
            if (!changed) return;
            if (dst is Graphic g) g.SetAllDirty();
            if (dst is FitToText f && f.isActiveAndEnabled) f.Apply();
        }

        [Serializable] class Words { public string m_Text; }

        /// <summary>The words a Text had when the prefab was made (from its stored settings).</summary>
        static string BakedWords(string json)
        {
            try { return JsonUtility.FromJson<Words>(json)?.m_Text; }
            catch (ArgumentException) { return null; }
        }

        static Object Picture(Component c) => c is Image i ? i.sprite : c is RawImage r ? (Object)r.texture : null;
        static void SetPicture(Component c, Object pic)
        {
            if (c is Image i) i.sprite = pic as Sprite;
            else if (c is RawImage r) r.texture = pic as Texture;
        }

        /// <summary>The prefab's picture and font (they are links, so not in the JSON); kept from the game when the prefab has none.</summary>
        static void SetRefs(Component src, Component dst, Object keepPic, Font keepFont, bool keepWhenNull)
        {
            var pic = Picture(src);
            SetPicture(dst, pic != null || !keepWhenNull ? pic : keepPic);
            if (src is Text st && dst is Text dt) dt.font = st.font != null || !keepWhenNull ? st.font : keepFont;
        }

        /// <summary>Invisible and not clickable, whatever the code later does with the object's active state.</summary>
        static void Hide(GameObject g)
        {
            if (!g.TryGetComponent<CanvasGroup>(out var cg)) cg = g.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; cg.ignoreParentGroups = false;
        }

        // ---------------- what a part looked like ----------------

        /// <summary>How <paramref name="t"/> looks now (stored in its <see cref="UiSkinPart"/> when the prefab is made).</summary>
        public static UiSkinSnapshot Snapshot(Transform t)
        {
            var s = new UiSkinSnapshot { valid = true };
            if (t is RectTransform r)
            {
                s.anchorMin = r.anchorMin; s.anchorMax = r.anchorMax; s.pivot = r.pivot; s.sizeDelta = r.sizeDelta; s.anchoredPosition = r.anchoredPosition;
            }
            s.scale = t.localScale; s.euler = t.localEulerAngles;
            foreach (var kind in Kinds)
            {
                var all = Comps(t.gameObject, kind);
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    s.looks.Add(new UiSkinLook
                    {
                        type = kind.Name, index = i, json = Strip(JsonUtility.ToJson(c)),
                        enabled = !(c is Behaviour b) || b.enabled, picture = Picture(c), font = c is Text tx ? tx.font : null,
                    });
                }
            }
            return s;
        }

        // ---------------- JSON ----------------

        static readonly Regex Link = new Regex("\\{\\s*\"(?:instanceID|entityId|m_EntityId|m_FileID)\"\\s*:\\s*-?\\d+\\s*\\}", RegexOptions.Compiled);

        /// <summary>The JSON with every link to an object written as null (links are compared as objects, not as their run-time ids).</summary>
        public static string Strip(string json) => Link.Replace(json, "null");

        /// <summary>A JSON with only the settings of <paramref name="now"/> that differ from <paramref name="was"/> (inner objects
        /// likewise, so changing a font size does not bring back the alignment the game set); null when nothing differs.</summary>
        public static string Partial(string now, string was)
        {
            var a = Fields(now); var b = Fields(was); if (a == null || b == null) return null;
            var old = new Dictionary<string, string>(); foreach (var kv in b) old[kv.Key] = kv.Value;
            var sb = new StringBuilder("{"); bool any = false;
            foreach (var kv in a)
            {
                if (Skip.Contains(kv.Key) || kv.Value == "null") continue;
                old.TryGetValue(kv.Key, out var o);
                if (o == kv.Value) continue;
                var v = kv.Value;
                if (v.StartsWith("{")) { v = Partial(v, o != null && o.StartsWith("{") ? o : "{}"); if (v == null) continue; }
                else if (v.StartsWith("[") && v.Contains("null")) continue;   // a list of links: not the prefab's to change
                if (any) sb.Append(',');
                sb.Append('"').Append(kv.Key).Append("\":").Append(v); any = true;
            }
            return any ? sb.Append('}').ToString() : null;
        }

        /// <summary>The top-level fields of a JSON object, in order, each value as its raw text; null if it is not an object.</summary>
        static List<KeyValuePair<string, string>> Fields(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int i = 0, n = json.Length;
            void Ws() { while (i < n && char.IsWhiteSpace(json[i])) i++; }
            void SkipString() { i++; while (i < n && json[i] != '"') { if (json[i] == '\\') i++; i++; } i++; }
            Ws(); if (i >= n || json[i] != '{') return null; i++;
            var list = new List<KeyValuePair<string, string>>();
            while (true)
            {
                Ws(); if (i >= n) return null;
                if (json[i] == '}') break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') return null;
                int ks = i; SkipString(); var key = json.Substring(ks + 1, i - ks - 2);
                Ws(); if (i >= n || json[i] != ':') return null; i++; Ws();
                int vs = i, depth = 0;
                while (i < n)
                {
                    char ch = json[i];
                    if (ch == '"') { SkipString(); continue; }
                    if (ch == '{' || ch == '[') depth++;
                    else if (ch == '}' || ch == ']') { if (depth == 0) break; depth--; }
                    else if (ch == ',' && depth == 0) break;
                    i++;
                }
                list.Add(new KeyValuePair<string, string>(key, json.Substring(vs, i - vs).Trim()));
            }
            return list;
        }
    }
}
