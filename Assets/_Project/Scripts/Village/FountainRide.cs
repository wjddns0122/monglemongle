using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The cake fountain (D-012): water that flows (a streak texture slides down every stream, the foam rings pulse), and six
    /// spots on its rim where toys sit for a while facing out. The streams keep their shape: stretching them made them look cut
    /// into pieces (user, 2026-10-12).
    /// It counts as a ride so toys pick it on their own (a calm one: hop up onto the rim, sit, hop down).
    /// </summary>
    public class FountainRide : Ride
    {
        public Transform[] sitSpots;    // FT_SitSpot_n on the rim top
        public Transform jet;           // FT_Jet: origin at the nozzle
        public Transform[] falls;       // FT_Fall_n: origin at the top
        public Transform pool;          // FT_WaterPool
        public Transform[] splashes;    // FT_Splash_n: foam rings where the falls land
        public float flowSpeed = 0.55f; // streak texture scroll, UV units (about metres) per second
        public Vector2 sitSeconds = new Vector2(10f, 18f);
        Vector3[] splashScale; float[] duration; bool ready; Material flow;

        public override int SeatCount => 6;

        protected override void Awake()
        {
            base.Awake();
            rideId = "Ride_Fountain"; displayName = "케이크 분수";
            duration = new float[SeatCount];
        }

        void Init()
        {
            if (ready) return; ready = true;
            splashScale = new Vector3[splashes != null ? splashes.Length : 0];
            for (int i = 0; i < splashScale.Length; i++) if (splashes[i] != null) splashScale[i] = splashes[i].localScale;
            // one runtime copy of the water material for the streams (the asset itself never changes while playing)
            var streams = new System.Collections.Generic.List<Renderer>();
            if (jet != null) streams.AddRange(jet.GetComponentsInChildren<Renderer>());
            if (falls != null) foreach (var f in falls) if (f != null) streams.AddRange(f.GetComponentsInChildren<Renderer>());
            if (streams.Count > 0 && streams[0].sharedMaterial != null && Application.isPlaying)
            {
                flow = new Material(streams[0].sharedMaterial) { name = streams[0].sharedMaterial.name + " (flowing)" };
                foreach (var r in streams) { var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = flow; r.sharedMaterials = mats; }
            }
        }

        void Update()
        {
            Init();
            float t = Time.time;
            if (flow != null) flow.SetTextureOffset("_BaseMap", new Vector2(0f, -t * flowSpeed));   // V runs downstream: the streaks slide down
            for (int i = 0; i < splashScale.Length; i++)
                if (splashes[i] != null) { float k = 1f + Mathf.Sin(t * 5f + i * 1.9f) * 0.08f; splashes[i].localScale = new Vector3(splashScale[i].x * k, splashScale[i].y, splashScale[i].z * k); }
            if (pool != null) pool.Rotate(Vector3.up, 6f * Time.deltaTime, Space.World);
        }

        void OnDestroy() { if (flow != null) Destroy(flow); }

        protected override int SeatOpenNow()
        {
            if (sitSpots == null) return -1;
            // the free spot nearest the boarding side first, so toys do not walk round the basin
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < SeatCount && i < sitSpots.Length; i++)
            {
                if (RiderAt(i) != null || sitSpots[i] == null) continue;
                float d = (sitSpots[i].position - BoardWorld).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        Vector3 Out(int seat) { var o = sitSpots[seat].position - transform.position; o.y = 0f; return o.sqrMagnitude > 1e-6f ? o.normalized : Vector3.forward; }
        public override Pose SeatPose(int seat) => new Pose(sitSpots[seat].position, Quaternion.LookRotation(Out(seat), Vector3.up));
        /// <summary>The grass in front of that spot on the rim.</summary>
        public override bool StepPointFor(int seat, out Vector3 p) { p = sitSpots[seat].position + Out(seat) * 0.4f; p.y = transform.position.y; return true; }
        public override Vector3 ExitWorld(int seat) { StepPointFor(seat, out var p); return p + Out(seat) * 0.2f; }

        protected override void OnBoarded(int seat, ToyAgent a) { duration[seat] = Random.Range(sitSeconds.x, sitSeconds.y); }
        public override bool RideOver(int seat, float ridden) => ridden >= duration[seat];
        public override bool CanLeave(int seat) => true;

        public override void OnClicked(VillageDirector v) => v.OnPlaygroundClicked(this, "분수", "분수에 쉬러");
        public override Vector3 ClickPoint => transform.position + Vector3.up * 1.0f;
    }
}
