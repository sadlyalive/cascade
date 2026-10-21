using Cascade.UI.Form.Field;
using UnityEngine;

namespace Cascade.UI.Form.Inject
{
    /// <summary>
    ///     Sets a constant input current on a number of randomly picked selected neurons. It stays on until changed (e.g.
    ///     Inject → Uniform with 0).
    /// </summary>
    public sealed class InjectRandomForm : ActionForm
    {
        private readonly IntFormField   _count;
        private readonly FloatFormField _current;
        private readonly Sandbox        _sandbox;
        private readonly UIntFormField  _seed;

        public InjectRandomForm(Sandbox sandbox) : base($"Inject randomly ({sandbox.Selection.Count} selected)")
        {
            _sandbox = sandbox;
            _current = Add(new FloatFormField("Current", 3.5f));
            _count   = Add(new IntFormField("Neurons", 10));
            _seed    = Add(new UIntFormField("Seed"));
        }

        public override void Apply()
        {
            var rng       = NewRandom(_seed.Value);
            var selection = _sandbox.Selection;
            var total     = selection.Count;
            var count     = Mathf.Clamp(_count.Value, 0, total);

            // partial Fisher-Yates over selection positions; only shuffle as many slots as there are distinct picks
            var positions = new int[total];

            for (var i = 0; i < total; i++)
                positions[i] = i;

            for (var i = 0; i < count; i++)
            {
                var j = rng.NextInt(i, total);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            for (var i = 0; i < count; i++)
                selection[positions[i]].InputCurrent = _current.Value;
        }
    }
}