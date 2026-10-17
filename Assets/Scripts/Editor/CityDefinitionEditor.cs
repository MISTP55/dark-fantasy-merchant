using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The city's fields, then every good of the economy's catalogue with how the city produces it:
    /// not at all, efficiently or inefficiently. The two lists of goods are written behind the buttons.
    /// </summary>
    [CustomEditor(typeof(CityDefinition))]
    [CanEditMultipleObjects]
    public sealed class CityDefinitionEditor : UnityEditor.Editor
    {
        private static readonly (GoodProduction Production, string Label)[] Choices =
        {
            (GoodProduction.None, "Not produced"),
            (GoodProduction.Efficient, "Efficient"),
            (GoodProduction.Inefficient, "Inefficient"),
        };

        /// <summary>The width of a column of choices, which holds its header ("Not produced" is the longest).</summary>
        private const float ChoiceWidth = 90f;

        private EconomyDefinition economy;

        private void OnEnable()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(EconomyDefinition));

            if (guids.Length > 0)
            {
                economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            if (economy == null)
            {
                // Without a catalogue, there is nothing to list the goods from: the lists are shown as they are.
                DrawPropertiesExcluding(serializedObject, "m_Script");
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("No economy found: the goods are listed as they are stored.", MessageType.Info);
            }
            else
            {
                DrawPropertiesExcluding(
                    serializedObject,
                    "m_Script",
                    CityProductionEditing.EfficientGoods,
                    CityProductionEditing.InefficientGoods);
                serializedObject.ApplyModifiedProperties();
                DrawProduction();
            }

            foreach (Object inspected in targets)
            {
                var city = (CityDefinition)inspected;

                if (!MapProjection.IsInsideMap(city.MapPosition))
                {
                    EditorGUILayout.HelpBox(
                        $"'{city.name}': map position is outside the map (expected 0 to 1 on both axes).",
                        MessageType.Warning);
                }
            }
        }

        private void DrawProduction()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Production", EditorStyles.boldLabel);

            // The choices head their columns: the rows only hold the buttons.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(" ");

                foreach ((GoodProduction _, string choiceLabel) in Choices)
                {
                    EditorGUILayout.LabelField(choiceLabel, GUILayout.Width(ChoiceWidth));
                }
            }

            foreach (CatalogueGood entry in economy.Catalogue)
            {
                if (entry.Good != null)
                {
                    DrawGood(entry.Good, entry.Active ? null : "(inactive)");
                }
            }

            // A good of the lists that the catalogue does not hold stays visible, to be taken out.
            foreach (GoodDefinition good in GoodsOutsideTheCatalogue())
            {
                DrawGood(good, "(not in the catalogue)");
            }
        }

        private void DrawGood(GoodDefinition good, string note)
        {
            GoodProduction? current = ProductionOfTheTargets(good);
            using (new EditorGUILayout.HorizontalScope())
            {
                // The asset's name: the display name is the game's, in French.
                using (new EditorGUI.DisabledScope(note != null))
                {
                    EditorGUILayout.PrefixLabel(note == null ? good.name : $"{good.name} {note}");
                }

                foreach ((GoodProduction production, string choiceLabel) in Choices)
                {
                    bool selected = current == production;

                    // A toggle turned on is a click on a choice that was not the city's (or not all the cities').
                    var content = new GUIContent(string.Empty, choiceLabel);

                    if (GUILayout.Toggle(selected, content, EditorStyles.radioButton, GUILayout.Width(ChoiceWidth))
                        && !selected)
                    {
                        SetProductionOfTheTargets(good, production);
                    }
                }
            }
        }

        /// <summary>How the inspected cities produce the good, or null when they do not all produce it the same way.</summary>
        private GoodProduction? ProductionOfTheTargets(GoodDefinition good)
        {
            GoodProduction? production = null;

            foreach (Object inspected in targets)
            {
                GoodProduction ofThisCity = ((CityDefinition)inspected).ProductionOf(good);

                if (production.HasValue && production != ofThisCity)
                {
                    return null;
                }

                production = ofThisCity;
            }

            return production;
        }

        private void SetProductionOfTheTargets(GoodDefinition good, GoodProduction production)
        {
            foreach (Object inspected in targets)
            {
                var city = new SerializedObject(inspected);
                CityProductionEditing.SetProduction(city, good, production);
                city.ApplyModifiedProperties();
            }

            serializedObject.Update();
        }

        private List<GoodDefinition> GoodsOutsideTheCatalogue()
        {
            var outside = new List<GoodDefinition>();

            foreach (Object inspected in targets)
            {
                var city = (CityDefinition)inspected;
                AddOutsideTheCatalogue(city.EfficientGoods, outside);
                AddOutsideTheCatalogue(city.InefficientGoods, outside);
            }

            return outside;
        }

        private void AddOutsideTheCatalogue(IReadOnlyList<GoodDefinition> goods, List<GoodDefinition> outside)
        {
            foreach (GoodDefinition good in goods)
            {
                if (good != null && !economy.IsInCatalogue(good) && !outside.Contains(good))
                {
                    outside.Add(good);
                }
            }
        }
    }
}
