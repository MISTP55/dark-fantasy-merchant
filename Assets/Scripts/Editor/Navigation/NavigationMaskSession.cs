using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The mask being edited: a working copy of its grid, painted on stroke by stroke and
    /// written back to the asset when a stroke ends, and a texture that shows it.
    /// </summary>
    public sealed class NavigationMaskSession : IDisposable
    {
        private static readonly Color32 NavigableColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 EmptyColor = new Color32(0, 0, 0, 0);

        private Texture2D preview;
        private bool previewIsStale;
        private bool hasUncommittedChanges;

        // Cells a stroke may have changed since the preview was last written. Rewriting
        // only these keeps painting smooth on a large grid.
        private bool hasStaleCells;
        private int staleMinX;
        private int staleMinY;
        private int staleMaxX;
        private int staleMaxY;

        public WorldMapDefinition Map { get; private set; }

        public NavigationMaskDefinition Mask { get; private set; }

        /// <summary>Working copy of the mask's grid; null when the map has no mask.</summary>
        public NavigationGrid Grid { get; private set; }

        public bool HasMask => Grid != null;

        public bool HasNavigableCells
        {
            get
            {
                if (Grid == null)
                {
                    return false;
                }

                // Unused bits are always zero, so any set bit is a navigable cell.
                foreach (byte packed in Grid.ToBytes())
                {
                    if (packed != 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Points the session at a map. Reloads the working copy only when the map or its
        /// mask changed, so it can be called on every Scene view event.
        /// </summary>
        public void SetMap(WorldMapDefinition map)
        {
            NavigationMaskDefinition mask = map != null ? map.NavigationMask : null;

            // The last test catches a mask asset deleted while it was being edited.
            if (map == Map && mask == Mask && (mask != null) == (Grid != null))
            {
                return;
            }

            Map = map;
            Mask = mask;
            Reload();
        }

        /// <summary>Rebuilds the working copy from the asset, dropping unwritten changes.</summary>
        public void Reload()
        {
            Grid = Mask != null ? Mask.CreateGrid() : null;
            hasUncommittedChanges = false;
            previewIsStale = true;
        }

        /// <summary>
        /// Reloads the working copy when the asset no longer holds the same grid: it was
        /// changed by something other than this session, such as a version-control
        /// checkout. Editing a stale copy would write it over the newer asset.
        /// </summary>
        public void SyncWithAsset()
        {
            // With unwritten changes the working copy is ahead of the asset on purpose.
            if (hasUncommittedChanges || Grid == null || Mask == null)
            {
                return;
            }

            NavigationGrid stored = Mask.CreateGrid();

            if (stored.Width == Grid.Width
                && stored.Height == Grid.Height
                && HaveSameContent(stored.ToBytes(), Grid.ToBytes()))
            {
                return;
            }

            Grid = stored;
            previewIsStale = true;
        }

        public void Paint(Vector2 from, Vector2 to, float radiusInCells, bool navigable)
        {
            if (Grid == null)
            {
                return;
            }

            SyncWithAsset();

            if (Grid.PaintStroke(from, to, radiusInCells, navigable))
            {
                hasUncommittedChanges = true;
                MarkCellsStale(from, to, radiusInCells);
            }
        }

        public void Fill(Vector2 point, bool navigable)
        {
            if (Grid == null)
            {
                return;
            }

            SyncWithAsset();

            if (Grid.Fill(point, navigable))
            {
                MarkChanged();
            }
        }

        /// <summary>Writes the working copy to the asset as one undo step.</summary>
        /// <returns>False when there was nothing to write.</returns>
        public bool Commit(string undoName)
        {
            if (!hasUncommittedChanges || Grid == null || Mask == null)
            {
                return false;
            }

            NavigationMaskAuthoring.Apply(Mask, Grid, undoName);
            hasUncommittedChanges = false;
            return true;
        }

        /// <summary>Replaces the whole grid and writes it to the asset at once.</summary>
        public void Replace(NavigationGrid grid, string undoName)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (Mask == null)
            {
                return;
            }

            Grid = grid;
            MarkChanged();
            Commit(undoName);
        }

        /// <summary>
        /// A texture of the grid's size, opaque white where a cell is navigable and
        /// transparent elsewhere, to be tinted when drawn. Null when there is no mask.
        /// </summary>
        public Texture2D GetPreview()
        {
            if (Grid == null)
            {
                return null;
            }

            if (preview == null || preview.width != Grid.Width || preview.height != Grid.Height)
            {
                DestroyPreview();

                preview = new Texture2D(Grid.Width, Grid.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                previewIsStale = true;
            }

            if (previewIsStale)
            {
                WritePreview(0, 0, Grid.Width - 1, Grid.Height - 1);
            }
            else if (hasStaleCells)
            {
                WritePreview(staleMinX, staleMinY, staleMaxX, staleMaxY);
            }

            previewIsStale = false;
            hasStaleCells = false;
            return preview;
        }

        private void WritePreview(int minX, int minY, int maxX, int maxY)
        {
            // Written in place: no copy of the whole texture per stroke.
            NativeArray<Color32> pixels = preview.GetPixelData<Color32>(0);

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * Grid.Width;

                for (int x = minX; x <= maxX; x++)
                {
                    pixels[row + x] = Grid.IsNavigable(x, y) ? NavigableColor : EmptyColor;
                }
            }

            preview.Apply(false);
        }

        /// <summary>Adds the cells a stroke can have touched to those the preview must rewrite.</summary>
        private void MarkCellsStale(Vector2 from, Vector2 to, float radiusInCells)
        {
            float reach = Mathf.Max(radiusInCells, 0f) + 1f;
            int minX = ClampToCell(Mathf.Min(from.x, to.x) * Grid.Width - reach, Grid.Width);
            int maxX = ClampToCell(Mathf.Max(from.x, to.x) * Grid.Width + reach, Grid.Width);
            int minY = ClampToCell(Mathf.Min(from.y, to.y) * Grid.Height - reach, Grid.Height);
            int maxY = ClampToCell(Mathf.Max(from.y, to.y) * Grid.Height + reach, Grid.Height);

            if (hasStaleCells)
            {
                minX = Mathf.Min(minX, staleMinX);
                maxX = Mathf.Max(maxX, staleMaxX);
                minY = Mathf.Min(minY, staleMinY);
                maxY = Mathf.Max(maxY, staleMaxY);
            }

            hasStaleCells = true;
            staleMinX = minX;
            staleMaxX = maxX;
            staleMinY = minY;
            staleMaxY = maxY;
        }

        // Clamped before the conversion: casting a float beyond the int range is undefined.
        private static int ClampToCell(float value, int size)
        {
            return (int)Mathf.Floor(Mathf.Clamp(value, 0f, size - 1));
        }

        private static bool HaveSameContent(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            DestroyPreview();
        }

        private void MarkChanged()
        {
            hasUncommittedChanges = true;
            previewIsStale = true;
        }

        private void DestroyPreview()
        {
            if (preview != null)
            {
                Object.DestroyImmediate(preview);
            }

            preview = null;
        }
    }
}
