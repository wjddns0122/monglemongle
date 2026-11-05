using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The playground slides (D-011): seat 0 is the open pink slide, seat 1 the spiral tube. A toy climbs the stairs, walks
    /// across the deck to its slide, sits down, slides to the bottom, sits there a moment and gets off at the slide's end.
    /// Every phase is timed from the moment it boarded, so the pose is a pure function of time.
    /// </summary>
    public class SlideRide : Ride
    {
        public Transform stairBottom, stairTop;   // PG_Stair_Bottom / PG_Stair_Top
        public Transform[] slidePath, tubePath;   // PG_Slide_NN / PG_Tube_NN, top to bottom
        public float climbSpeed = 0.32f, walkSpeed = 0.4f, slideSpeed = 1.3f, tubeSpeed = 1.0f, sitTop = 0.5f, sitEnd = 0.9f;
        public float sitDrop = 0.0f;              // the path empties lie on the slide surface
        PolyPath[] paths; float[] start;

        public override int SeatCount => 2;
        public override string SitAnim => "Idle";

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Slide"; displayName = "미끄럼틀";
            start = new float[SeatCount];
        }

        PolyPath Path(int seat)
        {
            if (paths == null)
            {
                paths = new PolyPath[2];
                for (int s = 0; s < 2; s++)
                {
                    var src = s == 0 ? slidePath : tubePath;
                    var pts = new Vector3[src != null ? src.Length : 0];
                    for (int i = 0; i < pts.Length; i++) pts[i] = src[i].position;   // the playground never moves while playing
                    paths[s] = new PolyPath(pts.Length > 0 ? pts : new[] { transform.position });
                }
            }
            return paths[seat];
        }

        Vector3 Bottom => stairBottom != null ? stairBottom.position : transform.position;
        Vector3 Top => stairTop != null ? stairTop.position : transform.position + Vector3.up * 0.6f;
        float ClimbTime => Vector3.Distance(Bottom, Top) / climbSpeed;
        float WalkTime(int seat) => Vector3.Distance(Top, Path(seat).At(0f)) / walkSpeed;
        float SlideTime(int seat) => Path(seat).Length / (seat == 0 ? slideSpeed : tubeSpeed);
        float Total(int seat) => ClimbTime + WalkTime(seat) + sitTop + SlideTime(seat) + sitEnd;
        float Elapsed(int seat) => Time.time - start[seat];

        protected override int SeatOpenNow()
        {
            // one toy on the stairs at a time
            for (int i = 0; i < SeatCount; i++) if (RiderAt(i) != null && Elapsed(i) < ClimbTime) return -1;
            for (int i = 0; i < SeatCount; i++) if (RiderAt(i) == null) return Random.value < 0.5f && RiderAt(1 - i) == null ? 1 - i : i;
            return -1;
        }

        protected override void OnBoarded(int seat, ToyAgent a) { start[seat] = Time.time + 0.8f; } // after the little hop onto the first step

        public override Pose SeatPose(int seat)
        {
            float t = Mathf.Max(0f, Elapsed(seat));
            var path = Path(seat);
            float c = ClimbTime, w = WalkTime(seat);
            if (t < c)
            {
                var p = Vector3.Lerp(Bottom, Top, t / c) + Vector3.up * Mathf.Abs(Mathf.Sin(t * 9f)) * 0.025f;   // step, step
                return new Pose(p, Face(Top - Bottom));
            }
            t -= c;
            var start0 = path.At(0f);
            if (t < w) return new Pose(Vector3.Lerp(Top, start0, t / w), Face(start0 - Top));
            t -= w;
            path.At(0f, out _, out var d0);
            if (t < sitTop) return new Pose(start0 - Vector3.up * sitDrop, Face(d0));
            t -= sitTop;
            float st = SlideTime(seat);
            float k = Mathf.Clamp01(t / st); k = k * k * (3f - 2f * k) * 0.35f + k * 0.65f;   // a soft push-off, then whoosh
            path.At(k * path.Length, out var p2, out var d2);
            return new Pose(p2 - Vector3.up * sitDrop, Quaternion.LookRotation(d2.sqrMagnitude > 1e-6f ? d2 : Vector3.forward, Vector3.up));
        }

        static Quaternion Face(Vector3 d) { d.y = 0f; return Quaternion.LookRotation(d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward, Vector3.up); }

        public override string RiderAnim(int seat)
        {
            float t = Elapsed(seat);
            if (t < 0f) return "Idle";
            if (t < ClimbTime + WalkTime(seat)) return "Walk";
            if (t < Total(seat) - sitEnd * 0.5f) return "SitIdle";
            return "Happy";
        }

        /// <summary>The tube is narrower than a sitting toy: it disappears inside and pops out at the bottom.</summary>
        public override bool HideRider(int seat)
        {
            if (seat != 1) return false;
            float t = Elapsed(seat) - ClimbTime - WalkTime(seat) - sitTop;
            float st = SlideTime(seat);
            return t > st * 0.06f && t < st * 0.94f;
        }

        public override bool RideOver(int seat, float ridden) => Elapsed(seat) >= Total(seat);
        public override bool CanLeave(int seat) => true;

        /// <summary>Off at the bottom of the slide, a step past its end.</summary>
        public override Vector3 ExitWorld(int seat)
        {
            var path = Path(seat);
            path.At(path.Length, out var end, out var dir);
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            var p = end + dir.normalized * 0.35f; p.y = transform.position.y;
            return p;
        }

        public override void OnClicked(VillageDirector v) => v.OnPlaygroundClicked(this, "미끄럼틀");
        public override Vector3 ClickPoint => Top + Vector3.up * 0.3f;
    }
}
