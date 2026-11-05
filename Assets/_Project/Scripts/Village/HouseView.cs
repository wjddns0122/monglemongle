using System.Collections;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// One gingerbread house (D-012, candy_houses.py): door spot, roof-top spot, roof icing tint and the little pop it does
    /// when it appears for a friend who just moved in.
    /// </summary>
    public class HouseView : MonoBehaviour
    {
        public Transform door, top;
        public string kindId;
        /// <summary>Roof icing colours (HouseLots.Tints of them): white, strawberry, mint, lilac, lemon, sky.</summary>
        public static readonly Color[] Tints =
        {
            new Color(1f, 0.98f, 0.95f), new Color(1f, 0.74f, 0.84f), new Color(0.74f, 0.95f, 0.86f),
            new Color(0.86f, 0.78f, 1f), new Color(1f, 0.94f, 0.66f), new Color(0.74f, 0.88f, 1f),
        };

        public Vector3 DoorWorld => door != null ? door.position : transform.position + transform.forward * 1f;
        public Vector3 TopWorld => top != null ? top.position : transform.position + Vector3.up * 2f;

        public void SetTint(int tint)
        {
            if (tint < 0) return;
            var c = Tints[tint % Tints.Length];
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains("HS_RoofIcing")) continue;
                    r.GetPropertyBlock(mpb, i); mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c); r.SetPropertyBlock(mpb, i);
                }
            }
        }

        /// <summary>The see-through look while the player is choosing a place (green = fits, red = does not); null = normal.</summary>
        public void SetGhost(Color? c)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (c == null) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", c.Value); mpb.SetColor("_Color", c.Value); r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Grows out of the ground with a soft bounce (a new friend's house).</summary>
        public void Pop() { StopAllCoroutines(); StartCoroutine(PopRoutine()); }
        IEnumerator PopRoutine()
        {
            var s = transform.localScale; float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / 0.9f);
                float e = 1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.18f; // overshoot, then settle
                transform.localScale = new Vector3(s.x * Mathf.Lerp(0.2f, 1f, k), s.y * e, s.z * Mathf.Lerp(0.2f, 1f, k));
                yield return null;
            }
            transform.localScale = s;
        }
    }
}
