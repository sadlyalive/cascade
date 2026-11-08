# Brain

Brain is a lightweight library that allows consumers to simply and efficiently assemble and simulate crude spiking
neural networks in C#. It currently only features basic neural attributes and small computational optimizations, though
thread-level parallelism and CPU vectorization are planned for the future.

## Installation

Brain targets both .NET 10 and .NET Standard 2.1 to remain compatible with Unity's Mono runtime (which
allows [Electrode](https://github.com/sadlyalive/cascade#electrode) to work properly at present) but will shift
completely to .NET 10 shortly after Unity releases CoreCLR.

### NuGet

You can fetch and include Brain in your projects via NuGet. There are only prereleases since the library is still in
very early stages of development. Pull from NuGet with the `--prerelease` flag:

```bash
dotnet add package Brain --prerelease
```

### Project Reference

Downloading the repository allows you to easily check and modify any code. From your project's folder, clone the
repository:

 ```bash
git clone https://github.com/sadlyalive/brain.git ../brain
 ```

Then reference Brain in your C# project file:

```bash
dotnet add reference ../brain/src/Brain.csproj
```

The library should then become usable and automatically build with your original project.

### DLL Compilation

You might prefer a single DLL for portability, in which case you will have to build manually. Clone the repository and
enter it:

```bash
git clone https://github.com/sadlyalive/brain.git && cd brain/src
```

Then build the DLL with the [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet) (min 10):

```bash
dotnet build -c Release
```

DLL, PDB, and XML files should then be ready for general use in `bin/Release/net10.0/`. Make sure to use the build in
`bin/Release/netstandard2.1/` instead for legacy environments (most notably Unity Mono). Unity can perceive the library
when you place all three output files anywhere together in `Assets/`.

## Getting Started

Once your project includes the package, creating a network is extremely easy:

```csharp
using Brain; // import the library

internal static class Program
{
    private static void Main(string[] args)
    {
        // create a network and store its reference
        var network = new Network();
    }
}
```

If you saved a network before as a file, load it with the static method `Network.FromFile()` instead:

```csharp
var network = Network.FromFile("path/to/file");
```

You can save your network to a file at any time with `Network.SaveFile()`:

```csharp
network.SaveFile("path/to/file");
```

### Adding Neurons

Append a single new neuron with `Network.AddNeuron()`. This method returns a single `Neuron` struct with properties that
represent neural attributes. Brain stores these attributes in a Structure of Arrays (SoA), so each property returns its
attribute's location in the relevant array:

```csharp
// readable struct property assignment to granularly configure a single neuron on the spot
var neuron0 = network.AddNeuron();

neuron0.Potential = -80f;
neuron0.RestPotential = -80f;

// less readable but more accurate depiction of what `Neuron` properties do
NeuronPool pool = network.Neurons; // the data structure that stores the SoA
var neuron1 = network.AddNeuron();

// `neuron1` implicitly converts into its underlying integer index/ID
pool.Potential[neuron1]     = -80f; // write directly to SoA
pool.RestPotential[neuron1] = -80f;
```

You can also create and manipulate multiple neurons at once with `Network.AddNeurons(int count)`, which returns an
`IEnumerable<Neuron>`:

```csharp
foreach (Neuron n in network.AddNeurons(1024))
    n.InputCurrent = 10f; // inject every new neuron with 10 mV/ms
```

### Fetching Existing Neurons

Both `AddNeuron` and `AddNeurons` use `NeuronPool`'s indexers internally. You can also use them when you need to fetch
neurons by index:

```csharp
NeuronPool pool = network.Neurons;

network.AddNeuron(); // woops, forgot to store the `Neuron` struct

Neuron neuron = pool[network.Count - 1]; // phew

network.AddNeurons(16); // dang it, not again

IEnumerable<Neuron> neurons = pool[^16..]; // phew

foreach (Neuron n in neurons)
    n.MoveTo(-4, -4, -4);
```

`NeuronPool` provides an `IEnumerator<Neuron>` which allows you to loop through all neurons with a `foreach` loop:

```csharp
foreach (Neuron n in pool)
    n.LeakRate = 1f / ((float)(network.Time - n.LastSpike) + 10f);
```

### Wiring the Network

A network needs synapses for neurons to communicate. `Network.Connectome` tracks connections between all neurons. Create
a new synapse between two neurons with `Connectome`'s `[pre, post]` indexer, where the neuron at index `pre` innervates
the neuron at index `post`:

```csharp
Connectome<float> connectome = network.Connectome;
Neuron n0 = network.AddNeuron(),
       n1 = network.AddNeuron(), 
       n2 = network.AddNeuron();

// create synapses from `n0` to `n1` and `n2` with weights of 8f and 16f respectively
// remember that `Neuron` implicitly converts to `int`
connectome[n0, n1] = 8f;
connectome[n0, n2] = 16f;
```

`Connectome` should now reflect both new synapses. We can verify with the same methods that the core `Network.Step`
logic uses: `Connectome` stores connections bidirectionally which allows you to efficiently fetch them from either the
presynaptic neuron (via `Connectome.From(int pre)`) or postsynaptic neuron (via `Connectome.To(int post)`):

```csharp
// outbound: iterate all connections FROM `n0`, i.e., where `n0` is presynaptic
foreach ((int post, float weight) in connectome.From(n0))
    Console.WriteLine($"{n0} → {post} has weight {weight}");
    // should show "0 → 1 has weight 8" and "0 → 2 has weight 16"
    
Console.ReadKey();
Console.Clear();

// inbound: iterate all connections TO `n1`, i.e., where `n1` is postsynaptic
foreach ((int pre, float weight) in connectome.To(n1))
    Console.WriteLine($"{pre} → {n1} has weight {weight}");
    // should show only "0 → 1 has weight 8"
    
Console.ReadKey();
Console.Clear();
    
// inbound: iterate all connections TO `n2`, i.e., where `n2` is postsynaptic
foreach ((int pre, float weight) in connectome.To(n2))
    Console.WriteLine($"{pre} → {n2} has weight {weight}");
    // should show only "0 → 2 has weight 16"
```

### Simulating the Network

`Network.Step(TimeSpan time)` encapsulates the basic core logic. It tracks which neurons spike, then delivers weighted
currents through all outbound synapses. Let's reuse the minimal three-neuron setup:

```csharp
Neuron n0 = network.AddNeuron(),
       n1 = network.AddNeuron(), 
       n2 = network.AddNeuron();

connectome[n0, n1] = 8f;
connectome[n0, n2] = 16f;
```

Reset potentials are -75 mV, and thresholds are -55 mV by default (subject to change). This means that any one of our
virtual neurons requires 20 mV/ms of injected current to fire every millisecond. We'll bump up to 21 mV/ms to avoid
floating-point imprecision, which still works fine if our steps always elapse one millisecond. Let's inject that current
into `n0` and also ignore leakage for maximal predictability:

```csharp
n0.InputCurrent = 21f;

foreach (Neuron n in network.Neurons)
    n.LeakRate = 0f;
```

Now, we can step the simulation forward to propagate `n0`'s 1000 Hz activity:

```csharp
var deltaTime = TimeSpan.FromMilliseconds(1);

network.Step(deltaTime); // no propagation; `n0` quickly depolarizes then spikes
network.Step(deltaTime); // `n0` spike propagates
```

`n0` innervates `n2` much more strongly than `n1`, so two steps should be enough for `n2` to reach its threshold. We
can check if its most recent spike aligns with the current simulation time:

```csharp
// bit comparison of floats is fine here because `Step` uses `Time` when updating `LastSpike`
Console.WriteLine($"n1 spiking: {network.Time == n1.LastSpike}"); // false
Console.WriteLine($"n2 spiking: {network.Time == n2.LastSpike}"); // true
```

Advance the simulation by one more millisecond, and `n1` should spike while `n2` already repolarized:

```csharp
network.Step(deltaTime); // `n0` spike propagates again

Console.WriteLine($"n1 spiking: {network.Time == n1.LastSpike}"); // true
Console.WriteLine($"n2 spiking: {network.Time == n2.LastSpike}"); // false
```