using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GroundMeshTests
    {
        private static readonly Color Free = new Color(0.25f, 0.3f, 0.2f, 1f);
        private static readonly Color Road = new Color(0.55f, 0.5f, 0.4f, 1f);

        private Texture2D texture;
        private Sprite cellSprite;
        private readonly List<GroundMesh> grounds = new List<GroundMesh>();

        [SetUp]
        public void SetUp()
        {
            // A cell's diamond, 64 by 32 pixels at 64 per unit, on the right half of a texture shared with another.
            texture = new Texture2D(128, 32);
            cellSprite = Sprite.Create(texture, new Rect(64, 0, 64, 32), new Vector2(0.5f, 0.5f), 64f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GroundMesh ground in grounds)
            {
                ground.Dispose();
            }

            grounds.Clear();
            Object.DestroyImmediate(cellSprite);
            Object.DestroyImmediate(texture);
        }

        private GroundMesh Ground(int width, int height)
        {
            var ground = new GroundMesh(width, height, cellSprite, Free);
            grounds.Add(ground);
            return ground;
        }

        // The vertices of a cell, in the mesh's order: a cell's quad is 4 of them, by x then y.
        private static Vector3[] QuadOf(Mesh mesh, int width, Vector2Int cell)
        {
            var quad = new Vector3[4];
            Array.Copy(mesh.vertices, (cell.y * width + cell.x) * 4, quad, 0, 4);
            return quad;
        }

        [Test]
        public void EveryCell_IsAQuadOfTheSpriteAroundTheCellsCentre()
        {
            GroundMesh ground = Ground(3, 2);
            Mesh mesh = ground.Mesh;

            Assert.AreEqual(6 * 4, mesh.vertexCount);
            Assert.AreEqual(6 * 6, mesh.triangles.Length);

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Vector3[] quad = QuadOf(mesh, 3, cell);
                    Vector3 center = IsoProjection.CenterOf(cell);
                    var min = new Vector3(float.MaxValue, float.MaxValue);
                    var max = new Vector3(float.MinValue, float.MinValue);

                    foreach (Vector3 vertex in quad)
                    {
                        min = Vector3.Min(min, vertex);
                        max = Vector3.Max(max, vertex);
                    }

                    // The diamond's width and height, centred on the cell.
                    Assert.AreEqual(center.x - IsoProjection.CellWidth * 0.5f, min.x, 1e-5f);
                    Assert.AreEqual(center.x + IsoProjection.CellWidth * 0.5f, max.x, 1e-5f);
                    Assert.AreEqual(center.y - IsoProjection.CellHeight * 0.5f, min.y, 1e-5f);
                    Assert.AreEqual(center.y + IsoProjection.CellHeight * 0.5f, max.y, 1e-5f);
                }
            }
        }

        [Test]
        public void TheQuads_FaceTheCamera()
        {
            Mesh mesh = Ground(1, 1).Mesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // Clockwise seen from the camera, which looks along +z: the side Unity draws.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 normal = Vector3.Cross(
                    vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                Assert.Less(normal.z, 0f);
            }
        }

        [Test]
        public void TheUVs_CoverTheSpritesRectangleOnItsTexture()
        {
            Vector2[] uv = Ground(1, 1).Mesh.uv;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (Vector2 point in uv)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            Assert.AreEqual(new Vector2(0.5f, 0f), min);
            Assert.AreEqual(new Vector2(1f, 1f), max);
        }

        [Test]
        public void EveryCell_TakesTheColourGiven()
        {
            GroundMesh ground = Ground(2, 2);

            foreach (Color color in ground.Mesh.colors)
            {
                Assert.AreEqual(GroundMesh.VertexColorOf(Free), color);
            }

            Assert.AreEqual(Free, ground.ColorOf(new Vector2Int(1, 1)));
        }

        [Test]
        public void AQuad_IsTheSpritesRectangleAroundItsPivot_WhereverThePivotIs()
        {
            // The pivot a quarter of the way across: three quarters of the 1-unit width lie to its right.
            Sprite offCentre = Sprite.Create(texture, new Rect(64, 0, 64, 32), new Vector2(0.25f, 0.5f), 64f);

            try
            {
                var ground = new GroundMesh(1, 1, offCentre, Free);
                grounds.Add(ground);
                Vector3[] quad = QuadOf(ground.Mesh, 1, Vector2Int.zero);
                float minX = float.MaxValue;
                float maxX = float.MinValue;

                foreach (Vector3 vertex in quad)
                {
                    minX = Mathf.Min(minX, vertex.x);
                    maxX = Mathf.Max(maxX, vertex.x);
                }

                Assert.AreEqual(-0.25f, minX, 1e-5f);
                Assert.AreEqual(0.75f, maxX, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(offCentre);
            }
        }

        [Test]
        public void SetColor_TintsOnlyThatCell_AndTheMeshShowsItAtApply()
        {
            GroundMesh ground = Ground(3, 2);
            var cell = new Vector2Int(2, 1);

            ground.SetColor(cell, Road);

            Assert.AreEqual(Road, ground.ColorOf(cell));
            Assert.AreEqual(GroundMesh.VertexColorOf(Free), ground.Mesh.colors[(1 * 3 + 2) * 4]);

            ground.Apply();

            Color[] colors = ground.Mesh.colors;

            for (int i = 0; i < colors.Length; i++)
            {
                Assert.AreEqual(GroundMesh.VertexColorOf(i / 4 == 1 * 3 + 2 ? Road : Free), colors[i], $"vertex {i}");
            }
        }

        [Test]
        public void AGroundOfMoreThan65535Vertices_HasIndicesThatReachThemAll()
        {
            Assert.AreEqual(IndexFormat.UInt16, Ground(80, 80).Mesh.indexFormat);

            Mesh large = Ground(130, 130).Mesh;

            Assert.AreEqual(IndexFormat.UInt32, large.indexFormat);
            Assert.AreEqual(130 * 130 * 4, large.vertexCount);
        }

        [Test]
        public void ACellOffTheGround_Throws()
        {
            GroundMesh ground = Ground(2, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => ground.SetColor(new Vector2Int(2, 0), Road));
            Assert.Throws<ArgumentOutOfRangeException>(() => ground.ColorOf(new Vector2Int(0, -1)));
        }
    }
}
