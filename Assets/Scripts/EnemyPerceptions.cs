using UnityEngine;

public class EnemyPerception : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Vision (Confirmed Detection)")]
    [SerializeField] private float visionRange = 10f;
    [SerializeField, Range(0f, 360f)]
    private float visionAngle = 90f;

    [SerializeField] private float eyeHeight = 1f;

    [Header("Alert (Vague Detection)")]
    [Tooltip("Radius & sudut deteksi 'samar' — biasanya lebih lebar dari Vision. " +
             "Dipakai untuk memicu state Alert, belum tentu benar-benar Player.")]
    [SerializeField] private float alertRange = 15f;
    [SerializeField, Range(0f, 360f)]
    private float alertAngle = 140f;

    [Header("Layer")]
    [SerializeField] private LayerMask obstacleMask;

    // Deteksi pasti: Enemy benar-benar melihat Player dengan jelas.
    public bool CanSeePlayer { get; private set; }

    // Deteksi samar: Enemy menyadari "ada sesuatu", belum tentu Player
    // sepenuhnya terkonfirmasi. Dipakai untuk transisi Patrol -> Alert.
    public bool CanDetectSomething { get; private set; }

    // Posisi terakhir kali sesuatu terdeteksi (dipakai Alert sebagai
    // titik tujuan penyelidikan).
    public Vector3 DetectedPosition { get; private set; }

    public float DistanceToPlayer
    {
        get
        {
            if (player == null)
                return Mathf.Infinity;

            return Vector3.Distance(
                transform.position,
                player.position
            );
        }
    }

    private void Update()
    {
        UpdateDetection();
    }

    private void UpdateDetection()
    {
        if (player == null)
        {
            CanSeePlayer = false;
            CanDetectSomething = false;
            return;
        }

        Vector3 origin =
            transform.position +
            Vector3.up * eyeHeight;

        Vector3 target =
            player.position +
            Vector3.up * 0.8f;

        Vector3 direction =
            target - origin;

        float distance = direction.magnitude;

        float angle = Vector3.Angle(
            transform.forward,
            direction
        );

        // Satu raycast dipakai bersama oleh kedua tingkat deteksi,
        // karena origin/direction/distance/obstacleMask-nya sama.
        bool blocked = Physics.Raycast(
            origin,
            direction.normalized,
            distance,
            obstacleMask
        );

        // =====================================
        // ALERT: deteksi samar (radius & sudut lebar).
        // "Enemy menyadari ada sesuatu" — belum tentu yakin itu Player.
        // =====================================

        bool withinAlertRange = distance <= alertRange;
        bool withinAlertAngle = angle <= alertAngle * 0.5f;

        CanDetectSomething =
            withinAlertRange &&
            withinAlertAngle &&
            !blocked;

        if (CanDetectSomething)
        {
            DetectedPosition = player.position;
        }

        // =====================================
        // VISION: deteksi pasti / konfirmasi (radius & sudut sempit).
        // =====================================

        bool withinVisionRange = distance <= visionRange;
        bool withinVisionAngle = angle <= visionAngle * 0.5f;

        CanSeePlayer =
            withinVisionRange &&
            withinVisionAngle &&
            !blocked;

        // Kalau benar-benar terkonfirmasi, itu juga otomatis
        // "terdeteksi" — jaga-jaga kalau alertRange/alertAngle
        // di-set lebih kecil daripada visionRange/visionAngle.
        if (CanSeePlayer)
        {
            CanDetectSomething = true;
            DetectedPosition = player.position;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Oranye = Alert range (deteksi samar).
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireSphere(
            transform.position,
            alertRange
        );

        // Kuning = Vision range (deteksi pasti / confirmed).
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            visionRange
        );

        Vector3 leftDirection =
            Quaternion.Euler(
                0f,
                -visionAngle * 0.5f,
                0f
            ) * transform.forward;

        Vector3 rightDirection =
            Quaternion.Euler(
                0f,
                visionAngle * 0.5f,
                0f
            ) * transform.forward;

        Gizmos.DrawRay(
            transform.position,
            leftDirection * visionRange
        );

        Gizmos.DrawRay(
            transform.position,
            rightDirection * visionRange
        );
    }
}
