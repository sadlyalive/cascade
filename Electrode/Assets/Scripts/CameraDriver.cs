using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Modeling-suite-style orbit camera: left-drag orbits around a pivot point,
/// scroll wheel zooms, right-drag pans the pivot. Deliberately not an FPS-style
/// fly camera — there's one bounded cluster to inspect from outside here, not
/// an open space to move through, and an orbit camera keeps it centered for
/// free instead of you constantly re-aiming to find it.
/// </summary>
public sealed class CameraDriver : MonoBehaviour
{
    [Header("Default state")]
    [SerializeField] private float _pitch = 24f;
    [field: SerializeField] public float Yaw { get; set; } = 32f;
    [SerializeField] private Vector3 _target = Vector3.zero;
    [SerializeField] private float _zoom = 16f;

    [Header("Constraints")]
    [SerializeField] private Vector3 targetBounds = new(16f, 16f, 16f);
    [SerializeField] private float _minZoom = 1f;
    [SerializeField] private float _maxZoom = 64f;
    [SerializeField] private float _pitchMin = -85f;
    [SerializeField] private float _pitchMax = 85f;

    [Header("Responsivity")]
    [SerializeField] private float _zoomSpeed = 0.8f;
    [SerializeField] private float _orbitSpeed = 0.2f;
    [SerializeField] private float _panSpeed = 0.01f;

    /// <summary>
    /// <para>Seconds to close ~63% of the remaining distance to <paramref name="Target"/>, independent of framerate by construction.</para>
    /// </summary>
    [SerializeField] private float _smoothing = 0.1f;

    // smoothing lives in angles to preserve spherical orbit while smoothing
    private float _smoothPitch;
    private float _smoothYaw;
    private Vector3 _smoothTarget;
    private float _smoothZoom;


    // TODO: delete correspondent fields and convert to auto-properties when unity 6.8 releases CoreCLR (supports c# 14)
    // then serialize with `[field: SerializeField]`
    public float Pitch
    {
        get => _pitch;
        set => _pitch = Mathf.Clamp(value, _pitchMin, _pitchMax);
    }
    public Vector3 Target
    {
        get => _target;
        set => _target = Vector3.Max(-targetBounds, Vector3.Min(value, targetBounds));
    }
    public float Zoom
    {
        get => _zoom;
        set => _zoom = Mathf.Clamp(value, _minZoom, _maxZoom);
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
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (mouse is null || kb is null)
            return; // not all necessary peripherals connected

        Vector2 delta = mouse.delta.ReadValue();

        if (mouse.middleButton.isPressed)
            if (kb.leftShiftKey.isPressed)
            {
                // mmb + shift: pan in screen space
                Target -= delta.x * Zoom * _panSpeed * transform.right;
                Target -= delta.y * Zoom * _panSpeed * transform.up;
            }
            else
            {
                // mmb: orbit around target
                Yaw += delta.x * _orbitSpeed;
                Pitch -= delta.y * _orbitSpeed;
            }

        Zoom -= mouse.scroll.ReadValue().y * _zoomSpeed;

        if (_smoothing <= 0f)
        {
            _smoothPitch = Pitch;
            _smoothYaw = Yaw;
        }
        else
        {
            // exponential decay relative to delta time for smoothing durations independent of framerate
            float alpha = 1f - Mathf.Exp(-Time.deltaTime / _smoothing);

            _smoothPitch = Mathf.Lerp(_smoothPitch, Pitch, alpha);
            _smoothYaw = Mathf.Lerp(_smoothYaw, Yaw, alpha);
            _smoothTarget = Vector3.Lerp(_smoothTarget, Target, alpha);
            _smoothZoom = Mathf.Lerp(_smoothZoom, Zoom, alpha);
        }

        Quaternion rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0f);
        transform.SetPositionAndRotation(_smoothTarget - rotation * Vector3.forward * _smoothZoom, rotation);
    }
}
