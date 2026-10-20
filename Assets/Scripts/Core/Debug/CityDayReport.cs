using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What happened in the last day of a city, for the debug tools: what each good was needed,
    /// consumed and produced, the workers its workshop hired and laid off, whether it stood still
    /// above T3; the poor who rose or fell between the classes and those who left, the beggars'
    /// change and the houses the city built. Filled by <see cref="CityMarket.StartDay"/>, which it
    /// changes nothing of; all zeros before the first day.
    /// </summary>
    public sealed class CityDayReport
    {
        private readonly double[] needed;
        private readonly double[] consumed;
        private readonly double[] produced;
        private readonly int[] hired;
        private readonly int[] laidOff;
        private readonly bool[] halted;
        private readonly int[] classChanges = new int[3];
        private readonly int[] housesBuilt = new int[3];

        public CityDayReport(int goodCount)
        {
            if (goodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goodCount));
            }

            needed = new double[goodCount];
            consumed = new double[goodCount];
            produced = new double[goodCount];
            hired = new int[goodCount];
            laidOff = new int[goodCount];
            halted = new bool[goodCount];
        }

        /// <summary>Poor who left the city, with their families.</summary>
        public int PoorLeft { get; private set; }

        /// <summary>Workers the poor who left took with them.</summary>
        public int WorkersLeft { get; private set; }

        /// <summary>Beggars who came, or, below 0, left.</summary>
        public int BeggarChange { get; private set; }

        /// <summary>Units the inhabitants needed in the day.</summary>
        public double NeededOf(int good)
        {
            return needed[good];
        }

        /// <summary>Units the inhabitants consumed: less than they needed when the stock ran out.</summary>
        public double ConsumedOf(int good)
        {
            return consumed[good];
        }

        /// <summary>Units the inhabitants needed and did not get.</summary>
        public double MissingOf(int good)
        {
            return Math.Max(0d, needed[good] - consumed[good]);
        }

        public double ProducedOf(int good)
        {
            return produced[good];
        }

        /// <summary>Workers the city's workshop for a good hired, from the reserves and the beggars.</summary>
        public int HiredOf(int good)
        {
            return hired[good];
        }

        /// <summary>Workers the city's workshop for a good sent to its reserve.</summary>
        public int LaidOffOf(int good)
        {
            return laidOff[good];
        }

        /// <summary>True when the city's workshop for a good produced nothing because its stock was above T3.</summary>
        public bool IsHaltedOf(int good)
        {
            return halted[good];
        }

        /// <summary>Inhabitants the rich or the wealthy took from the poor, or, below 0, gave back; 0 for the poor.</summary>
        public int ClassChangeOf(SocialClass housedClass)
        {
            return classChanges[(int)housedClass];
        }

        /// <summary>Houses of a class the city built of its own.</summary>
        public int HousesBuiltOf(SocialClass housedClass)
        {
            return housesBuilt[(int)housedClass];
        }

        internal void Clear()
        {
            Array.Clear(needed, 0, needed.Length);
            Array.Clear(consumed, 0, consumed.Length);
            Array.Clear(produced, 0, produced.Length);
            Array.Clear(hired, 0, hired.Length);
            Array.Clear(laidOff, 0, laidOff.Length);
            Array.Clear(halted, 0, halted.Length);
            Array.Clear(classChanges, 0, classChanges.Length);
            Array.Clear(housesBuilt, 0, housesBuilt.Length);
            PoorLeft = 0;
            WorkersLeft = 0;
            BeggarChange = 0;
        }

        internal void RecordConsumption(int good, double neededUnits, double consumedUnits)
        {
            needed[good] = neededUnits;
            consumed[good] = consumedUnits;
        }

        internal void RecordWorkshop(int good, int workersBefore, int workersAfter, double producedUnits, bool isHalted)
        {
            hired[good] = Math.Max(0, workersAfter - workersBefore);
            laidOff[good] = Math.Max(0, workersBefore - workersAfter);
            produced[good] = producedUnits;
            halted[good] = isHalted;
        }

        internal void RecordClassChange(SocialClass housedClass, int change)
        {
            classChanges[(int)housedClass] += change;
        }

        internal void RecordLeaving(int poor, int workers)
        {
            PoorLeft += poor;
            WorkersLeft += workers;
        }

        internal void RecordBeggarChange(int change)
        {
            BeggarChange += change;
        }

        internal void RecordHouse(SocialClass housedClass)
        {
            housesBuilt[(int)housedClass]++;
        }
    }
}
