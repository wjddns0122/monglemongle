using System;
using System.Collections.Generic;
using Mongle.UI2;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Mongle.Core
{
    /// <summary>Everything on the settings screen (D-038). Values are 0..1 unless noted.</summary>
    [Serializable]
    public class SettingsData
    {
        public int lang;                       // 0 Korean, 1 English
        public float fov = 0.5f;               // field of view (0.5 = the game's own)
        public float mouse = 0.5f;             // mouse sensitivity (0.5 = the game's own)
        public float pad = 0.5f;               // controller sensitivity
        public int quality = -1;               // QualitySettings level, -1 = keep the project's default
        public int resW, resH;                 // 0 = the current screen
        public bool fullscreen = true;
        public float brightness = 0.5f;        // 0.5 = no change, 0..1 = -1..+1 EV
        public float master = 0.8f, music = 0.7f, effects = 0.85f;
        public bool mute;
    }

    /// <summary>
    /// Device settings (D-038): stored in PlayerPrefs so they exist before any save is loaded (title screen) and are shared
    /// by every save. <see cref="Apply"/> pushes them into the game: language, quality, resolution / full screen, brightness
    /// (post exposure on the scene's volumes), audio (Sfx + listener), and the camera scales read by OrbitCamera.
    /// </summary>
    public static class GameSettings
    {
        const string Key = "mongle.settings.v1";
        static SettingsData data;
        public static event Action Changed;

        public static SettingsData Data { get { if (data == null) Load(); return data; } }

        /// <summary>Camera field of view multiplier (0.85 .. 1.2).</summary>
        public static float FovScale => Mathf.Lerp(0.85f, 1.2f, Data.fov);
        /// <summary>Mouse drag speed multiplier (0.4 .. 2).</summary>
        public static float MouseScale => Data.mouse < 0.5f ? Mathf.Lerp(0.4f, 1f, Data.mouse * 2f) : Mathf.Lerp(1f, 2f, (Data.mouse - 0.5f) * 2f);
        /// <summary>Controller stick speed multiplier (0.4 .. 2).</summary>
        public static float PadScale => Data.pad < 0.5f ? Mathf.Lerp(0.4f, 1f, Data.pad * 2f) : Mathf.Lerp(1f, 2f, (Data.pad - 0.5f) * 2f);

        public static void Load()
        {
            data = null;
            var json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json)) { try { data = JsonUtility.FromJson<SettingsData>(json); } catch { data = null; } }
            if (data == null)
            {
                data = new SettingsData();
                var sys = Application.systemLanguage;
                data.lang = sys == SystemLanguage.Korean ? 0 : 1;   // first run: follow the computer's language
            }
        }

        /// <summary>The old save kept only the master volume: take it over once, the first time settings are made.</summary>
        public static void AdoptLegacyVolume(float master)
        {
            if (PlayerPrefs.HasKey(Key)) return;
            Data.master = Mathf.Clamp01(master); Save();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        /// <summary>Change something, apply it and remember it.</summary>
        public static void Set(Action<SettingsData> change)
        {
            change(Data); Apply(); Save();
        }

        static bool hooked;

        /// <summary>
        /// An automated QA run (-mongleQa): it runs in a window beside the person's own work, silent, at the size asked for with
        /// -mongleQaSize WxH (default 1600x900); the saved settings are not used for these nor changed.
        /// </summary>
        public static readonly bool QaRun = System.Array.IndexOf(Environment.GetCommandLineArgs(), "-mongleQa") >= 0;
        static Vector2Int QaSize()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-mongleQaSize")
                {
                    var p = a[i + 1].Split('x');
                    if (p.Length == 2 && int.TryParse(p[0], out var w) && int.TryParse(p[1], out var h) && w > 0 && h > 0) return new Vector2Int(w, h);
                }
            return new Vector2Int(1600, 900);
        }
        public static void Apply()
        {
            var d = Data;
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => ApplyBrightness(); }
            Loc.Current = d.lang == 1 ? Lang.En : Lang.Ko;
            if (d.quality >= 0 && d.quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != d.quality)
                QualitySettings.SetQualityLevel(d.quality, true);
#if !UNITY_EDITOR
            var mode = d.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = d.resW > 0 ? d.resW : Screen.currentResolution.width, h = d.resH > 0 ? d.resH : Screen.currentResolution.height;
            if (QaRun) { var q = QaSize(); mode = FullScreenMode.Windowed; w = q.x; h = q.y; }
            if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode) Screen.SetResolution(w, h, mode);
#endif
            ApplyAudio();
            ApplyBrightness();
            Changed?.Invoke();
        }

        public static void ApplyAudio()
        {
            var d = Data;
            AudioListener.volume = d.mute || QaRun ? 0f : 1f;
            var root = GameRoot.Instance;
            if (root != null && root.Audio != null) root.Audio.SetVolumes(d.master, d.music, d.effects);
            if (root != null && root.Settings != null) root.Settings.masterVolume = d.master;   // the save keeps it too
        }

        // --- brightness: the scenes' own exposure + an offset, on runtime copies of their volume profiles ---
        static readonly Dictionary<VolumeProfile, float> baseExposure = new Dictionary<VolumeProfile, float>();
        public static void ApplyBrightness()
        {
            if (!Application.isPlaying) return;
            float ev = Mathf.Lerp(-1f, 1f, Data.brightness);
            foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>())
            {
                if (v == null || !v.isGlobal || v.sharedProfile == null) continue;
                var p = v.profile;   // runtime copy: the asset is never changed
                if (!p.TryGet<ColorAdjustments>(out var ca)) { ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(0f); }
                if (!baseExposure.ContainsKey(p)) baseExposure[p] = ca.postExposure.overrideState ? ca.postExposure.value : 0f;
                ca.postExposure.Override(baseExposure[p] + ev);
            }
        }

        /// <summary>Distinct screen sizes this computer offers, largest first (for the resolution list).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions) { var v = new Vector2Int(r.width, r.height); if (!list.Contains(v)) list.Add(v); }
            if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
            list.Sort((a, b) => (b.x * b.y).CompareTo(a.x * a.y));
            return list;
        }
    }
}
