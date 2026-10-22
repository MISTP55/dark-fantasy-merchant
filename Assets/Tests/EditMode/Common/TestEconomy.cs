using DarkFantasyMerchant.Core;
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
            double basePrice,
            double weeklyConsumptionPerThousand,
            GoodCategory category = GoodCategory.Food,
            int maxWorkers = 60,
            double dailyOutputPerWorker = 0.1,
            GoodUnit unit = GoodUnit.Barrel,
            int aiWeight = 9,
            double merchantDailyOutputPerWorker = 0.1)
        {
            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            good.name = displayName;

            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("basePrice").doubleValue = basePrice;
            serialized.FindProperty("richWeeklyConsumptionPerThousand").doubleValue = weeklyConsumptionPerThousand;
            serialized.FindProperty("wealthyWeeklyConsumptionPerThousand").doubleValue = weeklyConsumptionPerThousand;
            serialized.FindProperty("poorWeeklyConsumptionPerThousand").doubleValue = weeklyConsumptionPerThousand;
            serialized.FindProperty("maxWorkers").intValue = maxWorkers;
            serialized.FindProperty("dailyOutputPerWorker").doubleValue = dailyOutputPerWorker;
            serialized.FindProperty("merchantDailyOutputPerWorker").doubleValue = merchantDailyOutputPerWorker;
            serialized.FindProperty("unit").enumValueIndex = (int)unit;
            serialized.FindProperty("aiWeight").intValue = aiWeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return good;
        }

        /// <summary>Gives a good a weekly consumption per thousand and a satisfaction weight for each class.</summary>
        public static void SetConsumption(GoodDefinition good, PerClass<double> weekly, PerClass<int> weights)
        {
            var serialized = new SerializedObject(good);
            serialized.FindProperty("richWeeklyConsumptionPerThousand").doubleValue = weekly.Rich;
            serialized.FindProperty("wealthyWeeklyConsumptionPerThousand").doubleValue = weekly.Wealthy;
            serialized.FindProperty("poorWeeklyConsumptionPerThousand").doubleValue = weekly.Poor;
            serialized.FindProperty("richSatisfactionWeight").intValue = weights.Rich;
            serialized.FindProperty("wealthySatisfactionWeight").intValue = weights.Wealthy;
            serialized.FindProperty("poorSatisfactionWeight").intValue = weights.Poor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static CityDefinition CreateCity(GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            var serialized = new SerializedObject(city);
            SetGoods(serialized.FindProperty("efficientGoods"), efficient);
            SetGoods(serialized.FindProperty("inefficientGoods"), inefficient);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }

        /// <summary>An economy of these goods, all active, whose cities start with 6 000 inhabitants.</summary>
        public static EconomyDefinition CreateEconomy(params GoodDefinition[] goods)
        {
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();

            var serialized = new SerializedObject(economy);
            SerializedProperty catalogue = serialized.FindProperty("goods");
            catalogue.arraySize = goods.Length;

            for (int i = 0; i < goods.Length; i++)
            {
                SerializedProperty entry = catalogue.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("good").objectReferenceValue = goods[i];
                entry.FindPropertyRelative("active").boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetStartingPopulation(economy, 6000);
            return economy;
        }

        /// <summary>Switches on or off every entry of the catalogue that holds this good.</summary>
        public static void SetActive(EconomyDefinition economy, GoodDefinition good, bool active)
        {
            var serialized = new SerializedObject(economy);
            SerializedProperty catalogue = serialized.FindProperty("goods");

            for (int i = 0; i < catalogue.arraySize; i++)
            {
                SerializedProperty entry = catalogue.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative("good").objectReferenceValue == good)
                {
                    entry.FindPropertyRelative("active").boolValue = active;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gives the city's workshop of a good these inputs, in this order, in units per unit it makes.</summary>
        public static void SetInputs(GoodDefinition good, params (GoodDefinition good, double unitsPerUnit)[] inputs)
        {
            var serialized = new SerializedObject(good);
            SerializedProperty list = serialized.FindProperty("inputs");
            list.arraySize = inputs.Length;

            for (int i = 0; i < inputs.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("good").objectReferenceValue = inputs[i].good;
                entry.FindPropertyRelative("unitsPerUnit").doubleValue = inputs[i].unitsPerUnit;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Makes every city of the economy start with these inhabitants: as many workers, one
        /// inhabitant each. The workshops then start full.
        /// </summary>
        public static void SetStartingPopulation(EconomyDefinition economy, int inhabitants)
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = inhabitants;
            serialized.FindProperty("inhabitantsPerWorker").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
