using System.Linq;
using Mongle.Core;
using Mongle.Repair;
using Mongle.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>
    /// Park rides (D-006): the carousel, the big Ferris wheel and a tethered hot-air balloon stand in the walkable park.
    /// Toys ride them on their own; clicking the carousel stops/starts it, clicking the wheel or the balloon shows the
    /// toys inside up close (and calls a friend over when nobody is riding).
    /// </summary>
    public partial class VillageDirector
    {
        public Ride ViewingRide { get; private set; }
        public int ViewingSeat { get; private set; } = -1;
        GameObject rideBar; Text rideCaption; float viewEmptyFor;

        void SetupRides()
        {
            foreach (var r in Ride.All)
            {
                if (r is CarouselRide c) Grid.BlockDisc(c.transform.position, c.Motion.FootprintWorldRadius);
                else if (r is WheelRide w) Grid.BlockBox(w.transform.position, new Vector2(2.35f, 0.75f), w.transform.eulerAngles.y);
                else Grid.BlockDisc(r.transform.position, 0.6f);
                Grid.BlockDisc(r.BoardWorld, 0.45f); // keep the queue spot free: furniture there would cut the ride off
            }
            // Figma D-020: the cream plate with the caption and the blue "밖에서 보기" pill
            var bar = UiKit.Fig(canvas.transform, "ride_statusbar", UiKit.Edge.Middle, UiKit.Edge.End, 0f, 0f, true, "RideBar");
            bar.rectTransform.anchoredPosition = new Vector2(0f, 120f);   // bottom centre (D-024)
            rideBar = bar.gameObject;
            // the caption owns the plate left of the button (button: 267 wide, its centre 344 right of the plate's)
            rideCaption = UiKit.FigText(bar.transform, "", 38, UiKit.FigBrown, -150f, 0f, 680f, 110f);
            rideCaption.rectTransform.sizeDelta = new Vector2(680f, 56f); rideCaption.verticalOverflow = VerticalWrapMode.Truncate;
            rideCaption.horizontalOverflow = HorizontalWrapMode.Wrap; rideCaption.resizeTextForBestFit = true; rideCaption.resizeTextMinSize = 22; rideCaption.resizeTextMaxSize = 38;
            var see = UiKit.FigIn(bar.transform, "ride_btn_see", "ride_statusbar", true, "Btn_밖에서 보기").gameObject.AddComponent<Button>();
            see.targetGraphic = see.GetComponent<Image>(); see.onClick.AddListener(ExitRideView);
            UiKit.FigTextAt(see.transform, "ride_btn_see", "밖에서 보기", 38, UiKit.FigBrown, 1514f, 831f, 260f, 80f);
            rideBar.SetActive(false);
        }

        /// <summary>A ride with room that this toy can walk to (nearer ones a little more likely).</summary>
        public Ride PickRide(ToyAgent a)
        {
            var open = Ride.All.Where(r => r != null && r.isActiveAndEnabled && r.HasRoom && a.CanReach(r.BoardWorld)).ToList();
            if (open.Count == 0) return null;
            return open.OrderBy(r => Random.value * 6f + (r.BoardWorld - a.transform.position).magnitude * 0.25f).First();
        }

        public string NameOf(ToyAgent a)
        {
            if (a == null || a.Actor == null) return "";
            if (a.IsMain) return Catalog.toy != null ? Catalog.toy.displayName : "몽이";
            var def = Catalog.ToyDef(a.Actor.name); return def != null ? def.displayName : a.Actor.name;
        }

        public void ClickRide(Ride r)
        {
            root.Audio.Play("click", 0.5f, 1.1f);
            r.OnClicked(this);
        }

        public void OnCarouselToggled(CarouselRide c)
        {
            // nobody on it or on the way: the nearest free friend comes over to ride (like the wheel and the balloon)
            var called = c.RiderCount == 0 && !AllAgents().Any(a => a.Riding == c) ? CallFriendTo(c) : null;
            if (called != null)
                root.Say((c.Running ? "회전목마가 다시 돌아요! " : "회전목마가 멈췄어요. ") + Josa.IGa(NameOf(called)) + " 타러 와요!", 3f);
            else root.Say(c.Running ? "회전목마가 다시 돌아요!" : "회전목마가 멈췄어요. 한 번 더 누르면 다시 돌아요", 2.6f);
            root.Audio.Play(c.Running ? "chime" : "pop", 0.6f, c.Running ? 1.1f : 0.9f);
        }

        /// <summary>Swings, seesaw, slides (D-011): a click calls the nearest free friend over when nobody plays there yet.</summary>
        public void OnPlaygroundClicked(Ride r, string what, string comeTo = null)
        {
            var called = r.HasRoom && !AllAgents().Any(a => a.Riding == r) ? CallFriendTo(r) : null;
            if (called != null) root.Say(Josa.IGa(NameOf(called)) + " " + (comeTo ?? Josa.EulReul(what) + " 타러") + " 와요!", 2.8f);
            else root.Say(r.RiderCount > 0 ? (comeTo != null ? what + "에서 다들 즐거워 보여요!" : what + " 타는 게 신나 보여요!") : what + "에 놀러 올 친구를 기다려요", 2.4f);
            root.Audio.Play("pop", 0.6f, 1.15f);
        }

        // ---------------- watching a ride from up close ----------------
        public void EnterRideView(Ride r)
        {
            if (r == null || !r.HasInsideView) return;
            if (PlacingMode) ExitPlacing();
            ViewingRide = r; ViewingSeat = r.ViewSeat(); viewEmptyFor = 0f;
            if (r.RiderCount == 0 && !AllAgents().Any(a => a.Riding == r)) CallFriendTo(r);
            Orbit.FocusFollow(() => { ViewingRide.InsideView(ViewingSeat, out var c, out _); return c; }, () => { ViewingRide.InsideView(ViewingSeat, out _, out var l); return l; });
            mainBar.SetActive(false); hud.SetActive(false); rideBar.SetActive(true);
            RefreshRideCaption();
        }

        public void ExitRideView()
        {
            if (ViewingRide == null) return;
            ViewingRide = null; ViewingSeat = -1;
            Orbit.ReleaseFocus();
            rideBar.SetActive(false); hud.SetActive(true); mainBar.SetActive(state.Admitted);
            root.Audio.Play("click", 0.5f);
        }

        System.Collections.Generic.IEnumerable<ToyAgent> AllAgents()
        {
            if (Agent != null) yield return Agent;
            foreach (var a in ResidentAgents) if (a != null) yield return a;
        }

        /// <summary>Nobody is on the ride the player wants to watch: the nearest free toy (a friend before Mongi) comes to ride it.</summary>
        ToyAgent CallFriendTo(Ride r)
        {
            var who = AllAgents().Where(a => a.State != AgentState.Disabled && !a.OnRide && a.Riding == null && a.State != AgentState.Talk)
                .OrderBy(a => (a.IsMain ? 100f : 0f) + (a.transform.position - r.BoardWorld).magnitude).FirstOrDefault();
            if (who == null || !who.ForceRide(r)) return null;
            ShowBubble(who.Actor, r.displayName + " 타러 갈게요!", 2.4f);
            return who;
        }

        void UpdateRideView()
        {
            if (ViewingRide == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { ExitRideView(); return; }
            // keep watching the same gondola while someone sits in it; otherwise follow whoever rides now
            var rider = ViewingRide.RiderAt(ViewingSeat);
            if (rider == null)
            {
                viewEmptyFor += Time.deltaTime;
                int s = ViewingRide.ViewSeat();
                if (ViewingRide.RiderAt(s) != null || viewEmptyFor > 1.5f) { ViewingSeat = s; viewEmptyFor = 0f; }
            }
            else viewEmptyFor = 0f;
            RefreshRideCaption();
        }

        void RefreshRideCaption()
        {
            if (rideCaption == null || ViewingRide == null) return;
            var names = ViewingRide.Riders.Select(NameOf).Where(n => n != "").ToList();
            string coming = string.Join(", ", AllAgents().Where(a => a.Riding == ViewingRide && !a.OnRide).Select(NameOf));
            rideCaption.text = names.Count > 0 ? string.Join(", ", names) + " · " + ViewingRide.displayName + " 타는 중"
                : coming != "" ? Josa.IGa(coming) + " " + ViewingRide.displayName + " 타러 오고 있어요"
                : ViewingRide.displayName + " · 아직 아무도 안 탔어요";
        }

        public string RideCaptionShown => rideBar != null && rideBar.activeSelf ? rideCaption.text : "";
    }
}
