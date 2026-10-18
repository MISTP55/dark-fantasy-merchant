using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What the workshops of a merchant (the player's) make in a city, as in Patrician III:
    /// their workers make the good's merchant output, which lacks the coefficient of the city's
    /// own workshop, and the more workshops of the good the merchant has in the city, the more
    /// each worker makes. A count equal to a step gets its bonus.
    /// </summary>
    public sealed class MerchantOutputRule
    {
        /// <param name="firstStep">Workshops of a good from which the first bonus applies, at least 1.</param>
        /// <param name="firstBonus">Factor of the output from the first step: 1.03 is Patrician III's.</param>
        /// <param name="secondStep">Workshops from which the second bonus applies, above the first step.</param>
        /// <param name="secondBonus">Factor of the output from the second step.</param>
        /// <param name="thirdStep">Workshops from which the third bonus applies, above the second step.</param>
        /// <param name="thirdBonus">Factor of the output from the third step.</param>
        public MerchantOutputRule(
            int firstStep,
            double firstBonus,
            int secondStep,
            double secondBonus,
            int thirdStep,
            double thirdBonus)
        {
            if (firstStep < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(firstStep), "A step is at least one workshop.");
            }

            if (secondStep <= firstStep)
            {
                throw new ArgumentOutOfRangeException(nameof(secondStep), "The steps follow each other.");
            }

            if (thirdStep <= secondStep)
            {
                throw new ArgumentOutOfRangeException(nameof(thirdStep), "The steps follow each other.");
            }

            RequireBonus(firstBonus, nameof(firstBonus));
            RequireBonus(secondBonus, nameof(secondBonus));
            RequireBonus(thirdBonus, nameof(thirdBonus));

            FirstStep = firstStep;
            FirstBonus = firstBonus;
            SecondStep = secondStep;
            SecondBonus = secondBonus;
            ThirdStep = thirdStep;
            ThirdBonus = thirdBonus;
        }

        public int FirstStep { get; }

        public double FirstBonus { get; }

        public int SecondStep { get; }

        public double SecondBonus { get; }

        public int ThirdStep { get; }

        public double ThirdBonus { get; }

        /// <summary>The factor of a worker's output for this many workshops of the good in the city: 1 under the first step.</summary>
        public double BonusFor(int workshops)
        {
            if (workshops < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workshops), "Workshops are not negative.");
            }

            return workshops >= ThirdStep ? ThirdBonus
                : workshops >= SecondStep ? SecondBonus
                : workshops >= FirstStep ? FirstBonus
                : 1d;
        }

        /// <summary>Units the merchant's workshops of a good make in a day.</summary>
        /// <param name="workers">Workers of all the merchant's workshops of the good in the city.</param>
        /// <param name="outputPerWorker">Units a worker makes in a day without the bonus, the city's efficiency included.</param>
        /// <param name="workshops">The merchant's workshops of the good in the city.</param>
        public double DailyOutputOf(int workers, double outputPerWorker, int workshops)
        {
            if (workers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workers), "Workers are not negative.");
            }

            // The negated comparison also rejects NaN.
            if (!(outputPerWorker >= 0d) || double.IsInfinity(outputPerWorker))
            {
                throw new ArgumentOutOfRangeException(nameof(outputPerWorker));
            }

            return workers * outputPerWorker * BonusFor(workshops);
        }

        private static void RequireBonus(double value, string name)
        {
            // The negated comparison also rejects NaN.
            if (!(value >= 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "A bonus is a factor, 0 or more.");
            }
        }
    }
}
