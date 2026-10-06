using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Feeds input to a <see cref="MapCameraModel"/> and shows its state on the camera,
    /// eased by a <see cref="MapCameraSmoother"/>. Holds no clamping, zoom or easing math of its own.
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

        [Tooltip("Seconds for the camera to cover most of the way to where the input sent it. 0 disables smoothing.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        private Camera mapCamera;

        // The model is where the input has sent the camera; the smoother is what is displayed.
        private MapCameraModel model;
        private MapCameraSmoother smoother;
        private float appliedAspect;

        public bool IsReady => model != null;

        /// <summary>World units per screen pixel of the view currently displayed.</summary>
        public float WorldUnitsPerPixel => 2f * smoother.OrthographicSize / Screen.height;

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
            smoother = new MapCameraSmoother(model.Position, model.OrthographicSize);

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

                // A resized view must fit the map at once, not ease into it.
                smoother.SnapTo(model.Position, model.OrthographicSize);
            }

            Vector2 move = input.MoveAxis;

            if (move != Vector2.zero)
            {
                float unitsPerSecond = keyboardPanSpeed * 2f * model.OrthographicSize;
                model.Pan(move * (unitsPerSecond * Time.unscaledDeltaTime));
            }

            smoother.Advance(model.Position, model.OrthographicSize, Time.unscaledDeltaTime, smoothTime);
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
            model.Pan(-screenDelta * model.WorldUnitsPerPixel(Screen.height));
        }

        private void OnZoomed(float step, Vector2 screenPosition)
        {
            if (model == null || Screen.height <= 0)
            {
                return;
            }

            // Anchor in the view the camera is heading to, not the one displayed, so that
            // several quick scroll steps keep zooming on the same point.
            Vector2 anchor = model.ViewportToWorld(mapCamera.ScreenToViewportPoint(screenPosition));
            model.Zoom(Mathf.Pow(zoomStepFactor, step), anchor);
        }

        private void Apply()
        {
            Transform cameraTransform = mapCamera.transform;
            cameraTransform.position = new Vector3(smoother.Position.x, smoother.Position.y, cameraTransform.position.z);
            mapCamera.orthographicSize = smoother.OrthographicSize;
        }

        private static bool IsValidAspect(float aspect)
        {
            return aspect > 0f && !float.IsInfinity(aspect);
        }
    }
}
