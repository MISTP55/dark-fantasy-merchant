using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How satisfied a city's classes are and what it does to them: each class's satisfaction
    /// follows the stock of the goods it consumes against their T0; the rich and the wealthy
    /// take or give back poor towards a share of the poor set by their satisfaction and their
    /// room, and the poor without room or unhappy leave the city. Stateless: the population
    /// is passed in. Integer arithmetic, truncated.
    /// </summary>
    public sealed class SocialRule
    {
        // A quarter, as a shift of two bits: a constant of its own, not the inhabitants per
        // worker, though both are 4.
        private const int ExcessPoorDivisor = 4;

        /// <param name="startingSatisfaction">Satisfaction of every class when the game starts (50).</param>
        /// <param name="satisfactionBases">The base of each class's target (-13, -18, -27).</param>
        /// <param name="satisfactionRise">Points a satisfaction rises in a day towards its target (2).</param>
        /// <param name="satisfactionFall">Points it falls in a day (1).</param>
        /// <param name="commonSatisfaction">The term every class's target shares (9).</param>
        /// <param name="lowestSatisfaction">The lowest a satisfaction falls (-40).</param>
        /// <param name="highestSatisfaction">The highest a satisfaction rises (80).</param>
        /// <param name="classTargetOffset">Added to a satisfaction for the share of the rich and wealthy (40).</param>
        /// <param name="richTargetDivisor">The rich wanted are (satisfaction + offset) x poor / this (320).</param>
        /// <param name="wealthyTargetDivisor">The wealthy wanted are (satisfaction + offset) x poor / this (160).</param>
        /// <param name="classRoomFloor">A class short of room still takes up to this many (10).</param>
        /// <param name="poorLeavingDivisor">Unhappy poor leave each day at their number / this (100).</param>
        /// <param name="protectedWorkers">Workers of a city's workshop the leaving poor do not take while others have more (30).</param>
        public SocialRule(
            int startingSatisfaction,
            PerClass<int> satisfactionBases,
            int satisfactionRise,
            int satisfactionFall,
            int commonSatisfaction,
            int lowestSatisfaction,
            int highestSatisfaction,
            int classTargetOffset,
            int richTargetDivisor,
            int wealthyTargetDivisor,
            int classRoomFloor,
            int poorLeavingDivisor,
            int protectedWorkers)
        {
            if (lowestSatisfaction > highestSatisfaction)
            {
                throw new ArgumentOutOfRangeException(nameof(lowestSatisfaction), "The lowest satisfaction is at most the highest.");
            }

            if (startingSatisfaction < lowestSatisfaction || startingSatisfaction > highestSatisfaction)
            {
                throw new ArgumentOutOfRangeException(nameof(startingSatisfaction), "The starting satisfaction is between the lowest and the highest.");
            }

            RequireAtLeast(satisfactionRise, 0, nameof(satisfactionRise));
            RequireAtLeast(satisfactionFall, 0, nameof(satisfactionFall));
            RequireAtLeast(richTargetDivisor, 1, nameof(richTargetDivisor));
            RequireAtLeast(wealthyTargetDivisor, 1, nameof(wealthyTargetDivisor));
            RequireAtLeast(classRoomFloor, 0, nameof(classRoomFloor));
            RequireAtLeast(poorLeavingDivisor, 1, nameof(poorLeavingDivisor));
            RequireAtLeast(protectedWorkers, 0, nameof(protectedWorkers));

            StartingSatisfaction = startingSatisfaction;
            SatisfactionBases = satisfactionBases;
            SatisfactionRise = satisfactionRise;
            SatisfactionFall = satisfactionFall;
            CommonSatisfaction = commonSatisfaction;
            LowestSatisfaction = lowestSatisfaction;
            HighestSatisfaction = highestSatisfaction;
            ClassTargetOffset = classTargetOffset;
            RichTargetDivisor = richTargetDivisor;
            WealthyTargetDivisor = wealthyTargetDivisor;
            ClassRoomFloor = classRoomFloor;
            PoorLeavingDivisor = poorLeavingDivisor;
            ProtectedWorkers = protectedWorkers;
        }

        public int StartingSatisfaction { get; }

        public PerClass<int> SatisfactionBases { get; }

        public int SatisfactionRise { get; }

        public int SatisfactionFall { get; }

        public int CommonSatisfaction { get; }

        public int LowestSatisfaction { get; }

        public int HighestSatisfaction { get; }

        public int ClassTargetOffset { get; }

        public int RichTargetDivisor { get; }

        public int WealthyTargetDivisor { get; }

        public int ClassRoomFloor { get; }

        public int PoorLeavingDivisor { get; }

        public int ProtectedWorkers { get; }

        /// <summary>
        /// What the stock of one consumed good adds to a class's target: from -weight when it is
        /// empty, through 0 at T0, to +weight at twice T0 and above.
        /// </summary>
        /// <param name="t0">The good's first threshold, above 0.</param>
        public int GoodTerm(int weight, double stock, double t0)
        {
            // The negated comparison also rejects NaN.
            if (!(t0 > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(t0));
            }

            if (stock < t0)
            {
                return -(int)(weight * (t0 - stock) / t0);
            }

            if (stock < 2d * t0)
            {
                return (int)(weight * (stock - t0) / t0);
            }

            return weight;
        }

        /// <summary>The target of a class's satisfaction, from the sum of the goods' terms.</summary>
        public int TargetOf(SocialClass housedClass, int goodsTerm)
        {
            return SatisfactionBases[housedClass] + goodsTerm + CommonSatisfaction;
        }

        /// <summary>The satisfaction after a day: a step towards twice its target, within the bounds.</summary>
        public int NextSatisfaction(int current, int target)
        {
            int twice = 2 * target;

            if (current < twice)
            {
                return Math.Min(current + SatisfactionRise, HighestSatisfaction);
            }

            if (current > twice)
            {
                return Math.Max(current - SatisfactionFall, LowestSatisfaction);
            }

            return current;
        }

        /// <summary>
        /// The rich or the wealthy a city tends to: a share of its poor set by the class's
        /// satisfaction, at least 1, and at most the room of the class, which is never taken under the floor.
        /// </summary>
        public int ClassTargetOf(SocialClass housedClass, int satisfaction, int poor, int room)
        {
            int divisor;

            switch (housedClass)
            {
                case SocialClass.Rich:
                    divisor = RichTargetDivisor;
                    break;
                case SocialClass.Wealthy:
                    divisor = WealthyTargetDivisor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(housedClass), "Only the rich and the wealthy have a target.");
            }

            int target = Math.Max(1, (satisfaction + ClassTargetOffset) * poor / divisor);

            return room < target ? Math.Max(room, ClassRoomFloor) : target;
        }

        /// <summary>
        /// A day of the rich, then the wealthy: a class under its target takes poor, one above it
        /// gives some back; both targets are those of the poor before any move.
        /// </summary>
        public void RiseAndFall(CityPopulation population)
        {
            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            int poor = population.CountOf(SocialClass.Poor);
            int richTarget = TargetFor(population, SocialClass.Rich, poor);
            int wealthyTarget = TargetFor(population, SocialClass.Wealthy, poor);

            Step(population, SocialClass.Rich, richTarget);
            Step(population, SocialClass.Wealthy, wealthyTarget);
        }

        /// <summary>
        /// The poor who want to leave the city today: those beyond their room, a quarter of them
        /// plus one, or a hundredth of all of them when they are unhappy, whichever is more; never the last one.
        /// </summary>
        public int LeavingPoorOf(CityPopulation population)
        {
            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            int poor = population.CountOf(SocialClass.Poor);
            int leaving = population.SatisfactionOf(SocialClass.Poor) < 0 ? poor / PoorLeavingDivisor : 0;
            int excess = poor - population.RoomOf(SocialClass.Poor);

            if (excess > 0)
            {
                leaving = Math.Max(leaving, excess / ExcessPoorDivisor + 1);
            }

            return Math.Max(0, Math.Min(leaving, poor - 1));
        }

        private int TargetFor(CityPopulation population, SocialClass housedClass, int poor)
        {
            return ClassTargetOf(housedClass, population.SatisfactionOf(housedClass), poor, population.RoomOf(housedClass));
        }

        private static void Step(CityPopulation population, SocialClass housedClass, int target)
        {
            int count = population.CountOf(housedClass);

            if (target < count)
            {
                // A target of 0 is a class without room nor floor, which a city never has.
                int moved = 2 * count / Math.Max(1, target) + 1;

                if (moved >= count)
                {
                    moved = count - 1;
                }

                population.Move(housedClass, SocialClass.Poor, moved);
            }
            else
            {
                int moved = count == 0 ? 1 : 2 * target / count + 1;

                if (moved < population.CountOf(SocialClass.Poor))
                {
                    population.Move(SocialClass.Poor, housedClass, moved);
                }
            }
        }

        private static void RequireAtLeast(int value, int minimum, string name)
        {
            if (value < minimum)
            {
                throw new ArgumentOutOfRangeException(name, $"The value is at least {minimum}.");
            }
        }
    }
}
