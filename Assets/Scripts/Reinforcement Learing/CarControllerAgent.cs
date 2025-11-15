using System.Collections;
using UnityEngine;

/*
Used for RL Agent Run:
It is similar to the CarController script used for the Player Run, but with some modifications included below:
- The main difference is that the CarControllerAgent script uses the SetMovement method to set the movement of the car.
- This method is called by the RL Agent to control the car based on the action selected by the agent.
*/

public class CarControllerAgent : MonoBehaviour
{
    public float speed = 5f;
    public float moveSpeed = 3f;
    public float slipForce = 3f;
    public float slipDuration = 1f;
    public float jumpDuration = 1.5f;
    public GameObject car;
    public ProceduralTrackGeneration trackGenerator;
    public ScoreAgent scoreScript;

    private bool isSlipping = false;
    public bool isJumping = false;
    private Rigidbody2D rb;
    private float moveInput = 0f;
    private float slipVelocity = 0f;
    private float jumpScaleFactor = 1.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        // Find the ScoreAgent script in the scene 
        if (scoreScript == null)
        {
            scoreScript = FindObjectOfType<ScoreAgent>();
        }
    }

    void Update()
    {
        if (!isSlipping && !isJumping)
        {
            moveInput = 0f;
            if (Input.GetKey(KeyCode.LeftArrow)) moveInput = -moveSpeed;
            if (Input.GetKey(KeyCode.RightArrow)) moveInput = moveSpeed;
        }
    }

    void FixedUpdate()
    {
        rb.velocity = new Vector2(isSlipping ? slipVelocity : moveInput, -speed);
    }

    // Used by the RL Agent to set the movement of the car
    public void SetMovement(float movement)
    {
        moveInput = movement;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        Debug.Log($"Collision detected with: {other.gameObject.name}, Tag: {other.tag}");
        
        if (other.CompareTag("Wall"))
        {
            Debug.Log("Car crashed into a wall! Restarting...");
            RespawnCar();
        }

        if (other.CompareTag("Obstacle") && !isJumping)
        {
            Debug.Log("Car hit an obstacle!");
            RespawnCar();
        }

        if (other.CompareTag("Barrier") && !isJumping)
        {
            Debug.Log("Car hit a barrier!");
            RespawnCar();
        }


        if (other.CompareTag("OilSlick") && !isJumping)
        {
            Debug.Log("Car on Oil Slick!");
            StartCoroutine(OnSlip());
        }

        if (other.CompareTag("Ramp") && !isJumping)
        {
            Debug.Log("Car jumped on a ramp!");
            StartCoroutine(OnJump());
        }
    }

    private void RespawnCar()
    {
        ScoreAgent scoreScript = FindObjectOfType<ScoreAgent>();

        scoreScript.EndRun(); // Store score and check if game is over

        if (scoreScript.currentRun < 3)
        {
            trackGenerator = FindObjectOfType<ProceduralTrackGeneration>();

            if (trackGenerator != null)
            {
                trackGenerator.ResetTrack(); // Reset track before teleporting the car
            }
            else
            {
                Debug.LogError("Track Generator not found in the scene!");
            }

            // Reset car position
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.Euler(0, 0, 180); // rotation set at 180

            trackGenerator.ResetTrack();
            Debug.Log("Car reset to start position!");
        }
        else
        {
            Debug.Log("Game Over! No more runs left.");
        }
    }

    private IEnumerator OnSlip()
    {
        isSlipping = true;
        slipVelocity = (Random.Range(0, 2) * 2 - 1) * slipForce;

        float elapsedTime = 0f;
        while (elapsedTime < slipDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        slipVelocity = 0f;
        isSlipping = false;
    }

    private IEnumerator OnJump()
    {
        isJumping = true;
        Vector3 originalScale = transform.localScale;
        Vector3 jumpScale = originalScale * jumpScaleFactor;

        // Force horizontal velocity to zero and maintain it
        rb.velocity = new Vector2(0, rb.velocity.y);
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

        // First half of jump (scaling up)
        float elapsedTime = 0f;
        while (elapsedTime < jumpDuration / 2)
        {
            transform.localScale = Vector3.Lerp(originalScale, jumpScale, elapsedTime / (jumpDuration / 2));
            elapsedTime += Time.deltaTime;

            // Keep enforcing zero horizontal velocity
            rb.velocity = new Vector2(0, rb.velocity.y);
            yield return null;
        }

        transform.localScale = jumpScale;
        rb.velocity = new Vector2(0, speed * 1.2f);
        yield return new WaitForSeconds(jumpDuration / 2);

        // Second half of jump (scaling down)
        elapsedTime = 0f;
        while (elapsedTime < jumpDuration / 2)
        {
            transform.localScale = Vector3.Lerp(jumpScale, originalScale, elapsedTime / (jumpDuration / 2));
            elapsedTime += Time.deltaTime;

            // Keep enforcing zero horizontal velocity
            rb.velocity = new Vector2(0, rb.velocity.y);
            yield return null;
        }

        transform.localScale = new Vector3(1, 1, 0);
        rb.velocity = new Vector2(0, -speed);

        // Restore normal constraints when jump is complete
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        isJumping = false;
    }
}
