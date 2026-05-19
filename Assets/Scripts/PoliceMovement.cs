using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class PoliceMovement : MonoBehaviour
{
    private const float GroundSnapEpsilon = 0.02f;

    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float sprintMultiplier = 1.5f;
    public float acceleration = 35f;
    [Range(0f, 1f)] public float airControl = 0.45f;
    public float turnSpeed = 160f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.2f;

    [Header("Jump")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private static readonly RaycastHit[] GroundSnapHits = new RaycastHit[16];

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Animator animator;

    private Vector2 moveInput;
    private Vector3 desiredMoveDirection;
    private bool isGrounded;
    private bool sprintActionHeld;
    private bool sprintHeld;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        EnsurePhysicsSetup();
        SnapToGroundOnStart();
        ResolveCameraTransform();
        EnsureCameraFollow();
    }

    private void Reset()
    {
        EnsurePhysicsSetup();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            QueueJump();
        }
    }

    public void OnSprint(InputValue value)
    {
        sprintActionHeld = value.isPressed;
    }

    private void Update()
    {
        ResolveCameraTransform();
        ReadKeyboardShortcuts();
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        ApplyTurning();
        desiredMoveDirection = GetMoveDirection();

        ApplyHorizontalMovement();
        ApplyJump();
        ApplyGravity();
        CancelPhysicsRotation();
        UpdateAnimator();
    }

    private void EnsurePhysicsSetup()
    {
        animator = GetComponent<Animator>();
        capsule = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();

        CharacterController legacyController = GetComponent<CharacterController>();
        if (legacyController != null)
        {
            capsule.center = legacyController.center;
            capsule.height = legacyController.height;
            capsule.radius = legacyController.radius;
            legacyController.enabled = false;
        }
        else if (capsule.height <= 0f)
        {
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.3f;
        }

        animator.applyRootMotion = false;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = false;
    }

    private void SnapToGroundOnStart()
    {
        if (capsule == null)
        {
            return;
        }

        bool originalCapsuleState = capsule.enabled;
        capsule.enabled = false;

        Vector3 rayOrigin = transform.position + Vector3.up * 5f;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            GroundSnapHits,
            50f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit? closestValidHit = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = GroundSnapHits[i];

            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!closestValidHit.HasValue || hit.distance < closestValidHit.Value.distance)
            {
                closestValidHit = hit;
            }
        }

        if (closestValidHit.HasValue)
        {
            float capsuleBottomOffset = capsule.center.y - (capsule.height * 0.5f);
            Vector3 snappedPosition = transform.position;
            snappedPosition.y = closestValidHit.Value.point.y - capsuleBottomOffset + GroundSnapEpsilon;
            transform.position = snappedPosition;
        }

        capsule.enabled = originalCapsuleState;
        Physics.SyncTransforms();
    }

    private void ResolveCameraTransform()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void EnsureCameraFollow()
    {
        if (cameraTransform == null)
        {
            return;
        }

        PlayerCameraFollow cameraFollow = cameraTransform.GetComponent<PlayerCameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = cameraTransform.gameObject.AddComponent<PlayerCameraFollow>();
        }

        cameraFollow.Initialize(transform);
    }

    private void ReadKeyboardShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            sprintHeld = sprintActionHeld;
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            QueueJump();
        }

        sprintHeld = sprintActionHeld || keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    private void QueueJump()
    {
        lastJumpPressedTime = Time.time;
    }

    private Vector3 GetMoveDirection()
    {
        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);

        Vector3 forward = rb.rotation * Vector3.forward;
        if (forward.sqrMagnitude > 0f)
        {
            forward.Normalize();
        }

        return forward * clampedInput.y;
    }

    private void ApplyTurning()
    {
        float turnInput = Mathf.Clamp(moveInput.x, -1f, 1f);
        if (Mathf.Abs(turnInput) < 0.0001f)
        {
            return;
        }

        float yawDelta = turnInput * turnSpeed * Time.fixedDeltaTime;
        Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, Vector3.up);
        rb.MoveRotation(rb.rotation * yawRotation);
    }

    private void ApplyHorizontalMovement()
    {
        float targetSpeed = moveSpeed * (sprintHeld ? sprintMultiplier : 1f);
        Vector3 targetVelocity = desiredMoveDirection * targetSpeed;
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        float control = isGrounded ? 1f : airControl;
        Vector3 velocityDelta = targetVelocity - horizontalVelocity;
        Vector3 requiredAcceleration = velocityDelta / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        Vector3 clampedAcceleration = Vector3.ClampMagnitude(requiredAcceleration, acceleration * control);

        rb.AddForce(clampedAcceleration, ForceMode.Acceleration);
    }

    private void ApplyJump()
    {
        bool hasBufferedJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canUseGroundedJump = Time.time - lastGroundedTime <= coyoteTime;

        if (!hasBufferedJump || !canUseGroundedJump)
        {
            return;
        }

        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;

        Vector3 currentVelocity = rb.linearVelocity;
        if (currentVelocity.y < 0f)
        {
            currentVelocity.y = 0f;
            rb.linearVelocity = currentVelocity;
        }

        float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * Mathf.Min(gravity, -0.01f));
        rb.AddForce(Vector3.up * jumpVelocity, ForceMode.Impulse);
        isGrounded = false;
    }

    private void ApplyGravity()
    {
        rb.AddForce(Vector3.up * Mathf.Min(gravity, -0.01f), ForceMode.Acceleration);
    }

    private void CancelPhysicsRotation()
    {
        rb.angularVelocity = Vector3.zero;
    }

    private bool CheckGrounded()
    {
        Bounds capsuleBounds = capsule.bounds;
        float probeRadius = Mathf.Max(0.05f, capsuleBounds.extents.x * 0.8f);
        Vector3 probeOrigin = new Vector3(
            capsuleBounds.center.x,
            capsuleBounds.min.y + probeRadius + 0.05f,
            capsuleBounds.center.z
        );

        int hitCount = Physics.SphereCastNonAlloc(
            probeOrigin,
            probeRadius,
            Vector3.down,
            groundHits,
            groundCheckDistance + 0.05f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (hitCollider == capsule || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void UpdateAnimator()
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float maxAnimatedSpeed = Mathf.Max(moveSpeed * sprintMultiplier, 0.01f);
        float normalizedSpeed = Mathf.Clamp01(horizontalVelocity.magnitude / maxAnimatedSpeed);
        animator.SetFloat(SpeedHash, normalizedSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        CapsuleCollider currentCapsule = GetComponent<CapsuleCollider>();
        if (currentCapsule == null)
        {
            return;
        }

        Bounds capsuleBounds = currentCapsule.bounds;
        float probeRadius = Mathf.Max(0.05f, capsuleBounds.extents.x * 0.8f);
        Vector3 probePosition = new Vector3(
            capsuleBounds.center.x,
            capsuleBounds.min.y + probeRadius + 0.05f,
            capsuleBounds.center.z
        );

        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(probePosition, probeRadius);
        Gizmos.DrawLine(probePosition, probePosition + Vector3.down * (groundCheckDistance + 0.05f));
    }
}

[DisallowMultipleComponent]
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 3f, -5f);
    [SerializeField] private float positionSmoothTime = 0.12f;
    [SerializeField] private float rotationSmoothSpeed = 10f;
    [SerializeField] private float lookHeight = 1.35f;
    [SerializeField] private float lookAheadDistance = 3f;

    private Vector3 currentVelocity;

    public void Initialize(Transform followTarget)
    {
        target = followTarget;

        if (target == null)
        {
            return;
        }

        SnapImmediately();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Quaternion targetYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Vector3 desiredPosition = target.position + targetYaw * followOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            positionSmoothTime
        );

        Vector3 lookTarget = target.position + Vector3.up * lookHeight + targetYaw * Vector3.forward * lookAheadDistance;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    private void SnapImmediately()
    {
        if (target == null)
        {
            return;
        }

        Quaternion targetYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        transform.position = target.position + targetYaw * followOffset;

        Vector3 lookTarget = target.position + Vector3.up * lookHeight + targetYaw * Vector3.forward * lookAheadDistance;
        transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
    }
}
