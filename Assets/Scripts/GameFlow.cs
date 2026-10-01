using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Alur game: Main Menu -> Playing -> Menang / Kalah (dengan Restart & kembali ke Main Menu).
/// Tempel ke satu GameObject kosong di scene (mis. "GameFlow"). Seluruh UI dibuat otomatis.
/// </summary>
public class GameFlow : MonoBehaviour
{
    private enum Phase { Menu, Playing, Won, Lost }

    [Header("References (opsional, dicari otomatis jika kosong)")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private EnemyHealth enemy;

    [Header("Settings")]
    [Tooltip("Jeda (detik) setelah ada yang kalah sebelum layar hasil muncul.")]
    [SerializeField] private float endScreenDelay = 1.2f;

    // Setelah Restart, menu tidak ditampilkan lagi; setelah "Menu Utama", ditampilkan.
    private static bool skipMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => skipMenu = false;

    private Phase phase;
    private float endTime = -1f;

    private GameObject menuPanel, winPanel, losePanel;
    private SimplePlayerController playerController;
    private PlayerAttack playerAttack;
    private ThirdPersonCamera cameraRig;
    private Font font;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void Awake()
    {
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        if (player != null) player = player.GetComponent<PlayerHealth>(); // komponen pertama
        if (enemy == null) enemy = FindAnyObjectByType<EnemyHealth>();

        if (player != null)
        {
            playerController = player.GetComponent<SimplePlayerController>();
            playerAttack = player.GetComponent<PlayerAttack>();
        }

        cameraRig = FindAnyObjectByType<ThirdPersonCamera>();

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildUI();
    }

    private void Start()
    {
        if (skipMenu) StartGame();
        else ShowMenu();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (phase != Phase.Playing)
            return;

        bool playerDead = player != null && player.IsDead;
        bool enemyDead = enemy != null && enemy.IsDead;

        if (endTime < 0f)
        {
            if (playerDead || enemyDead)
            {
                endTime = Time.unscaledTime + endScreenDelay;

                // Hentikan kontrol Player selama jeda sebelum layar hasil.
                if (playerController != null) playerController.enabled = false;
                if (playerAttack != null) playerAttack.enabled = false;
            }
        }
        else if (Time.unscaledTime >= endTime)
        {
            // Player kalah lebih diprioritaskan jika keduanya mati bersamaan.
            if (playerDead) ShowEnd(Phase.Lost);
            else ShowEnd(Phase.Won);
        }
    }

    // =====================================================
    // PHASE
    // =====================================================

    private void ShowMenu()
    {
        phase = Phase.Menu;
        SetPaused(true);
        menuPanel.SetActive(true);
        winPanel.SetActive(false);
        losePanel.SetActive(false);
    }

    private void StartGame()
    {
        phase = Phase.Playing;
        endTime = -1f;
        menuPanel.SetActive(false);
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        SetPaused(false);
    }

    private void ShowEnd(Phase result)
    {
        phase = result;
        SetPaused(true);
        (result == Phase.Won ? winPanel : losePanel).SetActive(true);
    }

    private void SetPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;

        if (cameraRig != null) cameraRig.enabled = !paused;

        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // =====================================================
    // BUTTON ACTIONS
    // =====================================================

    private void OnStartClicked() => StartGame();

    private void OnRestartClicked()
    {
        skipMenu = true;
        ReloadScene();
    }

    private void OnMainMenuClicked()
    {
        skipMenu = false;
        ReloadScene();
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ReloadScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // =====================================================
    // UI
    // =====================================================

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.transform.SetParent(null);
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("GameFlow_Canvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // --- Main Menu ---
        menuPanel = CreateOverlay(canvasGO.transform, "MainMenu", new Color(0.04f, 0.05f, 0.09f, 0.92f));
        CreateLabel(menuPanel.transform, "ENEMY FSM", 96, FontStyle.Bold, Color.white, new Vector2(0, 230));
        CreateLabel(menuPanel.transform, "Praktikum 05 - Finite State Machine", 28, FontStyle.Normal,
            new Color(0.7f, 0.8f, 1f), new Vector2(0, 150));
        CreateButton(menuPanel.transform, "MULAI GAME", new Vector2(0, 30), new Color(0.2f, 0.65f, 0.35f), OnStartClicked);
        CreateButton(menuPanel.transform, "KELUAR", new Vector2(0, -70), new Color(0.6f, 0.25f, 0.25f), OnQuitClicked);
        CreateLabel(menuPanel.transform,
            "WASD / Panah: Gerak     Space / Klik Kiri: Serang     Esc: Lepas Kursor",
            24, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f), new Vector2(0, -220));

        // --- Menang ---
        winPanel = CreateOverlay(canvasGO.transform, "WinPanel", new Color(0f, 0.12f, 0.04f, 0.88f));
        CreateLabel(winPanel.transform, "KAMU MENANG!", 90, FontStyle.Bold, new Color(0.4f, 1f, 0.55f), new Vector2(0, 190));
        CreateLabel(winPanel.transform, "Enemy berhasil dikalahkan.", 30, FontStyle.Normal, Color.white, new Vector2(0, 110));
        CreateButton(winPanel.transform, "MAIN LAGI", new Vector2(0, 0), new Color(0.2f, 0.65f, 0.35f), OnRestartClicked);
        CreateButton(winPanel.transform, "MENU UTAMA", new Vector2(0, -100), new Color(0.25f, 0.4f, 0.7f), OnMainMenuClicked);

        // --- Kalah ---
        losePanel = CreateOverlay(canvasGO.transform, "LosePanel", new Color(0.15f, 0f, 0f, 0.88f));
        CreateLabel(losePanel.transform, "GAME OVER", 90, FontStyle.Bold, new Color(1f, 0.35f, 0.35f), new Vector2(0, 190));
        CreateLabel(losePanel.transform, "Player dikalahkan oleh Enemy.", 30, FontStyle.Normal, Color.white, new Vector2(0, 110));
        CreateButton(losePanel.transform, "COBA LAGI", new Vector2(0, 0), new Color(0.2f, 0.65f, 0.35f), OnRestartClicked);
        CreateButton(losePanel.transform, "MENU UTAMA", new Vector2(0, -100), new Color(0.25f, 0.4f, 0.7f), OnMainMenuClicked);

        menuPanel.SetActive(false);
        winPanel.SetActive(false);
        losePanel.SetActive(false);
    }

    private GameObject CreateOverlay(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = color;
        return go;
    }

    private Text CreateLabel(Transform parent, string content, int size, FontStyle style, Color color, Vector2 pos)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1400, size + 30);

        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void CreateButton(Transform parent, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "_Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(380, 76);

        var img = go.GetComponent<Image>();
        img.color = Color.white;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = color;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        CreateLabel(go.transform, label, 34, FontStyle.Bold, Color.white, Vector2.zero);
    }
}
