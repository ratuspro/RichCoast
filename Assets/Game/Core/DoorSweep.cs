using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>
    /// The trap-door's sweeping marker: a lit position stepping back and forth across nine evenly
    /// spaced columns along the Zone A/B boundary. Tapping freezes it, and the frozen column is
    /// where the ball enters Zone B — which is what makes WHERE a ball lands a timing skill rather
    /// than a fixed point.
    ///
    /// Pure and step-based (not continuous): the marker lands on discrete columns so the player
    /// can read and aim for one, and so the same column indices drive both the marker view and the
    /// entry position. Ping-pong rather than wrap, so both edges are reachable with the same dwell
    /// as the middle.
    /// </summary>
    public struct DoorSweep
    {
        /// <summary>Columns the marker can stop on.</summary>
        public const int Columns = 9;

        /// <summary>Time the marker dwells on each column (ms).</summary>
        public const float DefaultStepMs = 110f;

        private float _elapsed;
        private int _index;
        private int _direction;

        public static DoorSweep New() => new DoorSweep { _index = 0, _direction = 1, _elapsed = 0f };

        /// <summary>Column the marker is currently lit on, 0..<see cref="Columns"/>-1.</summary>
        public int Index => _index;

        /// <summary>Restart the sweep from the left edge — used whenever the door re-arms.</summary>
        public void Reset()
        {
            _index = 0;
            _direction = 1;
            _elapsed = 0f;
        }

        /// <summary>Advance the marker. Steps at most one column per call at normal frame rates.</summary>
        public void Tick(float deltaMs, float stepMs = DefaultStepMs)
        {
            _elapsed += deltaMs;
            while (_elapsed >= stepMs)
            {
                _elapsed -= stepMs;
                Step();
            }
        }

        private void Step()
        {
            // Bounce off the ends instead of wrapping: a wrap would make the edge columns flash
            // past in half the time of the others.
            if (_index + _direction < 0 || _index + _direction >= Columns) _direction = -_direction;
            _index += _direction;
        }

        /// <summary>
        /// Design-space x of a column, inset by <paramref name="margin"/> so a ball can never enter
        /// Zone B already inside a side wall.
        /// </summary>
        public static float ColumnX(int index, float margin)
        {
            var clamped = Mathf.Clamp(index, 0, Columns - 1);
            var usable = Layout.Width - margin * 2f;
            return margin + usable * (clamped / (float)(Columns - 1));
        }

        /// <summary>Design-space x of the currently lit column.</summary>
        public float CurrentX(float margin) => ColumnX(_index, margin);
    }
}
