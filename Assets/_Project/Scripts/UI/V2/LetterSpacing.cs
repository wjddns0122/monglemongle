using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>Letter spacing for a legacy Text (Figma "tracking"), in reference pixels per character.</summary>
    [RequireComponent(typeof(Text))]
    public class LetterSpacing : BaseMeshEffect
    {
        public float spacing;
        /// <summary>Hangul titles are tracked wide in the design; Latin words would look spaced out.</summary>
        public bool koreanOnly = true;
        readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Mathf.Approximately(spacing, 0f)) return;
            if (koreanOnly && Loc.Current != Lang.Ko) return;
            var text = GetComponent<Text>(); if (text == null) return;
            verts.Clear(); vh.GetUIVertexStream(verts);
            // legacy Text makes one quad (6 stream verts) per visible character and none for white space
            var lines = text.text.Split('\n');
            int quad = 0;
            bool centre = text.alignment == TextAnchor.UpperCenter || text.alignment == TextAnchor.MiddleCenter || text.alignment == TextAnchor.LowerCenter;
            bool right = text.alignment == TextAnchor.UpperRight || text.alignment == TextAnchor.MiddleRight || text.alignment == TextAnchor.LowerRight;
            foreach (var line in lines)
            {
                int n = line.Length;
                float shift0 = centre ? -spacing * (n - 1) * 0.5f : right ? -spacing * (n - 1) : 0f;
                for (int c = 0; c < n; c++)
                {
                    if (char.IsWhiteSpace(line[c])) continue;
                    int v0 = quad * 6; if (v0 + 6 > verts.Count) break;
                    float dx = shift0 + spacing * c;
                    for (int k = 0; k < 6; k++) { var v = verts[v0 + k]; v.position.x += dx; verts[v0 + k] = v; }
                    quad++;
                }
            }
            vh.Clear(); vh.AddUIVertexTriangleStream(verts);
        }
    }
}
