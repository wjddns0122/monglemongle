using System.Collections;
using Mongle.Data;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Visuals of a plant piece: stage models (Stage0..N), a floating "thirsty" droplet, and grow/water feedback.
    /// State lives in WorldState; this only reflects it.
    /// </summary>
    public class PlantView : MonoBehaviour
    {
        WorldState world; FurnitureDefinition def; string instanceId;
        Transform[] stages; Transform drop;
        int shown = -1; float nextPoll; bool thirsty;
        Coroutine pop;

        public int Stage { get; private set; }
        public bool Thirsty => thirsty;

        public void Init(WorldState w, FurnitureDefinition d, string id)
        {
            world = w; def = d; instanceId = id;
            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 0; ; i++) { var t = transform.Find("Stage" + i); if (t == null) break; list.Add(t); }
            stages = list.ToArray();
            drop = transform.Find("Drop"); if (drop != null) dropBaseY = drop.localPosition.y;
            Refresh(false);
        }

        public void Refresh(bool animate)
        {
            if (stages == null || world == null) return;
            var p = world.Plant(instanceId);
            int stage = Mathf.Clamp(world.StageOf(p, def), 0, stages.Length - 1);
            Stage = stage;
            if (stage != shown)
            {
                for (int i = 0; i < stages.Length; i++) stages[i].gameObject.SetActive(i == stage);
                if (animate && shown >= 0) Pop();
                shown = stage;
            }
            thirsty = p != null && world.NeedsWater(p, def);
            if (drop != null) drop.gameObject.SetActive(thirsty);
        }

        public void Pop() { if (pop != null) StopCoroutine(pop); pop = StartCoroutine(PopRoutine()); }

        IEnumerator PopRoutine()
        {
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(t / 0.6f * Mathf.PI * 2.5f) * Mathf.Exp(-t * 4f) * 0.25f;
                transform.localScale = new Vector3(1f - k * 0.5f, 1f + k, 1f - k * 0.5f);
                yield return null;
            }
            transform.localScale = Vector3.one; pop = null;
        }

        void Update()
        {
            if (drop != null && drop.gameObject.activeSelf)
            {
                var lp = drop.localPosition; lp.y = dropBaseY + Mathf.Sin(Time.time * 3f) * 0.015f; drop.localPosition = lp;
                var cam = Camera.main; if (cam != null) drop.rotation = Quaternion.LookRotation(drop.position - cam.transform.position);
            }
            if (Time.time < nextPoll) return;
            nextPoll = Time.time + 0.5f;
            Refresh(false);
        }

        float dropBaseY;
    }
}
