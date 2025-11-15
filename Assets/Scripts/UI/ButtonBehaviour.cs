using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// To manage scene transitions and button behaviours
public class ButtonBehaviour : MonoBehaviour
{

    public GameObject options;
    public GameObject mainMenu;

    public void closeGame()
    {
        Debug.Log("Game Closed");
        Application.Quit();
    }

    public void startGame()
    {
        Debug.Log("Game Started");
        SceneManager.LoadScene("Player");
    }

    public void startRLAgent()
    {
        Debug.Log("RL Agent Started");
        SceneManager.LoadScene("RL Agent");
    }

    public void optionsMenu()
    {   
        options.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void goToMainMenu()
    {
        options.SetActive(false);
        mainMenu.SetActive(true);
    }

    public void backToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void replayGame()
    {
        SceneManager.LoadScene("Player");
    }
}
