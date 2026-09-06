using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject pauseMenuPanel;

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
        Time.timeScale = 0f; // Freeze game speed

        // Pause background music explicitly
        if (backgroundMusic != null && backgroundMusic.isPlaying)
        {
            backgroundMusic.Pause();
        }

        // Unlock and show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable player look/movement script
        if (playerController != null)
            playerController.enabled = false;

        isPaused = true;
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f; // Restore normal speed

        // Resume background music from where it stopped
        if (backgroundMusic != null)
        {
            backgroundMusic.UnPause();
        }

        // Hide and lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable player look/movement script
        if (playerController != null)
            playerController.enabled = true;

        isPaused = false;
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