using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PoliceMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Animator animator;
    
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;
    private bool jumpRequested;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            jumpRequested = true;
        }
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;
        
        // Reset vertical velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Horizontal movement
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        
        if (move.magnitude > 0.1f)
        {
            // Rotate towards movement direction
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // Move
            controller.Move(move * moveSpeed * Time.deltaTime);
        }

        // Vertical movement (Jump & Gravity)
        if (jumpRequested)
        {
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            jumpRequested = false;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Update Animator
        float speed = move.magnitude;
        animator.SetFloat("Speed", speed);
    }
}
