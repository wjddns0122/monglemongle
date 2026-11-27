using Mongle.EditorTools;
using Mongle.Village;
using Mongle.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>Furniture placing's screen (Figma 115:1398) and its 되돌리기 / 다시 하기.</summary>
    public class PlaceBarViewTests
    {
        [Test]
        public void ThePrefabIsWiredAndListed()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceBarBuild.PrefabPath);
            Assert.IsNotNull(p, PlaceBarBuild.PrefabPath);
            var v = p.GetComponent<PlaceBarView>();
            Assert.IsNotNull(v.backButton); Assert.IsNotNull(v.undoButton); Assert.IsNotNull(v.redoButton);
            Assert.IsNotNull(v.rotateButton); Assert.IsNotNull(v.storeButton); Assert.IsNotNull(v.putButton); Assert.IsNotNull(v.cancelButton);
            Assert.AreEqual(4, v.tips.Length);
            Assert.AreEqual("Btn_여기에 두기", v.putButton.name, "the QA run finds picture-only buttons by name");
            Assert.AreEqual("Btn_나가기", v.backButton.name);
            foreach (var b in new[] { v.backButton, v.undoButton, v.redoButton, v.rotateButton, v.storeButton, v.putButton, v.cancelButton })
                Assert.IsNotNull(((UnityEngine.UI.Image)b.targetGraphic).sprite, b.name);
            Assert.AreEqual(p, ViewIndex.Prefab("PlaceBar"));
        }

        [Test]
        public void TheCornerButtonAndTheBarStandWhereTheFrameHasThem()
        {
            var go = PlaceBarBuild.Make();
            try
            {
                var v = go.GetComponent<PlaceBarView>();
                // ◀: the unified corner button, node (50, 48), its picture 1.5 out
                var back = (RectTransform)v.backButton.transform;
                Assert.AreEqual(48.5f, back.anchoredPosition.x, 0.01f); Assert.AreEqual(-46.5f, back.anchoredPosition.y, 0.01f);
                // the bar is pinned to the bottom left, 36 above the foot (886.5 + 157.5 = 1044)
                var bar = (RectTransform)v.putButton.transform.parent;
                Assert.AreEqual(Vector2.zero, bar.anchorMin); Assert.AreEqual(67f, bar.anchoredPosition.x, 0.01f); Assert.AreEqual(36f, bar.anchoredPosition.y, 0.01f);
                // every tip stays on the screen and above the bar
                foreach (var t in v.tips)
                {
                    var r = (RectTransform)t.transform;
                    Assert.GreaterOrEqual(bar.anchoredPosition.x + r.anchoredPosition.x, 0f, t.name);
                    Assert.Greater(r.anchoredPosition.y, 0f, t.name + " above the bar");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PutItHereShowsWhileAPieceIsPickedAndUndoDimsWhenEmpty()
        {
            var go = PlaceBarBuild.Make();
            try
            {
                var v = go.GetComponent<PlaceBarView>();
                v.Show(true);
                v.Refresh(false, false, false);
                foreach (var t in v.tips) Assert.IsFalse(t.activeSelf);
                Assert.IsFalse(v.UndoLit); Assert.IsFalse(v.RedoLit);
                v.Refresh(true, true, false);
                Assert.IsTrue(v.TipShown(PlaceBarView.PutTip), "Figma: 여기에 두기 over its button");
                Assert.IsTrue(v.UndoLit); Assert.IsFalse(v.RedoLit);
                // a house being set down: no 되돌리기 / 다시 하기 (nor their keys); 여기에 두기 dims where it cannot go
                v.SetUndoShown(false);
                Assert.IsFalse(v.UndoShown); Assert.IsFalse(v.redoButton.gameObject.activeSelf); foreach (var k in v.undoKeys) Assert.IsFalse(k.activeSelf);
                v.SetPutReady(false); Assert.Less(v.putButton.targetGraphic.color.a, 1f);
                v.SetPutReady(true); Assert.AreEqual(1f, v.putButton.targetGraphic.color.a, 0.001f);
                v.SetUndoShown(true); Assert.IsTrue(v.UndoShown);
                v.Show(false);
                Assert.IsFalse(v.Shown); Assert.IsFalse(v.TipShown(PlaceBarView.PutTip));
            }
            finally { Object.DestroyImmediate(go); }
        }

        static PlacementStep Move(string id, int fx, int tx) => new PlacementStep { kind = PlacementChange.Move, furnitureId = id, instanceId = id, fromX = fx, toX = tx };

        [Test]
        public void HistoryTakesBackNewestFirstAndANewChangeDropsTheRedo()
        {
            var h = new PlacementHistory();
            Assert.IsFalse(h.CanUndo); Assert.IsFalse(h.PeekUndo(out _));
            h.Record(Move("a", 0, 1)); h.Record(Move("b", 2, 3));
            Assert.IsTrue(h.PeekUndo(out var s)); Assert.AreEqual("b", s.instanceId);
            Assert.AreEqual(2, h.UndoCount, "peeking moves nothing");
            h.CommitUndo();
            Assert.IsTrue(h.CanRedo); Assert.IsTrue(h.PeekRedo(out s)); Assert.AreEqual("b", s.instanceId);
            Assert.IsTrue(h.PeekUndo(out s)); Assert.AreEqual("a", s.instanceId);
            h.CommitRedo();
            Assert.IsFalse(h.CanRedo); Assert.AreEqual(2, h.UndoCount);
            h.CommitUndo(); h.Record(Move("c", 4, 5));
            Assert.IsFalse(h.CanRedo, "a new change drops what was taken back");
            Assert.IsTrue(h.PeekUndo(out s)); Assert.AreEqual("c", s.instanceId);
            h.Clear(); Assert.IsFalse(h.CanUndo); Assert.IsFalse(h.CanRedo);
        }

        [Test]
        public void HistoryKeepsOnlyTheNewestChanges()
        {
            var h = new PlacementHistory { Limit = 3 };
            for (int i = 0; i < 5; i++) h.Record(Move("p" + i, i, i + 1));
            Assert.AreEqual(3, h.UndoCount);
            for (int i = 4; i >= 2; i--) { Assert.IsTrue(h.PeekUndo(out var s)); Assert.AreEqual("p" + i, s.instanceId); h.CommitUndo(); }
            Assert.IsFalse(h.CanUndo);
            h.CommitUndo();   // nothing left: no harm
            Assert.AreEqual(3, h.RedoCount);
        }
    }
}
