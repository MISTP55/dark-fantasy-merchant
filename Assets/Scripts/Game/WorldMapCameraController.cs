using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Feeds input to a <see cref="MapCameraModel"/> and applies its state to the camera.
    /// Holds no clamping or zoom math of its own.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class WorldMapCameraController : MonoBehaviour
    {
        private const float FallbackAspect = 16f / 9f;

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;

        [Tooltip("Orthographic size multiplier for one scroll step towards the map.")]
        [SerializeField, Range(0.5f, 0.99f)] private float zoomStepFactor = 0.85f;

        [Tooltip("Keyboard pan speed, in visible screen heights per second.")]
        [SerializeField, Min(0f)] private float keyboardPanSpeed = 0.75f;

        private Camera mapCamera;
        private MapCameraModel model;
        private float appliedAspect;

        public bool IsReady => model != null;

        public float WorldUnitsPerPixel => model.WorldUnitsPerPixel(Screen.height);

        private void Awake()
        {
            mapCamera = GetComponent<Camera>();
            mapCamera.orthographic = true;
        }

        private void Start()
        {
            if (mapView == null || input == null)
            {
                Debug.LogError("WorldMapCameraController needs a map view and an input component.", this);
                enabled = false;
                return;
            }

            if (!mapView.IsReady)
            {
                // WorldMapView already logged why the map could not be laid out.
                enabled = false;
                return;
            }

            appliedAspect = IsValidAspect(mapCamera.aspect) ? mapCamera.aspect : FallbackAspect;
            model = new MapCameraModel(
                mapView.Projection.WorldRect, appliedAspect, mapView.Definition.MaxZoomInOrthographicSize);

            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;
            Apply();
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.Dragged -= OnDragged;
                input.Zoomed -= OnZoomed;
            }
        }

        private void Update()
        {
            if (model == null)
            {
                return;
            }

            // The aspect is not a usable number while the window is minimized.
            float aspect = mapCamera.aspect;

            if (IsValidAspect(aspect) && !Mathf.Approximately(aspect, appliedAspect))
            {
                model.SetAspect(aspect);
                appliedAspect = aspect;
            }

            Vector2 move = input.MoveAxis;

            if (move != Vector2.zero)
            {
                float unitsPerSecond = keyboardPanSpeed * 2f * model.OrthographicSize;
                model.Pan(move * (unitsPerSecond * Time.unscaledDeltaTime));
            }

            Apply();
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return mapCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, mapCamera.nearClipPlane));
        }

        private void OnDragged(Vector2 screenDelta)
        {
            if (model == null || Screen.height <= 0)
            {
                return;
            }

            // Dragging the map to the right moves the camera to the left.
            model.Pan(-screenDelta * WorldUnitsPerPixel);
            Apply();
        }

        private void OnZoomed(float step, Vector2 screenPosition)
        {
            if (model == null || Screen.height <= 0)
            {
                return;
            }

            model.Zoom(Mathf.Pow(zoomStepFactor, step), ScreenToWorld(screenPosition));
            Apply();
        }

        private void Apply()
        {
            Transform cameraTransform = mapCamera.transform;
            cameraTransform.position = new Vector3(model.Position.x, model.Position.y, cameraTransform.position.z);
            mapCamera.orthographicSize = model.OrthographicSize;
        }

        private static bool IsValidAspect(float aspect)
        {
            return aspect > 0f && !float.IsInfinity(aspect);
        }
    }
}
