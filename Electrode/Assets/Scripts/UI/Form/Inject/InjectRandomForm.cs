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
        private readonly UIntFormField  _seed;

        public InjectRandomForm(Sandbox sandbox) : base(sandbox, "Inject randomly")
        {
            _current = Add(new FloatFormField
            {
                Label = "Current",
                Value = 3.5f
            });
            _count = Add(new IntFormField
            {
                Label = "Neurons",
                Value = 10
            });
            _seed = Add(new UIntFormField
            {
                Label = "Seed"
            });
        }

        public override void Apply()
        {
            var rng   = NewRandom(_seed.Value);
            var total = Selection.Count;
            var count = Mathf.Clamp(_count.Value, 0, total);

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
                Selection[positions[i]].InputCurrent = _current.Value;
        }
    }
}