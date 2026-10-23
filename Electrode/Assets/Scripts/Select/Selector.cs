using System;
using System.Runtime.CompilerServices;
using Cascade.Render;
using Cascade.UI;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cascade.Select
{
    /// <summary>
    ///     <para>
    ///         Edits <see cref="Sandbox.Selection" /> with conventional editor suite controls.
    ///     </para>
    ///     <para>
    ///         Somas are GPU-only geometry with no colliders, so <see cref="Physics.Raycast(Ray)" /> can't perceive them.
    ///         Each click instead performs manual ray intersection logic at the cursor against every soma as a sphere on the
    ///         CPU. A little arithmetic per neuron per click is scalable enough so that even a fly brain would be performant.
    ///     </para>
    /// </summary>
    [RequireComponent(typeof(Sandbox))]
    [RequireComponent(typeof(SomaRenderer))]
    // before FormDriver since it closes an open form on the same left click. if this were to run after, it would see
    // the UI already non-busy and consider the same click to be valid
    [DefaultExecutionOrder(-1)]
    [NoReorder]
    public sealed class Selector : MonoBehaviour
    {
        [SerializeField] private Camera   view;
        [SerializeField] private UIDriver ui;
        [SerializeField] private float    pickRadius    = 4f;
        [SerializeField] private float    dragThreshold = 4f;

        private VisualElement _box;

        private float        _dragThresholdSq;
        private Vector2      _press;
        private bool         _pressed;
        private SomaRenderer _renderer;
        private Sandbox      _sandbox;

        private void Awake()
        {
            _sandbox  = GetComponent<Sandbox>();
            _renderer = GetComponent<SomaRenderer>();

            _dragThresholdSq = dragThreshold * dragThreshold;

            if (!view)
                view = Camera.main;
        }

        private void OnEnable()
        {
            ui.Reloaded += UpdateDragBox;

            if (ui.Root != null)
                UpdateDragBox(ui.Root);
        }

        private void OnDisable()
        {
            ui.Reloaded -= UpdateDragBox;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var kb    = Keyboard.current;

            if (mouse is null || kb is null || !view)
                return;

            // ctrl + scroll up grows the selection one synapse downstream (+ shift: upstream), scroll down undoes the
            // last grow. CameraDriver ignores the wheel while ctrl is held, so this never zooms too. only the sign of
            // the scroll matters, so one grow per frame however fast the wheel spins
            var scroll = mouse.scroll.ReadValue().y;

            if (scroll != 0f && kb.ctrlKey.isPressed && !ui.CursorOverUI && !ui.IsBusy)
            {
                if (scroll > 0f)
                    _sandbox.Selection.Grow(!kb.shiftKey.isPressed);
                else
                    _sandbox.Selection.Shrink();
            }

            // probe a valid click/drag
            if (mouse.leftButton.wasPressedThisFrame && !ui.CursorOverUI && !ui.IsBusy)
            {
                _pressed = true;
                _press   = mouse.position.ReadValue();
            }

            // update drag box mid-drag, or hide it if selection is too small or a click
            if (_pressed && mouse.leftButton.isPressed)
            {
                var now     = mouse.position.ReadValue();
                var isPoint = (now - _press).sqrMagnitude < _dragThresholdSq;

                _box.EnableInClassList("hidden", isPoint);

                if (!isPoint)
                {
                    var a = ToPanel(_press);
                    var b = ToPanel(now);

                    _box.style.left   = Mathf.Min(a.x, b.x);
                    _box.style.top    = Mathf.Min(a.y, b.y);
                    _box.style.width  = Mathf.Abs(a.x - b.x);
                    _box.style.height = Mathf.Abs(a.y - b.y);
                }
            }

            if (!_pressed || !mouse.leftButton.wasReleasedThisFrame)
                return;

            _box.EnableInClassList("hidden", true);
            _pressed = false;

            var additive = kb.ctrlKey.isPressed;
            var release  = mouse.position.ReadValue();

            if ((release - _press).sqrMagnitude < _dragThresholdSq)
            {
                ProcessClick(release, additive);
                return;
            }

            ProcessDrag(_press, release, additive);

            return;

            // since panel space has a top-left origin and its own scale
            Vector2 ToPanel(Vector2 s)
            {
                return RuntimePanelUtils.ScreenToPanel(_box.panel, new Vector2(s.x, Screen.height - s.y));
            }
        }

        private void ProcessClick(Vector2 pos, bool additive)
        {
            var selection = _sandbox.Selection;

            var hit = Pick(pos);

            // void click
            if (hit < 0)
            {
                // a void ctrl + LMB is probably just a user slip while building a selection, so no-op in that case
                if (!additive)
                    selection.Clear();

                return;
            }

            if (additive)
                selection.Toggle(hit);
            else
                selection.Set(hit);
        }

        private void ProcessDrag(Vector2 p0, Vector2 p1, bool additive)
        {
            var min = NormalizeScreenPoint(Vector2.Min(p0, p1));
            var max = NormalizeScreenPoint(Vector2.Max(p0, p1));

            var m = view.projectionMatrix * view.worldToCameraMatrix;

            var network   = _sandbox.Network;
            var selection = _sandbox.Selection;
            var neurons   = network.Count;
            var pool      = network.Neurons;

            var xArray = pool.X;
            var yArray = pool.Y;
            var zArray = pool.Z;

            var within = new int[128];
            var count  = 0;

            for (var i = 0; i < neurons; i++)
            {
                var x = xArray[i];
                var y = yArray[i];
                var z = zArray[i];
                var w = m.m30 * x + m.m31 * y + m.m32 * z + m.m33;

                if (w <= 0f) // <= 0 means behind camera (depth)
                    continue;

                var cx = m.m00 * x + m.m01 * y + m.m02 * z + m.m03;
                var cy = m.m10 * x + m.m11 * y + m.m12 * z + m.m13;

                if (cx < min.x * w || cx > max.x * w || cy < min.y * w || cy > max.y * w) // rect in -1..1 space
                    continue;

                var cap = within.Length;

                if (count >= cap)
                    Array.Resize(ref within, cap + (cap >> 1));

                within[count++] = i;
            }

            var hits = within.AsSpan(0, count);

            if (additive)
                selection.Append(hits);
            else
                selection.Set(hits);

            return;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            Vector2 NormalizeScreenPoint(Vector2 p)
            {
                var rect = view.pixelRect;
                return (p - rect.position) / rect.size * 2f - Vector2.one;
            }
        }

        /// <returns>
        ///     The index of the front-most neuron under <paramref name="screenPoint" /> (bottom-left origin), or -1 if none.
        ///     Assumes a perspective camera.
        /// </returns>
        public int Pick(Vector2 screenPoint)
        {
            var ray    = view.ScreenPointToRay(screenPoint);
            var origin = (float3)ray.origin;
            var dir    = (float3)ray.direction; // normalized, so dot products below are distances along the ray

            // world-space height of one pixel at distance 1 from the camera. it grows linearly with distance, so
            // `t * pixelSlope * pickRadius` is a hit radius that's pickRadius pixels on screen at any depth
            var pixelSlope = 2f * math.tan(math.radians(view.fieldOfView) * 0.5f) / view.pixelHeight;
            var tolerance  = pixelSlope * pickRadius;
            var radius     = _renderer.Radius;

            var neurons = _sandbox.Network.Neurons;
            var x       = neurons.X;
            var y       = neurons.Y;
            var z       = neurons.Z;

            var best  = -1;
            var bestT = float.MaxValue;

            for (var i = 0; i < neurons.Count; i++)
            {
                var toSoma = new float3(x[i], y[i], z[i]) - origin;

                // how far along the ray the soma's closest approach is. behind the camera or behind a soma that
                // already hit can't win
                var t = math.dot(toSoma, dir);

                if (t <= 0f || t >= bestT)
                    continue;

                // squared distance from the soma's center to the ray, by pythagoras: |toSoma|² = t² + d²
                var distanceSq = math.lengthsq(toSoma) - t * t;
                var hitRadius  = math.max(radius, t * tolerance);

                if (distanceSq >= hitRadius * hitRadius)
                    continue;

                best  = i;
                bestT = t;
            }

            return best;
        }

        private void UpdateDragBox(VisualElement root)
        {
            _box = root.Q("drag-box");
        }
    }
}