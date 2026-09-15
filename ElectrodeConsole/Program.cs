using Brain;
using ElectrodeConsole;

internal class Program
{
    private static void Main(string[] args)
    {
        const int neuronCount = 20;

        var network = new Network(neuronCount);

        // lower values: neurons will go silent
        // higher values: overexcitation
        Generators.WireRandom(network, meanDegree: 7f, minWeight: 3.0f, maxWeight: 8.0f, seed: 7);

        // inject current into a few neurons so activity can actually occur
        for (int i = 0; i < 5; i++)
            network.Neurons[i].InputCurrent = 3.5f;

        const double dtMs = 0.5;
        TimeSpan dt = TimeSpan.FromMilliseconds(dtMs);
        const int steps = 200;

        Console.WriteLine($"Simulating {neuronCount} neurons for {steps * dtMs:0} ms");
        Console.WriteLine("Each column is one neuron; '|' marks a spike.\n");
        Console.WriteLine($"time(ms) | {string.Concat(Enumerable.Range(0, neuronCount).Select(i => i % 10))}");
        Console.WriteLine(new string('-', 11 + neuronCount));

        int totalSpikes = 0;

        for (int step = 0; step < steps; step++)
        {
            network.Step(dt);

            var row = new char[neuronCount];
            int spikes = 0;

            for (int i = 0; i < neuronCount; i++)
            {
                bool fired = network.Neurons[i].Spike;
                row[i] = fired ? '|' : ' ';
                if (fired)
                    spikes++;
            }

            totalSpikes += spikes;

            // only print rows where something happened to keep output readable
            if (spikes > 0)
                Console.WriteLine($"{step * dtMs,7:0.0}  | {new string(row)}");
        }

        Console.WriteLine($"\n{totalSpikes} spikes total.");
    }
}
