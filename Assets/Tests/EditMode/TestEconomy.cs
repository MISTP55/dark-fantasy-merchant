using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>In-memory goods, cities and economies. The caller destroys what it creates.</summary>
    public static class TestEconomy
    {
        public static GoodDefinition CreateGood(string displayName, long basePrice, double consumptionPerThousand)
        {
            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            good.name = displayName;

            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("basePrice").longValue = basePrice;
            serialized.FindProperty("consumptionPerThousand").doubleValue = consumptionPerThousand;
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
