using UnityEngine;

/// <summary>
/// Animasi prosedural untuk karakter berbentuk kapsul (tanpa rig/Animator).
/// Saat Play, script membuat "Visual" (badan + mata + dua lengan) sebagai child dan
/// menyembunyikan MeshRenderer asli, jadi Collider / CharacterController / NavMeshAgent
/// di objek utama tidak ikut terganggu.
/// Animasi: idle (napas), jalan/lari (ayun lengan, bobbing, condong), serang (ayun lengan
/// + dorongan ke depan), terkena hit (berkedip merah), mati (roboh).
/// Pasang di Player dan Enemy.
/// </summary>
public class ProceduralAnimator : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float strideRate = 2.2f;      // rad per (m/s)
    [SerializeField] private float bobHeight = 0.07f;
    [SerializeField] private float maxLean = 22f;          // derajat
    [SerializeField] private float maxArmSwing = 55f;      // derajat

    [Header("Attack")]
    [SerializeField] private float attackDuration = 0.28f;
    [SerializeField] private float enemyAttackInterval = 1.5f;
    [SerializeField] private float lungeDistance = 0.25f;

    [Header("Feedback")]
    [SerializeField] private float hurtFlashDuration = 0.18f;
    [SerializeField] private bool tintByEnemyState = true;

    // Komponen game (opsional, dicari otomatis)
    private PlayerHealth playerHealth;
    private PlayerAttack playerAttack;
    private EnemyHealth enemyHealth;
    private EnemyFSM enemyFsm;

    // Visual
    private Transform visual, armL, armR;
    private Material bodyMat;
    private Color baseColor = Color.white;
    private bool hasColor;

    // State animasi
    private Vector3 lastPos;
    private float speed;
    private float phase;
    private float lean;
    private float attackT = 1f;      // 0..1, >=1 artinya tidak sedang menyerang
    private float enemyAttackTimer;
    private float hurtUntil;
    private float lastHp;
    private float deadBlend;         // 0 = hidup, 1 = rebah
    private bool wasPlayerAttacking;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerAttack = GetComponent<PlayerAttack>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyFsm = GetComponent<EnemyFSM>();

        BuildVisual();
        lastPos = transform.position;
        lastHp = CurrentHp();
    }

    // =====================================================
    // BUILD
    // =====================================================

    private void BuildVisual()
    {
        var rootRenderer = GetComponent<MeshRenderer>();
        Material source = rootRenderer != null ? rootRenderer.sharedMaterial : null;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        // Badan (kapsul identik dengan mesh asli karena mewarisi scale parent).
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(visual, false);

        var bodyRenderer = body.GetComponent<MeshRenderer>();
        if (source != null) bodyRenderer.sharedMaterial = source;
        bodyMat = bodyRenderer.material; // instance sendiri agar bisa diwarnai

        if (bodyMat.HasProperty("_BaseColor") || bodyMat.HasProperty("_Color"))
        {
            baseColor = bodyMat.color;
            hasColor = true;
        }

        if (rootRenderer != null) rootRenderer.enabled = false;

        // Mata (penanda arah hadap).
        CreateEye(new Vector3(-0.18f, 0.55f, 0.42f));
        CreateEye(new Vector3(0.18f, 0.55f, 0.42f));

        // Lengan: pivot di bahu, kubus menggantung ke bawah.
        armL = CreateArm("ArmL", -0.62f);
        armR = CreateArm("ArmR", 0.62f);
    }

    private void CreateEye(Vector3 localPos)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        Destroy(eye.GetComponent<Collider>());
        eye.transform.SetParent(visual, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.17f;
        eye.GetComponent<MeshRenderer>().material.color = Color.black;
    }

    private Transform CreateArm(string name, float x)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(visual, false);
        pivot.localPosition = new Vector3(x, 0.55f, 0f);

        GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = "Mesh";
        Destroy(mesh.GetComponent<Collider>());
        mesh.transform.SetParent(pivot, false);
        mesh.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        mesh.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f);
        mesh.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

        return pivot;
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || visual == null)
            return; // game di-pause

        UpdateSpeed(dt);
        UpdateAttackTrigger(dt);
        UpdateHurt();

        bool dead = IsDead();
        deadBlend = Mathf.MoveTowards(deadBlend, dead ? 1f : 0f, dt * 3f);

        Animate(dt, dead);
        UpdateColor(dead);
    }

    private void UpdateSpeed(float dt)
    {
        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        lastPos = transform.position;

        float instant = delta.magnitude / dt;
        speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-12f * dt));
        if (speed < 0.05f) speed = 0f;

        phase += speed * strideRate * dt;
    }

    private void UpdateAttackTrigger(float dt)
    {
        // Player: mulai ayunan saat PlayerAttack baru saja menyerang.
        if (playerAttack != null)
        {
            bool attacking = playerAttack.IsAttacking;
            if (attacking && !wasPlayerAttacking) attackT = 0f;
            wasPlayerAttacking = attacking;
        }

        // Enemy: ayunan berkala selama state Attack.
        if (enemyFsm != null)
        {
            if (enemyFsm.CurrentState == EnemyFSM.EnemyState.Attack)
            {
                enemyAttackTimer -= dt;
                if (enemyAttackTimer <= 0f)
                {
                    attackT = 0f;
                    enemyAttackTimer = enemyAttackInterval;
                }
            }
            else
            {
                enemyAttackTimer = 0f;
            }
        }

        if (attackT < 1f)
            attackT = Mathf.Min(1f, attackT + dt / attackDuration);
    }

    private void UpdateHurt()
    {
        float hp = CurrentHp();
        if (hp < lastHp - 0.01f) hurtUntil = Time.time + hurtFlashDuration;
        lastHp = hp;
    }

    private void Animate(float dt, bool dead)
    {
        float moving = Mathf.Clamp01(speed / 1.5f);
        float alive = 1f - deadBlend;

        // --- Bobbing & napas ---
        float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moving;
        float breathe = Mathf.Sin(Time.time * 2f) * 0.015f * (1f - moving);

        // --- Condong ke depan mengikuti kecepatan (lari lebih condong) ---
        float targetLean = Mathf.Clamp(speed * 2.4f, 0f, maxLean);
        lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-8f * dt));

        // --- Serang: dorongan ke depan + ayun lengan kanan ---
        float punch = attackT < 1f ? Mathf.Sin(attackT * Mathf.PI) : 0f;
        float lunge = punch * lungeDistance;

        // --- Pose badan ---
        Vector3 pos = new Vector3(0f, bob * alive, lunge * alive);
        Quaternion rot = Quaternion.Euler(lean * alive + punch * 12f * alive, 0f, 0f);
        Vector3 scl = new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f - breathe * 0.5f);

        // --- Rebah saat mati: roboh ke samping & turun ---
        if (deadBlend > 0f)
        {
            pos = Vector3.Lerp(pos, new Vector3(0f, -0.45f, 0f), deadBlend);
            rot = Quaternion.Slerp(rot, Quaternion.Euler(0f, 0f, 90f), deadBlend);
        }

        visual.localPosition = pos;
        visual.localRotation = rot;
        visual.localScale = scl;

        // --- Lengan ---
        float swing = Mathf.Sin(phase) * maxArmSwing * moving * alive;
        float armLx = -swing;
        float armRx = swing;

        if (attackT < 1f && !dead)
            armRx = Mathf.Lerp(armRx, -130f, punch); // ayunan serangan

        armL.localRotation = Quaternion.Euler(armLx, 0f, 0f);
        armR.localRotation = Quaternion.Euler(armRx, 0f, 0f);
    }

    private void UpdateColor(bool dead)
    {
        if (!hasColor) return;

        Color c = baseColor;

        if (dead)
        {
            c = Color.Lerp(baseColor, Color.gray, 0.7f);
        }
        else if (Time.time < hurtUntil)
        {
            c = Color.red;
        }
        else if (tintByEnemyState && enemyFsm != null)
        {
            var st = enemyFsm.CurrentState;

            if (st != EnemyFSM.EnemyState.Patrol && st != EnemyFSM.EnemyState.Dead)
                c = Color.Lerp(baseColor, StateTint(st), 0.55f);
        }

        bodyMat.color = c;
    }

    private static Color StateTint(EnemyFSM.EnemyState s)
    {
        switch (s)
        {
            case EnemyFSM.EnemyState.Alert:  return new Color(1f, 0.65f, 0.1f);
            case EnemyFSM.EnemyState.Chase:  return new Color(1f, 0.35f, 0.25f);
            case EnemyFSM.EnemyState.Search: return new Color(0.3f, 0.9f, 0.9f);
            case EnemyFSM.EnemyState.Attack: return new Color(1f, 0.1f, 0.1f);
            case EnemyFSM.EnemyState.Flee:   return new Color(0.8f, 0.45f, 1f);
            case EnemyFSM.EnemyState.Heal:   return new Color(0.4f, 0.95f, 0.5f);
            default: return Color.white; // Patrol / Dead
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private float CurrentHp()
    {
        if (playerHealth != null) return playerHealth.CurrentHealth;
        if (enemyHealth != null) return enemyHealth.CurrentHealth;
        return 0f;
    }

    private bool IsDead()
    {
        if (playerHealth != null) return playerHealth.IsDead;
        if (enemyHealth != null) return enemyHealth.IsDead;
        return false;
    }
}
