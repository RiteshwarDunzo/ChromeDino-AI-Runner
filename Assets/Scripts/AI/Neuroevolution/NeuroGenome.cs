using System;
using UnityEngine;

namespace Neuroevolution
{
    [Serializable]
    public class NeuroGenome
    {
        // Simple NEAT-like genome: fixed topology neural net weights (mutation/crossover).
        // Inputs: 9, Hidden: 8, Outputs: 2 (jump_pressed, jump_hold)
        // Inputs include: grounded, yVel, distanceToTree, treeWidth, treeHeight, dinoWidth, dinoHeight, gameSpeed, gameSpeedIncrease
        public const int InputCount = 9;
        public const int HiddenCount = 8;
        public const int OutputCount = 2;

        // Layer weights: (inputs+1 bias)->hidden, (hidden+1 bias)->output
        public float[] w1; // (InputCount+1)*HiddenCount
        public float[] w2; // (HiddenCount+1)*OutputCount

        public float fitness;

        public NeuroGenome(bool initRandom)
        {
            w1 = new float[(InputCount + 1) * HiddenCount];
            w2 = new float[(HiddenCount + 1) * OutputCount];
            if (initRandom) Randomize();
        }

        public void Randomize()
        {
            for (int i = 0; i < w1.Length; i++) w1[i] = UnityEngine.Random.Range(-1f, 1f);
            for (int i = 0; i < w2.Length; i++) w2[i] = UnityEngine.Random.Range(-1f, 1f);
        }

        public NeuroGenome Clone()
        {
            var g = new NeuroGenome(false);
            Array.Copy(w1, g.w1, w1.Length);
            Array.Copy(w2, g.w2, w2.Length);
            g.fitness = fitness;
            return g;
        }

        public static NeuroGenome Crossover(NeuroGenome a, NeuroGenome b)
        {
            // Uniform crossover
            var child = new NeuroGenome(false);
            for (int i = 0; i < child.w1.Length; i++)
                child.w1[i] = UnityEngine.Random.value < 0.5f ? a.w1[i] : b.w1[i];
            for (int i = 0; i < child.w2.Length; i++)
                child.w2[i] = UnityEngine.Random.value < 0.5f ? a.w2[i] : b.w2[i];
            return child;
        }

        public void Mutate(float mutationRate, float mutationStrength)
        {
            for (int i = 0; i < w1.Length; i++)
            {
                if (UnityEngine.Random.value < mutationRate)
                    w1[i] += UnityEngine.Random.Range(-mutationStrength, mutationStrength);
            }
            for (int i = 0; i < w2.Length; i++)
            {
                if (UnityEngine.Random.value < mutationRate)
                    w2[i] += UnityEngine.Random.Range(-mutationStrength, mutationStrength);
            }
        }

        public void Evaluate(float[] inputs, float[] outputs)
        {
            if (inputs.Length != InputCount) throw new ArgumentException("inputs length");
            if (outputs.Length != OutputCount) throw new ArgumentException("outputs length");

            float[] hidden = new float[HiddenCount];

            // input->hidden
            for (int h = 0; h < HiddenCount; h++)
            {
                float sum = 0f;
                int baseIdx = h * (InputCount + 1);
                for (int i = 0; i < InputCount; i++)
                    sum += inputs[i] * w1[baseIdx + i];
                sum += 1f * w1[baseIdx + InputCount]; // bias
                hidden[h] = Tanh(sum);
            }

            // hidden->output
            for (int o = 0; o < OutputCount; o++)
            {
                float sum = 0f;
                int baseIdx = o * (HiddenCount + 1);
                for (int h = 0; h < HiddenCount; h++)
                    sum += hidden[h] * w2[baseIdx + h];
                sum += 1f * w2[baseIdx + HiddenCount]; // bias
                outputs[o] = Sigmoid(sum);
            }
        }

        private static float Sigmoid(float x) => 1f / (1f + Mathf.Exp(-x));
        private static float Tanh(float x) => (float)Math.Tanh(x);
    }
}

