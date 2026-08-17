using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public sealed class PlayerMovement : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("moveX");
    private static readonly int MoveYHash = Animator.StringToHash("moveY");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    [SerializeField, Min(0f)]
    private float moveSpeed = 4f;

    private Rigidbody2D body;
    private Animator animator;
    private Vector2 movementInput;
    private Vector2 facingDirection = Vector2.down;

    public Vector2 FacingDirection => facingDirection;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Keep top-down movement on the XY plane and prevent collision torque.
        body.gravityScale = 0f;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;

        UpdateAnimator(false);
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused)
        {
            movementInput = Vector2.zero;
            UpdateAnimator(false);
            return;
        }

        Vector2 rawInput = ReadKeyboardInput();
        bool isMoving = rawInput.sqrMagnitude > 0f;

        // Normalize keyboard diagonals so every direction has the same speed.
        movementInput = Vector2.ClampMagnitude(rawInput, 1f);

        if (isMoving)
        {
            facingDirection = GetStableCardinalDirection(rawInput);
        }

        UpdateAnimator(isMoving);
    }

    private void FixedUpdate()
    {
        if (LevelUpPanelController.IsGamePaused)
            return;

        Vector2 displacement = movementInput * (moveSpeed * Time.fixedDeltaTime);
        body.MovePosition(body.position + displacement);
    }

    private void OnDisable()
    {
        movementInput = Vector2.zero;

        if (animator != null)
        {
            UpdateAnimator(false);
        }
    }

    private static Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }

    private Vector2 GetStableCardinalDirection(Vector2 input)
    {
        float absoluteX = Mathf.Abs(input.x);
        float absoluteY = Mathf.Abs(input.y);

        if (absoluteX > absoluteY)
        {
            return new Vector2(Mathf.Sign(input.x), 0f);
        }

        if (absoluteY > absoluteX)
        {
            return new Vector2(0f, Mathf.Sign(input.y));
        }

        // A keyboard diagonal has equal axes. Preserve the current facing axis
        // when possible so pressing or releasing the second key cannot flicker.
        if (facingDirection.x != 0f && Mathf.Sign(input.x) == facingDirection.x)
        {
            return new Vector2(facingDirection.x, 0f);
        }

        if (facingDirection.y != 0f && Mathf.Sign(input.y) == facingDirection.y)
        {
            return new Vector2(0f, facingDirection.y);
        }

        return new Vector2(Mathf.Sign(input.x), 0f);
    }

    private void UpdateAnimator(bool isMoving)
    {
        animator.SetFloat(MoveXHash, facingDirection.x);
        animator.SetFloat(MoveYHash, facingDirection.y);
        animator.SetBool(IsMovingHash, isMoving);
    }
}
