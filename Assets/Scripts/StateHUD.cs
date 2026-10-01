using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD runtime untuk menampilkan state Player (kiri atas) dan Enemy (kanan atas).
/// Cukup tempel script ini ke satu GameObject kosong di scene (mis. "StateHUD").
/// Canvas & semua elemen UI dibuat otomatis saat Play, tanpa setup manual.
/// Referensi bisa diisi di Inspector; jika kosong dicari otomatis di scene.
/// </summary>
public class StateHUD : MonoBehaviour
{
    [Header("References (opsional, dicari otomatis jika kosong)")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private EnemyFSM enemy;

    [Header("Player")]
    [SerializeField] private float movingThreshold = 0.05f; // m/s
    [SerializeField] private float hurtDuration = 0.6f;     // detik

    // ---------- Player ----------
    private Text playerStateText, playerHpText;
    private RectTransform playerHpFill;
    private PlayerAttack playerAttack;
    private SimplePlayerController playerController;
    private Vector3 lastPlayerPos;
    private float lastPlayerHp;
    private float hurtUntil;

    // ---------- Enemy ----------
    private Text enemyStateText, enemyHpText, enemyPerceptionText, enemyDistanceText;
    private RectTransform enemyHpFill;
    private EnemyHealth enemyHealth;
    private EnemyPerception enemyPerception;

    private Font font;

    private void Awake()
    {
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        // Pakai komponen pertama di objek Player (sama seperti EnemyFSM).
        if (player != null) player = player.GetComponent<PlayerHealth>();
        if (enemy == null) enemy = FindAnyObjectByType<EnemyFSM>();

        if (enemy != null)
        {
            enemyHealth = enemy.GetComponent<EnemyHealth>();
            enemyPerception = enemy.GetComponent<EnemyPerception>();
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    private void Start()
    {
        if (player != null)
        {
            playerAttack = player.GetComponent<PlayerAttack>();
            playerController = player.GetComponent<SimplePlayerController>();
            lastPlayerPos = player.transform.position;
            lastPlayerHp = player.CurrentHealth;
        }
    }

    private void Update()
    {
        UpdatePlayerPanel();
        UpdateEnemyPanel();
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void UpdatePlayerPanel()
    {
        if (player == null)
        {
            playerStateText.text = "Player tidak ditemukan";
            return;
        }

        float hp = player.CurrentHealth;
        if (hp < lastPlayerHp) hurtUntil = Time.time + hurtDuration;
        lastPlayerHp = hp;

        float speed = Time.deltaTime > 0f
            ? (player.transform.position - lastPlayerPos).magnitude / Time.deltaTime
            : 0f;
        lastPlayerPos = player.transform.position;

        string state;
        Color color;

        if (player.IsDead)                      { state = "DEAD";   color = new Color(0.6f, 0.6f, 0.6f); }
        else if (Time.time < hurtUntil)         { state = "HURT";   color = new Color(1f, 0.35f, 0.35f); }
        else if (playerAttack != null && playerAttack.IsAttacking) { state = "ATTACK"; color = new Color(1f, 0.85f, 0.3f); }
        else if (speed > movingThreshold && playerController != null && playerController.IsSprinting) { state = "SPRINT"; color = new Color(1f, 0.7f, 0.2f); }
        else if (speed > movingThreshold)       { state = "MOVING"; color = new Color(0.4f, 0.9f, 0.5f); }
        else                                    { state = "IDLE";   color = new Color(0.6f, 0.8f, 1f); }

        playerStateText.text = state;
        playerStateText.color = color;
        SetBar(playerHpFill, playerHpText, hp, player.MaxHealth);
    }

    private void UpdateEnemyPanel()
    {
        if (enemy == null)
        {
            enemyStateText.text = "Enemy tidak ditemukan";
            return;
        }

        enemyStateText.text = enemy.CurrentState.ToString().ToUpper();
        enemyStateText.color = StateColor(enemy.CurrentState);

        if (enemyHealth != null)
            SetBar(enemyHpFill, enemyHpText, enemyHealth.CurrentHealth, enemyHealth.MaxHealth);

        if (enemyPerception != null)
        {
            enemyPerceptionText.text =
                "Melihat Player : " + YesNo(enemyPerception.CanSeePlayer) + "\n" +
                "Curiga (Alert) : " + YesNo(enemyPerception.CanDetectSomething);
            enemyDistanceText.text =
                "Jarak ke Player: " + FormatDistance(enemyPerception.DistanceToPlayer);
        }
    }

    private static string YesNo(bool v) => v ? "YA" : "tidak";

    private static string FormatDistance(float d) =>
        float.IsInfinity(d) ? "-" : d.ToString("0.0") + " m";

    private static Color StateColor(EnemyFSM.EnemyState s)
    {
        switch (s)
        {
            case EnemyFSM.EnemyState.Patrol: return new Color(0.6f, 0.8f, 1f);
            case EnemyFSM.EnemyState.Alert:  return new Color(1f, 0.65f, 0.1f);
            case EnemyFSM.EnemyState.Chase:  return new Color(1f, 0.4f, 0.3f);
            case EnemyFSM.EnemyState.Search: return new Color(0.3f, 0.9f, 0.9f);
            case EnemyFSM.EnemyState.Attack: return new Color(1f, 0.2f, 0.2f);
            case EnemyFSM.EnemyState.Flee:   return new Color(0.9f, 0.5f, 1f);
            case EnemyFSM.EnemyState.Heal:   return new Color(0.4f, 0.9f, 0.5f);
            case EnemyFSM.EnemyState.Dead:   return new Color(0.6f, 0.6f, 0.6f);
            default: return Color.white;
        }
    }

    private static void SetBar(RectTransform fill, Text label, float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        fill.anchorMax = new Vector2(ratio, 1f);
        label.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
    }

    // =====================================================
    // BUILD UI
    // =====================================================

    private void BuildUI()
    {
        var canvasGO = new GameObject("StateHUD_Canvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // --- Player panel (kiri atas) ---
        var pPanel = CreatePanel(canvasGO.transform, "PlayerPanel",
            new Vector2(0, 1), new Vector2(20, -20), new Vector2(340, 140));
        CreateText(pPanel, "PLAYER", 20, FontStyle.Bold, Color.white, new Vector2(12, -8), new Vector2(316, 26));
        playerStateText = CreateText(pPanel, "-", 30, FontStyle.Bold, Color.white, new Vector2(12, -36), new Vector2(316, 38));
        CreateBar(pPanel, new Vector2(12, -84), new Vector2(316, 22), new Color(0.3f, 0.85f, 0.4f),
            out playerHpFill, out playerHpText);

        // --- Enemy panel (kanan atas) ---
        var ePanel = CreatePanel(canvasGO.transform, "EnemyPanel",
            new Vector2(1, 1), new Vector2(-20, -20), new Vector2(340, 215));
        CreateText(ePanel, "ENEMY (FSM)", 20, FontStyle.Bold, Color.white, new Vector2(12, -8), new Vector2(316, 26));
        enemyStateText = CreateText(ePanel, "-", 30, FontStyle.Bold, Color.white, new Vector2(12, -36), new Vector2(316, 38));
        CreateBar(ePanel, new Vector2(12, -84), new Vector2(316, 22), new Color(0.9f, 0.3f, 0.3f),
            out enemyHpFill, out enemyHpText);
        enemyPerceptionText = CreateText(ePanel, "", 17, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(12, -118), new Vector2(316, 48));
        enemyDistanceText = CreateText(ePanel, "", 17, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), new Vector2(12, -170), new Vector2(316, 26));
    }

    private RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        return rt;
    }

    private Text CreateText(RectTransform parent, string content, int size, FontStyle style, Color color, Vector2 pos, Vector2 dim)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;

        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.UpperLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void CreateBar(RectTransform parent, Vector2 pos, Vector2 size, Color fillColor,
        out RectTransform fill, out Text label)
    {
        var bg = new GameObject("HpBar", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);

        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0, 1);
        bgRt.anchoredPosition = pos;
        bgRt.sizeDelta = size;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        bgImg.raycastTarget = false;

        var f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(bgRt, false);
        fill = (RectTransform)f.transform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var fImg = f.GetComponent<Image>();
        fImg.color = fillColor;
        fImg.raycastTarget = false;

        label = CreateText(bgRt, "", 16, FontStyle.Bold, Color.white, Vector2.zero, size);
        label.alignment = TextAnchor.MiddleCenter;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }
}
