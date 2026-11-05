using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Friends' houses (D-012): every friend that moves into the village gets a house lot of its own, in moving-in order,
    /// and a house look chosen at random (one of the gingerbread variants, a roof icing colour, a little turn). The choice is
    /// seeded by the friend's resident id and saved with the resident, so a house never changes once it is built.
    /// </summary>
    public static class HouseLots
    {
        public struct Lot { public Vector3 pos; public float yaw; public Lot(float x, float z, float yaw) { pos = new Vector3(x, 0f, z); this.yaw = yaw; } }

        /// <summary>Lots east of the old garden, three staggered rows facing the default camera (+Z), inside the train's loop (D-012 follow-up).</summary>
        public static readonly Lot[] Lots =
        {
            new Lot(5.1f, 4.8f, 0f), new Lot(7.8f, 4.8f, 0f), new Lot(10.5f, 4.8f, 0f), new Lot(13.2f, 4.8f, 0f),
            new Lot(6.1f, 8.2f, 0f), new Lot(8.8f, 8.2f, 0f), new Lot(11.5f, 8.2f, 0f),
            new Lot(4.8f, 11.4f, 0f), new Lot(7.5f, 11.4f, 0f), new Lot(10.2f, 11.4f, 0f), new Lot(12.6f, 11.4f, 0f),
        };
        public const int Variants = 8, Tints = 6;
        /// <summary>Half size of the ground a lot keeps clear (trees, bushes, furniture), metres.</summary>
        public static readonly Vector2 LotHalf = new Vector2(1.15f, 1.2f);

        /// <summary>The friend has a house to show: placed by the player, or on one of the old lots (D-012 saves).</summary>
        public static bool HasSpot(ResidentSave r) => r != null && (r.housePlaced ? HasLook(r) : HasHouse(r));
        public static bool HasLook(ResidentSave r) => r != null && r.houseVariant >= 0 && r.houseVariant < Variants && r.houseTint >= 0;

        /// <summary>Gives every resident without a house look a variant (an unused one while any is left) and a roof tint; no place is chosen.</summary>
        public static bool AssignLooks(IList<ResidentSave> residents)
        {
            bool changed = false;
            foreach (var r in residents)
            {
                if (r == null || HasLook(r)) continue;
                var rng = new System.Random(Seed(r.residentId ?? r.kindId));
                var used = residents.Where(x => x != r && HasLook(x)).Select(x => x.houseVariant).ToList();
                var fresh = Enumerable.Range(0, Variants).Where(v => !used.Contains(v)).ToList();
                r.houseVariant = fresh.Count > 0 ? fresh[rng.Next(fresh.Count)] : rng.Next(Variants);
                r.houseTint = rng.Next(Tints);
                changed = true;
            }
            return changed;
        }

        public static bool HasHouse(ResidentSave r) => r != null && r.house >= 0 && r.house < Lots.Length && r.houseVariant >= 0 && r.houseVariant < Variants;

        /// <summary>Stable hash (string.GetHashCode changes between runs).</summary>
        public static int Seed(string id)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in id ?? "") { h ^= c; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>
        /// Gives every resident without a valid house the first free lot, a variant (one nobody uses yet while there are any),
        /// a roof tint and a small turn. Residents keep what they already have. Returns true when anything changed.
        /// </summary>
        public static bool Assign(IList<ResidentSave> residents)
        {
            bool changed = false;
            var taken = new HashSet<int>();
            foreach (var r in residents)
            {
                if (!HasHouse(r) || taken.Contains(r.house)) continue;
                taken.Add(r.house);
            }
            foreach (var r in residents)
            {
                if (r == null) continue;
                if (HasHouse(r) && taken.Contains(r.house) && residents.First(x => x != null && x.house == r.house) == r) continue;
                int lot = Enumerable.Range(0, Lots.Length).FirstOrDefault(i => !taken.Contains(i));
                if (taken.Contains(lot)) { r.house = -1; changed = true; continue; } // every lot is taken: no house (more friends than lots)
                var rng = new System.Random(Seed(r.residentId ?? r.kindId));
                var used = residents.Where(x => x != r && HasHouse(x)).Select(x => x.houseVariant).ToList();
                var fresh = Enumerable.Range(0, Variants).Where(v => !used.Contains(v)).ToList();
                r.house = lot; taken.Add(lot);
                r.houseVariant = fresh.Count > 0 ? fresh[rng.Next(fresh.Count)] : rng.Next(Variants);
                r.houseTint = rng.Next(Tints);
                changed = true;
            }
            return changed;
        }

        /// <summary>Small turn of a house on its lot (degrees), from its resident id.</summary>
        public static float Turn(ResidentSave r) => ((Seed((r.residentId ?? r.kindId) + "#turn") % 25) - 12);
    }
}
