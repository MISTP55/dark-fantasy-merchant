using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The city view's camera: drag pans, the wheel zooms, and the view is kept over the grid. The input moves a
    /// <see cref="MapCameraModel"/> over the grid's rectangle, and the camera shows it eased by a
    /// <see cref="MapCameraSmoother"/>, as on the map.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CityViewCamera : MonoBehaviour
    {
        private const float FallbackAspect = 16f / 9f;

        [SerializeField] private CityViewInput input;

        [Tooltip("Orthographic size multiplier for one scroll step towards the grid.")]
        [SerializeField, Range(0.5f, 0.99f)] private float zoomStepFactor = 0.85f;

        [Tooltip("Smallest orthographic size, in world units: the closest view.")]
        [SerializeField, Min(0.1f)] private float maxZoomInOrthographicSize = 2f;

        [Tooltip("World units around the grid that the view can show.")]
        [SerializeField, Min(0f)] private float margin = 1f;

        [Tooltip("Seconds for the camera to cover most of the way to where the input sent it. 0 disables smoothing.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.09f;

        private Camera viewCamera;
        private MapCameraModel model;
        private MapCameraSmoother smoother;
        private float appliedAspect;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            viewCamera.orthographic = true;
        }

        private void OnEnable()
        {
            if (input == null)
            {
                Debug.LogError("CityViewCamera needs an input component.", this);
                return;
            }

            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Dragged -= OnDragged;
                input.Zoomed -= OnZoomed;
            }
        }

        /// <summary>
        /// Keeps the view over this rectangle, in world units, and shows as much of it as the view can.
        /// </summary>
        public void Frame(Rect worldRect)
        {
            Rect bounds = new Rect(
                worldRect.xMin - margin, worldRect.yMin - margin,
                worldRect.width + 2f * margin, worldRect.height + 2f * margin);

            appliedAspect = IsValidAspect(viewCamera.aspect) ? viewCamera.aspect : FallbackAspect;
            model = new MapCameraModel(bounds, appliedAspect, maxZoomInOrthographicSize);
            smoother = new MapCameraSmoother(model.Position, model.OrthographicSize);
            Apply();
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return viewCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, viewCamera.nearClipPlane));
        }

        private void Update()
        {
            if (model == null)
            {
                return;
            }

            // The aspect is not a usable number while the window is minimized.
            float aspect = viewCamera.aspect;

            if (IsValidAspect(aspect) && !Mathf.Approximately(aspect, appliedAspect))
            {
                model.SetAspect(aspect);
                appliedAspect = aspect;
                smoother.SnapTo(model.Position, model.OrthographicSize);
            }

            smoother.Advance(model.Position, model.OrthographicSize, Time.unscaledDeltaTime, smoothTime);
            Apply();
        }

        private void OnDragged(Vector2 screenDelta)
        {
            if (model == null || Screen.height <= 0)
            {
                return;
            }

            // Dragging the grid to the right moves the camera to the left.
            model.Pan(-screenDelta * model.WorldUnitsPerPixel(Screen.height));
        }

        private void OnZoomed(float step, Vector2 screenPosition)
        {
            if (model == null || Screen.height <= 0)
            {
                return;
            }

            // Anchor in the view the camera is heading to, so that quick steps keep zooming on the same point.
            Vector2 anchor = model.ViewportToWorld(viewCamera.ScreenToViewportPoint(screenPosition));
            model.Zoom(Mathf.Pow(zoomStepFactor, step), anchor);
        }

        private void Apply()
        {
            Transform cameraTransform = viewCamera.transform;
            cameraTransform.position = new Vector3(smoother.Position.x, smoother.Position.y, cameraTransform.position.z);
            viewCamera.orthographicSize = smoother.OrthographicSize;
        }

        private static bool IsValidAspect(float aspect)
        {
            return aspect > 0f && !float.IsInfinity(aspect);
        }
    }
}
