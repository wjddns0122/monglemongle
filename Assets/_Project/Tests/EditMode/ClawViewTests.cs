using Mongle.EditorTools;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The 11-26 claw machine screen (Figma 88:1866): ◀, five hearts, the key caps, the tip and 시작하기.</summary>
    public class ClawViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(ClawBuild.PrefabPath);
            Assert.IsNotNull(p, ClawBuild.PrefabPath);
            var v = p.GetComponent<ClawView>();
            Assert.IsNotNull(v.backButton); Assert.AreEqual("Btn_나가기", v.backButton.name);
            Assert.AreEqual(5, v.hearts.Length); foreach (var h in v.hearts) Assert.IsNotNull(h.sprite);
            Assert.AreEqual(4, v.arrows.Length); foreach (var a in v.arrows) Assert.IsNotNull(a);
            Assert.IsNotNull(v.hint); Assert.IsNotNull(v.hintText); Assert.IsNotNull(v.startButton); Assert.IsNotNull(v.startText);
            Assert.IsNotNull(v.timer); Assert.IsNotNull(v.result);
            Assert.AreEqual(p, ViewIndex.Prefab("Claw"));
        }

        [Test]
        public void HeartsKeysTipAndTheBigButton()
        {
            var go = ClawBuild.Make();
            try
            {
                var v = go.GetComponent<ClawView>(); v.Show();
                bool back = false, start = false; v.OnBack = () => back = true; v.OnStart = () => start = true;
                v.SetHearts(3);
                Assert.AreEqual(Color.white, v.hearts[2].color); Assert.AreEqual(v.usedHeart, v.hearts[3].color, "a used heart is pale");
                v.SetStart("시작하기", true); Assert.IsTrue(v.QaStart()); Assert.IsTrue(start);
                v.SetStart("...", false); Assert.IsFalse(v.QaStart(), "nothing while the claw goes down");
                v.SetArrows(false); Assert.IsFalse(v.arrows[0].GetComponent<UnityEngine.UI.Button>().interactable);
                v.SetArrows(true); v.arrows[2].QaHold(true); Assert.IsTrue(v.Held(2)); Assert.IsFalse(v.Held(0));
                v.SetHint("방향키 이동 · SPACE 내리기"); Assert.AreEqual(394.17f, v.hint.sizeDelta.x, 0.01f, "Figma's pill at least");
                v.SetHint("하트가 다 떨어졌어요. 30분마다 하나씩 채워져요"); Assert.Greater(v.hint.sizeDelta.x, 394.17f, "a longer tip widens the pill");
                v.SetTimer(null); Assert.IsFalse(v.timer.gameObject.activeSelf);
                v.SetTimer("다음 하트 12:30"); Assert.IsTrue(v.timer.gameObject.activeSelf);
                Assert.AreEqual("", v.ResultShown);
                v.ShowResult("와! 포근 의자가 나왔어요!"); Assert.AreEqual("와! 포근 의자가 나왔어요!", v.ResultShown);
                // the village's first hint pill's size (user 2026-11-27): 38 px, 62 in from its ends, 97 tall, taller for two lines
                Assert.AreEqual(38f, v.resultText.fontSize, 0.01f);
                Assert.AreEqual(v.resultText.GetPreferredValues(v.ResultShown, 1200f, 0f).x + 124f, v.result.sizeDelta.x, 0.5f);
                Assert.AreEqual(97f, v.result.sizeDelta.y, 0.5f);
                v.ShowResult("와! 포근 의자가 나왔어요!\n보관함에 넣어 뒀어요"); Assert.AreEqual(142.6f, v.result.sizeDelta.y, 4f);
                v.backButton.onClick.Invoke(); Assert.IsTrue(back);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void KeysPressedShowOnTheKeyCaps()
        {
            var go = ClawBuild.Make();
            try
            {
                var v = go.GetComponent<ClawView>(); v.Show();
                var left = v.arrows[0].GetComponent<UnityEngine.UI.Image>();
                v.SetArrows(true); v.SetStart("내리기!", true);
                v.ShowKeys(true, false, false, false, false);
                Assert.IsTrue(v.LooksPressed(0), "← held: its key cap is down"); Assert.IsFalse(v.LooksPressed(1));
                Assert.Less(left.color.r, 1f, "and a little darker");
                v.ShowKeys(false, false, false, false, true);
                Assert.IsFalse(v.LooksPressed(0)); Assert.AreEqual(1f, left.color.r, 1e-4f, "let go: back to its own colours");
                Assert.IsTrue(v.startButton.GetComponent<Mongle.UI2.PressScale>().Down, "Space: 내리기! goes down");
                v.SetArrows(false);
                v.ShowKeys(true, true, true, true, false);
                Assert.IsFalse(v.LooksPressed(0), "keys that cannot be used do not move");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
