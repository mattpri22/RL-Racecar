using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSettingsUI : MonoBehaviour
{
    [Header("Reference to Procedural Track Generation")]
    public ProceduralTrackGeneration trackGen;

    [Header("Gameplay Setting Sliders")]
    public Slider maxObstaclesStraightSlider;
    public TextMeshProUGUI maxObstaclesStraightText;
    public Slider maxObstaclesTurnSlider;
    public TextMeshProUGUI maxObstaclesTurnText;
    public Slider trackWidthSlider;
    public TextMeshProUGUI trackWidthText;

    void Start()
    {
        if (trackGen == null)
        {
            Debug.LogError("ProceduralTrackGeneration reference not assigned to GameSettingsUI!");
            return;
        }
        
        // Initialise slider values with current values from trackGen
        maxObstaclesStraightSlider.value = trackGen.maxObstaclesInStraightSection;
        maxObstaclesTurnSlider.value = trackGen.maxObstaclesInTurn;
        trackWidthSlider.value = trackGen.trackWidth;
        
        // Update text displays to match initial values
        maxObstaclesStraightText.text = trackGen.maxObstaclesInStraightSection.ToString();
        maxObstaclesTurnText.text = trackGen.maxObstaclesInTurn.ToString();
        trackWidthText.text = trackGen.trackWidth.ToString();
        
        // Add listeners to sliders
        maxObstaclesStraightSlider.onValueChanged.AddListener(OnMaxObstaclesStraightChanged);
        maxObstaclesTurnSlider.onValueChanged.AddListener(OnMaxObstaclesTurnChanged);
        trackWidthSlider.onValueChanged.AddListener(OnTrackWidthChanged);
    }

    public void OnMaxObstaclesStraightChanged(float newValue)
    {
        int roundedValue = Mathf.RoundToInt(newValue);
        if (trackGen != null)
        {
            trackGen.maxObstaclesInStraightSection = roundedValue;
        }
        maxObstaclesStraightText.text = roundedValue.ToString();
    }

    public void OnMaxObstaclesTurnChanged(float newValue)
    {
        int roundedValue = Mathf.RoundToInt(newValue);
        if (trackGen != null)
        {
            trackGen.maxObstaclesInTurn = roundedValue;
        }
        maxObstaclesTurnText.text = roundedValue.ToString();
    }

    public void OnTrackWidthChanged(float newValue)
    {
        int roundedValue = Mathf.RoundToInt(newValue);
        if (trackGen != null)
        {
            trackGen.trackWidth = roundedValue;
        }
        trackWidthText.text = roundedValue.ToString();
    }
}