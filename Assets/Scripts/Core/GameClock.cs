using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The time of the simulated world. Turns real seconds into simulated ones, at normal
    /// speed or in fast forward, and tells when a day starts. The simulation is advanced
    /// with <see cref="DeltaTime"/>, never with real time.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>A longer real step is a stall, not time played, and is clamped to this.</summary>
        public const float MaxRealDeltaSeconds = 3600f;

        private readonly double secondsPerDay;
        private readonly float fastForwardMultiplier;
        private readonly int startYear;
        private readonly int monthsPerYear;
        private readonly int daysPerMonth;

        // A double: a float would lose the seconds after a few months of play.
        private double elapsedSeconds;

        /// <param name="secondsPerDay">Real seconds a day lasts at normal speed.</param>
        /// <param name="fastForwardMultiplier">How many times faster the world runs in fast forward.</param>
        public GameClock(
            float secondsPerDay, float fastForwardMultiplier, int startYear, int monthsPerYear, int daysPerMonth)
        {
            if (!IsFinitePositive(secondsPerDay))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
            }

            if (!IsFinitePositive(fastForwardMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(fastForwardMultiplier));
            }

            if (monthsPerYear < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsPerYear));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            this.secondsPerDay = secondsPerDay;
            this.fastForwardMultiplier = fastForwardMultiplier;
            this.startYear = startYear;
            this.monthsPerYear = monthsPerYear;
            this.daysPerMonth = daysPerMonth;

            Date = DateAfter(0);
        }

        /// <summary>Simulated seconds of the last <see cref="Advance"/>.</summary>
        public float DeltaTime { get; private set; }

        /// <summary>Whole days since the start of the game.</summary>
        public int ElapsedDays { get; private set; }

        public GameDate Date { get; private set; }

        public bool IsFastForward { get; private set; }

        /// <summary>
        /// Raised once for every day that starts, in order, with its date. A step that
        /// crosses several days raises it for each of them.
        /// </summary>
        public event Action<GameDate> DayStarted;

        /// <summary>Raised with the new value, only on an actual change.</summary>
        public event Action<bool> FastForwardChanged;

        public void Advance(float realDeltaSeconds)
        {
            // The negated comparison also rejects NaN.
            if (!(realDeltaSeconds > 0f) || float.IsInfinity(realDeltaSeconds))
            {
                DeltaTime = 0f;
                return;
            }

            float realDelta = Math.Min(realDeltaSeconds, MaxRealDeltaSeconds);
            DeltaTime = IsFastForward ? realDelta * fastForwardMultiplier : realDelta;
            elapsedSeconds += DeltaTime;

            int days = (int)Math.Floor(elapsedSeconds / secondsPerDay);

            while (ElapsedDays < days)
            {
                ElapsedDays++;
                Date = DateAfter(ElapsedDays);
                DayStarted?.Invoke(Date);
            }
        }

        public void SetFastForward(bool isFastForward)
        {
            if (IsFastForward == isFastForward)
            {
                return;
            }

            IsFastForward = isFastForward;
            FastForwardChanged?.Invoke(isFastForward);
        }

        private GameDate DateAfter(int elapsedDays)
        {
            return GameDate.FromElapsedDays(elapsedDays, startYear, monthsPerYear, daysPerMonth);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }
    }
}
