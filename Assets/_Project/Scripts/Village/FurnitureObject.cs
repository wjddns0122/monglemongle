using System.Collections;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;
using UnityEngine.AI;

namespace Mongle.Village
{
    /// <summary>A placed furniture piece: grid transform, interaction points, nav obstacle, occupant lock.</summary>
    public class FurnitureObject : MonoBehaviour
    {
        public FurnitureDefinition Def;
        public FurniturePlacement Placement;
        public Transform Seat, Approach, Look;
        public ToyAgent Occupant;
        public bool InUse => Occupant != null;
        /// <summary>Being placed from the storage box; not confirmed yet, so the toy ignores it.</summary>
        public bool Pending;
        public bool IsNew;
        float height = 0.5f;
        public PlantView Plant;
        Coroutine wobble;
        public InteractionTag Tags => Def != null ? Def.tags : InteractionTag.None;
        NavMeshObstacle obstacle; BoxCollider pick;
        Renderer[] rends; MaterialPropertyBlock mpb;
        Color[] baseColors;

        public static FurnitureObject Spawn(FurnitureDefinition def, FurniturePlacement p, VillageGrid grid)
        {
            var go = Object.Instantiate(def.prefab);
            go.name = def.id;
            var f = go.AddComponent<FurnitureObject>();
            f.Def = def; f.Placement = p;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "IP_SEAT") f.Seat = t; else if (t.name == "IP_APPROACH") f.Approach = t; else if (t.name == "IP_LOOK") f.Look = t;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            f.pick = go.AddComponent<BoxCollider>();
            f.obstacle = go.AddComponent<NavMeshObstacle>();
            f.obstacle.shape = NavMeshObstacleShape.Box; f.obstacle.carving = true; f.obstacle.carveOnlyStationary = true;
            f.rends = go.GetComponentsInChildren<Renderer>();
            if (def.IsPlant) f.Plant = go.GetOrAdd<PlantView>();
            f.mpb = new MaterialPropertyBlock();
            var rb = go.GetComponentsInChildren<Renderer>(); float top = 0.5f;
            foreach (var r in rb) if (!(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y - go.transform.position.y + 0.05f);
            f.height = top;
            f.Apply(grid);
            return f;
        }

        /// <summary>Position/rotate according to Placement. Footprint is defined at rotation 0; the object rotates with the piece.</summary>
        public void Apply(VillageGrid grid) { Apply(grid, Placement.cellX, Placement.cellY, Placement.rotation); }

        public void Apply(VillageGrid grid, int cx, int cy, int rot)
        {
            transform.position = grid.CenterOf(Def.footprint, cx, cy, rot);
            transform.rotation = Quaternion.Euler(0, rot * 90f, 0);
            var size = new Vector3(Def.footprint.x * grid.Cell, height, Def.footprint.y * grid.Cell);
            pick.size = size; pick.center = new Vector3(0, height / 2f, 0);
            obstacle.size = size + new Vector3(0.18f, 0f, 0.18f); obstacle.center = new Vector3(0, 0.25f, 0);
        }

        public void Wobble()
        {
            var spin = GetComponentInChildren<SpinPart>(); if (spin != null) spin.Boost();
            if (wobble != null) StopCoroutine(wobble);
            wobble = StartCoroutine(WobbleRoutine());
        }

        IEnumerator WobbleRoutine()
        {
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(t / 0.5f * Mathf.PI * 3f) * Mathf.Exp(-t * 5f) * 0.12f;
                transform.localScale = new Vector3(1f + k, 1f - k, 1f + k);
                yield return null;
            }
            transform.localScale = Vector3.one; wobble = null;
        }

        /// <summary>Player poke: a squish, and lamps switch on/off.</summary>
        public void Poke()
        {
            Wobble();
            var lamp = GetComponentInChildren<LampLight>(); if (lamp != null) lamp.Toggle();
        }

        public Vector3 ApproachWorld => Approach != null ? Approach.position : transform.position + transform.forward * 0.45f;
        public Vector3 SeatWorld => Seat != null ? Seat.position : transform.position + Vector3.up * 0.18f;
        public Vector3 LookWorld => Look != null ? Look.position : transform.position + transform.forward * 0.4f;

        public void SetTint(Color? c)
        {
            foreach (var r in rends)
            {
                if (c.HasValue) { mpb.SetColor("_BaseColor", c.Value); r.SetPropertyBlock(mpb); }
                else r.SetPropertyBlock(null);
            }
        }
    }
}
