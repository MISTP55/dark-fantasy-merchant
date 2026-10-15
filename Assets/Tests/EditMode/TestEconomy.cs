using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>In-memory goods, cities and economies. The caller destroys what it creates.</summary>
    public static class TestEconomy
    {
        /// <summary>A good priced from its base price by the economy's curves.</summary>
        public static GoodDefinition CreateGood(
            string displayName,
            long basePrice,
            double weeklyConsumptionPerThousand,
            GoodCategory category = GoodCategory.Food,
            int maxWorkers = 60,
            double dailyOutputPerWorker = 0.1)
        {
            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            good.name = displayName;

            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("basePrice").longValue = basePrice;
            serialized.FindProperty("weeklyConsumptionPerThousand").doubleValue = weeklyConsumptionPerThousand;
            serialized.FindProperty("maxWorkers").intValue = maxWorkers;
            serialized.FindProperty("dailyOutputPerWorker").doubleValue = dailyOutputPerWorker;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return good;
        }

        public static CityDefinition CreateCity(int population, GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            var serialized = new SerializedObject(city);
            serialized.FindProperty("population").intValue = population;
            SetGoods(serialized.FindProperty("efficientGoods"), efficient);
            SetGoods(serialized.FindProperty("inefficientGoods"), inefficient);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }

        public static EconomyDefinition CreateEconomy(params GoodDefinition[] goods)
        {
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();

            var serialized = new SerializedObject(economy);
            SetGoods(serialized.FindProperty("goods"), goods);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return economy;
        }

        private static void SetGoods(SerializedProperty list, GoodDefinition[] goods)
        {
            list.arraySize = goods.Length;

            for (int i = 0; i < goods.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = goods[i];
            }
        }
    }
}
