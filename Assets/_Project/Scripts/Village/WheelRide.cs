using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The big Ferris wheel as a ride (D-006): one toy per gondola. The wheel turns slowly and stops whenever a gondola
    /// reaches the bottom and somebody has to get in or out there; riders stay on for one full turn.
    /// Gondolas always hang upright. Clicking it shows a close view of a gondola with a toy inside.
    /// </summary>
    public class WheelRide : Ride
    {
        public Transform spinner;
        public Transform[] gondolas;   // pivots at the hanging point, children of the spinner
        public Transform[] seats;      // gondola floor centres, children of the gondola pivots
        public float degreesPerSecond = 6f;
        public float settleDegrees = 10f;   // slow down over the last few degrees before a stop

        Quaternion spinnerRest; Quaternion[] gondolaRest;
        float turned; float speed;
        int stopIndex = -1; float stoppedFor;
        float[] angle; float[] boardedAt;
        bool started;

        public override int SeatCount => gondolas != null && gondolas.Length > 0 ? gondolas.Length : 8;
        public bool Stopped => stopIndex >= 0;
        public int StopIndex => stopIndex;
        public float Turned => turned;

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Wheel"; displayName = "관람차";
            spinnerRest = spinner.localRotation;
            gondolaRest = new Quaternion[gondolas.Length];
            for (int i = 0; i < gondolas.Length; i++) gondolaRest[i] = Quaternion.Inverse(transform.rotation) * gondolas[i].rotation;
            angle = new float[gondolas.Length]; boardedAt = new float[gondolas.Length];
            speed = degreesPerSecond;
        }

        public float AngleFromBottom(int i) => WheelMath.FromBottom(gondolas[i].position - spinner.position, transform.forward);

        void Pose()
        {
            spinner.localRotation = spinnerRest * Quaternion.Euler(0f, 0f, -turned);
            for (int i = 0; i < gondolas.Length; i++) gondolas[i].rotation = transform.rotation * gondolaRest[i];
        }

        bool NeedsStop(int i) => RiderAt(i) == null ? WaitingCount > 0 : LeaveWanted(i);

        void Update()
        {
            if (!started) { Pose(); for (int i = 0; i < gondolas.Length; i++) angle[i] = AngleFromBottom(i); started = true; }
            if (Stopped)
            {
                stoppedFor += Time.deltaTime;
                bool busy = AnyHopping || NeedsStop(stopIndex);
                if (!busy && stoppedFor > 1.0f) { stopIndex = -1; speed = 0f; }
                else return;
            }
            // ease into a stop when a gondola that has to stop is just before the bottom
            float target = degreesPerSecond;
            for (int i = 0; i < gondolas.Length; i++)
                if (NeedsStop(i) && Mathf.Abs(angle[i]) < settleDegrees && Approaching(i)) target = Mathf.Min(target, Mathf.Max(0.8f, degreesPerSecond * Mathf.Abs(angle[i]) / settleDegrees));
            speed = Mathf.MoveTowards(speed, target, degreesPerSecond * Time.deltaTime);
            turned += speed * Time.deltaTime;
            Pose();
            for (int i = 0; i < gondolas.Length; i++)
            {
                float before = angle[i]; angle[i] = AngleFromBottom(i);
                if (!Stopped && NeedsStop(i) && WheelMath.CrossedBottom(before, angle[i]))
                {
                    // land exactly on the bottom (the overshoot is a fraction of a degree)
                    turned -= Mathf.Abs(angle[i]); // "turned" only grows, so stepping it back always undoes the overshoot
                    Pose(); angle[i] = AngleFromBottom(i);
                    stopIndex = i; stoppedFor = 0f; speed = 0f;
                }
            }
        }

        // which way the gondola angles move as "turned" grows (worked out from the pose, so it never depends on the model's axes)
        float dirSign;
        float DirectionSign()
        {
            if (dirSign != 0f) return dirSign;
            float a0 = AngleFromBottom(0); turned += 1f; Pose(); float a1 = AngleFromBottom(0); turned -= 1f; Pose();
            dirSign = Mathf.Sign(Mathf.DeltaAngle(a0, a1)); if (dirSign == 0f) dirSign = 1f;
            return dirSign;
        }
        bool Approaching(int i) => Mathf.Sign(angle[i]) == -DirectionSign() || angle[i] == 0f;

        protected override int SeatOpenNow() => Stopped && RiderAt(stopIndex) == null && stoppedFor > 0.3f ? stopIndex : -1;
        public override Pose SeatPose(int seat)
        {
            var f = transform.forward; f.y = 0f;
            return new Pose(seats[seat].position, Quaternion.LookRotation(f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward, Vector3.up));
        }
        protected override void OnBoarded(int seat, ToyAgent a) { boardedAt[seat] = turned; }
        /// <summary>One full turn (the stops on the way do not count).</summary>
        public override bool RideOver(int seat, float ridden) => turned - boardedAt[seat] >= 350f;
        public override bool CanLeave(int seat) => Stopped && stopIndex == seat && stoppedFor > 0.3f;

        public override void OnClicked(VillageDirector v) => v.EnterRideView(this);
        public override bool HasInsideView => true;
        public override int ViewSeat()
        {
            for (int i = 0; i < SeatCount; i++) if (RiderAt(i) != null) return i;
            if (Stopped) return stopIndex;
            int best = 0; for (int i = 1; i < gondolas.Length; i++) if (Mathf.Abs(AngleFromBottom(i)) < Mathf.Abs(AngleFromBottom(best))) best = i;
            return best;
        }
        public override void InsideView(int seat, out Vector3 camera, out Vector3 look)
        {
            // in front of the wheel and off to the gondola's outer side, so the legs and spokes near the hub are not in the way
            var f = transform.forward; f.y = 0f; f.Normalize();
            var right = Vector3.Cross(Vector3.up, f);
            float side = Vector3.Dot(seats[seat].position - spinner.position, right) >= 0f ? 1f : -1f;
            look = seats[seat].position + Vector3.up * 0.26f;
            camera = look + f * 1.05f + right * side * 0.6f + Vector3.up * 0.22f;
        }
        public override Vector3 ClickPoint => spinner.position;
    }
}
