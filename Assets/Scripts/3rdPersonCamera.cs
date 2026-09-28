using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target & Distance")]
    [Tooltip("Transform karakter/player yang diikuti kamera")]
    public Transform target;
    [Tooltip("Jarak kamera dari target")]
    public float distance = 5.0f;
    [Tooltip("Tinggi titik fokus kamera relatif dari target")]
    public float heightOffset = 1.5f;

    [Header("Sensitivitas & Batas Rotasi")]
    public float sensitivityX = 0.15f;
    public float sensitivityY = 0.15f;
    public float minVerticalAngle = -20.0f;
    public float maxVerticalAngle = 60.0f;

    [Header("Kehalusan Gerakan")]
    public float smoothSpeed = 10.0f;

    private float currentX = 0.0f;
    private float currentY = 0.0f;

    void Start()
    {
        // Menyembunyikan dan mengunci kursor mouse di tengah layar
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Membaca input delta mouse menggunakan New Input System
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            currentX += mouseDelta.x * sensitivityX;
            currentY -= mouseDelta.y * sensitivityY;

            // Batasi sudut rotasi vertikal agar kamera tidak berputar terbalik
            currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);
        }

        // Tekan Escape untuk memunculkan kembali kursor mouse
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Hitung posisi dan rotasi kamera
        Vector3 targetPosition = target.position + Vector3.up * heightOffset;
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // Hitung offset posisi kamera ke belakang target sesuai rotasi
        Vector3 desiredPosition = targetPosition - (rotation * Vector3.forward * distance);

        // Pergerakan kamera yang halus (Smooth Interpolation)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
        transform.LookAt(targetPosition);
    }
}