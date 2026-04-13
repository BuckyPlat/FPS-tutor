using Photon.Pun;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance;

    [Header("UI Elements")]
    public GameObject pauseMenuPanel;

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu";      // Tên scene màn hình chính
    public string roomListSceneName = "RoomList";      // Tên scene chọn phòng (nếu có)

    private bool isPaused = false;
    private Movement playerMovement;
    private MouseLook mouseLook;                     // Script điều khiển camera nhìn chuột (thường tên là MouseLook hoặc PlayerLook)

    private void Awake()
    {
        Instance = this;
        pauseMenuPanel.SetActive(false);
    }

    private void Start()
    {
        // Tìm các component của người chơi cục bộ
        if (PhotonNetwork.LocalPlayer != null)
        {
            GameObject localPlayer = GameObject.FindGameObjectWithTag("Player"); // hoặc tìm theo PhotonView.IsMine

            if (localPlayer != null)
            {
                playerMovement = localPlayer.GetComponent<Movement>();
                mouseLook = localPlayer.GetComponent<MouseLook>();   // Thay tên script camera look của bạn nếu khác
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
            PauseGame();
        else
            ResumeGame();
    }

    private void PauseGame()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);

        // Tắt di chuyển và nhìn chuột
        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // Thả chuột ra (Cursor visible + không lock)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Dừng thời gian (tùy chọn)
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseMenuPanel.SetActive(false);

        // Bật lại di chuyển và camera
        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        // Khóa chuột lại vào game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Time.timeScale = 1f;
    }

    // ====================== NÚT TRONG MENU ======================

    public void Button_Resume()
    {
        ResumeGame();
    }

    public void Button_ExitRoom()
    {
        ResumeGame();                    // Reset time trước khi rời
        PhotonNetwork.LeaveRoom();       // Thoát phòng Photon

        // Chuyển về scene chọn phòng
        SceneManager.LoadScene(roomListSceneName);
    }

    public void Button_BackToMain()
    {
        ResumeGame();
        PhotonNetwork.Disconnect();      // Ngắt kết nối Photon

        // Chuyển về màn hình chính
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Gọi hàm này từ Health.cs khi chết (nếu muốn disable pause khi chết)
    public void DisablePauseMenu()
    {
        enabled = false;
    }
}