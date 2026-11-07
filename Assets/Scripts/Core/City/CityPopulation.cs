using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The inhabitants of one city, in whole numbers: the housed, the workers' families, who live in the city's
    /// houses (their room), and the beggars, who have no place. Workers' families are hired from the beggars and
    /// sent back to them.
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

        /// <summary>The places left in the houses; never negative, even when more live there than there is room.</summary>
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
        /// The population of a new game: every worker's family is housed, a share of beggars comes with them, and
        /// the city has as many houses as they need.
        /// </summary>
        /// <param name="workers">Workers of the city.</param>
        /// <param name="inhabitantsPerWorker">Inhabitants a worker's family takes up, at least 1.</param>
        /// <param name="beggarsPerWorker">Beggars per worker, rounded down for the city.</param>
        /// <param name="roomPerHouse">Places of one house, at least 1; the room is a whole number of houses, rounded up.</param>
        public static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, int roomPerHouse)
        {
            return AtStart(workers, inhabitantsPerWorker, beggarsPerWorker, roomPerHouse, RoomAtStart(workers, inhabitantsPerWorker, roomPerHouse));
        }

        /// <summary>
        /// The room of a new game: as many whole houses as the housed need, which is what
        /// <see cref="AtStart(int, int, double, int)"/> gives.
        /// </summary>
        /// <param name="workers">Workers of the city.</param>
        /// <param name="inhabitantsPerWorker">Inhabitants a worker's family takes up, at least 1.</param>
        /// <param name="roomPerHouse">Places of one house, at least 1.</param>
        public static int RoomAtStart(int workers, int inhabitantsPerWorker, int roomPerHouse)
        {
            RequireStart(workers, inhabitantsPerWorker, roomPerHouse);
            int housed = workers * inhabitantsPerWorker;

            return (housed + roomPerHouse - 1) / roomPerHouse * roomPerHouse;
        }

        /// <summary>The population of a new game, with the given room instead of the whole houses' (see <see cref="RoomAtStart"/>).</summary>
        /// <param name="room">Places of the city's houses.</param>
        public static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, int roomPerHouse, int room)
        {
            RequireStart(workers, inhabitantsPerWorker, roomPerHouse);

            // The negated comparison also rejects NaN.
            if (!(beggarsPerWorker >= 0d) || double.IsInfinity(beggarsPerWorker))
            {
                throw new ArgumentOutOfRangeException(nameof(beggarsPerWorker));
            }

            int beggars = (int)Math.Floor(workers * beggarsPerWorker + 1e-9);

            return new CityPopulation(workers * inhabitantsPerWorker, beggars, room);
        }

        private static void RequireStart(int workers, int inhabitantsPerWorker, int roomPerHouse)
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
        }

        /// <summary>Adds places to the houses.</summary>
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

        /// <summary>
        /// Sends the families of workers who leave their jobs back to the beggars, whole families only, and never
        /// the last housed inhabitant. A worker whose family cannot be taken keeps his job.
        /// </summary>
        /// <returns>The workers who stay, whose families were not taken.</returns>
        public int SendToBeggars(int workers, int inhabitantsPerWorker)
        {
            int inhabitants = InhabitantsOf(workers, inhabitantsPerWorker);

            if (inhabitants > Housed)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            int taken = Math.Min(workers, Math.Max(0, Housed - 1) / inhabitantsPerWorker);
            Housed -= taken * inhabitantsPerWorker;
            Beggars += taken * inhabitantsPerWorker;

            return workers - taken;
        }

        /// <summary>Housed inhabitants who leave the city: they do not become beggars.</summary>
        public void Leave(int inhabitants)
        {
            if (inhabitants < 0 || inhabitants > Housed)
            {
                throw new ArgumentOutOfRangeException(nameof(inhabitants));
            }

            Housed -= inhabitants;
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
