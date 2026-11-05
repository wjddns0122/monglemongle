using System;
using System.Collections.Generic;
using Mongle.Data;

namespace Mongle.Village
{
    /// <summary>Inspect = first-time reaction to a newly placed piece (D-002); it is queued by the agent, never picked by weight.</summary>
    public enum Intent { Idle, Wander, Bench, Flower, Play, Admire, Inspect, Ride, Home }

    /// <summary>
    /// Simple weighted selection with cooldowns (04 §7, D-001-8). Score = Base + Personality + FurnitureTag - CooldownPenalty.
    /// Unreachable/unavailable intents are excluded entirely.
    /// </summary>
    public class IntentSelector
    {
        public Random Rng;
        public Personality Personality = Personality.Shy;
        public InteractionTag Favorites = InteractionTag.Sit | InteractionTag.Flower;
        readonly Dictionary<Intent, float> cooldownUntil = new Dictionary<Intent, float>();
        public float FavoriteBonus = 1.4f;
        public float CooldownFactor = 0.15f;

        static readonly Intent[] Pickable = { Intent.Idle, Intent.Wander, Intent.Bench, Intent.Flower, Intent.Play, Intent.Admire, Intent.Ride, Intent.Home };

        public IntentSelector(int seed) { Rng = new Random(seed); }

        public static float CooldownSeconds(Intent i)
        {
            switch (i) { case Intent.Bench: return 25f; case Intent.Flower: return 20f; case Intent.Play: return 22f; case Intent.Admire: return 18f; case Intent.Wander: return 6f; case Intent.Ride: return 45f; case Intent.Home: return 70f; default: return 0f; }
        }

        public static InteractionTag TagOf(Intent i)
        {
            switch (i) { case Intent.Bench: return InteractionTag.Sit; case Intent.Flower: return InteractionTag.Flower; case Intent.Play: return InteractionTag.Play; case Intent.Admire: return InteractionTag.Watch; default: return InteractionTag.None; }
        }

        public void StartCooldown(Intent i, float now) { var c = CooldownSeconds(i); if (c > 0) cooldownUntil[i] = now + c; }

        public float Weight(Intent i, bool available, float now)
        {
            float w;
            switch (i)
            {
                case Intent.Idle: w = 1.0f + (Personality == Personality.Sleepy ? 0.8f : 0f); break;
                case Intent.Wander: w = 0.8f * (Personality == Personality.Shy ? 0.7f : Personality == Personality.Energetic ? 1.6f : 1f); break;
                case Intent.Bench: case Intent.Flower: case Intent.Play: case Intent.Admire:
                    if (!available) return 0f;
                    w = 0.8f + ((Favorites & TagOf(i)) != 0 ? FavoriteBonus : 0f);
                    if (i == Intent.Play && Personality == Personality.Energetic) w += 0.5f;
                    if (i == Intent.Admire) w -= 0.2f;
                    break;
                case Intent.Ride:
                    // the park rides (D-006): everyone likes them, the lively ones a bit more
                    if (!available) return 0f;
                    w = 0.9f + (Personality == Personality.Energetic || Personality == Personality.Mischievous ? 0.5f : Personality == Personality.Curious ? 0.4f : Personality == Personality.Sleepy ? -0.3f : 0f);
                    break;
                case Intent.Home:
                    // a friend's own house (D-012): the sleepy and the shy pop home more often
                    if (!available) return 0f;
                    w = 0.45f + (Personality == Personality.Sleepy ? 0.5f : Personality == Personality.Shy ? 0.35f : 0f);
                    break;
                default: return 0f;
            }
            if (cooldownUntil.TryGetValue(i, out var until) && now < until) w *= CooldownFactor;
            return w;
        }

        public Intent Pick(bool benchAvailable, bool flowerAvailable, float now)
            => Pick(i => i == Intent.Bench ? benchAvailable : i == Intent.Flower ? flowerAvailable : false, now);

        public Intent Pick(Func<Intent, bool> available, float now)
        {
            var ws = new float[Pickable.Length]; float sum = 0;
            for (int k = 0; k < Pickable.Length; k++)
            {
                var id = Pickable[k];
                bool av = id == Intent.Idle || id == Intent.Wander || available(id);
                ws[k] = Weight(id, av, now); sum += ws[k];
            }
            if (sum <= 0) return Intent.Idle;
            double r = Rng.NextDouble() * sum;
            for (int k = 0; k < Pickable.Length; k++) { r -= ws[k]; if (r <= 0 && ws[k] > 0) return Pickable[k]; }
            return Intent.Idle;
        }
    }
}
