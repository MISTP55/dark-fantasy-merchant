using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipDefinitionTests
    {
        private Texture2D texture;
        private Sprite[] sprites;
        private ShipDefinition definition;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(4, 4);
            sprites = new Sprite[ShipDefinition.DirectionCount];

            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero, 4f);
                sprites[i].name = ((CompassDirection)i).ToString();
            }

            definition = ScriptableObject.CreateInstance<ShipDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);

            foreach (Sprite sprite in sprites)
            {
                Object.DestroyImmediate(sprite);
            }

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void NewDefinition_HasAPositiveSpeed()
        {
            Assert.Greater(definition.Speed, 0f);
        }

        [Test]
        public void NewDefinition_HasRoomForACrew()
        {
            Assert.Greater(definition.CrewCapacity, 0);
        }

        [Test]
        public void SpriteFor_ReturnsTheSpriteOfEachDirection()
        {
            SetSprites(sprites.Length);

            for (int i = 0; i < ShipDefinition.DirectionCount; i++)
            {
                Assert.AreSame(sprites[i], definition.SpriteFor((CompassDirection)i));
            }
        }

        [Test]
        public void SpriteFor_ReturnsNull_WhenTheArrayIsTooShort()
        {
            SetSprites(3);

            Assert.AreSame(sprites[2], definition.SpriteFor(CompassDirection.E));
            Assert.IsNull(definition.SpriteFor(CompassDirection.SE));
            Assert.IsNull(definition.SpriteFor(CompassDirection.NW));
        }

        [Test]
        public void SpriteFor_ReturnsNull_ForAnEmptySlot()
        {
            SetSprites(sprites.Length);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("directionSprites").GetArrayElementAtIndex(4).objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(definition.SpriteFor(CompassDirection.S));
        }

        [Test]
        public void SpriteFor_ReturnsNull_ForAValueOutsideTheEnum()
        {
            SetSprites(sprites.Length);

            Assert.IsNull(definition.SpriteFor((CompassDirection)8));
            Assert.IsNull(definition.SpriteFor((CompassDirection)(-1)));
        }

        private void SetSprites(int count)
        {
            var serialized = new SerializedObject(definition);
            SerializedProperty property = serialized.FindProperty("directionSprites");
            property.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ANewDefinition_HoldsTwoHundredBarrels()
        {
            var fresh = UnityEngine.ScriptableObject.CreateInstance<ShipDefinition>();

            Assert.AreEqual(200, fresh.CargoCapacity);

            UnityEngine.Object.DestroyImmediate(fresh);
        }
    }
}
