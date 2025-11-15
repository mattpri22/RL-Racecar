using UnityEngine;
using TMPro;

public class ScoreScript : MonoBehaviour
{
    public Transform player;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI finalScoreText; // UI to show final score
    
    private float startY;
    private int score;
    private int[] runScores = new int[3]; // Store scores for 3 runs
    public int currentRun = 0; // Track which run the player is on
    private bool gameOver = false; // Track if game is over
    public Canvas FinalScoreScreen;

    void Start()
    {
        
        if (player != null)
        {
            startY = player.position.y;
        }
    }

    void Update()
    {
        if (gameOver) return; // Stop updating after 3 runs

        if (player != null)
        {
            score = (Mathf.Abs(Mathf.FloorToInt(player.position.y - startY))) / 20;
            scoreText.text = $"Run {currentRun + 1}/3\nScore: {score}";
        }
    }

    public void EndRun()
    {
        if (gameOver) return; // Prevent storing scores after game ends

        runScores[currentRun] = score; // Save score for this run
        currentRun++; // Move to next run

        if (currentRun >= 3)
        {
            ShowFinalScore();
            player.gameObject.SetActive(false);
        }
    }

    private void ShowFinalScore()
    {
        gameOver = true;
        int totalScore = runScores[0] + runScores[1] + runScores[2];
        finalScoreText.text = $"Run 1: " + runScores[0] + "\nRun 2: " + runScores[1] + "\nRun 3: " + runScores[2];
        scoreText.gameObject.SetActive(false); // Hide run score
        FinalScoreScreen.gameObject.SetActive(true);
    }
}
