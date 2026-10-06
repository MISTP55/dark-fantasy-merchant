using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Turns the cursor and clicks into hover and selection of cities.</summary>
    public sealed class WorldMapInteraction : MonoBehaviour
    {
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;
        [SerializeField] private WorldMapCameraController cameraController;

        [Tooltip("How close to a city the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float pickRadiusPixels = 20f;

        private bool isBound;

        private void Start()
        {
            if (mapView == null || input == null || cameraController == null)
            {
                Debug.LogError("WorldMapInteraction needs a map view, an input component and a camera controller.", this);
                enabled = false;
                return;
            }

            input.Clicked += OnClicked;
            input.Cancelled += OnCancelled;
            isBound = true;
        }

        private void OnDestroy()
        {
            if (isBound && input != null)
            {
                input.Clicked -= OnClicked;
                input.Cancelled -= OnCancelled;
            }
        }

        private void Update()
        {
            if (!CanPick())
            {
                return;
            }

            bool pointerIsBusy = input.IsPointerOverUi || input.IsDragging;
            mapView.Selection.SetHovered(pointerIsBusy ? null : Pick(input.PointerPosition));
        }

        private void LateUpdate()
        {
            if (CanPick())
            {
                mapView.SetMarkerScale(cameraController.WorldUnitsPerPixel);
            }
        }

        private void OnClicked(Vector2 screenPosition)
        {
            if (!CanPick())
            {
                return;
            }

            CityDefinition city = Pick(screenPosition);

            if (city != null)
            {
                mapView.Selection.Select(city);
            }
            else
            {
                mapView.Selection.ClearSelection();
            }
        }

        private void OnCancelled()
        {
            mapView.Selection.ClearSelection();
        }

        // Screen height is zero while the window is minimized.
        private bool CanPick()
        {
            return mapView.IsReady && cameraController.IsReady && Screen.height > 0;
        }

        private CityDefinition Pick(Vector2 screenPosition)
        {
            Vector2 worldPosition = cameraController.ScreenToWorld(screenPosition);
            float worldRadius = pickRadiusPixels * cameraController.WorldUnitsPerPixel;
            int index = CityPicker.PickNearest(mapView.CityWorldPositions, worldPosition, worldRadius);

            return index >= 0 ? mapView.Cities[index] : null;
        }
    }
}
