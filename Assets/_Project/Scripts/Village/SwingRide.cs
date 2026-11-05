using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The playground swings (D-011): two bucket seats on one top bar. A toy climbs in while its swing hangs still, swings
    /// higher and higher, slows down when it has had enough and hops off. Clicking calls a friend over.
    /// </summary>
    public class SwingRide : Ride
    {
        public Transform[] swings;      // PG_Swing_n: origin at the top-bar pivot
        public Transform[] seatPoints;  // PG_SwingSeat_n: where the toy sits, inside the bucket
        public float sitDrop = 0.03f;   // the empties mark the hips; a sitting toy's origin is a little lower
        public Vector2 rideSeconds = new Vector2(12f, 20f);
        SwingMotion[] motion; Quaternion[] rest; float[] duration, boardedAt;

        public override int SeatCount => 2;

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Swing"; displayName = "그네";
            duration = new float[SeatCount]; boardedAt = new float[SeatCount];
        }

        void Init()
        {
            if (motion != null) return;
            motion = new SwingMotion[SeatCount]; rest = new Quaternion[SeatCount];
            for (int i = 0; i < SeatCount; i++)
            {
                motion[i] = new SwingMotion { Period = 2.3f + 0.25f * i };
                if (i < swings.Length && swings[i] != null) rest[i] = swings[i].localRotation;
            }
        }

        /// <summary>World direction of the top bar (the swings turn about it).</summary>
        Vector3 Bar
        {
            get
            {
                if (swings != null && swings.Length >= 2 && swings[0] != null && swings[1] != null)
                {
                    var d = swings[1].position - swings[0].position; d.y = 0f;
                    if (d.sqrMagnitude > 1e-6f) return d.normalized;
                }
                return transform.right;
            }
        }

        /// <summary>Riders face the front of the playground (its +Z, toward the default camera), whichever side they climb in from.</summary>
        public Transform front;
        Vector3 Facing
        {
            get
            {
                var f = Vector3.Cross(Bar, Vector3.up);
                var want = front != null ? front.forward : transform.forward;
                return Vector3.Dot(f, want) < 0f ? -f : f;
            }
        }

        void Update()
        {
            Init();
            var bar = Bar;
            for (int i = 0; i < SeatCount && i < swings.Length; i++)
            {
                if (swings[i] == null) continue;
                bool riding = riders[i] != null && !LeaveWanted(i) && Time.time - boardedAt[i] > 0.9f; // after the hop in
                motion[i].Tick(Time.deltaTime, riding);
                var parent = swings[i].parent;
                var axis = parent != null ? parent.InverseTransformDirection(bar) : bar;
                swings[i].localRotation = Quaternion.AngleAxis(motion[i].Angle * Mathf.Rad2Deg, axis) * rest[i];
            }
        }

        protected override int SeatOpenNow()
        {
            Init();
            for (int i = 0; i < SeatCount; i++) if (RiderAt(i) == null && motion[i].Still) return i;
            return -1;
        }

        public override Pose SeatPose(int seat)
        {
            Init();
            var p = seatPoints != null && seat < seatPoints.Length && seatPoints[seat] != null ? seatPoints[seat].position : transform.position;
            var swing = Quaternion.AngleAxis(motion[seat].Angle * Mathf.Rad2Deg, Bar);
            return new Pose(p - swing * Vector3.up * sitDrop, swing * Quaternion.LookRotation(Facing, Vector3.up));
        }

        protected override void OnBoarded(int seat, ToyAgent a) { duration[seat] = Random.Range(rideSeconds.x, rideSeconds.y); boardedAt[seat] = Time.time; }
        public override bool RideOver(int seat, float ridden) => ridden >= duration[seat];
        public override bool CanLeave(int seat) { Init(); return motion[seat].Still; }
        public override string CheerAnim => "Happy";

        public override void OnClicked(VillageDirector v) => v.OnPlaygroundClicked(this, "그네");
        public override Vector3 ClickPoint => seatPoints != null && seatPoints.Length > 0 && seatPoints[0] != null ? seatPoints[0].position : transform.position + Vector3.up * 0.4f;
    }
}
