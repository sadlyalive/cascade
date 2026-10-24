using System;
using Brain;
using Cascade.Select;
using UnityEngine;

namespace Cascade
{
    /// <summary>
    ///     Owns a <see cref="Brain.Network" /> and its <see cref="Selection" />, and advances the network in fixed steps of
    ///     <see cref="StepSize" />, as many per frame as <see cref="TimeScale" /> asks for. Edits to the network live in the
    ///     forms that perform them (see Cascade.UI.Form).
    /// </summary>
    // some UI components depend on sandbox properties on enable, but unity does not guarantee sandbox's Awake calls
    // before the 'ui' game object's OnEnable
    [DefaultExecutionOrder(-100)]
    public sealed class Sandbox : MonoBehaviour
    {
        /// <summary>
        ///     <para>
        ///         Not reliably changeable until step calculations are independent of <c>dt</c>.
        ///     </para>
        /// </summary>
        public static readonly TimeSpan StepSize = TimeSpan.FromMilliseconds(1);

        // throttles steps per frame since they could accumulate if slower than framerate, especially at higher time
        // scales
        [SerializeField] private int maxStepsPerFrame = 256;

        // simulated time owed but not yet stepped, carried across frames so fractional steps add up instead of being
        // lost
        private double _pendingMs;

        public float TimeScale { get; set; } = 1f;

        /// <summary>The live neural network.</summary>
        public Network Network { get; private set; }

        /// <summary>The neurons the user has picked, which selection-aware forms act on.</summary>
        public Selection Selection { get; private set; }

        private void Awake()
        {
            Network   = new Network(0);
            Selection = new Selection(Network);

            // var diagnoser = Network.Diagnoser;
            // var trials    = diagnoser.TrialsPerDiagnosis;
            //
            // diagnoser.OnFinish += (_, dt) =>
            // {
            //     Debug.Log($"{trials} trials of propagation: {dt} ms, avg {dt * 1000 / trials} µs");
            // };
        }

        // accumulates steps in accordance with StepSize and actual time elapsed
        private void Update()
        {
            if (Network is null)
                return;

            // Step commits too, but a paused network never steps. without this, pending wiring would stay invisible to
            // rows (rendering, Grow) until unpaused. free when nothing is pending
            Network.Connectome.CommitChanges();

            // unscaled so Unity's own Time.timeScale (which also drives camera smoothing etc.) never affects the
            // network since it already has a TimeScale field
            _pendingMs += Time.unscaledDeltaTime * 1000.0 * TimeScale;

            var stepMs = StepSize.TotalMilliseconds;
            var steps  = 0;

            while (_pendingMs >= stepMs && steps < maxStepsPerFrame)
            {
                Network.Step(StepSize);
                _pendingMs -= stepMs;
                steps++;
            }

            // can't keep up, so drop the pending time, or else each frame would owe more steps than the last, get
            // slower, and owe even more ("spiral of death")
            if (steps >= maxStepsPerFrame)
                _pendingMs = 0;
        }
    }
}