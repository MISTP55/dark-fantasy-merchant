using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The time of the simulated world. Turns real seconds into simulated ones, at the
    /// current <see cref="GameSpeed"/>, cuts them into short steps and tells when a day
    /// starts. The simulation is advanced with <see cref="Stepped"/>, never with real time.
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
        private readonly float turboMultiplier;
        private readonly int startYear;
        private readonly int monthsPerYear;
        private readonly int daysPerMonth;

        // A double: a float would lose the seconds after a few months of play.
        private double elapsedSeconds;

        /// <param name="secondsPerDay">Real seconds a day lasts at normal speed.</param>
        /// <param name="fastForwardMultiplier">How many times faster the world runs in fast forward.</param>
        /// <param name="turboMultiplier">How many times faster the world runs in <see cref="GameSpeed.Turbo"/>.</param>
        public GameClock(
            float secondsPerDay,
            float fastForwardMultiplier,
            float turboMultiplier,
            int startYear,
            int monthsPerYear,
            int daysPerMonth)
        {
            if (!IsFinitePositive(secondsPerDay))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
            }

            if (!IsFinitePositive(fastForwardMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(fastForwardMultiplier));
            }

            if (!IsFinitePositive(turboMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(turboMultiplier));
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
            this.turboMultiplier = turboMultiplier;
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

        public GameSpeed Speed { get; private set; }

        public bool IsFastForward => Speed != GameSpeed.Normal;

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

        /// <summary>Raised with the new value of <see cref="IsFastForward"/>, only on an actual change.</summary>
        public event Action<bool> FastForwardChanged;

        /// <summary>Raised with the new value, only on an actual change.</summary>
        public event Action<GameSpeed> SpeedChanged;

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
                GameSpeed speed = Speed;
                double paceStart = elapsedSeconds;
                double paceReal = realLeft;
                double simulated = realLeft * MultiplierOf(speed);
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

                    if (Speed != speed)
                    {
                        // The steps of a pass are equal: so is the real time each takes.
                        realLeft = paceReal * (count - i) / count;
                        break;
                    }
                }
            }

            DeltaTime = (float)(elapsedSeconds - start);
        }

        public void SetSpeed(GameSpeed speed)
        {
            if (Speed == speed)
            {
                return;
            }

            bool wasFastForward = IsFastForward;
            Speed = speed;
            SpeedChanged?.Invoke(speed);

            if (IsFastForward != wasFastForward)
            {
                FastForwardChanged?.Invoke(IsFastForward);
            }
        }

        /// <summary>The player's fast forward, or the normal speed.</summary>
        public void SetFastForward(bool isFastForward)
        {
            SetSpeed(isFastForward ? GameSpeed.FastForward : GameSpeed.Normal);
        }

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

        private double MultiplierOf(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.FastForward:
                    return fastForwardMultiplier;
                case GameSpeed.Turbo:
                    return turboMultiplier;
                default:
                    return 1d;
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
