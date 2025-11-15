using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections.Generic;

/*
To launch the training process, follow these steps:

- Download the ML-Agents toolkit from the GitHub repository:
1) Go to config location inside the unity project and open a terminal window
2) "pipenv shell" to create a virtual environment (Prerequisite: Downgrade protobuf to 3.11.4 using "pip install protobuf==3.20")
3) "mlagents-learn config.yaml --run-id={Name of the run}"
4) "mlagents-learn config.yaml --run-id={Name of the run} --resume" to continue training from the same file
    - To start a new training run with an old model:
    "mlagents-learn config.yaml --run-id={NewRunID} --initialize-from={OldRunID}"
5) Create a new 'pipenv shell' in the terminal and "tensorboard --logdir=results" to visualize the training process

- To train multiple agents in parallel given that 'Builds' and Unity Project are in the same level in the directory and you currently in config location terminal:
mlagents-learn config.yaml --run-id=RacecarAgentTest --env ../../../Builds/RLAgent.app --num-envs=4 --no-graphics
*/

public class CarAgent : Agent // Inherits from the Agent class
{
    public CarControllerAgent carController; // To handle movement
    private Vector3 startPosition; // Initial position for resets
    private Quaternion startRotation; // Initial rotation for resets
    private float distanceTraveled = 0f; // Distance traveled by the agent for metrics
    private float lastDistanceReward = 0f; 
    private const float DISTANCE_REWARD_INTERVAL = 5f; // Reward interval for distance traveled

    // FOV visualization parameters
    private float rayLength = 20f;
    private int numRays = 41;
    private float startAngle = 30f; // Start 30 degrees to the right
    private float endAngle = -210f; // End 210 degrees to the left

    // Removing jitter
    private float smoothedMoveInput = 0f;
    private float smoothingFactor = 0.2f;

    // Capture the starting position an rotation when the agent is created
    public override void Initialize()
    {
        carController = GetComponent<CarControllerAgent>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    // Resets everything when a new training episode starts
    public override void OnEpisodeBegin()
    {
        // Reset the car and track
        transform.position = startPosition;
        transform.rotation = startRotation;
        distanceTraveled = 0f;
        lastDistanceReward = 0f;

        if (carController != null && carController.trackGenerator != null)
        {
            carController.trackGenerator.ResetTrack(); // Regenerate track
        }
    }

    // Collects observations from the environment - how the agent sees
    public override void CollectObservations(VectorSensor sensor)
    {
        // Current position and velocity
        sensor.AddObservation(transform.position.x);
        sensor.AddObservation(transform.position.y);

        if (carController != null)
        {
            var rb = carController.GetComponent<Rigidbody2D>();
            sensor.AddObservation(rb.velocity.x);
            sensor.AddObservation(rb.velocity.y);
        }

        sensor.AddObservation(smoothedMoveInput / carController.moveSpeed); // Smoothed movement input

        // Raycasts to detect walls and obstacles in the field of view
        for (int i = 0; i < numRays; i++)
        {
            // Interpolate between startAngle and endAngle to create a field of view
            float angle = Mathf.Lerp(startAngle, endAngle, (float)i / (numRays - 1));
            Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, rayLength);

            // If ray hits an object, add relevant data
            if (hit.collider != null)
            {
                // Normalise the hit distance for observation (0 to 1 range)
                sensor.AddObservation(hit.distance / rayLength);

                // Boolean values (0 or 1) for each tag
                // If nothing is hit, it reports maximum distance and all zeros for object types
                bool isWall = hit.collider.CompareTag("Wall");
                bool isObstacle = hit.collider.CompareTag("Obstacle");
                bool isOilSlick = hit.collider.CompareTag("OilSlick");
                bool isRamp = hit.collider.CompareTag("Ramp");
                bool isBarrier = hit.collider.CompareTag("Barrier");
                
                sensor.AddObservation(isWall ? 1 : 0);
                sensor.AddObservation(isObstacle ? 1 : 0);
                sensor.AddObservation(isOilSlick ? 1 : 0);
                sensor.AddObservation(isRamp ? 1 : 0);
                sensor.AddObservation(isBarrier ? 1 : 0); // Add barrier detection
            }
            else
            {
                // No hit, so maximum distance (1 for normalised distance)
                sensor.AddObservation(1f);  // Maximum distance

                sensor.AddObservation(0);   // No wall
                sensor.AddObservation(0);   // No obstacle
                sensor.AddObservation(0);   // No oil slick
                sensor.AddObservation(0);   // No ramp
                sensor.AddObservation(0);   // No barrier
            }
        }
    }

    // Decision making and actions - called every step
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Agent makes discrete decisions
        int moveAction = actions.DiscreteActions[0]; // 0: no move, 1: left, 2: right

        // Use the existing movement function
        float moveInput = moveAction == 1 ? -carController.moveSpeed :
                          moveAction == 2 ? carController.moveSpeed : 0f;

        // Apply smoothing to the movement input
        smoothedMoveInput = Mathf.Lerp(smoothedMoveInput, moveInput, smoothingFactor);
        carController.SetMovement(smoothedMoveInput); // Choices are translated into movement through setMovement from carController

        // Penalise excessive steering to encourage smoother driving
        float steeringPenalty = Mathf.Abs(smoothedMoveInput) * 0.005f;
        AddReward(-steeringPenalty);

        // Rewards
        float currentDistance = -transform.position.y; // Since car is driving downwards
        distanceTraveled = currentDistance;

        if (currentDistance - lastDistanceReward >= DISTANCE_REWARD_INTERVAL)
        {
            AddReward(1.0f); // Every unit of progress definded by DISTANCE_REWARD_INTERVAL gives a reward
            lastDistanceReward = currentDistance;
        }

        AddReward(0.001f); // Agent gets a small continuous reward for staying alive
    }

    // Collisions trigger different rewards
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Wall"))
        {
            // Negative reward for hitting walls
            AddReward(-50.0f);
            EndEpisode();
        }
        else if (other.CompareTag("Obstacle") && !carController.isJumping)
        {
            // Negative reward for hitting obstacles
            AddReward(-50.0f);
            EndEpisode();
        }
        else if (other.CompareTag("Barrier") && !carController.isJumping)
        {
            // Large negative reward for hitting barriers
            AddReward(-50f);
            EndEpisode();
        }
        else if (other.CompareTag("OilSlick") && !carController.isJumping)
        {
            // Smaller negative reward for hitting oil slicks
            AddReward(-25f);
        }
        else if (other.CompareTag("Ramp") && !carController.isJumping)
        {
            // Small positive reward for using ramps
            AddReward(10f);
        }
    }

    // Behavior type - Heuristic 
    // Allows the player to manually control the agent using keyboard inputs - for debugging
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.LeftArrow))
            discreteActionsOut[0] = 1;
        else if (Input.GetKey(KeyCode.RightArrow))
            discreteActionsOut[0] = 2;
        else
            discreteActionsOut[0] = 0;
    }

    // This method will draw the gizmos for the field of view
    private void OnDrawGizmos()
    {
        if (carController != null)
        {
            Gizmos.color = Color.yellow; // Set color for the rays

            // Draw the rays representing the agent's field of view
            for (int i = 0; i < numRays; i++)
            {
                // Interpolate between startAngle and endAngle to distribute rays evenly
                float angle = Mathf.Lerp(startAngle, endAngle, (float)i / (numRays - 1));

                // Calculate direction vector based on the angle
                Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;

                RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, rayLength);

                // Draw the ray
                if (hit.collider != null)
                {
                    Gizmos.color = hit.collider.CompareTag("Wall") ? Color.red :
                                hit.collider.CompareTag("Obstacle") ? Color.blue :
                                hit.collider.CompareTag("OilSlick") ? Color.green :
                                hit.collider.CompareTag("Ramp") ? Color.magenta :
                                hit.collider.CompareTag("Barrier") ? Color.cyan :
                                Color.yellow;
                    Gizmos.DrawLine(transform.position, hit.point);
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, transform.position + (Vector3)(direction * rayLength));
                }
            }
        }
    }
}