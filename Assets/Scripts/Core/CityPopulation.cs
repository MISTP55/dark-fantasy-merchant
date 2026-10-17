using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The inhabitants of one city, in whole numbers: the housed ones, who live in its
    /// houses (the room), and the beggars, who have no place. Nothing changes them but
    /// the labour of the city: workers' families are housed or sent back to the beggars.
    /// </summary>
    public sealed class CityPopulation
    {
        /// <summary>A city with housed inhabitants only, in houses with exactly their room.</summary>
        public CityPopulation(int housed)
            : this(housed, 0, housed)
        {
        }

        public CityPopulation(int housed, int beggars, int room)
        {
            if (housed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(housed));
            }

            if (beggars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beggars));
            }

            if (room < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(room));
            }

            Housed = housed;
            Beggars = beggars;
            Room = room;
            StartingInhabitants = housed + beggars;
        }

        /// <summary>The inhabitants who live in a house.</summary>
        public int Housed { get; private set; }

        /// <summary>The inhabitants who have no house.</summary>
        public int Beggars { get; private set; }

        /// <summary>The places of the city's houses.</summary>
        public int Room { get; private set; }

        /// <summary>The places left in the houses; never negative, even when more are housed than there is room.</summary>
        public int FreeRoom => Math.Max(0, Room - Housed);

        /// <summary>Every inhabitant, housed or not.</summary>
        public int Inhabitants => Housed + Beggars;

        /// <summary>The inhabitants the city had when the game started.</summary>
        public int StartingInhabitants { get; }

        /// <summary>
        /// The change since the game started, as a share of the inhabitants it started
        /// with: 0.25 is +25 %. 0 for a city that started empty.
        /// </summary>
        public double GrowthSinceStart => StartingInhabitants > 0 ? (double)Inhabitants / StartingInhabitants - 1d : 0d;

        /// <summary>
        /// The population of a new game, as Patrician III starts it: every worker's family is housed,
        /// a share of beggars comes with them, and the houses are as many as the housed need.
        /// </summary>
        /// <param name="workers">Workers of the city.</param>
        /// <param name="inhabitantsPerWorker">Inhabitants a worker's family takes up, at least 1.</param>
        /// <param name="beggarsPerWorker">Beggars per worker, rounded down for the city.</param>
        /// <param name="roomPerHouse">Places of one house, at least 1; the room is a whole number of houses, rounded up.</param>
        public static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, int roomPerHouse)
        {
            if (workers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            if (inhabitantsPerWorker < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(inhabitantsPerWorker));
            }

            if (roomPerHouse < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roomPerHouse));
            }

            // The negated comparison also rejects NaN.
            if (!(beggarsPerWorker >= 0d) || double.IsInfinity(beggarsPerWorker))
            {
                throw new ArgumentOutOfRangeException(nameof(beggarsPerWorker));
            }

            int housed = workers * inhabitantsPerWorker;
            int beggars = (int)Math.Floor(workers * beggarsPerWorker + 1e-9);
            int room = (housed + roomPerHouse - 1) / roomPerHouse * roomPerHouse;

            return new CityPopulation(housed, beggars, room);
        }

        /// <summary>Adds places to the city's houses.</summary>
        public void AddRoom(int places)
        {
            if (places < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(places));
            }

            Room += places;
        }

        /// <summary>Houses the families of hired workers: their inhabitants leave the beggars.</summary>
        public void Hire(int workers, int inhabitantsPerWorker)
        {
            int inhabitants = InhabitantsOf(workers, inhabitantsPerWorker);

            if (inhabitants > Beggars)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            Beggars -= inhabitants;
            Housed += inhabitants;
        }

        /// <summary>Sends the families of workers who leave their jobs back to the beggars.</summary>
        public void SendToBeggars(int workers, int inhabitantsPerWorker)
        {
            int inhabitants = InhabitantsOf(workers, inhabitantsPerWorker);

            if (inhabitants > Housed)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            Housed -= inhabitants;
            Beggars += inhabitants;
        }

        public void SetBeggars(int beggars)
        {
            if (beggars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beggars));
            }

            Beggars = beggars;
        }

        private static int InhabitantsOf(int workers, int inhabitantsPerWorker)
        {
            if (workers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            if (inhabitantsPerWorker < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(inhabitantsPerWorker));
            }

            return workers * inhabitantsPerWorker;
        }
    }
}
