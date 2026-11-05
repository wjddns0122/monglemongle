using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The pony seesaw (D-011): a toy on each pony. Alone, a rider just tips its end down and bounces; with a friend on the
    /// other pony they rock up and down in turn. Riders face the pony's head, which looks in toward the hub (the riders face each other).
    /// </summary>
    public class SeesawRide : Ride
    {
        public Transform beam;           // PG_Seesaw_Beam: origin at the hub pivot
        public Transform[] seatPoints;   // PG_SeesawSeat_A / _B (children of the beam)
        public float sitDrop = 0.03f;
        public Vector2 rideSeconds = new Vector2(12f, 20f);
        readonly SeesawMotion motion = new SeesawMotion();
        Quaternion rest; Vector3 axisLocal; Vector3[] seatLocal; bool ready; float[] duration;

        public override int SeatCount => 2;

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Seesaw"; displayName = "시소";
            duration = new float[SeatCount];
        }

        void Init()
        {
            if (ready || beam == null || seatPoints == null || seatPoints.Length < 2) return;
            ready = true;
            rest = beam.localRotation;
            // the beam tips about the horizontal axis across it; positive turns end A down
            var along = seatPoints[0].position - seatPoints[1].position; along.y = 0f; along.Normalize();
            var axis = Vector3.Cross(Vector3.up, along);                  // turning +about this lowers end A
            axisLocal = beam.parent != null ? beam.parent.InverseTransformDirection(axis) : axis;
            seatLocal = new[] { beam.InverseTransformPoint(seatPoints[0].position), beam.InverseTransformPoint(seatPoints[1].position) };
            motion.Snap(false, false); Apply();
        }

        void Apply() { beam.localRotation = Quaternion.AngleAxis(motion.Angle * Mathf.Rad2Deg, axisLocal) * rest; }

        void Update()
        {
            Init(); if (!ready) return;
            motion.Tick(Time.deltaTime, Riding(0), Riding(1));
            Apply();
        }
        bool Riding(int s) => riders[s] != null && !LeaveWanted(s);

        protected override int SeatOpenNow()
        {
            for (int i = 0; i < SeatCount; i++) if (RiderAt(i) == null) return i;
            return -1;
        }

        public override Pose SeatPose(int seat)
        {
            Init();
            if (!ready) return new Pose(transform.position, transform.rotation);
            var p = beam.TransformPoint(seatLocal[seat]);
            var head = beam.position - p;                                         // toward the hub, where this pony's head looks, tilted with the beam
            var flat = head; flat.y = 0f;
            var rot = Quaternion.LookRotation(head.normalized, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f) rot = transform.rotation;
            return new Pose(p - rot * Vector3.up * sitDrop, rot);
        }

        protected override void OnBoarded(int seat, ToyAgent a) { duration[seat] = Random.Range(rideSeconds.x, rideSeconds.y); }
        public override bool RideOver(int seat, float ridden) => ridden >= duration[seat];
        // hop off when this end is near the ground (the low end)
        public override bool CanLeave(int seat) { Init(); return !ready || (seat == 0 ? motion.Angle > 0.05f : motion.Angle < -0.05f) || RiderAt(1 - seat) == null; }
        public override string CheerAnim => "Happy";

        public override void OnClicked(VillageDirector v) => v.OnPlaygroundClicked(this, "시소");
        public override Vector3 ClickPoint => beam != null ? beam.position + Vector3.up * 0.15f : transform.position + Vector3.up * 0.3f;
    }
}
