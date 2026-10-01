using UnityEngine;
using UnityEngine.AI;

public class EnemyFSM : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Alert,
        Chase,
        Search,
        Attack,
        Flee,
        Heal,
        Dead
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private EnemyHealth health;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waypointTolerance = 0.5f;

    [Header("Alert")]
    [SerializeField] private float alertMoveSpeed = 2.5f;
    [SerializeField] private float alertInvestigateDuration = 3f;
    [SerializeField] private float alertRotateSpeed = 90f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float lostPlayerDelay = 2f;

    [Header("Search")]
    [SerializeField] private float searchSpeed = 3f;
    [SerializeField] private float searchDuration = 4f;
    [SerializeField] private float searchRotateSpeed = 60f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackExitRange = 2.75f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Flee")]
    [SerializeField] private Transform safePoint;
    [SerializeField] private float fleeSpeed = 5f;
    [SerializeField] private float lowHealthThreshold = 30f;
    [SerializeField] private float safeDistance = 12f;
    [SerializeField] private float safePointTolerance = 1f;

    [Header("Heal")]
    [SerializeField] private float healRatePerSecond = 15f;

    [Header("Debug")]
    [SerializeField] private EnemyState currentState;

    private int currentPatrolIndex = 0;

    private float lostPlayerTimer = 0f;
    private float nextAttackTime = 0f;

    // =====================================
    // MEMORY: posisi terakhir Player TERKONFIRMASI terlihat.
    // Diisi terus-menerus selama Chase (dan saat Alert berhasil
    // dikonfirmasi), lalu dipakai oleh state Search sebagai
    // tujuan pencarian setelah Player hilang dari pandangan.
    // =====================================
    private Vector3 lastSeenPosition;
    private float searchTimer = 0f;
    private bool reachedLastSeenPosition = false;

    // =====================================
    // MEMORY SEMENTARA: posisi "sesuatu" yang baru terdeteksi
    // samar (belum tentu Player). Dipakai oleh state Alert
    // sebagai tujuan penyelidikan.
    // =====================================
    private Vector3 alertPosition;
    private float alertTimer = 0f;
    private bool reachedAlertPosition = false;

    private bool fleeTriggered = false;

    private PlayerHealth playerHealth;

    public EnemyState CurrentState => currentState;
    public Vector3 LastSeenPosition => lastSeenPosition;
    public Vector3 AlertPosition => alertPosition;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (perception == null)
            perception = GetComponent<EnemyPerception>();

        if (health == null)
            health = GetComponent<EnemyHealth>();

        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        // Fallback: kalau waypoint / safe point belum di-assign di Inspector,
        // ambil otomatis dari objek "PatrolPoints" (semua child-nya) dan "SafePoint".
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            GameObject root = GameObject.Find("PatrolPoints");

            if (root != null && root.transform.childCount > 0)
            {
                patrolPoints = new Transform[root.transform.childCount];

                for (int i = 0; i < patrolPoints.Length; i++)
                    patrolPoints[i] = root.transform.GetChild(i);
            }
            else
            {
                Debug.LogWarning(name + ": patrolPoints kosong & objek 'PatrolPoints' tidak ditemukan.");
            }
        }

        if (safePoint == null)
        {
            GameObject sp = GameObject.Find("SafePoint");

            if (sp != null)
                safePoint = sp.transform;
        }
    }

    private void Start()
    {
        ChangeState(EnemyState.Patrol);
    }

    private void Update()
    {
        // ================================
        // GLOBAL TRANSITION PRIORITY
        // ================================

        if (health.IsDead)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        if (!fleeTriggered &&
            health.CurrentHealth <= lowHealthThreshold &&
            currentState != EnemyState.Flee)
        {
            fleeTriggered = true;
            ChangeState(EnemyState.Flee);
        }

        // ================================
        // UPDATE CURRENT STATE
        // ================================

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;

            case EnemyState.Alert:
                UpdateAlert();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            case EnemyState.Search:
                UpdateSearch();
                break;

            case EnemyState.Attack:
                UpdateAttack();
                break;

            case EnemyState.Flee:
                UpdateFlee();
                break;

            case EnemyState.Heal:
                UpdateHeal();
                break;

            case EnemyState.Dead:
                UpdateDead();
                break;
        }
    }

    // =====================================
    // CHANGE STATE
    // =====================================

    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
            return;

        ExitState(currentState);

        currentState = newState;

        Debug.Log(
            gameObject.name +
            " → State: " +
            currentState
        );

        EnterState(currentState);
    }

    // =====================================
    // ENTER STATE
    // =====================================

    private void EnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Patrol:

                agent.isStopped = false;
                agent.speed = patrolSpeed;

                SetPatrolDestination();

                break;

            case EnemyState.Alert:

                agent.isStopped = false;
                agent.speed = alertMoveSpeed;

                reachedAlertPosition = false;
                alertTimer = 0f;

                agent.SetDestination(
                    alertPosition
                );

                break;

            case EnemyState.Chase:

                agent.isStopped = false;
                agent.speed = chaseSpeed;

                lostPlayerTimer = 0f;

                break;

            case EnemyState.Search:

                agent.isStopped = false;
                agent.speed = searchSpeed;

                reachedLastSeenPosition = false;
                searchTimer = 0f;

                agent.SetDestination(
                    lastSeenPosition
                );

                break;

            case EnemyState.Attack:

                agent.isStopped = true;
                agent.ResetPath();

                break;

            case EnemyState.Flee:

                agent.isStopped = false;
                agent.speed = fleeSpeed;

                if (safePoint != null)
                {
                    agent.SetDestination(
                        safePoint.position
                    );
                }

                break;

            case EnemyState.Heal:

                // Enemy sudah sampai SafePoint → diam & pulih.
                agent.isStopped = true;
                agent.ResetPath();

                break;

            case EnemyState.Dead:

                agent.isStopped = true;
                agent.ResetPath();

                break;
        }
    }

    // =====================================
    // EXIT STATE
    // =====================================

    private void ExitState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Attack:
                agent.isStopped = false;
                break;

            case EnemyState.Alert:
                agent.isStopped = false;
                break;

            case EnemyState.Search:
                agent.isStopped = false;
                break;

            case EnemyState.Heal:
                agent.isStopped = false;
                break;
        }
    }

    // =====================================
    // PATROL
    // =====================================

    private void UpdatePatrol()
    {
        // Ada tanda-tanda (samar ataupun pasti) → selidiki dulu
        // lewat Alert, bukan langsung Chase.
        if (perception.CanDetectSomething ||
            perception.CanSeePlayer)
        {
            alertPosition = perception.DetectedPosition;

            ChangeState(EnemyState.Alert);
            return;
        }

        if (patrolPoints == null ||
            patrolPoints.Length == 0)
            return;

        if (!agent.pathPending &&
            agent.remainingDistance <=
            waypointTolerance)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >=
                patrolPoints.Length)
            {
                currentPatrolIndex = 0;
            }

            SetPatrolDestination();
        }
    }

    private void SetPatrolDestination()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
            return;

        Transform point =
            patrolPoints[currentPatrolIndex];

        if (point != null)
        {
            agent.SetDestination(
                point.position
            );
        }
    }

    // =====================================
    // ALERT
    // =====================================

    private void UpdateAlert()
    {
        // Player dikonfirmasi terlihat jelas → yakin, langsung Chase.
        if (perception.CanSeePlayer)
        {
            lastSeenPosition = player.position;

            ChangeState(EnemyState.Chase);
            return;
        }

        // Masih ada tanda-tanda (deteksi samar) → terus perbarui
        // titik tujuan penyelidikan & reset waktu menyerah.
        if (perception.CanDetectSomething)
        {
            alertPosition = perception.DetectedPosition;

            agent.isStopped = false;
            agent.SetDestination(alertPosition);

            reachedAlertPosition = false;
            alertTimer = 0f;

            return;
        }

        // Tidak ada tanda-tanda lagi → tetap selidiki titik
        // terakhir sebentar sebelum menyerah.
        if (!reachedAlertPosition)
        {
            if (!agent.pathPending &&
                agent.remainingDistance <=
                waypointTolerance)
            {
                reachedAlertPosition = true;

                agent.isStopped = true;
                agent.ResetPath();
            }
        }
        else
        {
            transform.Rotate(
                Vector3.up,
                alertRotateSpeed * Time.deltaTime
            );
        }

        alertTimer += Time.deltaTime;

        if (alertTimer >= alertInvestigateDuration)
        {
            // Gagal terkonfirmasi → anggap salah alarm, balik Patrol.
            ChangeState(EnemyState.Patrol);
            return;
        }
    }

    // =====================================
    // CHASE
    // =====================================

    private void UpdateChase()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (perception.CanSeePlayer)
        {
            lostPlayerTimer = 0f;

            // Player masih terlihat → update Memory posisi
            // terakhirnya, supaya kalau nanti hilang lagi,
            // Search tahu ke mana harus dituju.
            lastSeenPosition = player.position;

            agent.SetDestination(
                player.position
            );

            if (distance <= attackRange)
            {
                ChangeState(
                    EnemyState.Attack
                );

                return;
            }
        }
        else
        {
            lostPlayerTimer +=
                Time.deltaTime;

            if (lostPlayerTimer >=
                lostPlayerDelay)
            {
                // Player hilang cukup lama → jangan langsung
                // Patrol, tapi coba cari dulu ke posisi
                // terakhir Player terlihat (Search).
                ChangeState(
                    EnemyState.Search
                );

                return;
            }
        }
    }

    // =====================================
    // SEARCH
    // =====================================

    private void UpdateSearch()
    {
        // Kalau Player ketemu lagi selama pencarian,
        // langsung kembali mengejar.
        if (perception.CanSeePlayer)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        if (!reachedLastSeenPosition)
        {
            // Masih dalam perjalanan menuju lastSeenPosition.
            if (!agent.pathPending &&
                agent.remainingDistance <=
                waypointTolerance)
            {
                reachedLastSeenPosition = true;

                agent.isStopped = true;
                agent.ResetPath();
            }

            return;
        }

        // Sudah sampai di lastSeenPosition tapi Player tidak
        // ada di sana → "menoleh-noleh" mencari selama
        // searchDuration detik sebelum akhirnya menyerah.
        transform.Rotate(
            Vector3.up,
            searchRotateSpeed * Time.deltaTime
        );

        searchTimer += Time.deltaTime;

        if (searchTimer >= searchDuration)
        {
            // Gagal menemukan Player → kembali Patrol.
            ChangeState(EnemyState.Patrol);
            return;
        }
    }

    // =====================================
    // ATTACK
    // =====================================

    private void UpdateAttack()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        FacePlayer();

        if (!perception.CanSeePlayer ||
            distance > attackExitRange)
        {
            ChangeState(
                EnemyState.Chase
            );

            return;
        }

        if (Time.time >= nextAttackTime)
        {
            AttackPlayer();

            nextAttackTime =
                Time.time +
                attackCooldown;
        }
    }

    private void AttackPlayer()
    {
        Debug.Log("Enemy attacks Player!");

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(
                attackDamage
            );
        }
    }

    private void FacePlayer()
    {
        Vector3 direction =
            player.position -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                10f * Time.deltaTime
            );
    }

    // =====================================
    // FLEE
    // =====================================

    private void UpdateFlee()
    {
        if (safePoint == null)
            return;

        float playerDistance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        float safePointDistance =
            Vector3.Distance(
                transform.position,
                safePoint.position
            );

        // Sudah benar-benar sampai SafePoint → berhenti & pulihkan HP.
        if (safePointDistance <= safePointTolerance)
        {
            ChangeState(
                EnemyState.Heal
            );

            return;
        }

        // Player sudah cukup jauh walau belum sampai SafePoint
        // (mis. sudah kehilangan kejaran) → aman, balik Patrol.
        if (playerDistance >= safeDistance)
        {
            ChangeState(
                EnemyState.Patrol
            );

            return;
        }
    }

    // =====================================
    // HEAL
    // =====================================

    private void UpdateHeal()
    {
        health.Heal(
            healRatePerSecond * Time.deltaTime
        );

        if (health.IsFullHealth)
        {
            // HP pulih sepenuhnya → boleh Flee lagi nanti
            // kalau HP turun rendah di masa depan.
            fleeTriggered = false;

            ChangeState(EnemyState.Patrol);
            return;
        }
    }

    // =====================================
    // DEAD
    // =====================================

    private void UpdateDead()
    {
        // Tidak melakukan action.
    }

    // =====================================
    // DEBUG GIZMOS
    // =====================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        Gizmos.color = Color.magenta;

        Gizmos.DrawWireSphere(
            transform.position,
            attackExitRange
        );

        if (currentState == EnemyState.Alert)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f);

            Gizmos.DrawWireSphere(
                alertPosition,
                waypointTolerance
            );

            Gizmos.DrawLine(
                transform.position,
                alertPosition
            );
        }

        if (currentState == EnemyState.Search)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireSphere(
                lastSeenPosition,
                waypointTolerance
            );

            Gizmos.DrawLine(
                transform.position,
                lastSeenPosition
            );
        }
    }
}
