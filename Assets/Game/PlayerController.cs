using System.Collections.Generic;
using UnityEngine;

// First-person player.
// WASD = move, Mouse = look, Shift = run (loud), C or Ctrl = crouch (quiet and harder to see)
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float runSpeed = 6.5f;
    public float crouchSpeed = 2f;
    public float mouseSensitivity = 2f;

    public bool IsCrouching { get; private set; }
    public bool IsRunning { get; private set; }
    public List<int> keys = new List<int>();
    public Light flashlight;

    CharacterController controller;
    Transform cam;
    float pitch;
    float yVelocity;
    float stepTimer;

    public bool HasKey(int level) { return keys.Contains(level); }
    public void AddKey(int level) { if (!keys.Contains(level)) keys.Add(level); }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (GameManager.Instance.gameOver) return;
        if (Input.GetKeyDown(KeyCode.F) && flashlight != null) flashlight.enabled = !flashlight.enabled;
        Look();
        Move();
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -80f, 80f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        IsCrouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
        IsRunning = Input.GetKey(KeyCode.LeftShift) && !IsCrouching;

        float speed = walkSpeed;
        if (IsCrouching) speed = crouchSpeed;
        else if (IsRunning) speed = runSpeed;

        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);
        Vector3 move = transform.TransformDirection(input) * speed;

        // simple gravity
        if (controller.isGrounded && yVelocity < 0f) yVelocity = -1f;
        yVelocity -= 9.8f * Time.deltaTime;
        move.y = yVelocity;
        controller.Move(move * Time.deltaTime);

        // crouch makes the player shorter
        float targetHeight = IsCrouching ? 1.1f : 1.8f;
        controller.height = Mathf.Lerp(controller.height, targetHeight, 10f * Time.deltaTime);
        controller.center = new Vector3(0f, controller.height / 2f, 0f);
        cam.localPosition = new Vector3(0f, controller.height - 0.2f, 0f);

        MakeFootstepNoise(input.magnitude > 0.1f);
    }

    // Every footstep tells the enemies where you are (loudness = how far they can hear)
    void MakeFootstepNoise(bool moving)
    {
        if (!moving) { stepTimer = 0f; return; }

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        float loudness = 4f;     // walk
        stepTimer = 0.5f;
        if (IsRunning)  { loudness = 10f; stepTimer = 0.3f; }
        if (IsCrouching){ loudness = 1f;  stepTimer = 0.7f; }

        NoiseSystem.Emit(transform.position, loudness);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayerStep(loudness);
    }
}
