using UnityEngine;

namespace Neuroevolution
{
    /// <summary>
    /// Marks a Player instance as a parallel-training agent and reports deaths to the trainer.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingAgent : MonoBehaviour
    {
        public NeuroEvolutionTrainer trainer;
        public int agentIndex;

        public bool isAlive { get; private set; } = true;

        public void ResetAlive()
        {
            isAlive = true;
        }

        /// <summary>
        /// Returns true if the obstacle hit was handled by training (and should not call GameOver()).
        /// </summary>
        public bool HandleHitObstacle()
        {
            if (trainer == null) return false;
            if (!trainer.ParallelEvaluationActive) return false;
            if (!isAlive) return true;

            isAlive = false;
            trainer.NotifyAgentDied(agentIndex);
            return true;
        }
    }
}

