using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerStats))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Movimiento horizontal")]
    [SerializeField, Min(0f)] float baseSpeed = 6f;
    [SerializeField, Min(0f)] float speedPerRoom = 0.6f;
    [SerializeField, Min(0.01f)] float acceleration = 28f;
    [SerializeField, Min(0.01f)] float braking = 24f;
    [SerializeField, Min(0f)] float turnAcceleration = 32f;
    [SerializeField, Range(0f, 1f)] float airControl = 0.6f;
    [Header("Salto y pisotón")]
    [SerializeField, Min(0f)] float jumpSpeed = 9f;
    [SerializeField, Min(0f)] float gravity = 25f;
    [SerializeField, Min(0f)] float maxFallSpeed = 30f;
    [SerializeField, Min(0f)] float poundSpeed = 22f;
    [SerializeField, Min(0f)] float poundSpeedPerRoom = 0.4f;
    [SerializeField, Min(0f)] float coyoteTime = 0.1f;
    [SerializeField, Min(0f)] float jumpBufferTime = 0.12f;
    [SerializeField, Min(0f)] float groundStickSpeed = 2f;
    [Header("Correr y agacharse")]
    [SerializeField, Min(1f)] float sprintSpeedMultiplier = 1.15f;
    [SerializeField, Min(1f)] float sprintAccelerationMultiplier = 1.1f;
    [SerializeField, Range(0.3f, 1f)] float crouchHeightFraction = 0.6f;
    [SerializeField, Range(0f, 1f)] float crouchSpeedFraction = 0.6f;
    [SerializeField, Min(0f)] float slideMinimumSpeed = 5f;
    [SerializeField, Min(0f)] float slideBraking = 3f;
    [SerializeField, Min(1f)] float slideStartSpeedMultiplier = 1.1f;
    [SerializeField, Min(0f)] float slideBoostDuration = 0.3f;

    [Header("Sensaci�n de movimiento (c�mara)")]
    [SerializeField, Min(0.01f)] float crouchCameraTime = 0.12f;
    [SerializeField, Min(0f)] float movementBobAmount = 0.035f;
    [SerializeField, Min(0f)] float movementBobFrequency = 8f;
    [SerializeField, Min(0f)] float speedFovIncrease = 6f;
    [SerializeField, Min(0.01f)] float speedForFullFov = 30f;
    [SerializeField, Min(0.01f)] float movementFeedbackTime = 0.25f;

    [Header("Choque a velocidad extrema")]
    [SerializeField, Min(0f)] float wallCrashMinimumSpeed = 120f;
    [SerializeField, Range(0f, 1f)] float wallCrashMinimumSpeedFraction = 0.95f;
    [Tooltip("Fracción de la velocidad que debe dirigirse hacia la pared.")]
    [SerializeField, Range(0f, 1f)] float wallCrashMinimumHeadOn = 0.55f;
    [SerializeField, Range(0f, 1f)] float wallCrashReboundFraction = 0.2f;
    [SerializeField, Min(0f)] float wallCrashMaximumRebound = 25f;
    [SerializeField, Range(0f, 1f)] float wallCrashTangentialRetention = 0.35f;
    [SerializeField, Min(0f)] float wallCrashRecoveryTime = 0.2f;
    [SerializeField, Min(0f)] float wallCrashCooldown = 0.35f;

    [Header("Seguridad de colisiones")]
    [SerializeField, Min(0.01f)] float maxStepDistance = 0.2f;
    [SerializeField, Range(1, 256)] int maxStepsPerFrame = 128;
    CharacterController controller;
    PlayerStats stats;
    Vector3 velocity;
    float coyoteRemaining, jumpBuffered;
    bool grounded, pounding;
    bool crouching, sliding;
    float slideBoostRemaining;
    float wallCrashRecoveryRemaining, nextWallCrashTime;
    float standingHeight;
    Vector3 standingCenter, cameraStandingPosition;
    Transform playerCamera;
    Camera movementCamera;
    float cameraBaseFov, cameraHeightOffset, cameraHeightVelocity;
    float bobPhase, bobWeight, fovOffset, landingOffset;
    int roomsCleared;
    bool groundedForSweep;
    readonly RaycastHit[] sweepBuffer = new RaycastHit[32];
    static readonly IComparer<RaycastHit> HitDistanceComparer = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
    public Vector3 Velocity => velocity;
    public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;
    float firstTowerSpeed;
    public float DestructibleTowerScale { get; private set; } = 1f;
    public void SnapshotTowerSpeed()
    {
        float speed = Mathf.Max(0.01f, TargetSpeed * sprintSpeedMultiplier);
        if (firstTowerSpeed <= 0f) firstTowerSpeed = speed;
        // Growth is intentionally modest and capped, so later props remain breakable.
        float speedRatio = Mathf.Max(1f, speed / firstTowerSpeed);
        DestructibleTowerScale = Mathf.Min(3f, 1f + (speedRatio - 1f) * 0.25f);
    }
    // Sprint is the reference maximum; slides and external impulses can exceed it.
    public float MaximumRunSpeed => Mathf.Max(0f, TargetSpeed * sprintSpeedMultiplier);
    public float MaximumRunSpeedFraction => MaximumRunSpeed > 0.001f ? Mathf.Clamp01(HorizontalSpeed / MaximumRunSpeed) : 0f;
    public float TargetSpeed => stats.Evaluate(PlayerStat.MoveSpeed, baseSpeed + stats.Evaluate(PlayerStat.SpeedPerRoom, speedPerRoom) * roomsCleared);
    // Conservative starting point for encounter timing; actual travel depends on input and surfaces.
    public float GroundAcceleration => Mathf.Min(stats.Evaluate(PlayerStat.Acceleration, acceleration), stats.Evaluate(PlayerStat.TurnAcceleration, turnAcceleration));
    public PlayerStats Stats => stats;
    public int RoomsCleared => roomsCleared;
    public bool IsGrounded => grounded;
    public bool IsPounding => pounding;
    public bool IsCrouching => crouching;
    public bool IsSliding => sliding;
    public bool IsSprinting { get; private set; }
    public bool HasMovementInput { get; private set; }
    public event Action<int> RoomCleared;
    public event Action<float, bool> Landed;
    public event Action<float, Vector3> WallCrashed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        standingCenter = controller.center;
        var camera = GetComponentInChildren<Camera>();
        if (camera != null) { movementCamera = camera; cameraBaseFov = camera.fieldOfView; playerCamera = camera.transform; cameraStandingPosition = playerCamera.localPosition; }
        stats = GetComponent<PlayerStats>();
        if (GetComponent<GroundSurfaceSensor>() == null) gameObject.AddComponent<GroundSurfaceSensor>();
        if (GetComponent<EnemyDirector>() == null) gameObject.AddComponent<EnemyDirector>();
        if (GetComponent<PlayerRepel>() == null) gameObject.AddComponent<PlayerRepel>();
        if (GetComponent<PlayerSpeedFeedback>() == null) gameObject.AddComponent<PlayerSpeedFeedback>();
    }

    // El gestor de salas llamará una sola vez por transición confirmada.
    public void RegisterRoomCleared()
    {
        roomsCleared++;
        RoomCleared?.Invoke(roomsCleared);
    }
    public void ResetMotion()
    {
        velocity = Vector3.zero;
        grounded = pounding = sliding = false;
        slideBoostRemaining = 0f;
        wallCrashRecoveryRemaining = 0f; nextWallCrashTime = 0f;
        coyoteRemaining = jumpBuffered = 0f;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        var keyboard = Keyboard.current;
        float horizontal = keyboard == null ? 0f :
            ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
             (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f));
        float forward = keyboard == null ? 0f :
            ((keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
             (keyboard.sKey.isPressed && grounded ? 1f : 0f));
        bool jump = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        bool crouch = keyboard != null && keyboard.cKey.isPressed;
        bool crouchPressed = keyboard != null && keyboard.cKey.wasPressedThisFrame;
        bool pound = keyboard != null && (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame);
        bool sprint = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        IsSprinting = sprint;
        HasMovementInput = horizontal != 0f || forward != 0f;
        UpdateCrouch(crouch, crouchPressed);
        Tick(new Vector2(horizontal, forward), jump, pound, sprint, Time.deltaTime);
    }

    void UpdateCrouch(bool requested, bool pressed)
    {
        if (!requested && crouching && !CanStand()) requested = true;
        crouching = requested;
        if (!crouching || !grounded) { sliding = false; slideBoostRemaining = 0f; }
        else if (pressed && HorizontalSpeed >= slideMinimumSpeed && HorizontalSpeed > 0.001f)
        {
            sliding = true;
            velocity.x *= Mathf.Max(1f, slideStartSpeedMultiplier);
            velocity.z *= Mathf.Max(1f, slideStartSpeedMultiplier);
            slideBoostRemaining = slideBoostDuration;
        }
        else if (sliding && HorizontalSpeed < slideMinimumSpeed) sliding = false;
        float height = crouching ? Mathf.Max(controller.radius * 2f, standingHeight * crouchHeightFraction) : standingHeight;
        controller.height = height;
        controller.center = standingCenter + Vector3.down * ((standingHeight - height) * 0.5f);
    }
    // Presentation only: the capsule still changes immediately so low passages stay responsive.
    void LateUpdate()
    {
        if (playerCamera == null || movementCamera == null || Time.deltaTime <= 0f) return;
        float dt = Time.deltaTime;
        float targetHeight = -(standingHeight - controller.height) * 0.5f;
        cameraHeightOffset = Mathf.SmoothDamp(cameraHeightOffset, targetHeight, ref cameraHeightVelocity,
            Mathf.Max(0.01f, crouchCameraTime), Mathf.Infinity, dt);
        float speed = HorizontalSpeed;
        float targetBob = grounded && !sliding ? Mathf.Clamp01(speed / 6f) : 0f;
        float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, movementFeedbackTime));
        bobWeight = Mathf.Lerp(bobWeight, targetBob, blend);
        bobPhase += dt * movementBobFrequency * Mathf.Lerp(0.75f, 1.5f, Mathf.Clamp01(speed / 15f));
        landingOffset = Mathf.Lerp(landingOffset, 0f, 1f - Mathf.Exp(-dt / 0.16f));
        float bob = Mathf.Sin(bobPhase) * movementBobAmount * bobWeight;
        playerCamera.localPosition = cameraStandingPosition + Vector3.up * (cameraHeightOffset + bob + landingOffset);
        float targetFov = speedFovIncrease * Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullFov));
        fovOffset = Mathf.Lerp(fovOffset, targetFov, blend);
        movementCamera.fieldOfView = cameraBaseFov + fovOffset;
    }
    void OnDisable()
    {
        if (playerCamera != null) playerCamera.localPosition = cameraStandingPosition;
        if (movementCamera != null) movementCamera.fieldOfView = cameraBaseFov;
        IsSprinting = false; HasMovementInput = false;
        cameraHeightOffset = cameraHeightVelocity = bobWeight = fovOffset = landingOffset = 0f;
    }
    bool CanStand()
    {
        float radius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
        Vector3 center = transform.TransformPoint(standingCenter);
        float half = Mathf.Max(0f, standingHeight * 0.5f - radius);
        foreach (var hit in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore))
            if (hit != controller && !hit.transform.IsChildOf(transform)) return false;
        return true;
    }
    void Tick(Vector2 input, bool jumpPressed, bool poundPressed, bool sprint, float dt)
    {
        bool wasGrounded = grounded;
        if (wasGrounded) coyoteRemaining = stats.Evaluate(PlayerStat.CoyoteTime, coyoteTime);
        else coyoteRemaining = Mathf.Max(0f, coyoteRemaining - dt);
        jumpBuffered = jumpPressed ? stats.Evaluate(PlayerStat.JumpBufferTime, jumpBufferTime) : Mathf.Max(0f, jumpBuffered - dt);

        if (pounding) input = Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        float speed = TargetSpeed * (crouching ? crouchSpeedFraction : sprint ? sprintSpeedMultiplier : 1f);
        Vector3 desired = (transform.right * input.x + transform.forward * input.y) * speed;
        float rate = input.sqrMagnitude > 0.0001f ? stats.Evaluate(PlayerStat.Acceleration, acceleration) : stats.Evaluate(PlayerStat.Braking, braking);
        Vector3 currentHorizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (input.sqrMagnitude > 0.0001f && Vector3.Dot(currentHorizontal, desired) < 0f)
            rate = stats.Evaluate(PlayerStat.TurnAcceleration, turnAcceleration);
        if (!wasGrounded) rate *= stats.Evaluate(PlayerStat.AirControl, airControl);
        if (sprint && !crouching && input.sqrMagnitude > 0.0001f) rate *= sprintAccelerationMultiplier;
        if (sliding)
        {
            desired = Vector3.zero;
            rate = slideBoostRemaining > 0f ? 0f : slideBraking;
            slideBoostRemaining = Mathf.Max(0f, slideBoostRemaining - dt);
        }
        bool recoveringFromCrash = wallCrashRecoveryRemaining > 0f;
        wallCrashRecoveryRemaining = Mathf.Max(0f, wallCrashRecoveryRemaining - dt);
        Vector3 horizontalVelocity = recoveringFromCrash ? currentHorizontal : Vector3.MoveTowards(currentHorizontal, desired, rate * dt);
        // Ground control changes only the sideways correction towards the intended
        // direction. Forward acceleration, braking and sliding remain independent.
        if (!recoveringFromCrash && wasGrounded && !sliding && desired.sqrMagnitude > 0.0001f)
        {
            Vector3 direction = desired.normalized;
            Vector3 previousSideways = currentHorizontal - Vector3.Project(currentHorizontal, direction);
            Vector3 nextSideways = horizontalVelocity - Vector3.Project(horizontalVelocity, direction);
            float correction = (previousSideways - nextSideways).magnitude;
            float control = stats.Evaluate(PlayerStat.GroundControl, 1f);
            Vector3 adjustedSideways = Vector3.MoveTowards(previousSideways, Vector3.zero, correction * control);
            horizontalVelocity += adjustedSideways - nextSideways;
        }
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;

        if (jumpBuffered > 0f && coyoteRemaining > 0f && !pounding)
        {
            velocity.y = stats.Evaluate(PlayerStat.JumpSpeed, jumpSpeed);
            GameSfx.PlayJump(velocity.y);
            jumpBuffered = coyoteRemaining = 0f;
            wasGrounded = false;
        }
        if (poundPressed && !wasGrounded && !pounding)
        {
            pounding = true;
            velocity.x = velocity.z = 0f;
            coyoteRemaining = 0f;
            velocity.y = -Mathf.Max(stats.Evaluate(PlayerStat.PoundSpeed, poundSpeed + stats.Evaluate(PlayerStat.PoundSpeedPerRoom, poundSpeedPerRoom) * roomsCleared), -velocity.y);
        }
        if (wasGrounded && velocity.y < 0f) velocity.y = -stats.Evaluate(PlayerStat.GroundStickSpeed, groundStickSpeed);
        else if (!pounding) velocity.y = Mathf.Max(velocity.y - stats.Evaluate(PlayerStat.Gravity, gravity) * dt, -stats.Evaluate(PlayerStat.MaxFallSpeed, maxFallSpeed));

        // Bound movement distance per sweep. If a frame is exceptionally long or speed is
        // extreme, discard excess travel instead of risking tunnelling through thin colliders.
        float desiredDistance = velocity.magnitude * dt;
        float stepLength = Mathf.Max(0.01f, maxStepDistance);
        int limit = Mathf.Clamp(maxStepsPerFrame, 1, 256);
        int steps = Mathf.Clamp(Mathf.CeilToInt(desiredDistance / stepLength), 1, limit);
        float simulatedTime = desiredDistance > stepLength * limit && desiredDistance > 0f
            ? dt * (stepLength * limit / desiredDistance) : dt;
        float stepTime = simulatedTime / steps;
        bool landedThisFrame = false;
        groundedForSweep = wasGrounded;
        grounded = false;
        for (int i = 0; i < steps; i++)
        {
            float impactSpeed = Mathf.Max(0f, -velocity.y);
            bool impactWasPound = pounding;
            PredictBreakableImpacts(velocity * stepTime);
            CollisionFlags flags = controller.Move(velocity * stepTime);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if ((flags & CollisionFlags.Below) != 0)
            {
                groundedForSweep = true;
                grounded = true;
                if (!wasGrounded && !landedThisFrame)
                {
                    landedThisFrame = true;
                    pounding = false;
                    landingOffset = -Mathf.Min(0.09f, impactSpeed * 0.003f);
                    Landed?.Invoke(impactSpeed, impactWasPound);
                }
                if (velocity.y < 0f) velocity.y = -stats.Evaluate(PlayerStat.GroundStickSpeed, groundStickSpeed);
            }
        }
    }

    // Sweep the same capsule along this substep, nearest first. A solid obstacle
    // blocks prediction of objects behind it. A broken target is disabled before Move.
    void PredictBreakableImpacts(Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance <= 0.00001f) return;
        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(radius * 2f, controller.height * Mathf.Abs(scale.y));
        float castRadius = Mathf.Max(0.01f, radius - controller.skinWidth);
        Vector3 center = transform.TransformPoint(controller.center);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 bottom = center - Vector3.up * halfSegment;
        Vector3 direction = displacement / distance;
        float castDistance = distance + controller.skinWidth;
        int count = Physics.CapsuleCastNonAlloc(top, bottom, castRadius, direction, sweepBuffer, castDistance, ~0, QueryTriggerInteraction.Ignore);
        // Do not miss an intervening wall when the buffer fills.
        var hits = count == sweepBuffer.Length
            ? Physics.CapsuleCastAll(top, bottom, castRadius, direction, castDistance, ~0, QueryTriggerInteraction.Ignore) : sweepBuffer;
        if (count == sweepBuffer.Length) count = hits.Length;
        Array.Sort(hits, 0, count, HitDistanceComparer);
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.collider == controller || hit.collider.transform.IsChildOf(transform)) continue;
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer) || Physics.GetIgnoreCollision(controller, hit.collider)) continue;
            // Ground-stick motion must not hide a wall in a horizontal sweep.
            if (!pounding && groundedForSweep && hit.normal.y > 0.5f) continue;
            if (Vector3.Dot(displacement, hit.normal) >= -0.00001f) continue;
            var target = hit.collider.GetComponentInParent<ImpactTarget>();
            if (target != null && target.WillBreak(this, velocity, hit.normal, pounding))
            {
                target.ReceiveImpact(this, velocity, hit.normal, pounding);
                if (target.IsBroken) continue;
            }
            break;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (sliding && HorizontalSpeed >= slideMinimumSpeed)
        {
            var door = hit.collider.GetComponentInParent<RoomDoor>();
            if (door != null && !door.IsOpen) { door.Open(); return; }
        }
        // Publish the pre-collision velocity before removing the blocked component.
        var target = hit.collider.GetComponentInParent<ImpactTarget>();
        if (target != null)
        {
            target.ReceiveImpact(this, velocity, hit.normal, pounding);
            if (target.IsBroken) return;
        }
        if (Mathf.Abs(hit.normal.y) < 0.5f)
        {
            Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            float intoWall = Vector3.Dot(horizontal, wallNormal);
            if (intoWall >= 0f) return;
            bool extreme = Time.time >= nextWallCrashTime && speed >= wallCrashMinimumSpeed &&
                speed >= MaximumRunSpeed * wallCrashMinimumSpeedFraction &&
                -intoWall >= speed * wallCrashMinimumHeadOn;
            if (extreme)
            {
                Vector3 tangent = horizontal - wallNormal * intoWall;
                float rebound = Mathf.Min(wallCrashMaximumRebound, -intoWall * wallCrashReboundFraction);
                Vector3 result = tangent * wallCrashTangentialRetention + wallNormal * rebound;
                velocity.x = result.x; velocity.z = result.z;
                sliding = false; slideBoostRemaining = 0f;
                wallCrashRecoveryRemaining = Mathf.Max(0f, wallCrashRecoveryTime);
                nextWallCrashTime = Time.time + Mathf.Max(0f, wallCrashCooldown);
                landingOffset = -0.09f;
                WallCrashed?.Invoke(speed, hit.normal);
            }
            else
            {
                velocity.x -= wallNormal.x * intoWall;
                velocity.z -= wallNormal.z * intoWall;
            }
        }
    }
}
