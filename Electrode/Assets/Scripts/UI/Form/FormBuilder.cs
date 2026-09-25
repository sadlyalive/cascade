using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using UnityEngine;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     The concrete forms, as data. Each one adds its inputs to a form, keeps them as locals, then says what Apply does
    ///     by reading those same locals back. Defaults match the network <see cref="Sandbox" /> builds on startup. Knows
    ///     the sandbox it edits and the hint renderer it previews on, so callers just pick a form.
    /// </summary>
    public sealed class FormBuilder
    {
        private readonly Hint    _hint;
        private readonly Sandbox _sandbox;

        public FormBuilder(Sandbox sandbox, Hint hint)
        {
            _sandbox = sandbox;
            _hint    = hint;
        }

        public ActionForm PlaceSphere()
        {
            var form   = Hinted("Place randomly in sphere", WireMesh.Sphere);
            var origin = form.Add(new Vector3FormField("Origin", Vector3.zero));
            var radius = form.Add(new FloatFormField("Radius", 8f));
            var seed   = form.Add(new UIntFormField("Seed"));

            form.OnApply = () => _sandbox.PlaceRandomSphere(radius.Value, origin.Value, seed.Value);
            form.OnUpdate = h =>
            {
                h.Origin = origin.Value;
                h.Size   = Vector3.one * radius.Value;
            };

            return form;
        }

        public ActionForm PlaceBox()
        {
            var form   = Hinted("Place randomly in box", WireMesh.Cube);
            var origin = form.Add(new Vector3FormField("Origin", Vector3.zero));
            var size   = form.Add(new Vector3FormField("Size",   Vector3.one * 4f));
            var seed   = form.Add(new UIntFormField("Seed"));

            form.OnApply = () => _sandbox.PlaceRandomBox(size.Value, origin.Value, seed.Value);
            form.OnUpdate = h =>
            {
                h.Origin = origin.Value;
                h.Size   = size.Value;
            };

            return form;
        }

        public ActionForm WireRandom()
        {
            var form   = new ActionForm("Wire randomly");
            var degree = form.Add(new FloatFormField("Mean degree", 3f));
            var min    = form.Add(new FloatFormField("Min weight",  1f));
            var max    = form.Add(new FloatFormField("Max weight",  10f));
            var seed   = form.Add(new UIntFormField("Seed"));

            form.OnApply = () => _sandbox.WireRandom(degree.Value, min.Value, max.Value, seed.Value);

            return form;
        }

        public ActionForm InjectRandom()
        {
            var form    = new ActionForm("Inject randomly");
            var current = form.Add(new FloatFormField("Current", 3.5f));
            var count   = form.Add(new IntFormField("Neurons", 10));
            var seed    = form.Add(new UIntFormField("Seed"));

            form.OnApply = () => _sandbox.InjectRandom(current.Value, count.Value, seed.Value);

            return form;
        }

        // a form that previews on the hint renderer; injects it so each form only names its shape
        private HintActionForm Hinted(string title, Mesh shape)
        {
            return new HintActionForm(title, _hint, shape);
        }
    }
}