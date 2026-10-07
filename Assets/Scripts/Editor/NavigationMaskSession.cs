using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
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
        private Color32[] previewPixels;
        private bool previewIsStale;
        private bool hasUncommittedChanges;

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

        public void Paint(Vector2 from, Vector2 to, float radiusInCells, bool navigable)
        {
            if (Grid != null && Grid.PaintStroke(from, to, radiusInCells, navigable))
            {
                MarkChanged();
            }
        }

        public void Fill(Vector2 point, bool navigable)
        {
            if (Grid != null && Grid.Fill(point, navigable))
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

                previewPixels = new Color32[Grid.Width * Grid.Height];
                previewIsStale = true;
            }

            if (previewIsStale)
            {
                for (int y = 0; y < Grid.Height; y++)
                {
                    int row = y * Grid.Width;

                    for (int x = 0; x < Grid.Width; x++)
                    {
                        previewPixels[row + x] = Grid.IsNavigable(x, y) ? NavigableColor : EmptyColor;
                    }
                }

                preview.SetPixels32(previewPixels);
                preview.Apply(false);
                previewIsStale = false;
            }

            return preview;
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
            previewPixels = null;
        }
    }
}
