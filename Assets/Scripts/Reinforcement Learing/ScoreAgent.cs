using UnityEngine;
using TMPro;
using System.Collections.Generic; // Required for List<T>

public class ScoreAgent : MonoBehaviour
{
    public Transform player;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI finalScoreText;
    private float startY;
    private int score;
    private int[] runScores = new int[3]; // Normal mode scores
    private List<int> runAgent = new List<int>(); // Dynamic list for RL mode
    public int currentRun = 0; // For normal mode
    private int currentRunAgent = 0; // For RL agent
    private bool gameOver = false;
    public Canvas FinalScoreScreen;
    private bool isTrainingMode = false;

    void Start()
    {
        if (player != null)
        {
            startY = player.position.y;
        }

        // Check if we're in training mode
        isTrainingMode = player.GetComponent<Unity.MLAgents.Agent>() != null;

        // Ensure UI elements behave correctly in training mode
        if (isTrainingMode)
        {
            if (FinalScoreScreen != null) FinalScoreScreen.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (player != null)
        {
            score = (Mathf.Abs(Mathf.FloorToInt(player.position.y - startY))) / 20;

            if (!isTrainingMode)
            {
                scoreText.text = $"Run {currentRun + 1}\nScore: {score}";
            }
            else
            {
                scoreText.text = $"Training Run {currentRunAgent + 1}\nScore: {score}";
            }
        }
    }

    public void EndRun()
    {
        if (gameOver && !isTrainingMode) return;

        if (!isTrainingMode)
        {
            runScores[currentRun] = score;
            currentRun++;

            if (currentRun >= 3)
            {
                ShowFinalScore();
                player.gameObject.SetActive(false);
            }
        }
        else
        {
            // Use List to dynamically store scores in RL mode
            runAgent.Add(score);
            currentRunAgent++;
        }
    }

    private void ShowFinalScore()
    {
        if (isTrainingMode) return;

        gameOver = true;
        int totalScore = runScores[0] + runScores[1] + runScores[2];
        finalScoreText.text = $"Run 1: {runScores[0]}\nRun 2: {runScores[1]}\nRun 3: {runScores[2]}";
        scoreText.gameObject.SetActive(false);
        FinalScoreScreen.gameObject.SetActive(true);
    }
}
