using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    /// <summary>
    ///     The bottom bar which displays information about the sandbox, specifically elapsed time and selection counts.
    /// </summary>
    public sealed class StatusBar : UIListener
    {
        [SerializeField] private Sandbox sandbox;

        private Label _selection;
        private bool  _selectionDirty;

        // remember state so we don't rewrite text on every frame
        private double _shownMs      = -1;
        private int    _shownVersion = -1;
        private Label  _time;

        private void Update()
        {
            if (_time is null)
                return;

            var network = sandbox.Network;

            // defensive since clock only moves in 1 ms steps anyway
            var ms = Math.Floor(network.Time);

            if (Math.Abs(ms - _shownMs) > 1e-9d)
            {
                _shownMs   = ms;
                _time.text = $"Time:\t{ms / 1000:0.000} s";
            }

            // synapse counts change with the selection or the wiring. Version only moves on actual connectome changes,
            // so this recounts on demand rather than every frame
            if (!_selectionDirty && _shownVersion == network.Connectome.Version)
                return;

            _selectionDirty = false;
            _shownVersion   = network.Connectome.Version;
            _selection.text = DescribeSelection();
        }

        protected override void OnEnabled()
        {
            sandbox.Selection.Changed += MarkSelectionDirty;
        }

        protected override void OnDisabled()
        {
            sandbox.Selection.Changed -= MarkSelectionDirty;
        }

        protected override void OnUIReload(VisualElement root)
        {
            _time      = root.Q<Label>("status-time");
            _selection = root.Q<Label>("status-selection");

            // force an update since fresh labels are blank
            _shownMs        = -1;
            _selectionDirty = true;
        }

        private void MarkSelectionDirty()
        {
            _selectionDirty = true;
        }

        private string DescribeSelection()
        {
            var selection  = sandbox.Selection;
            var connectome = sandbox.Network.Connectome;

            if (selection.Count == 0)
                return string.Empty;

            long inbound  = 0;
            long outbound = 0;
            long within   = 0;

            foreach (var i in selection.Order)
            {
                // a row's length is a single subtraction in CSR, so each in/out count costs O(1)
                var from = connectome.From(i);

                outbound += from.Count;
                inbound  += connectome.To(i).Count;

                foreach (var post in from.Columns)
                {
                    // a synapse that projects back to the selection must be internal/within
                    if (selection.Contains(post))
                        within++;
                }
            }

            // inbound + outbound counts internal synapses twice, so subtract once
            var total = inbound + outbound - within;

            // TODO: raw string literals in CoreCLR
            return "Selection:\t" +
                   $"{Count(selection.Count, "soma",    "somas")},\t" +
                   $"{Count(total,           "synapse", "synapses")} " +
                   $"({inbound.ToString()} inbound, {outbound.ToString()} outbound)";
        }

        private static string Count(long n, string one, string many)
        {
            return $"{n} {(n == 1 ? one : many)}";
        }
    }
}