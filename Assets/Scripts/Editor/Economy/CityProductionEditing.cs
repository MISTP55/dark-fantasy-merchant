using DarkFantasyMerchant.Game;
using UnityEditor;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>Writes how a city produces a good into its two lists of goods.</summary>
    public static class CityProductionEditing
    {
        public const string EfficientGoods = "efficientGoods";
        public const string InefficientGoods = "inefficientGoods";

        /// <summary>
        /// Takes the good out of both lists, then adds it to the list of the production, if any. Empty
        /// entries are dropped on the way. The caller applies the serialized object.
        /// </summary>
        public static void SetProduction(SerializedObject city, GoodDefinition good, GoodProduction production)
        {
            SerializedProperty efficient = city.FindProperty(EfficientGoods);
            SerializedProperty inefficient = city.FindProperty(InefficientGoods);

            Remove(efficient, good);
            Remove(inefficient, good);

            if (production == GoodProduction.Efficient)
            {
                Add(efficient, good);
            }
            else if (production == GoodProduction.Inefficient)
            {
                Add(inefficient, good);
            }
        }

        private static void Remove(SerializedProperty list, GoodDefinition good)
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                UnityEngine.Object entry = list.GetArrayElementAtIndex(i).objectReferenceValue;

                if (entry == null || entry == good)
                {
                    // Clears the reference first: deleting a set reference only empties it.
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    list.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static void Add(SerializedProperty list, GoodDefinition good)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = good;
        }
    }
}
