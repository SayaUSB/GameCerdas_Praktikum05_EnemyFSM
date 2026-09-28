using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public Transform cameraTransform; // Drag Main Camera ke kolom ini

    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Cari Main Camera otomatis jika tidak di-assign di Inspector
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        // 1. Baca input keyboard
        float x = 0;
        float z = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z = -1;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) z = 1;
        }

        Vector3 inputDir = new Vector3(x, 0, z).normalized;

        // 2. Jika ada tombol arah yang ditekan
        if (inputDir.magnitude >= 0.1f)
        {
            // Hitung arah gerak relatif terhadap orientasi horizontal kamera
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            // Abaikan rotasi atas/bawah (pitch) kamera
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDirection = (camForward * inputDir.z + camRight * inputDir.x).normalized;

            // Putar badan player ke arah gerak tersebut secara halus
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            // Gerakkan player
            if (controller != null)
            {
                controller.Move(moveDirection * moveSpeed * Time.deltaTime);
            }
            else
            {
                transform.position += moveDirection * moveSpeed * Time.deltaTime;
            }
        }
    }
}