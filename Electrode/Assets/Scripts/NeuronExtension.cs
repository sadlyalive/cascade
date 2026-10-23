using Brain.Data;
using Unity.Mathematics;

namespace Cascade
{
    public static class NeuronExtension
    {
        public static float3 GetPosition(this Neuron neuron)
        {
            return new float3(neuron.X, neuron.Y, neuron.Z);
        }

        public static void MoveTo(this Neuron neuron, float3 position)
        {
            neuron.MoveTo(position.x, position.y, position.z);
        }
    }
}