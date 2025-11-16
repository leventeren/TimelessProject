using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player.Scripts
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed;
        [SerializeField] private InputActionReference move;
        
        [Header("Jump Settings")]
        [SerializeField] private InputActionReference jump;
        [SerializeField, ReadOnly] private bool isJumping;
        
        [Header("Rotation Settings")]
        [SerializeField] private bool rotate;
        [SerializeField] private float rotationSpeed;
        [SerializeField] private float resetRotationSpeed;
        
        private Vector2 moveDirection;
        private Vector3 movementDirection;
        private Vector3 eightDirectionMovement;
        
        [Header("Component References")]
        public Rigidbody rb;
        public Animator animator;
        
        [Header("Animator Parameters")]
        private static readonly int DirectionX = Animator.StringToHash("directionX");
        private static readonly int DirectionY = Animator.StringToHash("directionY");
        private static readonly int IsMoving = Animator.StringToHash("isMoving");
        private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
        private static readonly string JumpTrigger = "jumpTrigger";
        
        private Quaternion jumpRotation;
        
        public bool IsJumping
        {
            get => isJumping;
            set => isJumping = value;
        }

        private void OnEnable()
        {
            jump.action.performed += OnJumpProgress;
        }
        
        private void OnDisable()
        {
            jump.action.performed -= OnJumpProgress;
        }

        private void Update()
        {
            moveDirection = move.action.ReadValue<Vector2>();
            
            CalculateEightDirectionMovement();
            
            UpdateAnimator();
            
            transform.Translate(eightDirectionMovement * moveSpeed * Time.deltaTime, Space.World);
            
            if (!rotate) return;
            CalculateRotation();
        }

        private void OnJumpProgress(InputAction.CallbackContext context)
        {
            if (isJumping) return;
            
            isJumping = true;
            
            jumpRotation = transform.rotation;
            
            animator.SetBool(IsJumpingHash, isJumping);
            
            animator.SetTrigger(JumpTrigger);
        }

        public void OnJumpEnd()
        {
            isJumping = false;
            animator.SetBool(IsJumpingHash, isJumping);
        }
        
        private void CalculateEightDirectionMovement()
        {
            if (moveDirection.magnitude > 0)
            {
                var angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                
                angle = Mathf.Round(angle / 45f) * 45f;
                
                var radians = angle * Mathf.Deg2Rad;
                var snappedDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
                
                eightDirectionMovement = new Vector3(snappedDirection.x, 0, snappedDirection.y);
            }
            else
            {
                eightDirectionMovement = Vector3.zero;
            }
        }
        
        private void UpdateAnimator()
        {
            if (animator == null) return;
            
            var isMoving = eightDirectionMovement.magnitude > 0;
            animator.SetBool(IsMoving, isMoving);
            
            if (isMoving)
            {
                animator.SetFloat(DirectionX, eightDirectionMovement.x);
                animator.SetFloat(DirectionY, eightDirectionMovement.z);
            }
            else
            {
                animator.SetFloat(DirectionX, 0f);
                animator.SetFloat(DirectionY, 0f);
            }
        }
        
        private void CalculateRotation()
        {
            if (isJumping)
            {
                transform.rotation = jumpRotation;
                return;
            }
            
            if (eightDirectionMovement != Vector3.zero)
            {
                var toRotation = Quaternion.LookRotation(eightDirectionMovement, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
            }
            else
            {
                if (transform.rotation != Quaternion.Euler(0, 0, 0))
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 0, 0), resetRotationSpeed * Time.deltaTime);
            }
        }

        public Vector2 GetMoveDirection()
        {
            return moveDirection;
        }
    }
}
