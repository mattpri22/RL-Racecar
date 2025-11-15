using System.Collections;
using UnityEngine;

// Used for Player Run 
public class CarController : MonoBehaviour
{
    public float speed = 5f;
    public float moveSpeed = 3f;
    public float slipForce = 3f;
    public float slipDuration = 1f;
    public float jumpDuration = 1.5f;
    public GameObject car;
    public ProceduralTrackGeneration trackGenerator;
    public ScoreScript scoreScript;

    private bool isSlipping = false;
    private bool isJumping = false;
    private Rigidbody2D rb;
    private float moveInput = 0f;
    private float slipVelocity = 0f;
    private float jumpScaleFactor = 1.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
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

    private void OnTriggerEnter2D(Collider2D other)
    {
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
        ScoreScript scoreScript = FindObjectOfType<ScoreScript>();

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

    // When player hits an oil slick, the car will slip for a short duration
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

    // When player hits a ramp, the car will jump and become 'invisible' for a short duration
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
