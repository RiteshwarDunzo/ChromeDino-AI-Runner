using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Neuroevolution
{
    /// <summary>
    /// Genetic neuroevolution (NEAT-lite) trainer that runs entirely inside the Unity Editor.
    /// Attach this to a scene object. It will NOT affect gameplay unless training or AI play is enabled.
    /// </summary>
    public class NeuroEvolutionTrainer : MonoBehaviour
    {
        [Header("Mode")]
        public bool trainingEnabled = false;
        public bool playWithBestGenome = false;
        public bool loadSavedBestOnStart = true;
        public bool saveBestEachGeneration = true;

        [Header("Training")]
        public int populationSize = 60;
        public int elites = 8;
        public int generations = 50;
        public float mutationRate = 0.10f;
        public float mutationStrength = 0.65f;
        public float episodeTimeoutSeconds = 30f;
        public float timeScaleDuringTraining = 15f;
        public int deterministicSeed = 12345;
        public bool deterministicPerGeneration = true;

        [Header("Fitness shaping")]
        public float obstacleClearedBonus = 25f;
        public float maxDistanceForClearDetection = 50f;

        [Header("Selection")]
        [Range(0f, 1f)] public float crossoverRate = 0.7f;

        [Header("Parallel evaluation (actual multi-agent)")]
        [Tooltip("If enabled, evaluates multiple genomes at the same time using multiple Player instances.")]
        public bool parallelEvaluation = false;

        [Tooltip("How many genomes to evaluate simultaneously.")]
        [Range(1, 30)] public int parallelAgents = 15;

        [Tooltip("If enabled, evaluates the ENTIRE population simultaneously (spawns populationSize players).")]
        public bool parallelRunWholePopulation = true;

        [Tooltip("Visual-only horizontal separation so you can see multiple players.")]
        [Range(0f, 1.5f)] public float parallelVisualSpreadX = 0.65f;

        [Header("Parallel rendering")]
        [Tooltip("Render all agents at the same X position while keeping them physically separated (prevents agent-agent collisions).")]
        public bool parallelRenderSameSpot = true;

        [Tooltip("How much to physically spread agents on X (only used when parallelRenderSameSpot is on).")]
        [Range(0f, 2.5f)] public float parallelPhysicalSpreadX = 1.25f;

        [Tooltip("If true, stack agents like a motion-trail (all behind the lead) instead of spreading left/right.")]
        public bool parallelStackTrail = true;

        [Tooltip("How far behind (in X) the last agent sits when using stack-trail mode.")]
        [Range(0f, 2.5f)] public float parallelTrailLengthX = 1.15f;

        [Tooltip("Opacity of the lead agent in parallel mode.")]
        [Range(0f, 1f)] public float parallelLeadOpacity = 1f;

        [Tooltip("Opacity of the last agent in parallel mode.")]
        [Range(0f, 1f)] public float parallelTailOpacity = 0.18f;

        private List<NeuroGenome> population;
        private NeuroGenome best;

        private DinoNeuroController controller;
        private readonly List<DinoNeuroController> parallelControllers = new();
        private readonly List<TrainingAgent> parallelAgentMarkers = new();
        private readonly List<Player> parallelPlayers = new();
        private readonly List<Player> parallelPool = new();
        private readonly List<Vector3> parallelBaseSpawnPositions = new();

        private int aliveAgents;
        public bool ParallelEvaluationActive { get; private set; }
        // Bump when input scaling / fitness logic changes to avoid loading incompatible "best" genomes.
        private const string BestGenomeKey = "neuro_best_genome_v3";

        [Header("UI Overlay")]
        public bool showOverlay = true;
        public KeyCode toggleOverlayKey = KeyCode.F8;
        [Tooltip("Scales overlay UI relative to a 1080p baseline. 1 = baseline, 0 = auto.")]
        public float overlayScale = 0f;

        private bool isTraining;
        private int currentGeneration;
        private float lastBestFitness;

        private void Awake()
        {
            controller = FindObjectOfType<DinoNeuroController>(true);
            if (controller == null)
            {
                var player = FindObjectOfType<Player>(true);
                if (player != null) controller = player.gameObject.AddComponent<DinoNeuroController>();
            }

            // Ensure we have enough agent instances ready if parallel evaluation is enabled.
            if (parallelEvaluation)
            {
                EnsureParallelAgents(GetDesiredParallelCount());
            }

            if (loadSavedBestOnStart)
            {
                best = LoadBestGenome();
            }
        }

        private void Start()
        {
            if (trainingEnabled)
                StartCoroutine(TrainLoop());
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleOverlayKey))
            {
                showOverlay = !showOverlay;
            }

            if (!trainingEnabled && playWithBestGenome && best != null && controller != null)
            {
                controller.SetGenome(best);
                controller.aiEnabled = true;
            }
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            // Relative sizing for high-DPI / 4K screens.
            float scale = overlayScale > 0f ? overlayScale : Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale) * oldMatrix;

            const float w = 460f;
            const float h = 320f;
            const float pad = 10f;
            var rect = new Rect(pad, pad, w, h);
            GUI.Box(rect, "Neuroevolution");

            GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 25, rect.width - 20, rect.height - 30));

            GUILayout.Label($"Overlay toggle: {toggleOverlayKey}");
            GUILayout.Space(6);

            GUILayout.Label($"Training: {(isTraining ? "RUNNING" : "OFF")}");
            GUILayout.Label($"Gen: {currentGeneration}/{generations}   BestFit: {lastBestFitness:0.00}");
            GUILayout.Label($"Pop: {populationSize}  elites: {elites}  mut: {mutationRate:0.00}  str: {mutationStrength:0.00}");
            GUILayout.Label($"Deterministic: {(deterministicPerGeneration ? "ON" : "OFF")}  seed: {deterministicSeed}");
            GUILayout.Label($"SpawnRate: {GetSpawnerInfo()}");

            GUILayout.Space(8);

            if (!isTraining)
            {
                if (GUILayout.Button("Start training"))
                {
                    trainingEnabled = true;
                    StartCoroutine(TrainLoop());
                }
            }
            else
            {
                if (GUILayout.Button("Stop training (end after current genome)"))
                {
                    trainingEnabled = false;
                }
            }

            playWithBestGenome = GUILayout.Toggle(playWithBestGenome, "Play with best genome");

            if (GUILayout.Button("Clear saved best genome"))
            {
                PlayerPrefs.DeleteKey(BestGenomeKey);
                PlayerPrefs.Save();
                best = null;
                lastBestFitness = 0f;
            }

            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        private string GetSpawnerInfo()
        {
            var spawner = FindObjectOfType<Spawner>(true);
            if (spawner == null) return "Spawner not found";
            return $"{spawner.minSpawnRate:0.0}-{spawner.maxSpawnRate:0.0}s";
        }

        private IEnumerator TrainLoop()
        {
            if (controller == null)
            {
                Debug.LogError("[NeuroEvolution] No DinoNeuroController found/created.");
                yield break;
            }

            isTraining = true;
            Time.timeScale = timeScaleDuringTraining;

            population = new List<NeuroGenome>(populationSize);
            for (int i = 0; i < populationSize; i++)
                population.Add(new NeuroGenome(initRandom: true));

            for (int gen = 0; gen < generations; gen++)
            {
                currentGeneration = gen + 1;
                if (deterministicPerGeneration)
                {
                    // Makes evaluation more stable (genomes compared under similar obstacle sequences).
                    Random.InitState(deterministicSeed + gen);
                }

                // Make obstacle spawns deterministic for the whole generation.
                var spawner = FindObjectOfType<Spawner>(true);
                if (spawner != null && deterministicPerGeneration)
                {
                    spawner.SetDeterministic(true, deterministicSeed + gen);
                }

                // Evaluate
                if (parallelEvaluation)
                {
                    EnsureParallelAgents(GetDesiredParallelCount());
                    yield return EvaluatePopulationParallel(population, GetDesiredParallelCount());
                }
                else
                {
                    foreach (var g in population)
                    {
                        yield return EvaluateGenome(g);
                    }
                }

                population = population.OrderByDescending(g => g.fitness).ToList();
                best = population[0].Clone();
                lastBestFitness = best.fitness;

                Debug.Log($"[NeuroEvolution] Gen {gen + 1}/{generations} best fitness={best.fitness:0.00}");

                if (saveBestEachGeneration)
                {
                    SaveBestGenome(best);
                }

                // Breed next
                var next = new List<NeuroGenome>(populationSize);

                for (int i = 0; i < Mathf.Min(elites, population.Count); i++)
                    next.Add(population[i].Clone());

                while (next.Count < populationSize)
                {
                    var p1 = TournamentSelect(population);
                    NeuroGenome child;

                    if (Random.value < crossoverRate)
                    {
                        var p2 = TournamentSelect(population);
                        child = NeuroGenome.Crossover(p1, p2);
                    }
                    else
                    {
                        child = p1.Clone();
                    }

                    child.Mutate(mutationRate, mutationStrength);
                    next.Add(child);
                }

                population = next;

                // Return spawner to normal mode between generations (keeps gameplay unchanged).
                if (spawner != null)
                {
                    spawner.SetDeterministic(false, 0);
                }
                if (!trainingEnabled) break;
            }

            Time.timeScale = 1f;
            trainingEnabled = false;
            playWithBestGenome = true;
            isTraining = false;
        }

        public void NotifyAgentDied(int agentIndex)
        {
            // Called by TrainingAgent on obstacle hit.
            aliveAgents = Mathf.Max(0, aliveAgents - 1);

            if (agentIndex >= 0 && agentIndex < parallelControllers.Count)
            {
                var c = parallelControllers[agentIndex];
                if (c != null) c.aiEnabled = false;
            }
            if (agentIndex >= 0 && agentIndex < parallelPlayers.Count)
            {
                var p = parallelPlayers[agentIndex];
                if (p != null) p.enabled = false;
            }
        }

        private int GetDesiredParallelCount()
        {
            if (!parallelEvaluation) return 0;
            if (parallelRunWholePopulation) return Mathf.Max(1, populationSize);
            return Mathf.Clamp(parallelAgents, 1, 512);
        }

        private void EnsureParallelAgents(int desiredCount)
        {
            parallelControllers.Clear();
            parallelAgentMarkers.Clear();
            parallelPlayers.Clear();

            var templatePlayer = controller != null ? controller.GetComponent<Player>() : null;
            if (templatePlayer == null)
            {
                templatePlayer = FindObjectOfType<Player>(true);
            }
            if (templatePlayer == null)
            {
                Debug.LogError("[NeuroEvolution] Parallel eval: no Player found to clone.");
                return;
            }

            desiredCount = Mathf.Max(1, desiredCount);

            // Ensure pool exists up to desiredCount.
            while (parallelPool.Count < desiredCount)
            {
                int i = parallelPool.Count;
                Player p;
                if (i == 0)
                {
                    p = templatePlayer;
                }
                else
                {
                    var go = Instantiate(templatePlayer.gameObject);
                    go.name = $"ParallelAgent_{i:000}";
                    p = go.GetComponent<Player>();
                }

                if (p != null) parallelPool.Add(p);
                else
                {
                    Debug.LogError("[NeuroEvolution] Parallel eval: failed to create Player clone.");
                    break;
                }

                parallelBaseSpawnPositions.Add(p.spawnPosition);
            }

            ConfigureParallelAgentCollisions();

            // Activate only the desired ones, keep the rest hidden.
            for (int i = 0; i < parallelPool.Count; i++)
            {
                var p = parallelPool[i];
                bool active = i < desiredCount;
                if (p == null) continue;
                p.gameObject.SetActive(active);
            }

            for (int i = 0; i < desiredCount; i++)
            {
                var p = parallelPool[i];
                if (p == null) continue;

                var c = p.GetComponent<DinoNeuroController>();
                if (c == null) c = p.gameObject.AddComponent<DinoNeuroController>();

                var agent = p.GetComponent<TrainingAgent>();
                if (agent == null) agent = p.gameObject.AddComponent<TrainingAgent>();
                agent.trainer = this;
                agent.agentIndex = i;
                agent.ResetAlive();

                // Visual-only separation so you can see multiple players even when grounded.
                var vo = p.GetComponent<TrainingVisualOffset>();
                if (vo == null) vo = p.gameObject.AddComponent<TrainingVisualOffset>();
                vo.offset = new Vector3(ComputeParallelVisualOffsetX(i, desiredCount), 0f, 0f);

                var vis = p.GetComponent<TrainingAgentVisual>();
                if (vis == null) vis = p.gameObject.AddComponent<TrainingAgentVisual>();
                vis.Configure(
                    alpha: ComputeParallelOpacity(i, desiredCount),
                    orderOffset: -i * 5
                );

                parallelPlayers.Add(p);
                parallelControllers.Add(c);
                parallelAgentMarkers.Add(agent);
            }
        }

        private void ConfigureParallelAgentCollisions()
        {
            for (int i = 0; i < parallelPool.Count; i++)
            {
                var a = parallelPool[i];
                if (a == null) continue;

                var aColliders = a.GetComponentsInChildren<Collider>(true);
                if (aColliders == null || aColliders.Length == 0) continue;

                for (int j = i + 1; j < parallelPool.Count; j++)
                {
                    var b = parallelPool[j];
                    if (b == null) continue;

                    var bColliders = b.GetComponentsInChildren<Collider>(true);
                    if (bColliders == null || bColliders.Length == 0) continue;

                    for (int ca = 0; ca < aColliders.Length; ca++)
                    {
                        var colliderA = aColliders[ca];
                        if (colliderA == null) continue;

                        for (int cb = 0; cb < bColliders.Length; cb++)
                        {
                            var colliderB = bColliders[cb];
                            if (colliderB == null) continue;
                            Physics.IgnoreCollision(colliderA, colliderB, true);
                        }
                    }
                }
            }
        }

        private float ComputeParallelVisualOffsetX(int index, int count)
        {
            if (count <= 1) return 0f;
            float t = index / (float)(count - 1);

            if (parallelRenderSameSpot)
            {
                // Visuals will be offset back to the lead so all render at the same spot.
                return -ComputeParallelPhysicalOffsetX(index, count);
            }

            if (parallelStackTrail)
            {
                // Stack like a trailing silhouette behind the lead.
                return -Mathf.Lerp(0f, parallelTrailLengthX, t);
            }

            // Spread agents across [-spread, +spread] in a stable order.
            return Mathf.Lerp(-parallelVisualSpreadX, parallelVisualSpreadX, t);
        }

        private float ComputeParallelPhysicalOffsetX(int index, int count)
        {
            if (count <= 1) return 0f;
            float t = index / (float)(count - 1);
            float spread = parallelPhysicalSpreadX > 0f ? parallelPhysicalSpreadX : 1.25f;
            // Spread physically so CharacterControllers don't collide with each other.
            return Mathf.Lerp(-spread, spread, t);
        }

        private float ComputeParallelOpacity(int index, int count)
        {
            if (count <= 1) return parallelLeadOpacity;
            float t = index / (float)(count - 1);
            return Mathf.Lerp(parallelLeadOpacity, parallelTailOpacity, t);
        }

        private IEnumerator EvaluatePopulationParallel(List<NeuroGenome> pop, int desiredCount)
        {
            ParallelEvaluationActive = true;

            int batchSize = Mathf.Min(desiredCount, pop.Count);
            for (int startIndex = 0; startIndex < pop.Count; startIndex += batchSize)
            {
                int n = Mathf.Min(batchSize, pop.Count - startIndex);
                yield return EvaluateBatchParallel(pop, startIndex, n);
            }

            ParallelEvaluationActive = false;
        }

        private IEnumerator EvaluateBatchParallel(List<NeuroGenome> pop, int startIndex, int n)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.NewGame();

            // Reset and assign genomes.
            for (int i = 0; i < n; i++)
            {
                var p = i < parallelPlayers.Count ? parallelPlayers[i] : null;
                var c = i < parallelControllers.Count ? parallelControllers[i] : null;
                var a = i < parallelAgentMarkers.Count ? parallelAgentMarkers[i] : null;

                if (p == null) continue;

                // Physically separate agents so each real runner gets its own lane.
                var baseSpawn = i < parallelBaseSpawnPositions.Count ? parallelBaseSpawnPositions[i] : p.spawnPosition;
                float physDx = ComputeParallelPhysicalOffsetX(i, n);
                p.spawnPosition = new Vector3(baseSpawn.x + physDx, baseSpawn.y, baseSpawn.z);

                p.ResetForRun();
                p.enabled = true;
                if (a != null) a.ResetAlive();

                var g = pop[startIndex + i];
                if (c != null)
                {
                    c.SetGenome(g);
                    c.aiEnabled = true;
                }
            }

            // Disable unused pooled agents (if any).
            for (int i = n; i < parallelPlayers.Count; i++)
            {
                var p = parallelPlayers[i];
                var c = parallelControllers[i];
                if (c != null) c.aiEnabled = false;
                if (p != null) p.enabled = false;
            }

            aliveAgents = n;

            float t = 0f;
            var cleared = new int[n];
            var trackedId = new int[n];
            var tracked = new Obstacle[n];
            var aliveTime = new float[n];

            while (t < episodeTimeoutSeconds && aliveAgents > 0)
            {
                // Do NOT break on GameOver; in parallel mode we handle "death" per agent.
                for (int i = 0; i < n; i++)
                {
                    var agent = i < parallelAgentMarkers.Count ? parallelAgentMarkers[i] : null;
                    if (agent == null || !agent.isAlive) continue;

                    aliveTime[i] += Time.unscaledDeltaTime;

                    // Count clears for this agent.
                    float px = parallelControllers[i] != null ? parallelControllers[i].transform.position.x : 0f;
                    if (tracked[i] == null || trackedId[i] == 0)
                    {
                        tracked[i] = FindNextObstacleAhead(px);
                        trackedId[i] = tracked[i] != null ? tracked[i].GetInstanceID() : 0;
                    }
                    else
                    {
                        if (tracked[i] == null)
                        {
                            trackedId[i] = 0;
                        }
                        else if (tracked[i].transform.position.x < px)
                        {
                            cleared[i]++;
                            tracked[i] = null;
                            trackedId[i] = 0;
                        }
                    }
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Compute fitness for this batch.
            for (int i = 0; i < n; i++)
            {
                var g = pop[startIndex + i];
                float fit = aliveTime[i] + (cleared[i] * obstacleClearedBonus);
                // Small tie-breaker similar to single-agent path.
                fit += aliveTime[i] * 0.1f;
                g.fitness = Mathf.Max(0f, fit);
            }

            // Disable AI controllers after batch.
            for (int i = 0; i < n; i++)
            {
                if (i < parallelControllers.Count && parallelControllers[i] != null)
                    parallelControllers[i].aiEnabled = false;
            }
        }

        private NeuroGenome TournamentSelect(List<NeuroGenome> pop, int k = 5)
        {
            NeuroGenome bestLocal = null;
            for (int i = 0; i < k; i++)
            {
                var g = pop[Random.Range(0, pop.Count)];
                if (bestLocal == null || g.fitness > bestLocal.fitness) bestLocal = g;
            }
            return bestLocal;
        }

        private IEnumerator EvaluateGenome(NeuroGenome g)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.NewGame();

            controller.SetGenome(g);
            controller.aiEnabled = true;

            float startScore = GameManager.Instance != null ? GameManager.Instance.Score : 0f;
            float t = 0f;
            int cleared = 0;
            int trackedId = 0;
            Obstacle tracked = null;

            while (t < episodeTimeoutSeconds)
            {
                // Game over is represented by gameSpeed == 0 (in your GameManager).
                if (GameManager.Instance != null && GameManager.Instance.gameSpeed <= 0f) break;

                // Count obstacle clears (selection pressure).
                float px = controller != null ? controller.transform.position.x : 0f;
                if (tracked == null || trackedId == 0)
                {
                    tracked = FindNextObstacleAhead(px);
                    trackedId = tracked != null ? tracked.GetInstanceID() : 0;
                }
                else
                {
                    if (tracked == null)
                    {
                        trackedId = 0;
                    }
                    else if (tracked.transform.position.x < px)
                    {
                        cleared++;
                        tracked = null;
                        trackedId = 0;
                    }
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            float endScore = GameManager.Instance != null ? GameManager.Instance.Score : t;
            // Fitness: primarily score, with a small survival term to break ties.
            g.fitness = Mathf.Max(0f, (endScore - startScore) + (t * 0.1f) + (cleared * obstacleClearedBonus));

            controller.aiEnabled = false;
        }

        private Obstacle FindNextObstacleAhead(float playerX)
        {
            var obstacles = Object.FindObjectsOfType<Obstacle>();
            if (obstacles == null || obstacles.Length == 0) return null;

            Obstacle best = null;
            float bestDx = float.MaxValue;
            foreach (var o in obstacles)
            {
                if (o == null) continue;
                float dx = o.transform.position.x - playerX;
                if (dx <= 0f) continue;
                if (dx > maxDistanceForClearDetection) continue;
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = o;
                }
            }
            return best;
        }

        [System.Serializable]
        private class GenomeSave
        {
            public float[] w1;
            public float[] w2;
            public float fitness;
        }

        private void SaveBestGenome(NeuroGenome g)
        {
            if (g == null) return;
            var s = new GenomeSave { w1 = g.w1, w2 = g.w2, fitness = g.fitness };
            PlayerPrefs.SetString(BestGenomeKey, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        private NeuroGenome LoadBestGenome()
        {
            if (!PlayerPrefs.HasKey(BestGenomeKey)) return null;
            try
            {
                var json = PlayerPrefs.GetString(BestGenomeKey, "");
                if (string.IsNullOrEmpty(json)) return null;
                var s = JsonUtility.FromJson<GenomeSave>(json);
                if (s?.w1 == null || s?.w2 == null) return null;

                var g = new NeuroGenome(false) { fitness = s.fitness };
                if (s.w1.Length == g.w1.Length) s.w1.CopyTo(g.w1, 0);
                if (s.w2.Length == g.w2.Length) s.w2.CopyTo(g.w2, 0);
                return g;
            }
            catch
            {
                return null;
            }
        }
    }
}
