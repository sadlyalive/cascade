# Cascade

Cascade consists of two components: [Brain](https://github.com/sadlyalive/brain#brain) and [Electrode](#electrode). This README concerns mostly Electrode, a lightweight interface for Brain, since [Brain has its own repository](https://github.com/sadlyalive/brain#brain) as a standalone library.

## Electrode

Electrode is a very simple editor made with Unity that allows users to freely manipulate neurons and observe interneural interactions within a sandbox from almost any computer. Its versatile yet very digestible set of features with hardly any learning curve or setup:
<img width="1133" height="27" alt="image" src="https://github.com/user-attachments/assets/e3c807b1-43a8-4813-bddd-2f5478991050" />

 At the moment, Brain's internal calculations are inaccurate in comparison to other neural solvers in interest of portability, performance, and quantity over quality, so the library is more useful as a consumer-grade educational tool rather than a research supplement.

### Installation

Electrode releases will typically include builds for [Linux](#linux), [macOS](#macos), and [Windows](#windows). You can find latest releases [here](https://github.com/sadlyalive/cascade/releases).

#### Linux
1. Extract the tar file:
```sh
tar -xzf cascade*.tar.gz 
```

2. Run the executable:
```sh
./Cascade/cascade.x86_64
```
**Note:** you might have to allow execution before running:
```sh
chmod +x Cascade/cascade.x86_64
```

#### macOS
With Apple's notarization, macOS systems will very likely reject open-source apps. Unzip the file and either right-click and open or run:
```sh
xattr -dr com.apple.quarantine mac.app
```

#### Windows
Simply unzip the file into a folder and run `Cascade.exe`.

### Recent Features (v1.0)
- Navigation
  - Camera behavior takes a lot of inspiration from Blender and similar modelling suites:
    - Hold and drag `MMB`: orbit
    - Hold and drag `Shift + MMB`: pan
    - `Scroll up/down`: zoom in/out respectively
  - Topbar
    - `speed` input and accompanying logarithmic slider can deterministically slow/accelerate the simulation
      - **!! HIGH SPEEDS CAN HURT PERFORMANCE DEPENDING ON HOW LARGE YOUR SIMULATION IS !!** 
    - `Draw gizmos` checkbox can disable hint meshes (i.e., command previews and the axes guides)
    - `Draw synapses` dropdown can selectively render connections based on view distance, activity, or selection
- Selection
  - `LMB`: select neuron (exclusive)
  - `Ctrl + LMB`: select neuron (inclusive)
  - Hold and drag `LMB`: select neurons in box (exclusive)
  - Hold and drag `Ctrl + LMB`: select neurons in box (inclusive)
  - Hover over current selection and `Ctrl + Scroll up/down`: recursively select postsynaptic neurons (inclusive)
  - Change selected neurons' properties in bulk with the panel that appears automatically when your selection changes (changes apply instantly) 
- Commands
  - Soma
    - Create: Add `Count` new neurons at `Position`. Position is your camera's current target by default
    - Delete: Delete selected neurons
      - **!! VERY UNPERFORMANT RIGHT NOW !! SEE NETWORK MENU FOR FULL-CLEAR ALTERNATIVE **
  - Place
    - Random
      - Sphere: Pseudorandomly place selected neurons within a sphere of radius `Radius` at `Origin` (accepts a `Seed` for randomness, leave at `0` for clock-dependent seed)
      - Box: Pseudorandomly place selected neurons within a box of size `Size` at `Origin` (accepts a `Seed`)
    - Grid: Fill a grid bottom-up at `Origin` with selected neurons, spacing them `Spacing` apart
    - Ring: Lay selected neurons flat in a circle of radius `Radius` at `Origin`
  - Synapse
    - Random: Pseudorandomly connect selected neurons such that each owns an average of `Mean degree` inbound synapses with weights in [`Min weight`, `Max weight`] from neurons at most `Reach` units away, preferring closer selected neurons unless `Reach` is `0` (accepts a `Seed`)
    - Chain: Directionally connect selected neurons, all with `Weight`, in the order you selected them in, optionally forming a loop with `Closed`
    - Fan in: Project all selected neurons into the first selected neuron with `Weight`
    - Fan out: Project the first selected neuron to all other selected neurons with `Weight`
    - Sever: Prune selected neurons' synapses according to `Mode`
      - `All`: all synapses that the selected neurons touch
      - `Selection`: only synapses completely within the selection (both neurons selected)
      - `Inbound`: only synapses that innervate at least one selected neuron
      - `Outbound`: only synapses that project from at least one selected neuron
  - Inject
    - Random: Inject `Current` (mV) into `Neurons` pseudorandom neurons (accepts a `Seed`)
    - Uniform: Inject `Current` into all selected neurons
  - Network
    - Reset: Purge all neurons and reset the simulation's elapsed time