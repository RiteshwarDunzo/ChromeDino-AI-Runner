using UnityEngine;

namespace Neuroevolution
{
    /// <summary>
    /// Runs a NeuroGenome policy to control the existing Player by "holding jump".
    /// Does not affect gameplay unless aiEnabled is true.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class DinoNeuroController : MonoBehaviour
    {
        public bool aiEnabled = false;

        [Tooltip("Jump output threshold for pressing jump.")]
        public float jumpPressThreshold = 0.6f;

        [Tooltip("Jump hold output threshold for holding jump.")]
        public float jumpHoldThreshold = 0.5f;

        private NeuroGenome genome;
        private Player player;
        private CharacterController cc;

        private float lastY;
        private float approxYVel;

        private readonly float[] inputs = new float[NeuroGenome.InputCount];
        private readonly float[] outputs = new float[NeuroGenome.OutputCount];

        private bool jumpHeld;

        private static float ToSigned(float zeroToOne) => (zeroToOne * 2f) - 1f;

        private void Awake()
        {
            player = GetComponent<Player>();
            cc = GetComponent<CharacterController>();
            lastY = transform.position.y;
        }

        public void SetGenome(NeuroGenome g)
        {
            genome = g;
        }

        private void Update()
        {
            if (!aiEnabled || genome == null || player == null) return;

            float y = transform.position.y;
            approxYVel = (y - lastY) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastY = y;

            bool grounded = cc != null && cc.isGrounded;

            // Inputs (normalized-ish):
            // 0: grounded (0/1)
            // 1: y velocity
            // 2: distance to next tree (x)
            // 3: tree width
            // 4: tree height
            // 5: dino width
            // 6: dino height
            // 7: game speed
            // 8: game speed increase
            inputs[0] = grounded ? 1f : -1f;
            inputs[1] = Mathf.Clamp(approxYVel / 10f, -1f, 1f);

            float gameSpeed = GameManager.Instance != null ? GameManager.Instance.gameSpeed : 0f;
            float gameSpeedInc = GameManager.Instance != null ? GameManager.Instance.gameSpeedIncrease : 0f;
            inputs[7] = ToSigned(Mathf.Clamp01(gameSpeed / 20f));
            inputs[8] = ToSigned(Mathf.Clamp01(gameSpeedInc / 2f));

            float dinoW = 1f;
            float dinoH = 1f;
            if (cc != null)
            {
                dinoW = cc.radius * 2f;
                dinoH = cc.height;
            }
            inputs[5] = ToSigned(Mathf.Clamp01(dinoW / 2f));
            inputs[6] = ToSigned(Mathf.Clamp01(dinoH / 3f));

            var next = FindNextObstacle();
            if (next == null)
            {
                inputs[2] = 1f;   // far
                inputs[3] = -1f;  // unknown width
                inputs[4] = -1f;  // unknown height
            }
            else
            {
                float dxWorld = next.transform.position.x - transform.position.x;
                inputs[2] = ToSigned(Mathf.Clamp01(dxWorld / 20f));

                float treeW = 0.5f;
                float treeH = 0.5f;
                var col = next.GetComponent<Collider>();
                if (col != null)
                {
                    var s = col.bounds.size;
                    treeW = s.x;
                    treeH = s.y;
                }
                inputs[3] = ToSigned(Mathf.Clamp01(treeW / 2f));
                inputs[4] = ToSigned(Mathf.Clamp01(treeH / 3f));
            }

            genome.Evaluate(inputs, outputs);

            bool wantPress = outputs[0] >= jumpPressThreshold;
            bool wantHold = outputs[1] >= jumpHoldThreshold;

            if (grounded)
            {
                jumpHeld = wantPress;
            }
            else
            {
                jumpHeld = wantHold;
            }

            player.SetAIJumpHeld(jumpHeld);
        }

        private Obstacle FindNextObstacle()
        {
            // Minimal scan; acceptable for this game size.
            var obstacles = Object.FindObjectsOfType<Obstacle>();
            if (obstacles == null || obstacles.Length == 0) return null;

            float px = transform.position.x;
            Obstacle best = null;
            float bestX = float.MaxValue;
            foreach (var o in obstacles)
            {
                if (o == null) continue;
                float ox = o.transform.position.x;
                if (ox <= px) continue;
                if (ox < bestX)
                {
                    bestX = ox;
                    best = o;
                }
            }
            return best;
        }
    }
}

