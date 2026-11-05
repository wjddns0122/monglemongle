using System.Collections;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.Toy;
using UnityEngine;
using UnityEngine.AI;

namespace Mongle.Village
{
    public enum AgentState { Idle, Move, Align, Interact, Talk, Disabled }

    /// <summary>
    /// Autonomous life: Idle → SelectIntent → Move → Align → Interact → Reaction → Cooldown → Idle (04 §6).
    /// Stuck/unreachable targets are abandoned and marked unavailable for a while; the toy always returns to a safe Idle.
    /// </summary>
    public class ToyAgent : MonoBehaviour
    {
        public ToyActor Actor; public NavMeshAgent Nav; public VillageDirector Village; [System.NonSerialized] public IntentSelector Selector;
        public AgentState State { get; private set; } = AgentState.Disabled;
        public Intent CurrentIntent { get; private set; } = Intent.Idle;
        public FurnitureObject Target { get; private set; }
        public bool Seated { get; private set; }
        /// <summary>The ride this toy has a place on (walking over, queueing or riding, D-006).</summary>
        public Ride Riding { get; private set; }
        /// <summary>True while actually on the ride (from hopping in until it has hopped off).</summary>
        public bool OnRide { get; private set; }
        Ride forcedRide;
        /// <summary>Inside its own house right now (D-012): hidden, not walking, no request bubbles.</summary>
        public bool AtHome { get; private set; }
        HouseView home;
        public float WalkSpeed = 0.5f;
        /// <summary>False for resident friends (D-005): they live their own life but quests, "new furniture" reactions and
        /// player control belong to Mongi.</summary>
        public bool IsMain = true;
        public float IdleMin = 2.5f, IdleMax = 5f;
        public System.Func<float> Clock = () => Time.time;
        float wait; float stuckTimer; Vector3 stuckPos; float talkCooldownUntil;
        Coroutine routine; bool talkRequested;
        readonly System.Collections.Generic.Dictionary<FurnitureObject, float> unreachableUntil = new System.Collections.Generic.Dictionary<FurnitureObject, float>();
        public int Aborts { get; private set; }

        // ---- player control (D-003): click to walk, or hold WASD/arrows; autonomy pauses while the player is in charge ----
        public bool Driving { get; private set; }
        public float PlayerSpeed = 0.85f;
        float autoPauseUntil; bool playerMoving; int driveFrame = -10; Vector2 driveInput; Vector3 driveFwd, driveRight;

        /// <summary>Walk to a point on the village ground. Ignored while chatting or disabled.</summary>
        public bool PlayerMoveTo(Vector3 world)
        {
            if (State == AgentState.Disabled || State == AgentState.Talk) return false;
            if (OnRide) { Village.ShowBubble(Actor, "지금 " + Riding.displayName + " 타는 중이에요!", 2.2f); return false; } // no jumping off a moving ride
            if (!NavMesh.SamplePosition(world, out var hit, 0.4f, NavMesh.AllAreas)) return false;
            AbortToIdle("player command");
            if (!Nav.enabled || !Nav.isOnNavMesh) return false;
            CurrentIntent = Intent.Wander; playerMoving = true; autoPauseUntil = Clock() + 10f;
            BeginMove(hit.position);
            return true;
        }

        /// <summary>Per-frame manual steering (camera-relative). Call every frame the keys are held.</summary>
        public void Drive(Vector2 input, Vector3 camForward, Vector3 camRight)
        {
            if (State == AgentState.Disabled || State == AgentState.Talk || OnRide) return;
            driveInput = input; driveFwd = camForward; driveRight = camRight; driveFrame = Time.frameCount;
        }

        bool HandleDriving()
        {
            bool want = Time.frameCount - driveFrame <= 1 && driveInput.sqrMagnitude > 0.01f;
            if (want)
            {
                if (!Driving)
                {
                    AbortToIdle("player drive"); Driving = true; Aborts--; // taking the wheel is not a failure
                }
                autoPauseUntil = Clock() + 8f; State = AgentState.Idle; CurrentIntent = Intent.Idle;
                if (Nav.enabled && Nav.isOnNavMesh)
                {
                    var dir = (driveRight * driveInput.x + driveFwd * driveInput.y); dir.y = 0;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                    Nav.Move(dir * PlayerSpeed * Time.deltaTime);
                    if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
                }
                if (Actor.CurrentState != "Walk") Actor.Play("Walk", 0.12f);
                return true;
            }
            if (Driving) { Driving = false; if (Actor.CurrentState == "Walk") Actor.Play("Idle", 0.15f); }
            return false;
        }

        // ---- flying friends (D-009): a toy with wings hovers above the path and flaps fast; it lands to sit or ride ----
        WingFlap wings; float hover;
        public const float HoverHeight = 0.16f;

        void UpdateHover()
        {
            if (wings == null) return;
            bool fly = State != AgentState.Disabled && !Seated && !OnRide;
            wings.Flying = fly;
            float want = fly ? HoverHeight + Mathf.Sin(Time.time * 2.3f) * 0.015f : 0f;
            hover = Mathf.MoveTowards(hover, want, Time.deltaTime * 0.35f);
            if (Nav.enabled) Nav.baseOffset = hover;
        }

        public void Init(ToyActor actor, NavMeshAgent nav, VillageDirector v, IntentSelector sel)
        {
            Actor = actor; Nav = nav; Village = v; Selector = sel;
            wings = actor != null ? actor.GetComponent<WingFlap>() : null;
            Nav.speed = WalkSpeed; Nav.acceleration = 3f; Nav.angularSpeed = 360f; Nav.stoppingDistance = 0.04f; Nav.radius = 0.1f; Nav.height = 0.45f;
        }

        public void Enable()
        {
            State = AgentState.Idle; wait = Random.Range(1.0f, 2.0f); Actor.Play("Idle", 0.2f);
            Actor.SetFace(0.5f, 0f);
        }
        public void Disable() { State = AgentState.Disabled; if (routine != null) StopCoroutine(routine); if (Nav.enabled && Nav.isOnNavMesh) Nav.ResetPath(); }

        // ---------------- main loop ----------------
        void Update()
        {
            UpdateHover();
            if (State == AgentState.Disabled) return;
            if (!Seated) EnsureOnNavMesh();
            if (HandleDriving()) return;
            switch (State)
            {
                case AgentState.Idle:
                    if (Clock() < autoPauseUntil) break; // the player is in charge for a moment
                    wait -= Time.deltaTime;
                    if (wait <= 0f) SelectIntent();
                    break;
                case AgentState.Move: TickMove(); break;
            }
            // walk animation follows actual speed
            if (!Seated && State != AgentState.Talk && State != AgentState.Interact && State != AgentState.Align)
            {
                bool moving = Nav.enabled && Nav.isOnNavMesh && Nav.velocity.magnitude > 0.06f;
                string want = moving ? "Walk" : "Idle";
                if (Actor.CurrentState != want && (Actor.CurrentState == "Walk" || Actor.CurrentState == "Idle" || Actor.CurrentState == "Nervous" || Actor.CurrentState == null)) Actor.Play(want, 0.2f);
            }
        }

        void EnsureOnNavMesh()
        {
            if (!Nav.enabled) return;
            if (!Nav.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 1.5f, NavMesh.AllAreas)) Nav.Warp(hit.position);
                else if (Village != null) Nav.Warp(Village.SafePoint());
                AbortToIdle("off navmesh");
            }
        }

        // ---------------- intent ----------------
        public bool Available(FurnitureObject f)
        {
            if (f == null || f.InUse || f.Pending) return false;
            if (unreachableUntil.TryGetValue(f, out var u) && Clock() < u) return false;
            return Reachable(f.ApproachWorld);
        }

        bool Reachable(Vector3 p)
        {
            if (!Nav.enabled || !Nav.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(p, out var hit, 0.35f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        void SelectIntent()
        {
            // first-time reaction to a newly placed piece wins over everything else (D-002)
            var fresh = IsMain ? Village.NextUnseen(this) : null;
            if (fresh != null) { StartIntent(Intent.Inspect, fresh); return; }
            var pick = Selector.Pick(i =>
            {
                if (i == Intent.Ride) return Village.PickRide(this) != null;
                if (i == Intent.Home) { var h = Village.HouseOf(this); return h != null && Reachable(h.DoorWorld); }
                var t = IntentSelector.TagOf(i); return t != InteractionTag.None && Village.PickTarget(t, this) != null;
            }, Clock());
            StartIntent(pick);
        }

        public void ForceIntent(Intent i) { if (State == AgentState.Disabled) return; AbortToIdle("forced"); StartIntent(i); }

        /// <summary>Go and ride this ride now (the player wants to watch it, or QA). False when there is no room or no way there.</summary>
        public bool ForceRide(Ride r)
        {
            if (State == AgentState.Disabled || r == null) return false;
            if (Riding == r) return true;
            AbortToIdle("ride"); Aborts--; // not a failure
            forcedRide = r; StartIntent(Intent.Ride);
            return Riding == r;
        }

        /// <summary>Go home now (the player clicked its house, or it just moved in). False when it has no house.</summary>
        public bool ForceHome()
        {
            if (State == AgentState.Disabled || Village.HouseOf(this) == null) return false;
            if (CurrentIntent == Intent.Home) return true;
            AbortToIdle("home"); Aborts--; // not a failure
            StartIntent(Intent.Home);
            return CurrentIntent == Intent.Home;
        }

        /// <summary>Can this toy walk to that point from where it stands?</summary>
        public bool CanReach(Vector3 p) => Reachable(p);

        void StartIntent(Intent i, FurnitureObject forced = null)
        {
            CurrentIntent = i; Target = null;
            switch (i)
            {
                case Intent.Idle: wait = Random.Range(IdleMin, IdleMax); Selector.StartCooldown(i, Clock()); return;
                case Intent.Wander:
                    if (!Village.RandomPoint(transform.position, 4.5f, out var p)) { wait = 2f; return; } // a stroll, not a march across the park
                    BeginMove(p); return;
                case Intent.Bench: case Intent.Flower: case Intent.Play: case Intent.Admire:
                    Target = forced != null ? forced : Village.PickTarget(IntentSelector.TagOf(i), this); break;
                case Intent.Inspect: Target = forced; break;
                case Intent.Home:
                    {
                        home = Village.HouseOf(this);
                        if (home == null || !NavMesh.SamplePosition(home.DoorWorld, out var dh, 0.8f, NavMesh.AllAreas)) { wait = 1.5f; CurrentIntent = Intent.Idle; return; }
                        BeginMove(dh.position);
                        return;
                    }
                case Intent.Ride:
                    {
                        var r = forcedRide != null ? forcedRide : Village.PickRide(this); forcedRide = null;
                        if (r == null || !r.Reserve(this) || !NavMesh.SamplePosition(r.BoardWorld, out var bh, 0.8f, NavMesh.AllAreas))
                        { if (r != null) r.Leave(this); wait = 1.5f; CurrentIntent = Intent.Idle; return; }
                        Riding = r;
                        BeginMove(bh.position);
                        return;
                    }
            }
            if (Target == null || !Available(Target)) { wait = 1.5f; CurrentIntent = Intent.Idle; return; }
            Target.Occupant = this; // reserve: furniture cannot be moved while in use
            NavMesh.SamplePosition(Target.ApproachWorld, out var hit, 0.35f, NavMesh.AllAreas);
            BeginMove(hit.position);
        }

        void BeginMove(Vector3 dest)
        {
            State = AgentState.Move;
            Nav.isStopped = false; Nav.SetDestination(dest);
            stuckTimer = 0f; stuckPos = transform.position;
        }

        void TickMove()
        {
            if (Nav.pathPending) return;
            if (Nav.pathStatus != NavMeshPathStatus.PathComplete) { MarkUnreachable(); AbortToIdle("path " + Nav.pathStatus); return; }
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2.5f)
            {
                if ((transform.position - stuckPos).magnitude < 0.04f) { MarkUnreachable(); AbortToIdle("stuck"); return; }
                stuckTimer = 0f; stuckPos = transform.position;
            }
            if (Nav.remainingDistance <= Nav.stoppingDistance + 0.03f)
            {
                Nav.ResetPath();
                if (CurrentIntent == Intent.Wander) { Finish(); return; }
                State = AgentState.Align;
                routine = StartCoroutine(RoutineFor(CurrentIntent));
            }
        }

        IEnumerator RoutineFor(Intent i)
        {
            switch (i)
            {
                case Intent.Bench: return BenchRoutine();
                case Intent.Flower: return FlowerRoutine();
                case Intent.Play: return PlayRoutine();
                case Intent.Admire: return AdmireRoutine();
                case Intent.Ride: return RideRoutine();
                case Intent.Home: return HomeRoutine();
                default: return InspectRoutine();
            }
        }

        public void ClearUnreachableForTest() { unreachableUntil.Clear(); }
        public void MarkUnreachableForTest(FurnitureObject f) { if (f != null) unreachableUntil[f] = Clock() + 30f; }

        void MarkUnreachable() { if (Target != null) unreachableUntil[Target] = Clock() + 30f; }

        // ---------------- interactions ----------------
        IEnumerator FaceRoutine(Vector3 dir, float dur = 0.5f)
        {
            dir.y = 0; if (dir.sqrMagnitude < 1e-5f) yield break;
            var from = transform.rotation; var to = Quaternion.LookRotation(dir.normalized); float t = 0;
            while (t < dur) { t += Time.deltaTime; transform.rotation = Quaternion.Slerp(from, to, t / dur); yield return null; }
            transform.rotation = to;
        }

        IEnumerator BenchRoutine()
        {
            var f = Target; var front = f.transform.forward; // bench front faces +Z at rotation 0
            Actor.Play("Idle", 0.1f);
            yield return FaceRoutine(front, 0.6f);
            State = AgentState.Interact; Seated = true;
            Nav.enabled = false;
            Actor.PlayThen("SitDown", "SitIdle");
            if (IsMain) Village.World.Report("MongiSat", f.Def.id);
            var from = transform.position; var seat = f.SeatWorld; float t = 0, dur = Mathf.Max(0.8f, Actor.ClipLength("SitDown"));
            while (t < dur) { t += Time.deltaTime; transform.position = Vector3.Lerp(from, seat, Mathf.SmoothStep(0, 1, t / dur)); yield return null; }
            transform.position = seat;
            Actor.Play("SitIdle", 0.2f); Actor.SetFace(0.8f, 0f);
            float sit = Random.Range(8f, 14f);
            while (sit > 0 && f != null) { sit -= Time.deltaTime; yield return null; }
            Actor.PlayThen("StandUp", "Idle");
            var appr = f != null ? f.ApproachWorld : transform.position; float u = 0, d2 = Mathf.Max(0.8f, Actor.ClipLength("StandUp"));
            var sp = transform.position;
            while (u < d2) { u += Time.deltaTime; transform.position = Vector3.Lerp(sp, appr, Mathf.SmoothStep(0, 1, u / d2)); yield return null; }
            Seated = false;
            if (NavMesh.SamplePosition(appr, out var hit, 1f, NavMesh.AllAreas)) { Nav.enabled = true; Nav.Warp(hit.position); }
            else { Nav.enabled = true; Nav.Warp(Village.SafePoint()); }
            Finish();
        }

        IEnumerator FlowerRoutine()
        {
            var f = Target;
            var dir = f.LookWorld - transform.position; dir.y = 0;
            var look = f.transform.position + Vector3.up * 0.05f - transform.position; look.y = 0;
            yield return FaceRoutine(look, 0.5f);
            State = AgentState.Interact;
            float len = Actor.PlayThen("LookFlower", "Idle");
            Actor.SetFace(1f, 0f);
            yield return new WaitForSeconds(Mathf.Max(0.6f, len * 0.55f));
            Fx.Burst(Village.Catalog, Actor.Point("Head", transform.position + Vector3.up * 0.4f) + Vector3.up * 0.08f, new Color(1f, 0.65f, 0.78f), 10, 0.014f, 0.18f, 0.9f, -0.04f);
            Village.Audio.Play("hello", 0.6f, 1.2f);
            yield return new WaitForSeconds(Mathf.Max(0.6f, len * 0.5f + 1.2f));
            Finish();
        }

        IEnumerator PlayRoutine()
        {
            var f = Target;
            var look = f.transform.position - transform.position; look.y = 0;
            yield return FaceRoutine(look, 0.45f);
            State = AgentState.Interact;
            Actor.SetFace(1f, 0f);
            for (int k = 0; k < 2 && f != null; k++)
            {
                float len = Actor.PlayThen("Happy", "Idle");
                f.Wobble();
                Village.Audio.Play("pop", 0.6f, 1.1f + k * 0.2f);
                Fx.Burst(Village.Catalog, f.transform.position + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.5f), 12, 0.014f, 0.22f, 0.8f, -0.03f);
                yield return new WaitForSeconds(Mathf.Max(1.1f, len));
            }
            if (IsMain) Village.World.Report("MongiPlayed", f.Def.id);
            Finish();
        }

        IEnumerator AdmireRoutine()
        {
            var f = Target;
            var look = f.transform.position - transform.position; look.y = 0;
            yield return FaceRoutine(look, 0.5f);
            State = AgentState.Interact;
            float len = Actor.PlayThen("LookFlower", "Idle");
            Actor.SetFace(0.9f, 0f);
            yield return new WaitForSeconds(Mathf.Max(0.6f, len * 0.5f));
            Village.Audio.Play("sparkle", 0.5f);
            Fx.Burst(Village.Catalog, f.transform.position + Vector3.up * 0.35f, new Color(1f, 0.95f, 0.7f), 10, 0.012f, 0.16f, 0.9f, -0.03f);
            yield return new WaitForSeconds(Mathf.Max(0.8f, len * 0.5f + 1.0f));
            Finish();
        }

        // ---------------- rides (D-006) ----------------
        IEnumerator RideRoutine()
        {
            var r = Riding;
            yield return FaceRoutine(r.transform.position - transform.position, 0.4f);
            State = AgentState.Interact;
            Actor.Play("Idle", 0.15f); Actor.SetFace(0.9f, 0f);
            if (Nav.enabled && Nav.isOnNavMesh) Nav.ResetPath();
            r.Arrive(this);
            float waited = 0f; int seat;
            while ((seat = r.OfferSeat(this)) < 0)
            {
                waited += Time.deltaTime;
                if (waited > 90f) { AbortToIdle("ride wait"); yield break; }
                yield return null;
            }
            // hop in (a tall ride: up onto its stairs first, then onto the horse)
            r.Board(this, seat); r.BeginHop(this);
            Nav.enabled = false; Seated = true; OnRide = true;
            Village.Audio.Play("pop", 0.5f, 1.25f);
            bool step = r.StepPointFor(seat, out var stepAt);
            if (step)
            {
                var inward = r.transform.position - stepAt; inward.y = 0f;
                var stepRot = Quaternion.LookRotation(inward.sqrMagnitude > 1e-4f ? inward.normalized : transform.forward, Vector3.up);
                yield return Hop(() => new Pose(stepAt, stepRot), 0.5f);
                Village.Audio.Play("pop", 0.4f, 1.4f);
            }
            if (r.SitAnim == "SitIdle") Actor.PlayThen("SitDown", "SitIdle"); else Actor.PlayThen("Happy", r.SitAnim);
            yield return Hop(() => r.SeatPose(seat), 0.75f, step ? 0.32f : 0.22f);
            r.EndHop(this);
            Actor.Play(r.SitAnim, 0.25f); Actor.SetFace(1f, 0f);
            if (IsMain) Village.World.Report("MongiRode", r.rideId);
            float ridden = 0f, cheer = Random.Range(3f, 6f);
            while (true)
            {
                var pose = r.SeatPose(seat); transform.SetPositionAndRotation(pose.position, pose.rotation);
                ridden += Time.deltaTime;
                if (r.RideOver(seat, ridden)) { r.WantLeave(seat); if (r.CanLeave(seat)) break; }
                Actor.SetHidden(r.HideRider(seat));
                var want = r.RiderAnim(seat);
                if (want != null) { if (Actor.CurrentState != want) Actor.Play(want, 0.2f); yield return null; continue; }
                cheer -= Time.deltaTime;
                if (cheer <= 0f)
                {
                    cheer = Random.Range(5f, 9f);
                    if (r.CheerAnim != null) Actor.PlayThen(r.CheerAnim, r.SitAnim);
                    Fx.Burst(Village.Catalog, Actor.Point("Head", transform.position + Vector3.up * 0.4f) + Vector3.up * 0.1f, new Color(1f, 0.8f, 0.9f), 8, 0.012f, 0.16f, 0.8f, -0.03f);
                }
                yield return null;
            }
            // hop off next to the boarding spot
            Actor.SetHidden(false);
            r.BeginHop(this);
            var exit = r.ExitWorld(seat);
            if (NavMesh.SamplePosition(exit, out var eh, 1.2f, NavMesh.AllAreas)) exit = eh.position;
            var away = exit - r.transform.position; away.y = 0f;
            var exitRot = Quaternion.LookRotation(away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward, Vector3.up);
            if (r.SitAnim == "SitIdle" || Actor.CurrentState == "SitIdle") Actor.PlayThen("StandUp", "Idle"); else Actor.Play("Idle", 0.15f);
            if (r.StepPointFor(seat, out var stepOff)) yield return Hop(() => new Pose(stepOff, exitRot), 0.6f, 0.3f);
            yield return Hop(() => new Pose(exit, exitRot), 0.65f);
            r.Leave(this); r.EndHop(this);
            OnRide = false; Seated = false; Riding = null;
            Nav.enabled = true; Nav.Warp(exit);
            Actor.Play("Idle", 0.2f);
            Village.Audio.Play("pop", 0.4f, 1.0f);
            Finish();
        }

        /// <summary>In through its own front door (D-012): out of sight for a while, then back out with a happy hop.</summary>
        IEnumerator HomeRoutine()
        {
            var h = home;
            if (h == null) { Finish(); yield break; }
            var inward = h.transform.position - transform.position; inward.y = 0f;
            yield return FaceRoutine(inward, 0.35f);
            State = AgentState.Interact;
            Actor.Play("Walk", 0.1f);
            var door = transform.position; var inside = door + inward.normalized * 0.28f; float t = 0f;
            while (t < 0.45f) { t += Time.deltaTime; transform.position = Vector3.Lerp(door, inside, t / 0.45f); yield return null; }
            Village.Audio.Play("pop", 0.35f, 0.9f);
            AtHome = true; Nav.enabled = false; Actor.SetHidden(true);
            float stay = Random.Range(8f, 16f);
            while (stay > 0f && h != null) { stay -= Time.deltaTime; yield return null; }
            Actor.SetHidden(false); AtHome = false;
            transform.position = door; transform.rotation = Quaternion.LookRotation(-inward.normalized, Vector3.up);
            Nav.enabled = true; Nav.Warp(door);
            Village.Audio.Play("pop", 0.45f, 1.2f);
            float len = Actor.PlayThen("Happy", "Idle");
            yield return new WaitForSeconds(Mathf.Max(0.8f, len));
            Finish();
        }

        /// <summary>A little jump in an arc to a (possibly moving) pose.</summary>
        IEnumerator Hop(System.Func<Pose> to, float dur, float arc = 0.22f)
        {
            var from = transform.position; var fromRot = transform.rotation; float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / dur);
                var p = to();
                transform.SetPositionAndRotation(Vector3.Lerp(from, p.position, k) + Vector3.up * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)) * arc, Quaternion.Slerp(fromRot, p.rotation, k));
                yield return null;
            }
            var end = to(); transform.SetPositionAndRotation(end.position, end.rotation);
        }

        /// <summary>First time Mongi sees a newly placed piece: walk over, light up, say so, then (if it is a seat) try it out.</summary>
        IEnumerator InspectRoutine()
        {
            var f = Target;
            Village.World.MarkSeen(f.Placement.Id);
            Village.SaveWorld();
            var look = f.transform.position - transform.position; look.y = 0;
            yield return FaceRoutine(look, 0.45f);
            State = AgentState.Interact;
            float len = Actor.PlayThen("Happy", "Idle");
            Actor.SetFace(1f, 0f);
            Village.Audio.Play("hello", 0.8f, 1.15f);
            Village.Audio.Play("sparkle", 0.5f);
            f.Wobble();
            Fx.Burst(Village.Catalog, Actor.Point("Head", transform.position + Vector3.up * 0.4f) + Vector3.up * 0.1f, new Color(1f, 0.65f, 0.78f), 14, 0.016f, 0.22f, 1.0f, -0.04f);
            Village.ShowBubble(Actor, "새 " + f.Def.displayName + "! 마음에 쏙 들어요", 3.2f);
            Reactions++;
            Village.World.Report("MongiReacted", f.Def.id);
            yield return new WaitForSeconds(Mathf.Max(1.8f, len));
            if ((f.Tags & InteractionTag.Sit) != 0) { yield return BenchRoutine(); yield break; } // Target/Occupant are already reserved for us
            Finish();
        }

        public int Reactions { get; private set; }

        // ---------------- completion / recovery ----------------
        void Finish()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            Selector.StartCooldown(CurrentIntent, Clock());
            if (playerMoving) { autoPauseUntil = Clock() + 8f; playerMoving = false; }
            Release();
            State = AgentState.Idle; wait = Random.Range(IdleMin, IdleMax);
            Actor.SetFace(0.5f, 0f);
        }

        void Release()
        {
            if (Target != null && Target.Occupant == this) Target.Occupant = null; Target = null;
            if (Riding != null && !OnRide) { Riding.Leave(this); Riding = null; } // gave up on the way or in the queue
        }

        public void AbortToIdle(string why)
        {
            Aborts++; playerMoving = false;
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (Actor != null) Actor.SetHidden(false); // cut off inside the tube slide or at home
            if (AtHome)
            {
                AtHome = false;
                var spot = home != null ? home.DoorWorld : transform.position;
                if (NavMesh.SamplePosition(spot, out var dh, 1.5f, NavMesh.AllAreas)) spot = dh.position;
                transform.position = spot; Nav.enabled = true; Nav.Warp(spot);
            }
            if (OnRide)
            {
                // taken off the ride (QA, scene change, a forced plan): land next to it
                var spot = Riding.BoardWorld;
                if (NavMesh.SamplePosition(spot, out var rh, 1.5f, NavMesh.AllAreas)) spot = rh.position;
                Riding.Leave(this); Riding = null; OnRide = false; Seated = false;
                transform.position = spot; Nav.enabled = true; Nav.Warp(spot);
                Actor.Play("Idle", 0.1f);
            }
            if (Seated)
            {
                // leave the seat safely
                Seated = false;
                var spot = Target != null ? Target.ApproachWorld : Village.SafePoint();
                if (NavMesh.SamplePosition(spot, out var h, 1.5f, NavMesh.AllAreas)) spot = h.position;
                transform.position = spot; Nav.enabled = true; Nav.Warp(spot);
                Actor.Play("Idle", 0.1f);
            }
            if (Nav.enabled && Nav.isOnNavMesh) Nav.ResetPath();
            Release();
            State = AgentState.Idle; wait = 1.5f; CurrentIntent = Intent.Idle;
        }

        /// <summary>Furniture changed (moved/rotated): drop any plan that targeted it.</summary>
        public void OnFurnitureChanged(FurnitureObject f)
        {
            if (Target == f && !Seated && State != AgentState.Interact) AbortToIdle("furniture moved");
            unreachableUntil.Remove(f);
        }

        // ---------------- talk ----------------
        public bool TryTalk(string line)
        {
            if (State == AgentState.Disabled || Clock() < talkCooldownUntil) return false;
            talkCooldownUntil = Clock() + 3.5f;
            StartCoroutine(TalkRoutine(line));
            return true;
        }

        IEnumerator TalkRoutine(string line)
        {
            var prev = State; bool seated = Seated;
            if (!seated)
            {
                if (routine != null) { StopCoroutine(routine); routine = null; }
                Release(); CurrentIntent = Intent.Idle;
                if (Nav.enabled && Nav.isOnNavMesh) { Nav.ResetPath(); Nav.isStopped = true; Nav.velocity = Vector3.zero; }
            }
            State = seated ? AgentState.Interact : AgentState.Talk;
            Village.Audio.Play("hello", 0.8f);
            Village.ShowBubble(Actor, line, 3f);
            Actor.SetFace(1f, 0f);
            if (!seated)
            {
                var cam = Camera.main; var dir = cam.transform.position - transform.position;
                yield return FaceRoutine(dir, 0.35f);
                float len = Actor.PlayThen("Talk", "Idle");
                yield return new WaitForSeconds(Mathf.Max(2.2f, len));
                if (Nav.enabled && Nav.isOnNavMesh) Nav.isStopped = false;
                State = AgentState.Idle; wait = 1.5f;
            }
            else
            {
                yield return new WaitForSeconds(2.5f);
                State = AgentState.Interact; // the bench routine continues on its own
            }
            Actor.SetFace(0.5f, 0f);
        }
    }
}
