using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    private CharacterController character;
    private float verticalVelocity;
    private float jumpHeldTime;
    private bool aiJumpHeld;
    private bool prevAiJumpHeld;

    [Header("Jump")]
    [Tooltip("Initial upward velocity when jump starts.")]
    public float jumpVelocity = 8f;
    [Tooltip("Extra upward acceleration applied while holding jump.")]
    public float jumpHoldAcceleration = 25f;
    [Tooltip("Maximum time (seconds) jump hold affects height.")]
    public float maxJumpHoldTime = 0.12f;
    [Tooltip("Gravity acceleration (positive value).")]
    public float gravity = 9.81f * 2f;
    [Tooltip("Extra gravity multiplier when jump is released early (short hop).")]
    public float jumpCutGravityMultiplier = 2.0f;

    [Header("Spawn / Alignment")]
    public Vector3 spawnPosition = new Vector3(-4.5f, 1.0f, 0f);

    private void Awake()
    {
        character = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        ResetForRun();
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 10f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            float halfHeight = Mathf.Max(0.01f, character.height * 0.5f);
            // Place the controller so its bottom sits on the hit point.
            transform.position = new Vector3(transform.position.x, hit.point.y + halfHeight, transform.position.z);
        }
    }

    private void Update()
    {
        bool grounded = character.isGrounded;
        bool humanHeld = Input.GetButton("Jump");
        bool humanDown = Input.GetButtonDown("Jump");

        bool aiDown = aiJumpHeld && !prevAiJumpHeld;
        prevAiJumpHeld = aiJumpHeld;

        bool jumpHeld = aiJumpHeld || humanHeld;
        bool jumpDown = aiDown || humanDown;

        if (grounded)
        {
            // Keep grounded stable.
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            jumpHeldTime = 0f;

            if (jumpDown)
            {
                verticalVelocity = jumpVelocity;
                // Avoid spamming SFX during parallel training runs.
                if (GameManager.Instance != null && GetComponent<Neuroevolution.TrainingAgent>() == null)
                    GameManager.Instance.PlayJumpSfx();
            }
        }
        else
        {
            // Holding jump increases height for a short window.
            if (jumpHeld && jumpHeldTime < maxJumpHoldTime && verticalVelocity > 0f)
            {
                verticalVelocity += jumpHoldAcceleration * Time.deltaTime;
                jumpHeldTime += Time.deltaTime;
            }
        }

        float gravityMultiplier = 1f;
        if (!grounded && !jumpHeld && verticalVelocity > 0f)
        {
            // Released early while going up -> shorter jump.
            gravityMultiplier = jumpCutGravityMultiplier;
        }

        verticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;

        character.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    public void SetAIJumpHeld(bool held)
    {
        aiJumpHeld = held;
    }

    public void ResetForRun()
    {
        verticalVelocity = 0f;
        jumpHeldTime = 0f;
        aiJumpHeld = false;
        prevAiJumpHeld = false;

        if (character == null)
        {
            character = GetComponent<CharacterController>();
        }

        if (character != null)
        {
            character.enabled = false;
        }

        // Keep the dino at a consistent starting spot in the Game scene,
        // then snap down onto the ground if we can find it.
        transform.position = spawnPosition;

        if (character != null)
        {
            character.enabled = true;
        }

        SnapToGround();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle")) {
            // If this Player is being driven as a parallel-training agent, don't end the whole game.
            var agent = GetComponent<Neuroevolution.TrainingAgent>();
            if (agent != null && agent.HandleHitObstacle())
                return;

            GameManager.Instance.GameOver();
        }
    }

}
