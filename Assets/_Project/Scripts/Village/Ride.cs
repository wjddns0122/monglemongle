using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Mongle.Village
{
    /// <summary>
    /// A ride in the park (D-006): toys reserve a place, walk to the boarding spot, wait until the ride lets them on,
    /// ride, then wait until they may get off. Each ride decides when boarding and leaving are possible
    /// (the carousel always, the Ferris wheel when a gondola stops at the bottom, the balloon when it is on the ground).
    /// </summary>
    public abstract class Ride : MonoBehaviour
    {
        public static readonly List<Ride> All = new List<Ride>();
        public string rideId = "Ride";
        public string displayName = "탈것";
        /// <summary>Where toys queue and get off, in the ride's local space (metres, before the root scale).</summary>
        public Vector3 boardLocal = new Vector3(0, 0, 1f);

        public abstract int SeatCount { get; }
        protected ToyAgent[] riders;
        readonly List<ToyAgent> reserved = new List<ToyAgent>();   // walking over or waiting, not on yet
        readonly List<ToyAgent> waiting = new List<ToyAgent>();    // standing at the boarding spot, in arrival order
        readonly HashSet<ToyAgent> hopping = new HashSet<ToyAgent>();
        protected bool[] leaveWanted;

        protected virtual void Awake() { riders = new ToyAgent[SeatCount]; leaveWanted = new bool[SeatCount]; }
        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        // ---------------- queries ----------------
        /// <summary>Boarding spot on the ground (boardLocal is in metres: the root scale only sizes the model).</summary>
        public Vector3 BoardWorld
        {
            get
            {
                if (!boardChecked && Application.isPlaying) { boardChecked = true; KeepBoardWalkable(); }
                var p = transform.position + transform.rotation * boardLocal; p.y = transform.position.y; return p;
            }
        }
        bool boardChecked;

        /// <summary>
        /// A boarding spot that ended up off the walkable ground (a scene built before the ride grew) is moved straight out from the
        /// ride until toys can stand there; otherwise nobody could ever reach it.
        /// </summary>
        void KeepBoardWalkable()
        {
            var b = transform.position + transform.rotation * boardLocal; b.y = transform.position.y;
            if (NavMesh.SamplePosition(b, out _, 0.25f, NavMesh.AllAreas)) return;
            var outward = b - transform.position; outward.y = 0f;
            if (outward.sqrMagnitude < 1e-4f) return;
            outward.Normalize();
            for (float d = 0.1f; d <= 2.5f; d += 0.1f)
                if (NavMesh.SamplePosition(b + outward * d, out var hit, 0.2f, NavMesh.AllAreas))
                {
                    var l = Quaternion.Inverse(transform.rotation) * (hit.position - transform.position); l.y = 0f;
                    boardLocal = l;
                    Debug.Log("[Ride] " + rideId + ": boarding spot moved " + d.ToString("F1") + " m out onto the walkable ground");
                    return;
                }
        }

        /// <summary>A point toys hop onto on the way in and out (the carousel's stairs); false when they hop straight on.</summary>
        public virtual bool StepPoint(out Vector3 p) { p = default; return false; }
        /// <summary>The step point for one seat (the train's platform spot next to each wagon, D-012); default: StepPoint.</summary>
        public virtual bool StepPointFor(int seat, out Vector3 p) => StepPoint(out p);
        public int RiderCount => riders.Count(r => r != null);
        public IEnumerable<ToyAgent> Riders => riders.Where(r => r != null);
        public ToyAgent RiderAt(int seat) => seat >= 0 && seat < riders.Length ? riders[seat] : null;
        public bool HasRoom => RiderCount + reserved.Count < SeatCount;
        public bool IsWaiting(ToyAgent a) => waiting.Contains(a);
        public int WaitingCount => waiting.Count;
        public bool AnyHopping => hopping.Count > 0;
        public int SeatOf(ToyAgent a) { for (int i = 0; i < riders.Length; i++) if (riders[i] == a) return i; return -1; }
        public bool Involves(ToyAgent a) => reserved.Contains(a) || SeatOf(a) >= 0;

        // ---------------- the rider's side ----------------
        public bool Reserve(ToyAgent a) { if (Involves(a)) return true; if (!HasRoom) return false; reserved.Add(a); return true; }
        public void Arrive(ToyAgent a) { if (reserved.Contains(a) && !waiting.Contains(a)) waiting.Add(a); }
        /// <summary>The seat this waiting toy may climb into right now, or -1.</summary>
        public int OfferSeat(ToyAgent a)
        {
            if (waiting.Count == 0 || waiting[0] != a) return -1; // one at a time, in arrival order
            return SeatOpenNow();
        }
        public void Board(ToyAgent a, int seat)
        {
            reserved.Remove(a); waiting.Remove(a);
            riders[seat] = a; leaveWanted[seat] = false;
            OnBoarded(seat, a);
        }
        public void WantLeave(int seat) { if (seat >= 0 && seat < leaveWanted.Length) leaveWanted[seat] = true; }
        public void BeginHop(ToyAgent a) => hopping.Add(a);
        public void EndHop(ToyAgent a) => hopping.Remove(a);
        /// <summary>Gets a toy off (or out of the queue). Safe to call more than once.</summary>
        public void Leave(ToyAgent a)
        {
            reserved.Remove(a); waiting.Remove(a); hopping.Remove(a);
            int s = SeatOf(a);
            if (s >= 0) { riders[s] = null; leaveWanted[s] = false; OnLeft(s, a); }
        }

        // ---------------- per ride ----------------
        /// <summary>A free seat that can be boarded at this moment (-1 when boarding is not possible now).</summary>
        protected abstract int SeatOpenNow();
        public abstract Pose SeatPose(int seat);
        /// <summary>True once this seat's ride is over (time on the carousel, a full turn of the wheel, a balloon trip).</summary>
        public abstract bool RideOver(int seat, float ridden);
        public abstract bool CanLeave(int seat);
        public virtual string SitAnim => "SitIdle";
        /// <summary>A happy gesture riders make now and then (null = keep sitting).</summary>
        public virtual string CheerAnim => null;
        /// <summary>The clip a rider should play right now, for rides that walk the toy around (the slide, D-011); null = SitAnim and cheers.</summary>
        public virtual string RiderAnim(int seat) => null;
        /// <summary>Where a toy gets off this seat (default: next to the boarding spot; the slide lets toys off at its bottom).</summary>
        public virtual Vector3 ExitWorld(int seat) => BoardWorld;
        /// <summary>True while the rider is out of sight (inside the tube slide).</summary>
        public virtual bool HideRider(int seat) => false;
        protected virtual void OnBoarded(int seat, ToyAgent a) { }
        protected virtual void OnLeft(int seat, ToyAgent a) { }
        protected bool LeaveWanted(int seat) => leaveWanted[seat];

        // ---------------- player ----------------
        public abstract void OnClicked(VillageDirector v);
        /// <summary>Close camera looking at the toys inside (Ferris wheel gondola, balloon basket).</summary>
        public virtual bool HasInsideView => false;
        public virtual int ViewSeat() => -1;
        public virtual void InsideView(int seat, out Vector3 camera, out Vector3 look) { camera = transform.position + Vector3.up * 3f; look = transform.position; }
        /// <summary>World point the player clicks on to use this ride (for the QA pointer).</summary>
        public virtual Vector3 ClickPoint => transform.position + Vector3.up * 0.8f;
    }
}
