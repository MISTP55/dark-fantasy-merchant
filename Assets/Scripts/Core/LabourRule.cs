using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a city's labour does, as in Patrician III (see
    /// <c>patrician3-re\resultats\Population des villes.md</c>): the beggars it attracts,
    /// how many workers they can be hired as, the vacancies they fill and the reserve of
    /// laid-off workers that leaks away. Stateless: the figures are passed in.
    /// </summary>
    public sealed class LabourRule
    {
        /// <param name="inhabitantsPerWorker">Inhabitants a worker's family takes up (4).</param>
        /// <param name="beggarSatisfaction">The satisfaction that sets the beggars' target; 0 or less gives the minimum.</param>
        /// <param name="beggarTargetDivisor">Divisor of the square root in the beggars' target.</param>
        /// <param name="beggarTargetMinimum">Beggars every city attracts at least.</param>
        /// <param name="beggarArrivalDivisor">Divisor of the square root of the population for arrivals.</param>
        /// <param name="beggarDepartureDivisor">Divisor of the square root of the population for departures.</param>
        /// <param name="vacancyMinimumBeggars">Beggars needed before vacancies are filled.</param>
        /// <param name="vacancyMinimumFreeRoom">Free room that must be exceeded before vacancies are filled.</param>
        /// <param name="reserveLeakShare">Share of the reserve that leaks away each day.</param>
        /// <param name="reserveLeakCeiling">Workers plus reserve from which nothing leaks.</param>
        public LabourRule(
            int inhabitantsPerWorker,
            double beggarSatisfaction,
            int beggarTargetDivisor,
            int beggarTargetMinimum,
            int beggarArrivalDivisor,
            int beggarDepartureDivisor,
            int vacancyMinimumBeggars,
            int vacancyMinimumFreeRoom,
            double reserveLeakShare,
            int reserveLeakCeiling)
        {
            RequireAtLeast(inhabitantsPerWorker, 1, nameof(inhabitantsPerWorker));

            // The negated comparison also rejects NaN.
            if (!(Math.Abs(beggarSatisfaction) <= double.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(beggarSatisfaction), "The satisfaction is a finite number.");
            }

            RequireAtLeast(beggarTargetDivisor, 1, nameof(beggarTargetDivisor));
            RequireAtLeast(beggarTargetMinimum, 0, nameof(beggarTargetMinimum));
            RequireAtLeast(beggarArrivalDivisor, 1, nameof(beggarArrivalDivisor));
            RequireAtLeast(beggarDepartureDivisor, 1, nameof(beggarDepartureDivisor));
            RequireAtLeast(vacancyMinimumBeggars, 0, nameof(vacancyMinimumBeggars));
            RequireAtLeast(vacancyMinimumFreeRoom, 0, nameof(vacancyMinimumFreeRoom));

            // The negated comparison also rejects NaN.
            if (!(reserveLeakShare >= 0d && reserveLeakShare <= 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(reserveLeakShare), "The leak share is between 0 and 1.");
            }

            RequireAtLeast(reserveLeakCeiling, 0, nameof(reserveLeakCeiling));

            InhabitantsPerWorker = inhabitantsPerWorker;
            BeggarSatisfaction = beggarSatisfaction;
            BeggarTargetDivisor = beggarTargetDivisor;
            BeggarTargetMinimum = beggarTargetMinimum;
            BeggarArrivalDivisor = beggarArrivalDivisor;
            BeggarDepartureDivisor = beggarDepartureDivisor;
            VacancyMinimumBeggars = vacancyMinimumBeggars;
            VacancyMinimumFreeRoom = vacancyMinimumFreeRoom;
            ReserveLeakShare = reserveLeakShare;
            ReserveLeakCeiling = reserveLeakCeiling;
        }

        public int InhabitantsPerWorker { get; }

        public double BeggarSatisfaction { get; }

        public int BeggarTargetDivisor { get; }

        public int BeggarTargetMinimum { get; }

        public int BeggarArrivalDivisor { get; }

        public int BeggarDepartureDivisor { get; }

        public int VacancyMinimumBeggars { get; }

        public int VacancyMinimumFreeRoom { get; }

        public double ReserveLeakShare { get; }

        public int ReserveLeakCeiling { get; }

        /// <summary>The beggars a city of this population draws towards.</summary>
        public int BeggarTargetFor(int population)
        {
            if (!(BeggarSatisfaction > 0d))
            {
                return BeggarTargetMinimum;
            }

            int root = (int)Math.Sqrt(Math.Floor(population * BeggarSatisfaction / 2d));

            return root / BeggarTargetDivisor + BeggarTargetMinimum;
        }

        /// <summary>The beggars after a day: some come or go, never past the target.</summary>
        public int NextBeggars(int beggars, int population)
        {
            int target = BeggarTargetFor(population);
            int root = (int)Math.Sqrt(Math.Max(0, population));

            if (beggars < target)
            {
                return beggars + Math.Min(target - beggars, (root + BeggarArrivalDivisor - 1) / BeggarArrivalDivisor);
            }

            if (beggars > target)
            {
                return beggars - Math.Min(beggars - target, (root + BeggarDepartureDivisor - 1) / BeggarDepartureDivisor);
            }

            return beggars;
        }

        /// <summary>The workers a workshop can hire from the beggars, within the free room.</summary>
        public int WorkersHireableFrom(int beggars, int freeRoom)
        {
            return Math.Min(beggars / InhabitantsPerWorker, Math.Max(0, freeRoom) / InhabitantsPerWorker);
        }

        /// <summary>The workers that fill the vacancies of the workshops of a city, from its beggars.</summary>
        public int VacanciesFillable(int beggars, int freeRoom)
        {
            if (beggars < VacancyMinimumBeggars || freeRoom <= VacancyMinimumFreeRoom)
            {
                return 0;
            }

            return Math.Min(beggars - 1, freeRoom) / InhabitantsPerWorker;
        }

        /// <summary>
        /// The laid-off workers who leave the city today, and the counter that accumulates the
        /// reserve's leak between days.
        /// </summary>
        public ReserveLeak LeakAfterLayoff(double counter, int workers, int reserve)
        {
            double next = counter + reserve * ReserveLeakShare;
            int whole = Math.Max(0, (int)next);
            next -= whole;
            int total = workers + reserve;

            if (whole == 0 || total >= ReserveLeakCeiling)
            {
                return new ReserveLeak(0, next);
            }

            int leaving = Math.Min(whole, ReserveLeakCeiling - total);

            // Patrician III subtracts what leaves a second time.
            return new ReserveLeak(leaving, next - leaving);
        }

        private static void RequireAtLeast(int value, int minimum, string name)
        {
            if (value < minimum)
            {
                throw new ArgumentOutOfRangeException(name, $"The value is at least {minimum}.");
            }
        }
    }

    /// <summary>The outcome of a day's leak of the reserve: who leaves and the counter to keep.</summary>
    public readonly struct ReserveLeak
    {
        public ReserveLeak(int leaving, double counter)
        {
            Leaving = leaving;
            Counter = counter;
        }

        /// <summary>Laid-off workers who leave the city.</summary>
        public int Leaving { get; }

        /// <summary>The fractional counter carried to the next day.</summary>
        public double Counter { get; }
    }
}
