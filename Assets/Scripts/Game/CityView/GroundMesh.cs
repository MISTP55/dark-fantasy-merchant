using System;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A grid's ground in one mesh: a quad per cell, the cell sprite's rectangle around the cell's centre
    /// (<see cref="IsoProjection.CenterOf(Vector2Int)"/>), textured with the sprite and tinted by its vertices' colour,
    /// which shows a cell's colour as a sprite renderer of that colour would.
    /// One renderer instead of one per cell: a sprite per cell cost every frame to cull, sort and batch thousands of
    /// renderers when the whole grid is in view. A cell's colour is uploaded at <see cref="Apply"/>, once for all the
    /// cells changed since.
    /// </summary>
    public sealed class GroundMesh : IDisposable
    {
        private const int VerticesPerCell = 4;

        private readonly int width;
        private readonly int height;
        // The cells' colours as given, and their vertices' as uploaded.
        private readonly Color[] cellColors;
        private readonly Color[] vertexColors;
        private bool colorsChanged;

        /// <param name="cellSprite">Drawn on every cell, its pivot at the cell's centre; rectangle-packed, not rotated, if in an atlas.</param>
        /// <param name="color">The colour of every cell until <see cref="SetColor"/>.</param>
        public GroundMesh(int width, int height, Sprite cellSprite, Color color)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "A ground has at least one cell.");
            }

            if (cellSprite == null)
            {
                throw new ArgumentNullException(nameof(cellSprite));
            }

            this.width = width;
            this.height = height;

            int cellCount = width * height;
            var vertices = new Vector3[cellCount * VerticesPerCell];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[cellCount * 6];
            cellColors = new Color[cellCount];
            vertexColors = new Color[vertices.Length];

            // The sprite's whole rectangle around its pivot, whatever mesh its importer gives it, and the same
            // rectangle on its texture, alone on it or packed with others (rectangle-packed, not rotated).
            float pixelsPerUnit = cellSprite.pixelsPerUnit;
            var bounds = new Bounds();
            bounds.SetMinMax(-cellSprite.pivot / pixelsPerUnit, (cellSprite.rect.size - cellSprite.pivot) / pixelsPerUnit);
            Rect textureRect = cellSprite.textureRect;
            var textureSize = new Vector2(cellSprite.texture.width, cellSprite.texture.height);
            Vector2 uvMin = textureRect.min / textureSize;
            Vector2 uvMax = textureRect.max / textureSize;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int first = VertexOf(new Vector2Int(x, y));
                    Vector3 center = IsoProjection.CenterOf(new Vector2Int(x, y));

                    vertices[first] = center + new Vector3(bounds.min.x, bounds.min.y);
                    vertices[first + 1] = center + new Vector3(bounds.min.x, bounds.max.y);
                    vertices[first + 2] = center + new Vector3(bounds.max.x, bounds.max.y);
                    vertices[first + 3] = center + new Vector3(bounds.max.x, bounds.min.y);
                    uv[first] = uvMin;
                    uv[first + 1] = new Vector2(uvMin.x, uvMax.y);
                    uv[first + 2] = uvMax;
                    uv[first + 3] = new Vector2(uvMax.x, uvMin.y);

                    int triangle = first / VerticesPerCell * 6;
                    triangles[triangle] = first;
                    triangles[triangle + 1] = first + 1;
                    triangles[triangle + 2] = first + 2;
                    triangles[triangle + 3] = first;
                    triangles[triangle + 4] = first + 2;
                    triangles[triangle + 5] = first + 3;
                }
            }

            Color vertexColor = VertexColorOf(color);

            for (int i = 0; i < cellCount; i++)
            {
                cellColors[i] = color;
            }

            for (int i = 0; i < vertexColors.Length; i++)
            {
                vertexColors[i] = vertexColor;
            }

            Mesh = new Mesh { name = "Ground" };

            // Past 65 535 vertices, a grid of more than 16 383 cells, 16-bit indices no longer reach them all.
            if (vertices.Length > ushort.MaxValue)
            {
                Mesh.indexFormat = IndexFormat.UInt32;
            }

            Mesh.vertices = vertices;
            Mesh.uv = uv;
            Mesh.colors = vertexColors;
            Mesh.triangles = triangles;
            Mesh.RecalculateBounds();
        }

        public Mesh Mesh { get; }

        /// <summary>
        /// The colour a vertex is given to show this colour. A sprite renderer's colour is converted to linear when the
        /// project renders in linear space; a mesh's vertex colours are not, and would show too light.
        /// </summary>
        public static Color VertexColorOf(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }

        /// <summary>The colour of a cell as given, as it will be drawn after the next <see cref="Apply"/>.</summary>
        public Color ColorOf(Vector2Int cell)
        {
            return cellColors[VertexOf(cell) / VerticesPerCell];
        }

        /// <summary>Tints a cell as a sprite renderer of this colour would; the mesh shows it at the next <see cref="Apply"/>.</summary>
        public void SetColor(Vector2Int cell, Color color)
        {
            int first = VertexOf(cell);
            Color vertexColor = VertexColorOf(color);
            cellColors[first / VerticesPerCell] = color;

            for (int i = first; i < first + VerticesPerCell; i++)
            {
                vertexColors[i] = vertexColor;
            }

            colorsChanged = true;
        }

        /// <summary>Uploads the colours changed since the last call, if any.</summary>
        public void Apply()
        {
            if (!colorsChanged)
            {
                return;
            }

            Mesh.colors = vertexColors;
            colorsChanged = false;
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(Mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(Mesh);
            }
        }

        private int VertexOf(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is off a {width} by {height} ground.");
            }

            return (cell.y * width + cell.x) * VerticesPerCell;
        }
    }
}
