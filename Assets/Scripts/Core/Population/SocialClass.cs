using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The housed classes of a city, in Patrician III's order; the beggars are apart.</summary>
    public enum SocialClass
    {
        Rich,
        Wealthy,
        Poor,
    }

    /// <summary>One value for each housed class.</summary>
    public readonly struct PerClass<T>
    {
        public PerClass(T rich, T wealthy, T poor)
        {
            Rich = rich;
            Wealthy = wealthy;
            Poor = poor;
        }

        public T Rich { get; }

        public T Wealthy { get; }

        public T Poor { get; }

        public T this[SocialClass housedClass]
        {
            get
            {
                switch (housedClass)
                {
                    case SocialClass.Rich:
                        return Rich;
                    case SocialClass.Wealthy:
                        return Wealthy;
                    case SocialClass.Poor:
                        return Poor;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(housedClass));
                }
            }
        }

        /// <summary>The same value for the three classes.</summary>
        public static PerClass<T> All(T value)
        {
            return new PerClass<T>(value, value, value);
        }
    }
}
