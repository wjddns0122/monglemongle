using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// A tethered hot-air balloon as a ride (D-006): two toys climb into the basket while it rests on the ground, it lifts
    /// a few metres, floats and turns gently, then comes back down and they climb out. Clicking it shows the basket up close.
    /// </summary>
    public class BalloonRide : Ride
    {
        public Transform lift;          // everything that goes up (model, seats); the root stays on the ground
        public Transform[] seats;       // standing spots in the basket (on a raised floor so heads and shoulders show)
        public Transform[] tetherTops;  // rope ends on the basket
        public Transform[] tetherStakes;// rope ends on the ground
        public LineRenderer[] tethers;
        public float basketTop = 0.67f; // metres above the lift origin
        public float boardPause = 3f, waitForMore = 10f;
        readonly BalloonCycle cycle = new BalloonCycle();
        int[] tripAtBoard; float sinceBoard = 99f; float yaw;

        public override int SeatCount => seats != null && seats.Length > 0 ? seats.Length : 2;
        public BalloonCycle Cycle => cycle;
        public float Altitude => lift.localPosition.y;
        public override string SitAnim => "Idle";
        public override string CheerAnim => "Wave";

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Balloon"; displayName = "열기구";
            tripAtBoard = new int[SeatCount];
        }

        bool AllFresh() { for (int i = 0; i < SeatCount; i++) if (RiderAt(i) != null && tripAtBoard[i] != cycle.Trips) return false; return true; }

        void Update()
        {
            sinceBoard += Time.deltaTime;
            bool full = RiderCount >= SeatCount;
            bool mayLift = RiderCount > 0 && AllFresh() && !AnyHopping && sinceBoard >= boardPause && (full || WaitingCount == 0 || sinceBoard >= waitForMore);
            cycle.Tick(Time.deltaTime, mayLift);
            // a lazy turn while up in the air, back to facing front on the ground
            float wantYaw = cycle.Phase == BalloonPhase.Floating ? Mathf.Sin(cycle.PhaseTime * 0.35f) * 25f : 0f;
            yaw = Mathf.Lerp(yaw, wantYaw, Time.deltaTime * 0.8f);
            lift.localPosition = new Vector3(0f, cycle.Altitude, 0f);
            lift.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (tethers != null)
                for (int i = 0; i < tethers.Length && i < tetherTops.Length; i++)
                {
                    var lr = tethers[i]; var a = tetherStakes[i].position; var b = tetherTops[i].position;
                    var mid = Vector3.Lerp(a, b, 0.5f) + Vector3.down * 0.08f * Mathf.Clamp01(1f - cycle.Altitude / cycle.Height);
                    if (!lr.useWorldSpace) { var t = lr.transform; a = t.InverseTransformPoint(a); b = t.InverseTransformPoint(b); mid = t.InverseTransformPoint(mid); }
                    lr.SetPosition(0, a); lr.SetPosition(1, mid); lr.SetPosition(2, b);
                }
        }

        bool Grounded => cycle.Phase == BalloonPhase.Grounded;
        protected override int SeatOpenNow()
        {
            if (!Grounded) return -1;
            for (int i = 0; i < SeatCount; i++) if (RiderAt(i) == null) return i;
            return -1;
        }
        float[] standUp;   // per seat: extra height so a short toy's head still shows above the rim
        public override Pose SeatPose(int seat)
        {
            float up = standUp != null && seat < standUp.Length ? standUp[seat] : 0f;
            return new Pose(seats[seat].position + Vector3.up * up, seats[seat].rotation);
        }
        protected override void OnBoarded(int seat, ToyAgent a)
        {
            tripAtBoard[seat] = cycle.Trips; sinceBoard = 0f;
            if (standUp == null || standUp.Length != SeatCount) standUp = new float[SeatCount];
            // top of the toy ~0.18 m over the rim: a tall plush stands on the floor, a little car stands a bit higher
            float h = a != null && a.Actor != null ? a.Actor.BodyBounds.size.y : 0.45f;
            float floorY = seats[seat].localPosition.y;
            standUp[seat] = Mathf.Clamp(basketTop + 0.18f - h - floorY, 0f, 0.22f);
        }
        public override bool RideOver(int seat, float ridden) => cycle.Trips > tripAtBoard[seat];
        public override bool CanLeave(int seat) => Grounded;

        public override void OnClicked(VillageDirector v) => v.EnterRideView(this);
        public override bool HasInsideView => true;
        public override int ViewSeat() { for (int i = 0; i < SeatCount; i++) if (RiderAt(i) != null) return i; return 0; }
        public override void InsideView(int seat, out Vector3 camera, out Vector3 look)
        {
            // just outside the front of the basket, level with the riders' faces, travelling up with it
            var f = lift.forward; f.y = 0f; f.Normalize();
            look = lift.position + Vector3.up * (basketTop + 0.1f);
            camera = look + f * 1.6f + Vector3.up * 0.25f;
        }
        public override Vector3 ClickPoint => lift.position + Vector3.up * 3.6f;
    }
}
