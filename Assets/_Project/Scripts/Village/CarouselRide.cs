using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The carousel as a ride (D-006): one toy per horse. Toys hop up its stairs and onto a free horse as it comes past (any horse
    /// while it stands still), ride for a while and hop off by the stairs. A click stops it; another click starts it.
    /// </summary>
    [RequireComponent(typeof(CarouselMotion))]
    public class CarouselRide : Ride
    {
        /// <summary>How far round from the stairs (degrees) a horse may be for a toy to hop on or off while it turns.</summary>
        public float reachDegrees = 35f;
        public float riderBehindPole = 0.22f;  // m behind the pole: a sitting toy reaches ~0.1 m forward of its origin, the pole is ~0.03 m thick
        public Vector2 rideSeconds = new Vector2(18f, 28f);
        CarouselMotion motion; Transform[] seats; Quaternion[] seatLocalRot; float[] duration;
        Transform stairs; float horseRing;

        public override int SeatCount => 6;
        public CarouselMotion Motion => motion != null ? motion : (motion = GetComponent<CarouselMotion>());
        public bool Running => Motion.Running;
        /// <summary>Boarding window in metres: from the boarding spot to a horse <see cref="reachDegrees"/> round (sized by the model, any height).</summary>
        public float Reach { get { BuildSeats(); var b = BoardWorld - transform.position; b.y = 0f; return CarouselMath.Reach(b.magnitude, horseRing, reachDegrees); } }

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Carousel"; displayName = "회전목마";
            duration = new float[SeatCount];
        }

        void BuildSeats()
        {
            if (seats != null) return;
            var horses = Motion.Horses;
            int n = Mathf.Min(SeatCount, horses.Length);
            seats = new Transform[n]; seatLocalRot = new Quaternion[n];
            float s = transform.lossyScale.y; horseRing = 0f;
            for (int i = 0; i < n; i++)
            {
                var h = horses[i];
                var rs = h.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                // saddle: just behind the pole (the pole runs through the horse's origin), a little above its foot
                // (measured on the Blender horse: +0.20 of the height); sitting on the pole made the rider overlap it
                var pole = h.position; var poleRadial = pole - transform.position; poleRadial.y = 0f;
                var travel = Vector3.Cross(Vector3.up, poleRadial.normalized);   // the carousel turns positive about up
                var seat = new GameObject("Seat").transform; seat.SetParent(h, false);
                seat.position = new Vector3(pole.x, h.position.y + 0.2f * s, pole.z) - travel * riderBehindPole;
                var radial = seat.position - transform.position; radial.y = 0f;
                horseRing += radial.magnitude / n;
                seatLocalRot[i] = Quaternion.Inverse(h.rotation) * Quaternion.LookRotation(travel, Vector3.up);
                seats[i] = seat;
            }
            var model = transform.Find("Model"); if (model == null) model = transform;
            stairs = model.Find("CR_Stairs");
        }

        public override Pose SeatPose(int seat)
        {
            BuildSeats();
            var t = seats[seat];
            return new Pose(t.position, t.parent.rotation * seatLocalRot[seat]);
        }

        /// <summary>Top of the stairs: toys climb there first, then hop onto the horse (the 4.5 m carousel is too tall to jump on from the grass).</summary>
        public override bool StepPoint(out Vector3 p)
        {
            BuildSeats();
            var rs = stairs != null ? stairs.GetComponentsInChildren<Renderer>() : null;
            if (rs != null && rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                p = new Vector3(b.center.x, b.max.y, b.center.z);
                return true;
            }
            // no stairs node: a spot on the deck edge toward the boarding spot
            var o = BoardWorld - transform.position; o.y = 0f;
            p = transform.position + o.normalized * (Motion.FootprintWorldRadius * 0.85f) + Vector3.up * 0.06f * transform.lossyScale.y;
            return true;
        }

        bool Near(int seat) { var p = SeatPose(seat).position; p.y = 0; var b = BoardWorld; b.y = 0; return (p - b).magnitude <= Reach; }
        bool StandingStill => Motion.Speed01 < 0.02f;

        protected override int SeatOpenNow()
        {
            BuildSeats();
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < seats.Length; i++)
            {
                if (RiderAt(i) != null) continue;
                var p = SeatPose(i).position; p.y = 0; var b = BoardWorld; b.y = 0; float d = (p - b).magnitude;
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return -1;
            return StandingStill || bd <= Reach ? best : -1; // while it turns, wait for a free horse to come round
        }

        protected override void OnBoarded(int seat, ToyAgent a) { duration[seat] = Random.Range(rideSeconds.x, rideSeconds.y); }
        public override bool RideOver(int seat, float ridden) => ridden >= duration[seat];
        public override bool CanLeave(int seat) => StandingStill || Near(seat);

        public override void OnClicked(VillageDirector v)
        {
            Motion.SetRunning(!Motion.Running);
            v.OnCarouselToggled(this);
        }

        public override Vector3 ClickPoint => transform.position + Vector3.up * transform.lossyScale.y * 0.55f;
    }
}
