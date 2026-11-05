using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The claw machine's tries (D-014): five hearts, one heart comes back every 30 minutes (the clock keeps running while the
    /// game is closed because the time of the last charge is saved). Pure, clock injected, unit-tested.
    /// </summary>
    public class ClawHearts
    {
        public const int Max = 5;
        public const long ChargeSeconds = 30 * 60;
        readonly Func<long> clock;
        public int Count { get; private set; } = Max;
        /// <summary>Unix time the current charge started counting from (only meaningful while Count &lt; Max).</summary>
        public long LastUnix { get; private set; }

        public ClawHearts(Func<long> clock = null)
        {
            this.clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            LastUnix = this.clock();
        }

        /// <summary>Hearts that came back while time passed. A clock set back never gives hearts (the timer just restarts).</summary>
        public void Refresh()
        {
            long now = clock();
            if (Count >= Max) { Count = Max; LastUnix = now; return; }
            if (now < LastUnix) { LastUnix = now; return; }
            long gain = (now - LastUnix) / ChargeSeconds;
            if (gain <= 0) return;
            Count = (int)Math.Min(Max, Count + gain);
            LastUnix = Count >= Max ? now : LastUnix + gain * ChargeSeconds;
        }

        public bool TryUse()
        {
            Refresh();
            if (Count <= 0) return false;
            if (Count == Max) LastUnix = clock();   // the first missing heart starts the 30 minutes
            Count--;
            return true;
        }

        public long SecondsToNext { get { Refresh(); return Count >= Max ? 0 : Math.Max(0, ChargeSeconds - (clock() - LastUnix)); } }

        /// <summary>count &lt; 0 = a save from before the machine existed (full hearts).</summary>
        public void Restore(int count, long lastUnix)
        {
            Count = count < 0 ? Max : Math.Min(Max, count);
            LastUnix = lastUnix > 0 ? lastUnix : clock();
            Refresh();
        }
    }

    public enum ClawPhase { Move, Drop, Done }

    /// <summary>
    /// One try (D-014): the player steers the claw freely over the capsule pile (arrow keys or the on-screen arrows) and lets it
    /// down with "내리기". What the claw catches is decided by the machine from the real capsules under it (ClawMachine).
    /// Positions are 0..1 across the chamber (x: 0 = left, 1 = right as seen from the front; y: 0 = back, 1 = front).
    /// </summary>
    public class ClawRound
    {
        public float MoveSpeed = 0.5f;     // chamber widths per second
        public ClawPhase Phase { get; private set; } = ClawPhase.Move;
        public float X { get; private set; } = 0.5f;
        public float Y { get; private set; } = 0.5f;
        public bool Won { get; private set; }

        public ClawRound() { }
        public ClawRound(float x, float y) { X = Mathf.Clamp01(x); Y = Mathf.Clamp01(y); }

        /// <summary>Steer: dx / dy in -1..1 (right / front are positive).</summary>
        public void Steer(float dx, float dy, float dt)
        {
            if (Phase != ClawPhase.Move) return;
            X = Mathf.Clamp01(X + Mathf.Clamp(dx, -1f, 1f) * MoveSpeed * dt);
            Y = Mathf.Clamp01(Y + Mathf.Clamp(dy, -1f, 1f) * MoveSpeed * dt);
        }

        /// <summary>Let the claw down where it is now.</summary>
        public void Release() { if (Phase == ClawPhase.Move) Phase = ClawPhase.Drop; }

        public void Finish(bool won) { Won = won; Phase = ClawPhase.Done; }
    }

    /// <summary>What the claw does with the capsule under it (D-014), pure so it can be tested: nearest reachable capsule decides.</summary>
    public static class ClawGrab
    {
        public const float SureRadius = 0.05f, ReachRadius = 0.085f;
        public enum Outcome { Miss, Slip, Catch }

        /// <summary>
        /// d = horizontal distance from the claw's axis to the capsule it would take (m); roll in 0..1.
        /// The claw always closes on the capsule that is under it and lifts it (a capsule just out of reach is simply missed);
        /// the grip is weak, so it may let go on the way to the hole (user: rigging the odds openly looked wrong):
        /// right under the claw about every second try is brought home, toward the edge of its reach about one in ten.
        /// </summary>
        public static Outcome Decide(float d, float roll)
        {
            if (d > ReachRadius) return Outcome.Miss;
            float k = Mathf.Clamp01(d / ReachRadius);
            return roll < Mathf.Lerp(0.55f, 0.88f, k) ? Outcome.Slip : Outcome.Catch;
        }

        /// <summary>Where on the way to the hole (0..1: lifting, then carrying) a weak grip lets go.</summary>
        public static float SlipPoint(float roll) => Mathf.Lerp(0.12f, 0.8f, Mathf.Clamp01(roll));

        /// <summary>The capsule the open claw reaches first: among those within reach of its axis, the highest one.</summary>
        public static int PickTarget(IList<Vector3> centres, Vector3 axisXZ, out float distance)
        {
            int best = -1; distance = float.MaxValue; float bestY = float.MinValue;
            for (int i = 0; i < centres.Count; i++)
            {
                float dx = centres[i].x - axisXZ.x, dz = centres[i].z - axisXZ.z; float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > ReachRadius) continue;
                if (centres[i].y > bestY + 0.02f || (Mathf.Abs(centres[i].y - bestY) <= 0.02f && d < distance)) { best = i; bestY = centres[i].y; distance = d; }
            }
            return best;
        }
    }

    /// <summary>
    /// Which of the pile's capsules are gone (D-014): a capsule that was won stays out and one comes back every 30 minutes,
    /// oldest first. Saved with the world; the clock keeps running while the game is closed. Pure, clock injected.
    /// </summary>
    public class ClawStock
    {
        public const long RefillSeconds = 30 * 60;
        readonly Func<long> clock;
        readonly List<int> removed = new List<int>();
        public long LastUnix { get; private set; }
        public IReadOnlyList<int> Removed => removed;
        public ClawStock(Func<long> clock = null) { this.clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds()); LastUnix = this.clock(); }

        public bool IsGone(int id) => removed.Contains(id);

        public void Take(int id)
        {
            Refresh();
            if (removed.Contains(id)) return;
            if (removed.Count == 0) LastUnix = clock();   // the first missing capsule starts the 30 minutes
            removed.Add(id);
        }

        /// <summary>Capsules that came back while time passed. A clock set back brings none.</summary>
        public void Refresh()
        {
            long now = clock();
            if (removed.Count == 0) { LastUnix = now; return; }
            if (now < LastUnix) { LastUnix = now; return; }
            long gain = (now - LastUnix) / RefillSeconds;
            if (gain <= 0) return;
            for (long i = 0; i < gain && removed.Count > 0; i++) removed.RemoveAt(0);
            LastUnix = removed.Count == 0 ? now : LastUnix + gain * RefillSeconds;
        }

        public long SecondsToNext { get { Refresh(); return removed.Count == 0 ? 0 : Math.Max(0, RefillSeconds - (clock() - LastUnix)); } }

        public string Serialize() => string.Join(",", removed);

        public void Restore(string csv, long lastUnix)
        {
            removed.Clear();
            if (!string.IsNullOrEmpty(csv))
                foreach (var part in csv.Split(','))
                    if (int.TryParse(part, out int id) && id >= 0 && !removed.Contains(id)) removed.Add(id);
            LastUnix = lastUnix > 0 ? lastUnix : clock();
            Refresh();
        }
    }

    /// <summary>The prize is any item sold in the village shops, picked at random (D-014).</summary>
    public static class ClawRewards
    {
        public static string Pick(IList<string> shopItemIds, System.Random rng)
            => shopItemIds == null || shopItemIds.Count == 0 ? null : shopItemIds[rng.Next(shopItemIds.Count)];
    }
}
