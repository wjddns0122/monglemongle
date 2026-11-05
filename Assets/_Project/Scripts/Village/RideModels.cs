using UnityEngine;

namespace Mongle.Village
{
    public enum BalloonPhase { Grounded, Rising, Floating, Descending }

    /// <summary>
    /// Tethered hot-air balloon trip (D-006), pure timing: it waits on the ground while toys climb in, lifts when the
    /// ride says it may, floats for a while, comes back down and counts the trip. No Unity objects, so it is unit-tested.
    /// </summary>
    public class BalloonCycle
    {
        public float RiseTime = 7f, FloatTime = 10f, DescendTime = 7f, Height = 4f;
        public BalloonPhase Phase { get; private set; } = BalloonPhase.Grounded;
        public float PhaseTime { get; private set; }
        /// <summary>Finished trips; a toy that boarded before the current count has had its ride.</summary>
        public int Trips { get; private set; }

        public void Tick(float dt, bool mayLift)
        {
            PhaseTime += dt;
            switch (Phase)
            {
                case BalloonPhase.Grounded: if (mayLift) Go(BalloonPhase.Rising); break;
                case BalloonPhase.Rising: if (PhaseTime >= RiseTime) Go(BalloonPhase.Floating); break;
                case BalloonPhase.Floating: if (PhaseTime >= FloatTime) Go(BalloonPhase.Descending); break;
                case BalloonPhase.Descending: if (PhaseTime >= DescendTime) { Trips++; Go(BalloonPhase.Grounded); } break;
            }
        }

        void Go(BalloonPhase p) { Phase = p; PhaseTime = 0f; }

        /// <summary>Basket height above the ground for the current moment (eased at both ends).</summary>
        public float Altitude
        {
            get
            {
                switch (Phase)
                {
                    case BalloonPhase.Rising: return Height * Mathf.SmoothStep(0f, 1f, PhaseTime / RiseTime);
                    case BalloonPhase.Floating: return Height + 0.25f * Mathf.Sin(PhaseTime * 0.9f);
                    case BalloonPhase.Descending: return Height * (1f - Mathf.SmoothStep(0f, 1f, PhaseTime / DescendTime));
                    default: return 0f;
                }
            }
        }
    }

    /// <summary>
    /// Carousel ground layout (D-006), pure maths: where toys queue in front of the stairs, the NavMesh hole around the deck and
    /// how far from the stairs a passing horse may be boarded. The carousel grew to 4.5 m after the rides were built and its old
    /// square hole (axis-aligned, corners at 1.41 x the radius) swallowed the boarding spot, so nobody could walk there any more.
    /// </summary>
    public static class CarouselMath
    {
        /// <summary>Stairs direction in the 1-unit model (x, z), measured on the Blender carousel.</summary>
        public static readonly Vector3 StairsDir = new Vector3(-0.425f, 0f, 0.384f).normalized;
        /// <summary>Deck + stairs radius as a fraction of the carousel height (CarouselMotion.footprintRadius).</summary>
        public const float Footprint = 0.66f;
        /// <summary>Width of each hole strip as a fraction of its length: four strips 45° apart then cover the whole disc.</summary>
        public const float StripWidth = 0.42f;

        /// <summary>Boarding spot in the ride's own axes (metres): straight out from the stairs, <paramref name="gap"/> past the footprint.</summary>
        public static Vector3 BoardLocal(float height, float gap = 0.5f) => StairsDir * (Footprint * height + gap);

        /// <summary>World yaw that turns +x onto the stairs direction of a carousel turned by <paramref name="rideYaw"/>.</summary>
        public static float StairsYaw(float rideYaw) => rideYaw + Mathf.Atan2(-StairsDir.z, StairsDir.x) * Mathf.Rad2Deg;

        /// <summary>
        /// The NavMesh hole: four strips turned 45° apart around the centre (a rough disc of <paramref name="radius"/>). The first one
        /// points at the stairs, so the boarding spot just past its end stays walkable.
        /// </summary>
        public static (float yaw, Vector2 half)[] HoleStrips(float radius, float stairsYaw)
        {
            var s = new (float, Vector2)[4];
            for (int k = 0; k < 4; k++) s[k] = (stairsYaw + k * 45f, new Vector2(radius, radius * StripWidth));
            return s;
        }

        /// <summary>True when a ground offset from the carousel centre lies inside the hole (grown by <paramref name="margin"/>).</summary>
        public static bool InHole(Vector3 offset, float radius, float stairsYaw, float margin = 0f)
        {
            foreach (var (yaw, half) in HoleStrips(radius, stairsYaw))
            {
                var l = Quaternion.Euler(0f, -yaw, 0f) * new Vector3(offset.x, 0f, offset.z);
                if (Mathf.Abs(l.x) < half.x + margin && Mathf.Abs(l.z) < half.y + margin) return true;
            }
            return false;
        }

        /// <summary>Distance from the boarding spot to a horse <paramref name="degrees"/> round from it (the boarding window).</summary>
        public static float Reach(float boardDistance, float horseRing, float degrees)
            => Mathf.Sqrt(Mathf.Max(0f, boardDistance * boardDistance + horseRing * horseRing - 2f * boardDistance * horseRing * Mathf.Cos(degrees * Mathf.Deg2Rad)));
    }

    /// <summary>Angles of Ferris-wheel gondolas measured from the bottom of the wheel (D-006).</summary>
    public static class WheelMath
    {
        /// <summary>Signed angle in degrees (-180..180] from straight down to <paramref name="offset"/> (gondola minus hub), around <paramref name="axis"/>.</summary>
        public static float FromBottom(Vector3 offset, Vector3 axis) => Vector3.SignedAngle(Vector3.down, Vector3.ProjectOnPlane(offset, axis), axis);

        /// <summary>True when a gondola passed the bottom between two frames (either turning direction), not when it wraps at the top.</summary>
        public static bool CrossedBottom(float before, float now)
        {
            if (Mathf.Abs(before) >= 45f || Mathf.Abs(now) >= 45f) return false;
            return (before < 0f) != (now < 0f) || (now == 0f && before != 0f);
        }
    }
}
