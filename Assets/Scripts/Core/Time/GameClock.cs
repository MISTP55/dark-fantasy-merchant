using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The time of the simulated world. Turns real seconds into simulated ones, at normal
    /// speed or in fast forward, cuts them into short steps and tells when a day starts.
    /// The simulation is advanced with <see cref="Stepped"/>, never with real time.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>A longer real step is a stall, not time played, and is clamped to this.</summary>
        public const float MaxRealDeltaSeconds = 3600f;

        /// <summary>
        /// The longest step of the world, in days. What is checked once per step (a ship
        /// that arrives, a stopover that ends) loses at most this much time, whatever the speed.
        /// </summary>
        public const double MaxStepDays = 0.1d;

        /// <summary>
        /// The most steps one <see cref="Advance"/> takes. The time they cannot hold is
        /// dropped: the world then runs slower than asked rather than freezing the game.
        /// </summary>
        public const int MaxStepsPerAdvance = 10000;

        private readonly double secondsPerDay;
        private readonly double maxStepSeconds;
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
            maxStepSeconds = secondsPerDay * MaxStepDays;
            this.fastForwardMultiplier = fastForwardMultiplier;
            this.startYear = startYear;
            this.monthsPerYear = monthsPerYear;
            this.daysPerMonth = daysPerMonth;

            Date = DateAfter(0);
        }

        /// <summary>Simulated seconds of the last <see cref="Advance"/>, all its steps together.</summary>
        public float DeltaTime { get; private set; }

        /// <summary>Whole days since the start of the game.</summary>
        public int ElapsedDays { get; private set; }

        public GameDate Date { get; private set; }

        public bool IsFastForward { get; private set; }

        /// <summary>
        /// How many times faster the world runs at normal speed: 1, but in the debug tools.
        /// Fast forward keeps its own multiplier.
        /// </summary>
        public float NormalSpeedMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Raised once for every day that starts, in order, with its date. A step that
        /// crosses several days raises it for each of them.
        /// </summary>
        public event Action<GameDate> DayStarted;

        /// <summary>
        /// Raised for every step of the world's time, with its simulated seconds, after the
        /// days it starts. Steps last at most <see cref="MaxStepDays"/>, whatever the speed.
        /// </summary>
        public event Action<float> Stepped;

        /// <summary>Raised with the new value, only on an actual change.</summary>
        public event Action<bool> FastForwardChanged;

        /// <summary>
        /// Passes the world's time of a real step. A listener that changes the speed changes
        /// it from the next step: what is left of the real step passes at the new speed.
        /// </summary>
        public void Advance(float realDeltaSeconds)
        {
            // The negated comparison also rejects NaN.
            if (!(realDeltaSeconds > 0f) || float.IsInfinity(realDeltaSeconds))
            {
                DeltaTime = 0f;
                return;
            }

            double start = elapsedSeconds;
            double realLeft = Math.Min(realDeltaSeconds, MaxRealDeltaSeconds);
            int steps = 0;

            // One pass for each speed the real step is played at.
            while (realLeft > 0d && steps < MaxStepsPerAdvance)
            {
                double multiplier = Multiplier;
                double paceStart = elapsedSeconds;
                double paceReal = realLeft;
                double simulated = realLeft * multiplier;
                double count = Math.Ceiling(simulated / maxStepSeconds);
                int budget = MaxStepsPerAdvance - steps;

                if (count > budget)
                {
                    count = budget;
                    simulated = budget * maxStepSeconds;
                }

                realLeft = 0d;

                for (int i = 1; i <= count; i++)
                {
                    // Positions in the pass rather than a sum of steps, so that its end is
                    // exactly where one multiplication puts it.
                    double previous = elapsedSeconds;
                    elapsedSeconds = i == count ? paceStart + simulated : paceStart + simulated * i / count;
                    steps++;
                    AnnounceDays();
                    Stepped?.Invoke((float)(elapsedSeconds - previous));

                    if (Multiplier != multiplier)
                    {
                        // The steps of a pass are equal: so is the real time each takes.
                        realLeft = paceReal * (count - i) / count;
                        break;
                    }
                }
            }

            DeltaTime = (float)(elapsedSeconds - start);
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

        /// <summary>For the debug tools: speeds the normal speed up without a fast forward.</summary>
        public void SetNormalSpeedMultiplier(float multiplier)
        {
            if (!IsFinitePositive(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            NormalSpeedMultiplier = multiplier;
        }

        private double Multiplier => IsFastForward ? fastForwardMultiplier : NormalSpeedMultiplier;

        private void AnnounceDays()
        {
            int days = (int)Math.Floor(elapsedSeconds / secondsPerDay);

            while (ElapsedDays < days)
            {
                ElapsedDays++;
                Date = DateAfter(ElapsedDays);
                DayStarted?.Invoke(Date);
            }
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
