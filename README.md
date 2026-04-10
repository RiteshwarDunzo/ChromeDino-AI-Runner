# Chrome Dino AI

A Unity recreation of the Chrome dinosaur runner with an in-editor neuroevolution system that learns when to jump using a genetic algorithm.

This project combines:

- classic endless-runner gameplay
- obstacle spawning and score progression
- audio and UI feedback
- an AI trainer that evolves neural-network weights directly in C#

## Features

- Playable Chrome Dino-style runner in Unity
- Increasing difficulty as game speed rises over time
- Persistent high score plus current session score
- Neuroevolution trainer built directly into the Unity scene
- Parallel population evaluation for faster experimentation
- Overlay controls for training, playback, and debugging

## Tech Stack

- Unity 6 (`6000.5.0a7`)
- C#
- Unity gameplay, UI, audio, and editor tooling

## Project Structure

- `Assets/Scenes/Game.unity` - main gameplay scene
- `Assets/Scripts/` - gameplay systems such as player movement, scoring, spawning, and game flow
- `Assets/Scripts/AI/Neuroevolution/` - genome, controller, trainer, and training helpers
- `ProjectSettings/` - Unity project configuration
- `reporting/` - generated diagrams and report assets

## How To Run

### Play the game

1. Open the project in Unity Hub.
2. Use Unity `6000.5.0a7` if possible.
3. Open `Assets/Scenes/Game.unity`.
4. Press Play in the Unity Editor.
5. Use `Space` or Unity's default `Jump` input to avoid obstacles.

### Run the AI

The AI system is already implemented in the project. It does not require Python or ML-Agents.

1. Open `Assets/Scenes/Game.unity`.
2. Select the GameObject that contains `NeuroEvolutionTrainer`.
3. Choose one of these modes in the Inspector:
   - Enable `trainingEnabled` to evolve genomes
   - Enable `playWithBestGenome` to let the best saved genome control the dino
4. Press Play.
5. Use the in-game overlay to start or stop training.
6. Press `F8` to toggle the training overlay if needed.

### Suggested training settings

Good starting values from the current trainer:

- Population size: `60`
- Elites: `8`
- Generations: `50`
- Mutation rate: `0.10`
- Mutation strength: `0.65`
- Crossover rate: `0.7`
- Episode timeout: `30s`
- Time scale during training: `15`

Parallel evaluation can be enabled to test many genomes at once inside the same scene.

## How the Genetic Algorithm Works

This project uses a lightweight neuroevolution approach. Instead of training with backpropagation, it evolves the weights of a small neural network over many generations.

### 1. Each genome is a neural network

Each `NeuroGenome` stores the weights for a fixed network:

- `9` inputs
- `8` hidden neurons
- `2` outputs

The outputs represent:

- whether the dino should press jump
- whether the dino should keep holding jump

### 2. The AI observes game state

The controller feeds hand-crafted gameplay values into the network, including:

- whether the dino is grounded
- vertical velocity
- distance to the next obstacle
- obstacle width and height
- dino width and height
- current game speed
- game speed increase rate

This means the AI learns from game state, not from screen pixels.

### 3. A full population is evaluated

At the start of training, the trainer creates a population of random genomes. Each genome gets a run in the game and receives a fitness score based on how well it survives.

Fitness is shaped mainly by:

- survival time and score progress
- obstacles cleared
- a small tie-break bonus for lasting longer

### 4. The best genomes are selected

After evaluation, genomes are ranked by fitness. The strongest ones are more likely to become parents.

This trainer uses:

- elitism, so the top genomes are copied directly into the next generation
- tournament selection, so parent choice favors better performers without always picking only the single best genome

### 5. Children are created with crossover

New genomes are produced by combining weights from two parents.

This project uses uniform crossover:

- each weight in the child is taken from either parent A or parent B

### 6. Mutation adds variation

After crossover, random weights are perturbed.

Mutation is what lets the population explore new behaviors, such as:

- jumping earlier
- holding jump longer
- reacting differently at higher speeds

### 7. The cycle repeats

Evaluation, selection, crossover, and mutation repeat for many generations. Over time, the population improves and the trainer saves the best genome using `PlayerPrefs`.

When `playWithBestGenome` is enabled, the dino uses the strongest saved genome for autonomous play.

## Training Notes

- Training runs entirely inside the Unity Editor.
- Deterministic spawning can be enabled per generation so genomes are compared on similar obstacle sequences.
- Parallel evaluation can spawn many dino agents at once for faster iteration.
- Birds are currently skipped by the spawner.
- The best genome is saved using `PlayerPrefs`.

## Demo Video

Add your final demo here using one of these options.

### Option 1: Link to a hosted video

Replace this with your real link:

`[Watch the demo video](https://your-video-link.com)`

### Option 2: Reference your MP4 in the repo

If you add your video file to the repository, update this section with its path:

- Demo file: `demo.mp4`
- Example path in repo: `assets/demo.mp4`

GitHub does not reliably play local MP4 files inline inside a README, so the usual approach is:

1. Add the MP4 to the repo or a release.
2. Link to it from the README.
3. Optionally add a screenshot thumbnail that links to the video.

Example:

```md
[Download or watch the demo video](assets/demo.mp4)
```

### Option 3: Clickable thumbnail

If you add a preview image, you can make it clickable:

```md
[![Demo Video Preview](assets/demo-thumbnail.png)](assets/demo.mp4)
```

Replace the file names with your actual image and video paths.

## Future Improvements

- Save multiple checkpoints instead of only the best genome
- Add charts for per-generation fitness trends
- Compare sequential versus parallel evaluation results
- Reintroduce more obstacle variety such as birds
- Export trained genomes in a reusable format

## Author

Riteshwar Singh
