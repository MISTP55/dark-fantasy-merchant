using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The inhabitants of one city, in whole numbers: the rich, the wealthy and the poor, who
    /// live in the houses of their class (its room), and the beggars, who have no place; and the
    /// satisfaction of each housed class. Workers' families are hired from the beggars as poor
    /// and sent back to them; the poor rise into the other classes and fall back
    /// (<see cref="SocialRule"/>).
    /// </summary>
    public sealed class CityPopulation
    {
        // The weight of a satisfied class when a family leaves for the beggars.
        private const int LeavingWeight = 10;

        // The weights are shifted down by 4 bits until their total is no more than this. The
        // arithmetic would not overflow without it, but the scaling is kept on purpose: its
        // truncation changes the share each class gives.
        private const int LeavingWeightCeiling = 0x1000;

        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        private readonly int[] counts = new int[3];
        private readonly int[] rooms = new int[3];
        private readonly int[] satisfactions = new int[3];

        /// <summary>A city with poor inhabitants only, in houses with exactly their room.</summary>
        public CityPopulation(int housed)
            : this(housed, 0, housed)
        {
        }

        /// <summary>A city whose housed inhabitants are all poor, the room being theirs.</summary>
        public CityPopulation(int housed, int beggars, int room)
            : this(new PerClass<int>(0, 0, housed), beggars, new PerClass<int>(0, 0, room))
        {
        }

        public CityPopulation(PerClass<int> housed, int beggars, PerClass<int> room)
        {
            if (beggars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beggars));
            }

            foreach (SocialClass housedClass in Classes)
            {
                if (housed[housedClass] < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(housed));
                }

                if (room[housedClass] < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(room));
                }

                counts[(int)housedClass] = housed[housedClass];
                rooms[(int)housedClass] = room[housedClass];
            }

            Beggars = beggars;
            StartingInhabitants = Housed + beggars;
        }

        /// <summary>The inhabitants who live in a house, of every class.</summary>
        public int Housed => counts[0] + counts[1] + counts[2];

        /// <summary>The inhabitants who have no house.</summary>
        public int Beggars { get; private set; }

        /// <summary>The places of the city's houses, of every class.</summary>
        public int Room => rooms[0] + rooms[1] + rooms[2];

        /// <summary>Every inhabitant, housed or not.</summary>
        public int Inhabitants => Housed + Beggars;

        /// <summary>The inhabitants the city had when the game started.</summary>
        public int StartingInhabitants { get; }

        /// <summary>
        /// The change since the game started, as a share of the inhabitants it started
        /// with: 0.25 is +25 %. 0 for a city that started empty.
        /// </summary>
        public double GrowthSinceStart => StartingInhabitants > 0 ? (double)Inhabitants / StartingInhabitants - 1d : 0d;

        public int CountOf(SocialClass housedClass)
        {
            return counts[IndexOf(housedClass)];
        }

        /// <summary>The places of the houses of a class.</summary>
        public int RoomOf(SocialClass housedClass)
        {
            return rooms[IndexOf(housedClass)];
        }

        /// <summary>The places left in the houses of a class; never negative, even when more live there than there is room.</summary>
        public int FreeRoomOf(SocialClass housedClass)
        {
            return Math.Max(0, RoomOf(housedClass) - CountOf(housedClass));
        }

        /// <summary>How satisfied a class is: 0 until it is set.</summary>
        public int SatisfactionOf(SocialClass housedClass)
        {
            return satisfactions[IndexOf(housedClass)];
        }

        public void SetSatisfaction(SocialClass housedClass, int satisfaction)
        {
            satisfactions[IndexOf(housedClass)] = satisfaction;
        }

        /// <summary>
        /// The population of a new game: every worker's family is housed, a tenth of them rich,
        /// three tenths wealthy and the rest poor; a share of beggars comes with them, and each
        /// class has as many of its houses as it needs.
        /// </summary>
        /// <param name="workers">Workers of the city.</param>
        /// <param name="inhabitantsPerWorker">Inhabitants a worker's family takes up, at least 1.</param>
        /// <param name="beggarsPerWorker">Beggars per worker, rounded down for the city.</param>
        /// <param name="roomPerHouse">Places of one house of each class, at least 1; each room is a whole number of houses, rounded up.</param>
        public static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, PerClass<int> roomPerHouse)
        {
            if (workers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            if (inhabitantsPerWorker < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(inhabitantsPerWorker));
            }

            foreach (SocialClass housedClass in Classes)
            {
                if (roomPerHouse[housedClass] < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(roomPerHouse));
                }
            }

            // The negated comparison also rejects NaN.
            if (!(beggarsPerWorker >= 0d) || double.IsInfinity(beggarsPerWorker))
            {
                throw new ArgumentOutOfRangeException(nameof(beggarsPerWorker));
            }

            int housed = workers * inhabitantsPerWorker;
            int rich = housed / 10;
            int wealthy = 3 * rich;
            var counts = new PerClass<int>(rich, wealthy, housed - rich - wealthy);
            int beggars = (int)Math.Floor(workers * beggarsPerWorker + 1e-9);
            var room = new PerClass<int>(
                RoomFor(counts.Rich, roomPerHouse.Rich),
                RoomFor(counts.Wealthy, roomPerHouse.Wealthy),
                RoomFor(counts.Poor, roomPerHouse.Poor));

            return new CityPopulation(counts, beggars, room);
        }

        /// <summary>Adds places to the houses of a class.</summary>
        public void AddRoom(SocialClass housedClass, int places)
        {
            if (places < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(places));
            }

            rooms[IndexOf(housedClass)] += places;
        }

        /// <summary>Houses the families of hired workers: their inhabitants leave the beggars and become poor.</summary>
        public void Hire(int workers, int inhabitantsPerWorker)
        {
            int inhabitants = InhabitantsOf(workers, inhabitantsPerWorker);

            if (inhabitants > Beggars)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            Beggars -= inhabitants;
            counts[(int)SocialClass.Poor] += inhabitants;
        }

        /// <summary>
        /// Sends the families of workers who leave their jobs back to the beggars: they are taken
        /// from the three classes in proportion to their count, the unhappy ones giving more, and
        /// no class falls below one inhabitant. A worker whose family cannot be taken in full keeps
        /// his job.
        /// </summary>
        /// <returns>The workers who stay, whose families were not taken.</returns>
        public int SendToBeggars(int workers, int inhabitantsPerWorker)
        {
            int inhabitants = InhabitantsOf(workers, inhabitantsPerWorker);

            if (inhabitants > Housed)
            {
                throw new ArgumentOutOfRangeException(nameof(workers));
            }

            var weights = new int[3];
            int total = 0;

            for (int i = 0; i < 3; i++)
            {
                // An empty class gets no weight rather than a negative one; a city never has an empty class.
                int factor = satisfactions[i] < 0 ? LeavingWeight - satisfactions[i] : LeavingWeight;
                weights[i] = Math.Max(0, counts[i] - 1) * factor;
                total += weights[i];
            }

            while (total > LeavingWeightCeiling)
            {
                total >>= 4;

                for (int i = 0; i < 3; i++)
                {
                    weights[i] >>= 4;
                }
            }

            int remaining = inhabitants;

            for (int i = 0; i < 3; i++)
            {
                int taken = total > 0 ? (int)((long)weights[i] * inhabitants / total) : 0;
                taken = Math.Max(0, Math.Min(taken, Math.Min(counts[i] - 1, remaining)));
                counts[i] -= taken;
                Beggars += taken;
                remaining -= taken;
            }

            int kept = remaining / inhabitantsPerWorker;
            remaining %= inhabitantsPerWorker;

            // The last few, one at a time from the poor up. The loop also stops once no class
            // has more than one inhabitant, so that it never runs forever, though a city never
            // comes to that.
            for (int i = (int)SocialClass.Poor; remaining > 0 && AnyClassAboveOne(); i = i == 0 ? 2 : i - 1)
            {
                if (counts[i] > 1)
                {
                    counts[i]--;
                    Beggars++;
                    remaining--;
                }
            }

            if (remaining == 0)
            {
                return kept;
            }

            // One more worker stays, with the part of his family already taken back among the
            // poor: the housed stay whole families, four per worker.
            int givenBack = inhabitantsPerWorker - remaining;
            Beggars -= givenBack;
            counts[(int)SocialClass.Poor] += givenBack;

            return kept + 1;
        }

        private bool AnyClassAboveOne()
        {
            return counts[0] > 1 || counts[1] > 1 || counts[2] > 1;
        }

        /// <summary>Moves inhabitants from one class to another, in the same houses' count.</summary>
        public void Move(SocialClass from, SocialClass to, int inhabitants)
        {
            int source = IndexOf(from);
            int target = IndexOf(to);

            if (inhabitants < 0 || inhabitants > counts[source])
            {
                throw new ArgumentOutOfRangeException(nameof(inhabitants));
            }

            counts[source] -= inhabitants;
            counts[target] += inhabitants;
        }

        /// <summary>Inhabitants of a class who leave the city: they do not become beggars.</summary>
        public void Leave(SocialClass housedClass, int inhabitants)
        {
            int index = IndexOf(housedClass);

            if (inhabitants < 0 || inhabitants > counts[index])
            {
                throw new ArgumentOutOfRangeException(nameof(inhabitants));
            }

            counts[index] -= inhabitants;
        }

        public void SetBeggars(int beggars)
        {
            if (beggars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beggars));
            }

            Beggars = beggars;
        }

        private static int RoomFor(int inhabitants, int roomPerHouse)
        {
            return (inhabitants + roomPerHouse - 1) / roomPerHouse * roomPerHouse;
        }

        private static int IndexOf(SocialClass housedClass)
        {
            if (housedClass < SocialClass.Rich || housedClass > SocialClass.Poor)
            {
                throw new ArgumentOutOfRangeException(nameof(housedClass));
            }

            return (int)housedClass;
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
