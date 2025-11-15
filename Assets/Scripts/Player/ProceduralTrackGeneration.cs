using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.Analytics;
using Unity.VisualScripting;
using System.Data.Common;
using System.Net.Sockets;
using System.Numerics;

using Vector3 = UnityEngine.Vector3;

public class ProceduralTrackGeneration : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap roadTilemap;
    public Tilemap wallTilemap;
    public Tilemap barrierTilemap;

    [Header("Tiles")]
    public TileBase roadTile;
    public TileBase wallTile;
    public TileBase barrierTile;

    [Header("References")]
    public Transform player;
    public Camera mainCamera;

    [Header("Track Configuration")]
    public int trackWidth = 10;
    [Tooltip("Increased buffer distance for better barrier prediction")]
    public float trackBufferDistance = 10f;
    public int maxTrackSegments = 100;

    [Header("Obstacle System")]
    [Tooltip("Array of obstacles with their spawn weights")]
    public ObstacleWithWeight[] weightedObstacles;

    [Header("Obstacle Placement Rules")]
    [Tooltip("Minimum vertical distance between obstacles")]
    public float minSafePathVertical = 3f;
    [Tooltip("Minimum distance from wall edges")]
    public float minDistanceFromWalls = 3f;

    [Header("Barrier Safe Zone Configuration")]
    [Tooltip("Clear space before barrier")]
    public float clearanceBeforeBarrier = 8f;
    [Tooltip("Clear space after barrier")]
    public float clearanceAfterBarrier = 5f;
    [Tooltip("Minimum width for safe path through barriers")]
    public float minSafePathWidth = 3f;

    [Header("Obstacle Count")]
    [Range(1, 10)]
    public int maxObstaclesInStraightSection = 2;
    [Range(0, 8)]
    public int maxObstaclesInTurn = 1;

    [System.Serializable]
    public class ObstacleWithWeight
    {
        public GameObject prefab;
        [Range(1, 100)]
        public int spawnWeight = 10;
        public string description;
        public bool isRamp;
    }

    // Track generation
    private Vector3Int currentPos = new Vector3Int(0, 0, 0);
    private int direction = 0;
    private float cameraHalfWidth;
    private bool isGracePeriod = true;
    private Queue<Vector3Int> placedTiles = new Queue<Vector3Int>();
    private Queue<Vector3Int> barrierTiles = new Queue<Vector3Int>();

    // Barrier management
    private List<BarrierSection> barrierSections = new List<BarrierSection>();
    private bool nextSegmentWillHaveBarrier = false;
    private int nextSegmentOpeningStartX = 0;
    private int nextSegmentOpeningWidth = 0;

    // Obstacle tracking
    private class TrackedObstacle
    {
        public GameObject gameObject;
        public Vector3 position;
        public bool checkedForSafeZoneViolation = false;
    }
    private List<TrackedObstacle> spawnedObstacles = new List<TrackedObstacle>();

    // Class to track barrier sections and their safe zones
    public class BarrierSection
    {
        public Rect safeZone; // Main path safe zone
        public List<Rect> bufferZones = new List<Rect>(); // Buffer zones around each barrier piece
        public float barrierY;
        public int openingStartX;
        public int openingWidth;
        public float openingCenterX;
        public List<Vector3Int> barrierPositions = new List<Vector3Int>();
    }

    void Start()
    {
        cameraHalfWidth = CalculateCameraHalfWidth();
        Invoke("EndGracePeriod", 3f);

        // Generate initial track segments below the player
        for (int i = 0; i < 8; i++)
        {
            DecideNextSegmentBarrier();
            GenerateTrackSegment(10, trackWidth, 0);
        }
    }

    void Update()
    {
        // Generate new track when player approaches the end of current section
        if (!isGracePeriod && (player.position.y - currentPos.y < trackBufferDistance))
        {
            GenerateTrackSegment(10, trackWidth, GetNextDirection());
        }

        // Check and remove obstacles in barrier safe zones
        CheckAndRemoveObstaclesInSafeZones();

        // Cleanup barrier sections and obstacles that are far behind
        CleanupOldBarrierSections();
        CleanupOldObstacles();
    }

    private float CalculateCameraHalfWidth()
    {
        float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        return Mathf.Floor(halfWidth) - 1;
    }

    private void EndGracePeriod()
    {
        isGracePeriod = false;
        Debug.Log("Grace period over, track generation and obstacles enabled.");
    }

    private int GetNextDirection()
    {
        // Prevent consecutive turns in the same direction and keep track within camera bounds
        int newDirection;
        do
        {
            newDirection = Random.Range(-1, 2); // -1 = left, 0 = straight, 1 = right
        }
        while ((direction == -1 && newDirection == -1) ||
               (direction == 1 && newDirection == 1) ||
               !IsTurnWithinBounds(newDirection));

        return newDirection;
    }

    private bool IsTurnWithinBounds(int turnDirection)
    {
        int predictedX = currentPos.x + turnDirection * Mathf.FloorToInt(10 / 2f);
        return predictedX - trackWidth / 2 >= -cameraHalfWidth &&
               predictedX + trackWidth / 2 <= cameraHalfWidth;
    }

    private void DecideNextSegmentBarrier()
    {
        // 20% chance for a barrier
        nextSegmentWillHaveBarrier = Random.Range(0, 5) == 0;

        if (nextSegmentWillHaveBarrier)
        {
            // Configure the opening in the barrier
            nextSegmentOpeningWidth = Random.Range(3, 5); // 3-4 tiles wide
            int trackHalfWidth = trackWidth / 2;
            nextSegmentOpeningStartX = Random.Range(-trackHalfWidth + 2, trackHalfWidth - nextSegmentOpeningWidth - 1);
        }
    }

    public void GenerateTrackSegment(int segmentLength, int newWidth, int turnDirection)
    {
        // Update track parameters
        trackWidth = newWidth;
        direction = turnDirection;

        // Prepare for new segment
        List<Vector3Int> roadPositions = new List<Vector3Int>();
        List<Vector3Int> leftWallPositions = new List<Vector3Int>();
        List<Vector3Int> rightWallPositions = new List<Vector3Int>();

        // Create a new barrier section if needed
        BarrierSection currentBarrierSection = null;

        if (nextSegmentWillHaveBarrier)
        {
            currentBarrierSection = new BarrierSection
            {
                barrierY = currentPos.y,
                openingStartX = nextSegmentOpeningStartX,
                openingWidth = nextSegmentOpeningWidth,
                openingCenterX = currentPos.x + nextSegmentOpeningStartX + (nextSegmentOpeningWidth / 2f)
            };
        }

        // Generate the segment tiles
        for (int y = 0; y < segmentLength; y++)
        {
            // Calculate turn offset
            int xOffset = (direction != 0) ? direction * Mathf.CeilToInt(y / 2f) : 0;

            // Place road tiles across the width
            for (int x = -trackWidth / 2; x <= trackWidth / 2; x++)
            {
                Vector3Int tilePos = new Vector3Int(currentPos.x + x + xOffset, currentPos.y - y, 0);
                roadPositions.Add(tilePos);
                placedTiles.Enqueue(tilePos);

                // Place barriers only on first row if needed
                if (currentBarrierSection != null && y == 0)
                {
                    bool isOpening = (x >= currentBarrierSection.openingStartX &&
                                     x < currentBarrierSection.openingStartX + currentBarrierSection.openingWidth);
                    if (!isOpening)
                    {
                        currentBarrierSection.barrierPositions.Add(tilePos);
                        barrierTiles.Enqueue(tilePos);
                    }
                }
            }


            Vector3Int leftWallPos = new Vector3Int(currentPos.x - trackWidth / 2 - 1 + xOffset, currentPos.y - y, 0);
            Vector3Int rightWallPos = new Vector3Int(currentPos.x + trackWidth / 2 + 1 + xOffset, currentPos.y - y, 0);

            if (roadTilemap.GetTile(leftWallPos) == null && wallTilemap.GetTile(leftWallPos) == null)
                leftWallPositions.Add(leftWallPos);
            if (roadTilemap.GetTile(rightWallPos) == null && wallTilemap.GetTile(rightWallPos) == null)
                rightWallPositions.Add(rightWallPos);

            Vector3Int leftGapCheck = new Vector3Int(leftWallPos.x - 1, leftWallPos.y, 0);
            Vector3Int rightGapCheck = new Vector3Int(rightWallPos.x + 1, rightWallPos.y, 0);

            bool leftGapValid = roadTilemap.GetTile(leftGapCheck) == null
                                && wallTilemap.GetTile(leftGapCheck) == null
                                && !(direction > 0 && roadTilemap.GetTile(new Vector3Int(leftGapCheck.x, leftGapCheck.y, 0)) != null);

            bool rightGapValid = roadTilemap.GetTile(rightGapCheck) == null
                                 && wallTilemap.GetTile(rightGapCheck) == null
                                 && !(direction < 0 && roadTilemap.GetTile(new Vector3Int(rightGapCheck.x, rightGapCheck.y, 0)) != null);

            if (leftGapValid) leftWallPositions.Add(leftGapCheck);
            if (rightGapValid) rightWallPositions.Add(rightGapCheck);

            leftWallPositions.RemoveAll(pos => roadTilemap.GetTile(pos) != null);
            rightWallPositions.RemoveAll(pos => roadTilemap.GetTile(pos) != null);
        }

        // Set all tiles at once for better performance
        roadTilemap.SetTiles(roadPositions.ToArray(), roadPositions.ConvertAll(_ => roadTile).ToArray());
        wallTilemap.SetTiles(leftWallPositions.ToArray(), leftWallPositions.ConvertAll(_ => wallTile).ToArray());
        wallTilemap.SetTiles(rightWallPositions.ToArray(), rightWallPositions.ConvertAll(_ => wallTile).ToArray());

        // Setup barriers and safe zones if needed
        if (currentBarrierSection != null && currentBarrierSection.barrierPositions.Count > 0)
        {
            // Set barrier tiles
            barrierTilemap.SetTiles(currentBarrierSection.barrierPositions.ToArray(),
                                   currentBarrierSection.barrierPositions.ConvertAll(_ => barrierTile).ToArray());

            // Create the safe zone around this barrier
            CreateBarrierSafeZone(currentBarrierSection);

            // Add the barrier section to our tracking list
            barrierSections.Add(currentBarrierSection);
        }

        // Place obstacles only after grace period
        if (!isGracePeriod)
        {
            PlaceObstaclesInSegment(roadPositions, leftWallPositions, rightWallPositions);
        }

        // Update position for next segment
        currentPos.y -= segmentLength;
        if (direction != 0) currentPos.x += direction * Mathf.FloorToInt(segmentLength / 2f);

        // Decide barrier for next segment
        DecideNextSegmentBarrier();

        // Clean up old track segments
        RefreshTrack();
    }

    // Creates both the safe path zone and buffer zones around barrier pieces
    private void CreateBarrierSafeZone(BarrierSection barrierSection)
    {
        if (barrierSection.barrierPositions.Count == 0) return;

        // Safe path through barrier
        barrierSection.safeZone = new Rect(
            barrierSection.openingCenterX - minSafePathWidth / 2f,
            barrierSection.barrierY - clearanceAfterBarrier,
            minSafePathWidth,
            clearanceBeforeBarrier + clearanceAfterBarrier
        );
    }

    public List<BarrierSection> GetBarrierSections()
    {
        return barrierSections;
    }

    private void PlaceObstaclesInSegment(List<Vector3Int> roadPositions, List<Vector3Int> leftWalls, List<Vector3Int> rightWalls)
    {
        if (weightedObstacles.Length == 0) return;

        int maxObstacles = (direction == 0) ? maxObstaclesInStraightSection : maxObstaclesInTurn;
        if (maxObstacles <= 0) return;

        int numObstaclesToPlace = Random.Range(1, maxObstacles + 1);
        int attempts = 0;
        int placedObstacles = 0;

        while (placedObstacles < numObstaclesToPlace && attempts < 30)
        {
            attempts++;

            int centralRoadIndex = Random.Range(roadPositions.Count / 4, roadPositions.Count * 3 / 4);
            Vector3Int tilePos = roadPositions[centralRoadIndex];
            Vector3 worldPos = roadTilemap.GetCellCenterWorld(tilePos);

            if (!IsValidObstaclePosition(tilePos, worldPos, leftWalls, rightWalls, centralRoadIndex))
            {
                continue;
            }

            GameObject selectedObstacle = SelectObstacleByWeight(direction != 0);
            if (selectedObstacle != null)
            {
                SpawnObstacle(tilePos, selectedObstacle);
                placedObstacles++;
            }
        }
    }

    private bool IsValidObstaclePosition(Vector3Int tilePos, Vector3 worldPos, List<Vector3Int> leftWalls, List<Vector3Int> rightWalls, int centralRoadIndex)
    {
        // Check if position is in any current barrier safe zone
        if (IsInAnySafeZone(worldPos))
        {
            return false;
        }

        // Check vertical spacing from other obstacles
        if (IsTooCloseVertically(tilePos))
        {
            return false;
        }

        // Left wall
        Vector3Int obstacleTile = roadTilemap.WorldToCell(worldPos);

        float nearestLeftWallDistance = float.MaxValue;
        foreach (var wall in leftWalls)
        {
            float d = Mathf.Abs(wall.y - obstacleTile.y);
            if (d < 1)
            {
                float dist = Vector3.Distance(worldPos, wallTilemap.GetCellCenterWorld(wall));
                nearestLeftWallDistance = Mathf.Min(nearestLeftWallDistance, dist);
            }
        }

        // Right wall
        float nearestRightWallDistance = float.MaxValue;
        foreach (var wall in rightWalls)
        {
            float d = Mathf.Abs(wall.y - obstacleTile.y);
            if (d < 1)
            {
                float dist = Vector3.Distance(worldPos, wallTilemap.GetCellCenterWorld(wall));
                nearestRightWallDistance = Mathf.Min(nearestRightWallDistance, dist);
            }
        }

        float distanceToLeftWall = nearestLeftWallDistance;
        float distanceToRightWall = nearestRightWallDistance;

        if (distanceToLeftWall < minDistanceFromWalls || distanceToRightWall < minDistanceFromWalls)
        {
            return false;
        }

        // Check if obstacle would block the path
        return !WouldBlockPath(tilePos);
    }

    // Periodically check and remove obstacles that are in barrier safe zones
    private void CheckAndRemoveObstaclesInSafeZones()
    {
        // Iterate through all tracked obstacles.
        for (int i = spawnedObstacles.Count - 1; i >= 0; i--)
        {
            TrackedObstacle obstacle = spawnedObstacles[i];
            if (obstacle.gameObject == null)
            {
                spawnedObstacles.RemoveAt(i);
                continue;
            }

            // Always update the obstacle position.
            obstacle.position = obstacle.gameObject.transform.position;

            // Check if the obstacle is in any safe zone regardless of player position.
            if (IsInAnySafeZone(obstacle.position))
            {
                Debug.Log("Obstacle found in safe zone - removing it early.");
                Destroy(obstacle.gameObject);
                spawnedObstacles.RemoveAt(i);
            }
        }
    }

    private bool IsInAnySafeZone(Vector3 worldPos)
    {
        foreach (BarrierSection barrierSection in barrierSections)
        {
            // Check main safe path zone
            Rect safeZone = barrierSection.safeZone;
            if (worldPos.x >= safeZone.x &&
                worldPos.x <= safeZone.x + safeZone.width &&
                worldPos.y >= safeZone.y &&
                worldPos.y <= safeZone.y + safeZone.height)
            {
                return true; // In safe path - no obstacles allowed
            }

            // Check buffer zones around barrier pieces
            foreach (Rect bufferZone in barrierSection.bufferZones)
            {
                if (worldPos.x >= bufferZone.x &&
                    worldPos.x <= bufferZone.x + bufferZone.width &&
                    worldPos.y >= bufferZone.y &&
                    worldPos.y <= bufferZone.y + bufferZone.height)
                {
                    return true; // Too close to a barrier piece
                }
            }
        }
        return false;
    }

    private bool IsTooCloseVertically(Vector3Int tilePos)
    {
        Vector3 worldPos = roadTilemap.GetCellCenterWorld(tilePos);

        foreach (TrackedObstacle obstacle in spawnedObstacles)
        {
            if (obstacle.gameObject == null) continue;

            // Check vertical distance
            float verticalDistance = Mathf.Abs(obstacle.position.y - worldPos.y);

            if (verticalDistance < minSafePathVertical)
            {
                // Check if in the same lane
                float horizontalDistance = Mathf.Abs(obstacle.position.x - worldPos.x);
                if (horizontalDistance < 2f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool WouldBlockPath(Vector3Int obstaclePos)
    {
        int obstacleX = obstaclePos.x;
        int obstacleY = obstaclePos.y;
        int leftSpace = 0;
        int rightSpace = 0;

        // Count free spaces to the left
        for (int x = obstacleX - 1; x >= obstacleX - trackWidth / 2; x--)
        {
            Vector3Int checkPos = new Vector3Int(x, obstacleY, 0);
            if (roadTilemap.HasTile(checkPos) && !IsPositionOccupied(checkPos))
            {
                leftSpace++;
            }
            else
            {
                break;
            }
        }

        // Count free spaces to the right
        for (int x = obstacleX + 1; x <= obstacleX + trackWidth / 2; x++)
        {
            Vector3Int checkPos = new Vector3Int(x, obstacleY, 0);
            if (roadTilemap.HasTile(checkPos) && !IsPositionOccupied(checkPos))
            {
                rightSpace++;
            }
            else
            {
                break;
            }
        }

        // Return true if both sides have less than 2 tiles of space 
        return (leftSpace < 2 && rightSpace < 2); // Meaning the obstacle would block the path)
    }

    private bool IsPositionOccupied(Vector3Int position)
    {
        Vector3 worldPos = roadTilemap.GetCellCenterWorld(position);

        foreach (TrackedObstacle obstacle in spawnedObstacles)
        {
            if (obstacle.gameObject != null && Vector3.Distance(obstacle.position, worldPos) < 0.5f)
            {
                return true;
            }
        }

        return barrierTilemap.HasTile(position);
    }

    private GameObject SelectObstacleByWeight(bool isTurn)
    {
        List<ObstacleWithWeight> validObstacles = new List<ObstacleWithWeight>();

        foreach (ObstacleWithWeight obstacle in weightedObstacles)
        {
            // Skip ramps in turns
            if (isTurn && obstacle.isRamp)
                continue;

            validObstacles.Add(obstacle);
        }

        if (validObstacles.Count == 0)
            return null;

        // Calculate total weight and select based on weighted random
        int totalWeight = 0;
        foreach (ObstacleWithWeight obstacle in validObstacles)
        {
            totalWeight += obstacle.spawnWeight;
        }

        int randomValue = Random.Range(0, totalWeight);
        int accumulatedWeight = 0;

        foreach (ObstacleWithWeight obstacle in validObstacles)
        {
            accumulatedWeight += obstacle.spawnWeight;
            if (randomValue < accumulatedWeight)
            {
                return obstacle.prefab;
            }
        }

        return validObstacles[0].prefab;
    }

    private void SpawnObstacle(Vector3Int tilePosition, GameObject obstaclePrefab)
    {
        if (isGracePeriod || obstaclePrefab == null)
            return;

        Vector3 worldPosition = roadTilemap.GetCellCenterWorld(tilePosition);
        GameObject spawnedObstacle = Instantiate(obstaclePrefab, worldPosition, UnityEngine.Quaternion.identity);

        // Add to tracked obstacles list with initial checked state as false
        spawnedObstacles.Add(new TrackedObstacle
        {
            gameObject = spawnedObstacle,
            position = worldPosition,
            checkedForSafeZoneViolation = false
        });
    }

    private void CleanupOldBarrierSections()
    {
        for (int i = barrierSections.Count - 1; i >= 0; i--)
        {
            // If barrier safe zone is far behind the player, remove it
            if (barrierSections[i].barrierY < player.position.y - 30f)
            {
                barrierSections.RemoveAt(i);
            }
        }
    }

    private void CleanupOldObstacles()
    {
        for (int i = spawnedObstacles.Count - 1; i >= 0; i--)
        {
            TrackedObstacle obstacle = spawnedObstacles[i];

            // Clean up null references
            if (obstacle.gameObject == null)
            {
                spawnedObstacles.RemoveAt(i);
                continue;
            }

            // Remove obstacles far behind the player
            if (Vector3.Distance(player.position, obstacle.position) > 30f)
            {
                Destroy(obstacle.gameObject);
                spawnedObstacles.RemoveAt(i);
            }
        }
    }

    private void RefreshTrack()
    {
        // Calculate maximum number of tiles to keep
        int maxTiles = maxTrackSegments * trackWidth;

        // Remove oldest tiles once we exceed the limit
        while (placedTiles.Count > maxTiles)
        {
            Vector3Int oldTile = placedTiles.Dequeue();
            roadTilemap.SetTile(oldTile, null);

            // Remove corresponding wall tiles
            Vector3Int leftWallPos = new Vector3Int(oldTile.x - trackWidth / 2 - 1, oldTile.y, 0);
            Vector3Int rightWallPos = new Vector3Int(oldTile.x + trackWidth / 2 + 1, oldTile.y, 0);
            wallTilemap.SetTile(leftWallPos, null);
            wallTilemap.SetTile(rightWallPos, null);

            // Remove old barriers
            if (barrierTiles.Count > 0 && barrierTiles.Peek().y >= oldTile.y)
            {
                Vector3Int barrierTile = barrierTiles.Dequeue();
                barrierTilemap.SetTile(barrierTile, null);
            }
        }
    }

    public void ResetTrack()
    {
        // Clear all tilemaps
        roadTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        barrierTilemap.ClearAllTiles();

        // Destroy all obstacles
        foreach (TrackedObstacle obstacle in spawnedObstacles)
        {
            if (obstacle.gameObject != null)
                Destroy(obstacle.gameObject);
        }

        // Clear all tracking collections
        spawnedObstacles.Clear();
        placedTiles.Clear();
        barrierTiles.Clear();
        barrierSections.Clear();

        // Reset track generation state
        currentPos = new Vector3Int(0, 0, 0);
        nextSegmentWillHaveBarrier = false;
        isGracePeriod = true;

        // Restart the grace period
        Invoke("EndGracePeriod", 3f);

        // Generate the initial track
        DecideNextSegmentBarrier();
        for (int i = 0; i < 8; i++)
        {
            GenerateTrackSegment(10, trackWidth, 0);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw debug visualization for barrier safe zones
        foreach (BarrierSection barrierSection in barrierSections)
        {
            // Draw main safe path
            DrawSafeZoneGizmo(barrierSection.safeZone, new Color(0, 1, 0, 0.2f));

            // Draw buffer zones around barrier pieces
            foreach (Rect bufferZone in barrierSection.bufferZones)
            {
                DrawSafeZoneGizmo(bufferZone, new Color(1, 0, 0, 0.15f));
            }
        }
    }

    private void DrawSafeZoneGizmo(Rect zone, Color fillColor)
    {
        // Calculate center of zone
        Vector3 center = new Vector3(
            zone.x + zone.width / 2f,
            zone.y + zone.height / 2f,
            0
        );

        Vector3 size = new Vector3(zone.width, zone.height, 1f);

        // Draw outline
        Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, 1f);
        Gizmos.DrawWireCube(center, size);

        // Draw semi-transparent fill
        Gizmos.color = fillColor;
        Gizmos.DrawCube(center, size);
    }
}
