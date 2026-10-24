using Cascade.UI.Form.Field;
using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     Edits the selected neurons' properties. Input fields for properties that not all neurons possess the same value for
    ///     will show as mixed. Mixed fields only write to the whole selection if the user edits the value.
    /// </summary>
    public sealed class ConfigureForm : ActionForm
    {
        private readonly FloatFormField   _leakRate;
        private readonly Vector3FormField _position;
        private readonly FloatFormField   _resetPotential;
        private readonly FloatFormField   _restPotential;
        private readonly FloatFormField   _threshold;

        // selection's average position to offset each neuron in the entire selection
        private float3 _center;

        public ConfigureForm(Sandbox sandbox) : base(sandbox, "Configure")
        {
            var count = Selection.Count;

            if (count == 0)
                return;

            var restShared   = true;
            var resetShared  = true;
            var leakShared   = true;
            var threshShared = true;

            var first = Selection[0];

            _center = first.GetPosition();

            // compare against first rather than previous to prevent Approximately() from allowing slight drifts
            for (var i = 1; i < count; i++)
            {
                var n = Selection[i];

                restShared   &= Mathf.Approximately(first.RestPotential,  n.RestPotential);
                resetShared  &= Mathf.Approximately(first.ResetPotential, n.ResetPotential);
                leakShared   &= Mathf.Approximately(first.LeakRate,       n.LeakRate);
                threshShared &= Mathf.Approximately(first.Threshold,      n.Threshold);

                _center += n.GetPosition();
            }

            _center /= count;

            _restPotential = Add(new FloatFormField
            {
                Label = "Rest potential (mV)",
                Value = first.RestPotential,
                Mixed = !restShared
            });
            _resetPotential = Add(new FloatFormField
            {
                Label = "Reset potential (mV)",
                Value = first.ResetPotential,
                Mixed = !resetShared
            });
            _leakRate = Add(new FloatFormField
            {
                Label    = "Leak rate (1/ms)",
                Value    = first.LeakRate,
                MinValue = 0f,
                Mixed    = !leakShared
            });
            _threshold = Add(new FloatFormField
            {
                Label = "Spike threshold (mV)",
                Value = first.Threshold,
                Mixed = !threshShared
            });
            _position = Add(new Vector3FormField
            {
                Label = count > 1 ? "Center" : "Position",
                Value = _center
            });
        }

        public override void OnChange()
        {
            // nothing was selected when the form opened, so it has no fields
            if (_position is null)
                return;

            var offset = (float3)_position.Value - _center;

            foreach (var n in Selection)
            {
                if (_restPotential.Edited)
                    n.RestPotential = _restPotential.Value;

                if (_resetPotential.Edited)
                    n.ResetPotential = _resetPotential.Value;

                if (_leakRate.Edited)
                    n.LeakRate = _leakRate.Value;

                if (_threshold.Edited)
                    n.Threshold = _threshold.Value;

                if (_position.Edited)
                    n.MoveTo(n.GetPosition() + offset);
            }

            if (_position.Edited)
                _center = _position.Value;
        }
    }
}