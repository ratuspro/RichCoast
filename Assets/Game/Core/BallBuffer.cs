using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>
    /// The player's fuel supply: balls available to drop into Zone A. Pure state, no scene.
    ///
    /// Dropping consumes a slot; slots are NOT replaced as they are consumed — the count falls
    /// until a score-bar cash-in refills it. At 0 no new balls can be dropped, but balls already
    /// on the board or in flight play out normally, which is what makes the last-chance window
    /// possible: a ball still in Zone B can fill the bar and restore the buffer before the run
    /// ends.
    ///
    /// The refill does not land all at once. A cash-in banks the owed slots via
    /// <see cref="BeginRefill"/> and the caller drips them in one at a time with
    /// <see cref="TakeRefillSlot"/>, each with its own pop and sound — so the buffer visibly
    /// climbs back rather than jumping. Dropping unlocks the moment the first slot lands.
    ///
    /// Replaces the milestone-driven <c>zoneB/BallBuffer.ts</c>, which the progression redesign
    /// superseded: capacity now comes from <see cref="Progression.BufferForLevel"/>, and a
    /// multi-level roll-through pays <see cref="Progression.BurstRefillBonus"/> extra per level
    /// beyond the first.
    /// </summary>
    public sealed class BallBuffer
    {
        public BallBuffer(int initialCount)
        {
            Count = Mathf.Max(0, initialCount);
        }

        /// <summary>Slots remaining right now.</summary>
        public int Count { get; private set; }

        /// <summary>Slots banked by a cash-in and not yet dripped in.</summary>
        public int PendingRefill { get; private set; }

        public bool IsExhausted => Count == 0;

        /// <summary>True while the run still has fuel — either in hand or owed.</summary>
        public bool HasSupply => Count > 0 || PendingRefill > 0;

        /// <summary>Consume one slot for a drop. False (and no change) when empty.</summary>
        public bool Spend()
        {
            if (Count <= 0) return false;
            Count -= 1;
            return true;
        }

        /// <summary>
        /// Bank the refill owed by a cash-in: enough slots to reach the level's capacity, plus the
        /// burst bonus for every level crossed beyond the first. Refills top the buffer up to
        /// capacity rather than adding on top of it, so carrying slots over is never punished but
        /// never stacks either.
        /// </summary>
        /// <param name="capacity">Capacity for the level just reached (<see cref="Progression.BufferForLevel"/>).</param>
        /// <param name="levelsCrossed">Levels the cash-in rolled through; 1 for an ordinary fill.</param>
        /// <returns>Slots banked by this call.</returns>
        public int BeginRefill(int capacity, int levelsCrossed = 1)
        {
            var bonus = Progression.BurstRefillBonus * Mathf.Max(0, levelsCrossed - 1);
            var target = Mathf.Max(0, capacity) + bonus;
            var owed = Mathf.Max(0, target - (Count + PendingRefill));
            PendingRefill += owed;
            return owed;
        }

        /// <summary>Land one banked slot. False when nothing is owed.</summary>
        public bool TakeRefillSlot()
        {
            if (PendingRefill <= 0) return false;
            PendingRefill -= 1;
            Count += 1;
            return true;
        }

        /// <summary>Start a fresh run: full buffer, nothing owed.</summary>
        public void Reset(int initialCount)
        {
            Count = Mathf.Max(0, initialCount);
            PendingRefill = 0;
        }
    }
}
