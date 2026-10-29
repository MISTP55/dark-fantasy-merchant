using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The workers of a city's own workshops, held by its <see cref="CityMarket"/>: by good, the workers and the
    /// reserve of the laid-off. Given a <see cref="LabourRule"/>, a workshop hires from its reserve, the beggars
    /// within the free room of the houses and the other reserves, and lays off into its reserve, which leaks back
    /// to the beggars; without one, a workshop has at once the workers it wants and its layoffs vanish.
    /// </summary>
    internal sealed class CityWorkforce
    {
        private readonly int[] workers;
        private readonly int[] reserves;
        private readonly double[] leakCounters;
        private readonly CityPopulation population;
        private readonly LabourRule labour;
        private readonly SocialRule social;

        /// <param name="startingWorkers">The workers of each good's workshop at the start; kept, not copied.</param>
        /// <param name="labour">Null for immediate hiring.</param>
        /// <param name="social">Null when the beggars are always hired.</param>
        public CityWorkforce(int[] startingWorkers, CityPopulation population, LabourRule labour, SocialRule social)
        {
            workers = startingWorkers;
            reserves = new int[startingWorkers.Length];
            leakCounters = new double[startingWorkers.Length];
            this.population = population;
            this.labour = labour;
            this.social = social;
        }

        public int WorkersOf(int good)
        {
            return workers[good];
        }

        public int ReserveOf(int good)
        {
            return reserves[good];
        }

        /// <summary>Brings a workshop to the workers it wants: hiring or laying off with labour, at once without.</summary>
        public void MoveTo(int good, int target)
        {
            if (labour == null)
            {
                workers[good] = target;
            }
            else if (target > workers[good])
            {
                Hire(good, target - workers[good]);
            }
            else if (target < workers[good])
            {
                LayOff(good, workers[good] - target);
            }
        }

        /// <summary>Takes away up to the wanted workers from the reserves, in the catalogue's order, and lowers wanted by as many.</summary>
        /// <returns>The workers taken.</returns>
        public int TakeReserves(ref int wanted)
        {
            int removed = 0;

            for (int good = 0; good < reserves.Length && wanted > 0; good++)
            {
                int taken = Math.Min(wanted, reserves[good]);
                reserves[good] -= taken;
                wanted -= taken;
                removed += taken;
            }

            return removed;
        }

        /// <summary>The workers above the protected ones, added up over the workshops.</summary>
        public int TakeableAbove(int protectedWorkers)
        {
            int takeable = 0;

            for (int good = 0; good < workers.Length; good++)
            {
                takeable += Math.Max(0, workers[good] - protectedWorkers);
            }

            return takeable;
        }

        /// <summary>
        /// Takes away workers above the protected ones, spread over the workshops in the catalogue's order: a worker
        /// for every step of the takeable workers accumulated, never a workshop's last one. Lowers wanted by as many,
        /// and leaves in accumulated what was not taken, for the workshops after these.
        /// </summary>
        /// <returns>The workers taken.</returns>
        public int TakeAbove(int protectedWorkers, int step, ref int wanted, ref int accumulated)
        {
            int removed = 0;

            for (int good = 0; good < workers.Length; good++)
            {
                if (workers[good] <= protectedWorkers)
                {
                    continue;
                }

                accumulated += workers[good] - protectedWorkers;

                if (accumulated >= step)
                {
                    int taken = Math.Min(accumulated / step, wanted);

                    if (taken >= workers[good])
                    {
                        taken = workers[good] - 1;
                    }

                    workers[good] -= taken;
                    wanted -= taken;
                    accumulated -= taken * step;
                    removed += taken;
                }
            }

            return removed;
        }

        /// <summary>The starting workforce spread over the goods that are produced: 0 for the others.</summary>
        public static int[] StartingWorkersOf(IReadOnlyList<MarketGood> goods, WorkforceRule workforce)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            if (workforce == null)
            {
                throw new ArgumentNullException(nameof(workforce));
            }

            int workshops = 0;

            for (int i = 0; i < goods.Count; i++)
            {
                if (goods[i].IsProduced)
                {
                    workshops++;
                }
            }

            var starting = new int[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                starting[i] = goods[i].IsProduced ? workforce.StartingWorkersOf(goods[i].MaxWorkers, workshops) : 0;
            }

            return starting;
        }

        // In this order: the workshop's own reserve, the beggars within the free room,
        // then the reserves of the other workshops in the order of the catalogue. What is still
        // missing is not hired today.
        private void Hire(int good, int wanted)
        {
            wanted -= TakeFromReserve(good, good, wanted);

            // The beggars are hired only while the poor are satisfied.
            bool poorSatisfied = social == null || population.SatisfactionOf(SocialClass.Poor) > 0;
            int fromBeggars = poorSatisfied
                ? Math.Min(wanted, labour.WorkersHireableFrom(population.Beggars, population.FreeRoomOf(SocialClass.Poor)))
                : 0;
            population.Hire(fromBeggars, labour.InhabitantsPerWorker);
            workers[good] += fromBeggars;
            wanted -= fromBeggars;

            for (int other = 0; other < workers.Length && wanted > 0; other++)
            {
                if (other != good)
                {
                    wanted -= TakeFromReserve(other, good, wanted);
                }
            }
        }

        // Moves up to the wanted workers from a workshop's reserve to a workshop; housed inhabitants do not change.
        private int TakeFromReserve(int reserve, int good, int wanted)
        {
            int taken = Math.Min(wanted, reserves[reserve]);
            reserves[reserve] -= taken;
            workers[good] += taken;

            return taken;
        }

        // The laid-off workers stay housed in the reserve, which leaks back to the beggars.
        private void LayOff(int good, int laidOff)
        {
            workers[good] -= laidOff;
            reserves[good] += laidOff;

            ReserveLeak leak = labour.LeakAfterLayoff(leakCounters[good], workers[good], reserves[good]);
            leakCounters[good] = leak.Counter;
            reserves[good] -= leak.Leaving;

            // A worker whose family cannot be taken stays in the reserve.
            reserves[good] += population.SendToBeggars(leak.Leaving, labour.InhabitantsPerWorker);
        }
    }
}
