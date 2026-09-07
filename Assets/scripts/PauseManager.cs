using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject pauseMenuPanel;

    [Tooltip("Drag your Outcome manager GameObject here to disable its UI raycast block during pause")]
    public GameObject outcomeManagerUI;

    [Header("Player Control Reference")]
    public MonoBehaviour playerController;

    [Header("Audio Settings")]
    [Tooltip("Drag your Background Music AudioSource here")]
    public AudioSource backgroundMusic;

    [Header("Scene Settings")]
    public string mainMenuSceneName = "main menu";

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true);

        if (outcomeManagerUI != null)
            outcomeManagerUI.SetActive(false);

        Time.timeScale = 0f; // Freeze game speed

        if (backgroundMusic != null && backgroundMusic.isPlaying)
        {
            backgroundMusic.Pause();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null)
            playerController.enabled = false;

        isPaused = true;
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);

        if (outcomeManagerUI != null)
            outcomeManagerUI.SetActive(true);

        Time.timeScale = 1f; // Restore normal speed

        if (backgroundMusic != null)
        {
            backgroundMusic.UnPause();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerController != null)
            playerController.enabled = true;

        isPaused = false;
    }

    public void RestartLevel()
    {
        // Unfreeze time scale before reloading the scene
        Time.timeScale = 1f;

        // Reload the currently active scene (Level 1)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;

        if (backgroundMusic != null)
        {
            backgroundMusic.Stop();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}