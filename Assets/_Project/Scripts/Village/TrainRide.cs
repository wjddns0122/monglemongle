using System.Linq;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The strawberry cake train (D-012): a locomotive with a driver's seat and four shortcake wagons, one toy each, running a
    /// lap of the village on its biscuit track (it winds between the houses, the carousel and the fountain and crosses the
    /// paths at level crossings, where it waits for any toy on the track). It waits at the station, toys hop on from the platform, it runs one lap and stops
    /// at the station again, where riders who have had their lap hop off. Clicking it follows the train up close.
    /// </summary>
    public class TrainRide : Ride
    {
        public Transform[] pieces;        // loco first, then the wagons (each moves along the loop)
        public float[] pieceBehind;       // how far behind the loco's centre each piece's centre runs (m)
        public Transform[] seats;         // 0 = driver (in the loco), 1..4 = wagons
        public Transform[] wheels;        // spin about their local X
        public float wheelRadius = 0.1f;
        public float[] wheelRadii;        // per wheel (the loco's front wheels are smaller than its drivers)
        public Transform smoke;
        public Vector3[] loopLocal;       // the track loop in this ride's space (the ride sits at the station)
        public float stationAt;           // where on the loop the loco stops
        public float platformOffset = 0.85f; // platform side, metres from the track centre toward the park
        TrainCycle cycle; LoopPath loop; int[] boardedTrip; Vector3 smokeScale; Quaternion[] wheelRest;

        public override int SeatCount => 5;
        public TrainCycle Cycle { get { Init(); return cycle; } }
        public LoopPath Loop { get { Init(); return loop; } }

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Train"; displayName = "케이크 기차";
            boardedTrip = new int[SeatCount];
        }

        void Init()
        {
            if (loop != null) return;
            loop = new LoopPath((loopLocal ?? new Vector3[0]).Select(p => transform.TransformPoint(p)).ToArray());
            cycle = new TrainCycle(loop.Length);
            if (smoke != null) smokeScale = smoke.localScale;
            wheelRest = wheels != null ? wheels.Select(w => w != null ? w.localRotation : Quaternion.identity).ToArray() : new Quaternion[0];
            if (boardedTrip == null) boardedTrip = new int[SeatCount];
            Place();
        }

        /// <summary>Puts the train on the track at the station (the scene builder calls it so the editor shows it in place).</summary>
        public void SnapToTrack() { loop = null; Init(); }

        void Update()
        {
            Init();
            if (loop.Length <= 0f) return;
            // leave once nobody is climbing in and nobody waits for a seat (or the train is full, or it has waited long enough)
            bool full = Enumerable.Range(0, SeatCount).All(i => RiderAt(i) != null);
            cycle.Tick(Time.deltaTime, !AnyHopping && (WaitingCount == 0 || full || cycle.PhaseTime > cycle.MinDwell + 15f), CrossingBlocked());
            Place();
            if (wheels != null)
                for (int i = 0; i < wheels.Length; i++)
                    if (wheels[i] != null)
                    {
                        float r = wheelRadii != null && i < wheelRadii.Length && wheelRadii[i] > 0.01f ? wheelRadii[i] : wheelRadius;
                        wheels[i].localRotation = wheelRest[i] * Quaternion.AngleAxis(cycle.Odometer / r * Mathf.Rad2Deg, Vector3.right);
                    }
            if (smoke != null)
            {
                float puff = 1f + Mathf.Sin(Time.time * (cycle.AtStation ? 2f : 7f)) * (cycle.AtStation ? 0.05f : 0.14f);
                smoke.localScale = smokeScale * puff;
            }
        }

        ToyAgent[] walkers; float walkersAt = -9f;
        /// <summary>
        /// A toy standing on the track just ahead of the loco (the track crosses the village paths at level crossings):
        /// the train slows down and waits until it has crossed.
        /// </summary>
        bool CrossingBlocked()
        {
            if (cycle.AtStation) return false;
            if (walkers == null || Time.time - walkersAt > 1f) { walkers = FindObjectsByType<ToyAgent>(FindObjectsInactive.Exclude); walkersAt = Time.time; }
            float s = stationAt + cycle.Travelled;
            foreach (var a in walkers)
            {
                if (a == null || a.Riding == this || a.AtHome) continue;
                var ap = a.transform.position;
                for (float d = 0.6f; d <= 1.9f; d += 0.33f)
                {
                    loop.At(s + d, out var p, out _);
                    var off = ap - p; off.y = 0f;
                    if (off.sqrMagnitude < 0.55f * 0.55f) return true;
                }
            }
            return false;
        }

        /// <summary>Puts every piece on the track: the loco at the station plus how far it has run, the wagons behind it.</summary>
        void Place()
        {
            if (pieces == null) return;
            float s = stationAt + cycle.Travelled;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                float behind = pieceBehind != null && i < pieceBehind.Length ? pieceBehind[i] : 0f;
                loop.At(s - behind, out var p, out var dir);
                // face along the chord between the piece's ends so long pieces sit nicely in the curves
                loop.At(s - behind + 0.3f, out var fwd, out _); loop.At(s - behind - 0.3f, out var back, out _);
                var d = fwd - back; d.y = 0f; if (d.sqrMagnitude < 1e-6f) d = dir;
                pieces[i].SetPositionAndRotation(p, Quaternion.LookRotation(d.normalized, Vector3.up));
            }
        }

        // ---------------- riders ----------------
        protected override int SeatOpenNow()
        {
            Init();
            if (!cycle.AtStation) return -1;
            foreach (int i in new[] { 1, 2, 3, 4, 0 }) if (i < seats.Length && seats[i] != null && RiderAt(i) == null) return i; // wagons first, then the driver
            return -1;
        }
        public override Pose SeatPose(int seat)
        {
            Init();
            var t = seats[seat];
            return new Pose(t.position, Quaternion.LookRotation(Flat(t.forward), Vector3.up));
        }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        /// <summary>The platform spot next to this seat (toys hop there first, then into the wagon; off the same way).</summary>
        public override bool StepPointFor(int seat, out Vector3 p)
        {
            Init();
            var t = seats[seat];
            var side = Vector3.Cross(Vector3.up, Flat(t.forward));          // left of the train
            var board = BoardWorld - t.position; board.y = 0f;
            if (Vector3.Dot(side, board) < 0f) side = -side;               // the platform side
            p = t.position + side * platformOffset; p.y = transform.position.y;
            return true;
        }

        /// <summary>Off onto the platform beside the wagon, a step further toward the park.</summary>
        public override Vector3 ExitWorld(int seat)
        {
            StepPointFor(seat, out var p);
            var away = p - seats[seat].position; away.y = 0f;
            return p + (away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.zero) * 0.35f;
        }

        protected override void OnBoarded(int seat, ToyAgent a) { Init(); boardedTrip[seat] = cycle.Trips; }
        public override bool RideOver(int seat, float ridden) => Cycle.Trips > boardedTrip[seat];
        public override bool CanLeave(int seat) => Cycle.AtStation;

        // ---------------- player ----------------
        public override void OnClicked(VillageDirector v) => v.EnterRideView(this);
        public override bool HasInsideView => true;
        public override int ViewSeat() { foreach (int i in new[] { 1, 2, 3, 4, 0 }) if (RiderAt(i) != null) return i; return 1; }
        public override void InsideView(int seat, out Vector3 camera, out Vector3 look)
        {
            Init();
            var t = seat >= 0 && seat < seats.Length && seats[seat] != null ? seats[seat] : (pieces != null && pieces.Length > 0 ? pieces[0] : transform);
            var f = Flat(t.forward); var side = Vector3.Cross(Vector3.up, f);
            camera = t.position - f * 1.6f + side * 0.9f + Vector3.up * 1.15f;
            look = t.position + f * 0.6f + Vector3.up * 0.25f;
        }
        public override Vector3 ClickPoint { get { Init(); return pieces != null && pieces.Length > 0 && pieces[0] != null ? pieces[0].position + Vector3.up * 0.4f : transform.position; } }
    }
}
