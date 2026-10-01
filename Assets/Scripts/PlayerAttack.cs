using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kemampuan serang Player: tekan Space atau klik kiri mouse.
/// Menyerang semua EnemyHealth di depan Player dalam jarak & sudut tertentu.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField, Range(10f, 360f)] private float attackAngle = 120f;
    [SerializeField] private float cooldown = 0.5f;

    [Header("Feedback")]
    [SerializeField] private float attackStateDuration = 0.25f;

    private float nextAttackTime;
    private float attackingUntil;
    private PlayerHealth health;

    public bool IsAttacking => Time.time < attackingUntil;

    /// <summary>0 = baru menyerang, 1 = siap menyerang lagi.</summary>
    public float CooldownReady =>
        cooldown <= 0f ? 1f : Mathf.Clamp01(1f - (nextAttackTime - Time.time) / cooldown);

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        // Jangan menyerang saat game di-pause (menu / layar menang-kalah).
        if (Time.timeScale == 0f)
            return;

        bool pressed =
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (pressed && Time.time >= nextAttackTime)
            Attack();
    }

    private void Attack()
    {
        nextAttackTime = Time.time + cooldown;
        attackingUntil = Time.time + attackStateDuration;

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        bool hitSomething = false;

        // Satu musuh bisa punya banyak collider; pastikan hanya kena sekali per serangan.
        var alreadyHit = new HashSet<EnemyHealth>();

        foreach (Collider col in hits)
        {
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();

            if (enemy == null || enemy.IsDead || !alreadyHit.Add(enemy))
                continue;

            Vector3 dir = enemy.transform.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, dir) > attackAngle * 0.5f)
                continue;

            enemy.TakeDamage(damage);
            hitSomething = true;
            Debug.Log("Player attacks Enemy! (-" + damage + ")");
        }

        if (!hitSomething)
            Debug.Log("Player attack missed.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Vector3 left = Quaternion.Euler(0f, -attackAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, attackAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position, left * attackRange);
        Gizmos.DrawRay(transform.position, right * attackRange);
    }
}
