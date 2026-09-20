using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cascade
{
    [NoReorder]
    public sealed class CameraDriver : MonoBehaviour
    {
        // smoothing lives in angles to preserve spherical movement while orbiting around target
        private float _smoothPitch;
        private Vector3 _smoothTarget;
        private float _smoothYaw;
        private float _smoothZoom;

        [Header("Default state")]
        [SerializeField] private float pitch = 24f;

        [field: SerializeField] public float Yaw { get; set; } = 32f;
        [SerializeField] private Vector3 target = Vector3.zero;
        [SerializeField] private float zoom = 16f;

        [Header("Constraints")]
        [SerializeField] private Vector3 targetBounds = new(16f, 16f, 16f);

        [SerializeField] private float maxZoom = 64f;
        [SerializeField] private float minZoom = 1f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private float minPitch = -85f;

        [Header("Responsivity")]
        [SerializeField] private float zoomSpeed = 1f;

        [SerializeField] private float orbitSpeed = 0.2f;
        [SerializeField] private float panSpeed = 0.004f;

        /// <summary>
        ///     <para>
        ///         Seconds to close ~63% of the remaining distance to <see cref="Target" />. Independent of framerate by
        ///         construction.
        ///     </para>
        /// </summary>
        [SerializeField] private float smoothing = 0.1f;

        // TODO:
        // delete correspondent fields and convert to auto-properties when unity 6.8 releases CoreCLR (supports c# 14)
        // then serialize with `[field: SerializeField]`
        public float Pitch
        {
            get => pitch;
            set => pitch = Mathf.Clamp(value, minPitch, maxPitch);
        }

        public Vector3 Target
        {
            get => target;
            set => target = Vector3.Max(-targetBounds, Vector3.Min(value, targetBounds));
        }

        public float Zoom
        {
            get => zoom;
            set => zoom = Mathf.Clamp(value, minZoom, maxZoom);
        }

        private void Awake()
        {
            // immediately settle to defaults regardless of smoothing
            _smoothPitch = Pitch;
            _smoothYaw = Yaw;
            _smoothTarget = Target;
            _smoothZoom = Zoom;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (mouse is null ||
                kb is null)
                return; // not all necessary peripherals connected

            var delta = mouse.delta.ReadValue();

            if (mouse.middleButton.isPressed)
                if (kb.leftShiftKey.isPressed)
                {
                    // mmb + shift: pan in screen space
                    Target -= delta.x * Zoom * panSpeed * transform.right;
                    Target -= delta.y * Zoom * panSpeed * transform.up;
                }
                else
                {
                    // mmb: orbit around target
                    Yaw += delta.x * orbitSpeed;
                    Pitch -= delta.y * orbitSpeed;
                }

            Zoom -= mouse.scroll.ReadValue().y * zoomSpeed;

            if (smoothing <= 0f)
            {
                _smoothPitch = Pitch;
                _smoothYaw = Yaw;
            }
            else
            {
                // exponential decay relative to delta time for smoothing durations independent of framerate
                var alpha = 1f - Mathf.Exp(-Time.deltaTime / smoothing);

                _smoothPitch = Mathf.Lerp(_smoothPitch, Pitch, alpha);
                _smoothYaw = Mathf.Lerp(_smoothYaw, Yaw, alpha);
                _smoothTarget = Vector3.Lerp(_smoothTarget, Target, alpha);
                _smoothZoom = Mathf.Lerp(_smoothZoom, Zoom, alpha);
            }

            var rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0f);
            transform.SetPositionAndRotation(_smoothTarget - rotation * Vector3.forward * _smoothZoom, rotation);
        }
    }
}