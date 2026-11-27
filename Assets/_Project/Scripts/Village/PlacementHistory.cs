using System.Collections.Generic;

namespace Mongle.Village
{
    /// <summary>What one confirmed change while placing furniture did.</summary>
    public enum PlacementChange
    {
        /// <summary>A piece in the park moved or turned (from → to).</summary>
        Move,
        /// <summary>A piece came out of the box onto the park (to).</summary>
        Place,
        /// <summary>A piece in the park went into the box (from).</summary>
        Store,
    }

    /// <summary>One confirmed change: which piece, where it stood before and where it stands after.</summary>
    public struct PlacementStep
    {
        public PlacementChange kind;
        public string furnitureId, instanceId;
        public int fromX, fromY, fromRot;   // Move, Store
        public int toX, toY, toRot;         // Move, Place
    }

    /// <summary>
    /// 되돌리기 (Z) and 다시 하기 (Y) while placing furniture (Figma 115:1398): the confirmed changes, newest last. A new change drops
    /// what was undone. The game first tries to apply a step (or its opposite) and only commits it when that worked, so a step
    /// that cannot be applied now (its spot taken, the piece in use) stays where it is.
    /// </summary>
    public class PlacementHistory
    {
        readonly List<PlacementStep> done = new List<PlacementStep>(), undone = new List<PlacementStep>();

        /// <summary>How many changes are kept; the oldest go first.</summary>
        public int Limit = 50;

        public bool CanUndo => done.Count > 0;
        public bool CanRedo => undone.Count > 0;
        public int UndoCount => done.Count;
        public int RedoCount => undone.Count;

        public void Record(PlacementStep step)
        {
            done.Add(step);
            if (done.Count > Limit) done.RemoveAt(0);
            undone.Clear();
        }

        /// <summary>The newest change, to be taken back (nothing is moved yet).</summary>
        public bool PeekUndo(out PlacementStep step) => Peek(done, out step);

        /// <summary>The change taken back last, to be done again.</summary>
        public bool PeekRedo(out PlacementStep step) => Peek(undone, out step);

        /// <summary>The newest change was taken back: it can be done again.</summary>
        public void CommitUndo() => Shift(done, undone);

        /// <summary>The change taken back last was done again.</summary>
        public void CommitRedo() => Shift(undone, done);

        public void Clear() { done.Clear(); undone.Clear(); }

        static bool Peek(List<PlacementStep> list, out PlacementStep step)
        {
            step = list.Count > 0 ? list[list.Count - 1] : default;
            return list.Count > 0;
        }

        static void Shift(List<PlacementStep> from, List<PlacementStep> to)
        {
            if (from.Count == 0) return;
            to.Add(from[from.Count - 1]);
            from.RemoveAt(from.Count - 1);
        }
    }
}
