using System;
using System.Collections;
using System.Net.Sockets;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEngine.RuleTile.TilingRuleOutput;

namespace FrostFalls
{
    /// <summary>
    /// Credits to https://github.com/Matthew-J-Spencer/Ultimate-2D-Controller for the inital player controller
    /// 2D player controller for Unity, designed to provide a high-quality, customizable movement experience
    /// handles input, jumping, grapling, gravity, and collision detection
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        #region Settings Store
        [Header("Settings Store")]
        #region Layers
        [Header("LAYERS")]
        [Tooltip("The layer your player is on")]
        public LayerMask PlayerLayer;

        [Header("PLAYER")]
        [Tooltip("The player sprite parent")]
        public UnityEngine.Transform playerParentSprite;
        #endregion

        #region Input
        [Header("INPUT")]
        [Tooltip("Makes all Input snap to an integer. Prevents gamepads from walking slowly. Recommended value is true to ensure gamepad/keybaord parity.")]
        public bool SnapInput = true;

        [Tooltip("Minimum input required before you mount a ladder or climb a ledge. Avoids unwanted climbing using controllers"), Range(0.01f, 0.99f)]
        public float VerticalDeadZoneThreshold = 0.3f;

        [Tooltip("Minimum input required before a left or right is recognized. Avoids drifting with sticky controllers"), Range(0.01f, 0.99f)]
        public float HorizontalDeadZoneThreshold = 0.1f;
        #endregion

        #region Tools/Weapons
        [Header("Tools/Weaponse")]
        [Tooltip("The players right pickaxe")]
        public GameObject rightIcePickaxe;

        [Tooltip("The players left pickaxe")]
        public GameObject leftIcePickaxe;

        [Tooltip("The players grappling gun")]
        public GameObject grapplingGun;
        #endregion

        #region Movement
        [Header("MOVEMENT")]
        [Tooltip("The top horizontal movement speed")]
        public float MaxSpeed = 14;

        [Tooltip("The player's capacity to gain horizontal speed")]
        public float Acceleration = 120;

        [Tooltip("The pace at which the player comes to a stop")]
        public float GroundDeceleration = 60;

        [Tooltip("Deceleration in air only after stopping input mid-air")]
        public float AirDeceleration = 30;

        [Tooltip("A constant downward force applied while grounded. Helps on slopes"), Range(0f, -10f)]
        public float GroundingForce = -1.5f;

        [Tooltip("The detection distance for grounding and roof detection"), Range(0f, 2f)]
        public float GrounderDistance = 0.05f;
        #endregion

        #region Jump
        [Header("JUMP")]
        [Tooltip("The immediate velocity applied when jumping")]
        public float JumpPower = 36;

        [Tooltip("The maximum vertical movement speed")]
        public float MaxFallSpeed = 40;

        [Tooltip("The player's capacity to gain fall speed. a.k.a. In Air Gravity")]
        public float FallAcceleration = 110;

        [Tooltip("The gravity multiplier added when jump is released early")]
        public float JumpEndEarlyGravityModifier = 3;

        [Tooltip("The time before coyote jump becomes unusable. Coyote jump allows jump to execute even after leaving a ledge")]
        public float CoyoteTime = .15f;

        [Tooltip("The amount of time we buffer a jump. This allows jump input before actually hitting the ground")]
        public float JumpBuffer = .2f;
        #endregion

        #region Grapple
        [Header("GRAPPLE")]
        [Tooltip("The layers considered grappleable surfaces")]
        public LayerMask GrappleableLayers;

        [Tooltip("Maximum distance the grappling hook can travel from the player to attach to a grappleable surface")]
        public float GrappleDistance = 10f;

        [Tooltip("Position from where the grapple is shot, typically the player's hand or a grappling device")]
        public UnityEngine.Transform direction;

        [Tooltip("Grapple boost force")]
        public float BoostForce;

        [Tooltip("Grapple boost particle effect")]
        public ParticleSystem BoostEffect;

        [Tooltip("Grapple boost max speed")]
        public float MaxGrappleBoostSpeed;

        [Tooltip("Grapple pull force")]
        public float PullForce;

        [Tooltip("Grapple dash particle effect")]
        public ParticleSystem DashEffect;

        [Tooltip("Grapple dash cooldown")]
        public float DashCooldown;

        [Tooltip("Grapple dash force")]
        public float DashForce;
        #endregion

        #region WallClimb
        [Header("WALLCLIMB")]
        [Tooltip("Set this to the layers considered climbable surfaces")]
        public LayerMask ClimbableLayers;

        [Tooltip("The allowed disance between the player and a wall"), Range(0f, 2f)]
        public float WalledDistance;

        [Tooltip("The wall slide speed")]
        public float WallSlideSpeed;

        [Tooltip("The wall climb speed")]
        public float WallClimbSpeed;

        [Tooltip("The horizontal jump force")]
        public float HorizontalJumpPower = 10f;

        [Tooltip("Wall jump duration")]
        public float WallJumpDuration = 2.0f;

        [Tooltip("Wall jump cooldown duration")]
        public float WallJumpCoolDown = 1.0f;

        [Tooltip("Set this to the layers considered non-ledge surfaces")]
        public LayerMask LedgeLayers;

        [Tooltip("The allowed disance between the player and a ledge"), Range(0f, 2f)]
        public float LedgeCheckDistance;

        [Tooltip("The allowed height offset between the player and a ledge"), Range(0f, 2f)]
        public float LedgeCheckOffset;

        [Tooltip("The players ledge climb start position")]
        public Vector2 LedgeClimbStart;

        [Tooltip("he players ledge climb end position")]
        public Vector2 LedgeClimbOffsetEnd;

        [Tooltip("The player ledge climb animation duration")]
        public float LedgeClimbDuration;
        #endregion

        #endregion


        private Rigidbody2D _rb; // Rigidbody component used for physics
        private CapsuleCollider2D _col; // Collider component used for collision detection
        private FrameInput _frameInput; // Stores current frame's input data
        private Vector2 _frameVelocity; // Calculated velocity to apply to the Rigidbody

        // Player position & respawn
        public Vector2 RespawnPosition;
        public Vector2 currentPos;


        // Exposed properties and events through IPlayerController interface
        public Vector2 FrameInput => _frameInput.Move;
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        public event Action Grappled;
        public event Action FreeFall;
        public event Action WallSlid;
        public event Action WallClimbed;
        public event Action WallJumped;
        public event Action LedgeClimb;

        // Elapsed time
        private float _time;

        // Other
        private float originalGravityScale;
        private Coroutine resetGravityCoroutine;
        private Animator _animator;
        private bool _dead;


        private bool _ignorePlayerInput = false;

        private void Awake()
        {
            // Initialize components and create inital cache
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _lineRenderer = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
            originalGravityScale = _rb.gravityScale;
            _animator = GetComponentInChildren<Animator>();
        }

        // Start is called before the first frame update
        private void Start()
        {
            //Time since last dash
            transform.position = new Vector2(-16f, -12f);
            RespawnPosition = transform.position;
            _currentDashWaitTime = Time.time;
        }

        private void Update()
        {
            // Update time and gather player input for each frame
            _time += Time.deltaTime;
            
            if (_dead)
            {
                _ignorePlayerInput = true;
                _rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            if (_ignorePlayerInput != true) //Gather player input aslong it's expected
            {
                GatherInput();
            }

            _animator.SetBool("OnGround", _grounded);
            _animator.SetBool("Walled", _walled);
            _animator.SetBool("HasPickaxe", _hasPickaxe);
            _animator.SetBool("IsLedgeClimbing", _isLedgeClimbing);
            _animator.SetBool("IsGrappling", _isGrappling);
            _animator.SetBool("IsWallClimbing", _isWallClimbing);
            _animator.SetBool("IsWallSliding", _isWallSliding);
            currentPos = transform.position;

            if (_isLedgeClimbing)
            {
                transform.position = _ledgeClimbStartPos;
            }
        }

        /// <summary>
        /// Gather and processe player input, updating the FrameInput struct
        /// </summary>
        private void GatherInput()
        {
            _frameInput = new FrameInput
            {
                JumpDown = Input.GetKeyDown(KeyCode.W),
                JumpHeld = Input.GetKey(KeyCode.W),
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), _rb.velocity.y),
                Grapple = Input.GetMouseButtonDown(0),
                StopGrapple = Input.GetMouseButtonUp(0),
                Boost = Input.GetKeyDown(KeyCode.Space) || Input.GetKey(KeyCode.Space),
                StopBoost = Input.GetKeyUp(KeyCode.Space),
                Dash = Input.GetKeyDown(KeyCode.Q),
                ToolSwap = Input.GetMouseButtonDown(1),
                WallClimb = Input.GetMouseButton(0),
                StopWallClimb = Input.GetMouseButtonUp(0)
            };

            // Mark jump input for processing
            if (_frameInput.JumpDown)
            {
                _jumpToConsume = true;
                _timeJumpWasPressed = _time;
            }

            //Mark grapple input for processing
            if (_frameInput.Grapple)
            {
                _grappleToConsume = true;
            }

            //Mark grapple release for processing
            if (_frameInput.StopGrapple)
            {
                _stopGrappleToConsume = true;
            }

            //Mark boost input for processing
            if (_frameInput.Boost)
            {
                _boostToConsume = true;
            }

            //Mark boost release for processing
            if (_frameInput.StopBoost)
            {
                _stopBoostToConsume = true;
            }

            //Mark dash for processing
            if (_frameInput.Dash)
            {
                _dashToConsume = true;
            }

            //Mark tool swap for processing
            if (_frameInput.ToolSwap)
            {
                _toolSwapToConsume = true;
            }

            //Mark wall climb input for processing
            if (_frameInput.WallClimb)
            {
                _wallClimbToConsume = true;
            }

            //Mark wall climb release for processing
            if (_frameInput.StopWallClimb)
            {
                _stopWallClimbToConsume = true;
            }
        }

        private void FixedUpdate()
        {
            // Perform physics and movement calculations at a fixed intervals
            CheckCollisions();
            WallCheck();
            LedgeCheck();
            if (!_isLedgeClimbing || !_ignorePlayerInput)
            {
                HandleJump();
                HandleGrapple();
                HandleBoost();
                HandleDash();
                HandleDirection();
                HandleGravity();
                ApplyMovement();
                HandleToolSwap();
                HandleWallClimb();
            }

            // _rb.velocity = new Vector2(_frameVelocity.x, _frameVelocity.y);

            if (_grounded && _joint)
            {
                // Allow the joint distance to decrease while the player is grounded so they don't get stuck
                _joint.maxDistanceOnly = true;
            }


            // Fix upwards float glitch after jumping from wall
            //if (!_walled && (_isWallClimbing || _isWallSliding))
            if (!_walled && _hasPickaxe)
            {
                _isWallClimbing = false;
                _isWallSliding = false;
                //if(!_grounded) FreeFall?.Invoke();
            }
            //if (_grounded == true || !_walled) _isWallSliding = false;
        }

        private void LateUpdate()
        {
            //  Visually update grappling rope's position
            UpdateGrappleRope();

            if (!_hasPickaxe)
            {
                _isWallClimbing = false;
                _isWallSliding = false;
            }
            if (_hasPickaxe && _isGrappling)
            {
                StopGrapple();
                _isGrappling = false;
            }
        }

        #region Collisions

        private float _frameLeftGrounded = float.MinValue; // Tracks the time when the player last left the ground
        private bool _grounded; // Indicates whether the player is currently grounded
        private bool _walled; // Indicates whether the player is currently against a climbable wall

        /// <summary>
        /// Check for collisions with the ground and ceiling using capsule casts
        /// Update the grounded state and invokes GroundedChanged events if collisions occur
        /// </summary>
        private void CheckCollisions()
        {
            // Starting point of the raycast at the bottom of the player collider
            Vector2 rayStart = new Vector2(_col.bounds.center.x, _col.bounds.min.y);
            float rayLength = GrounderDistance; // Distance to cast the ray downwards

            // Perform raycast downwards to check for ground
            RaycastHit2D groundHit = Physics2D.Raycast(rayStart, Vector2.down, rayLength, ~PlayerLayer);

            // Visualize Ground Check Raycast
            Debug.DrawRay(rayStart, Vector2.down * rayLength, Color.blue, 0.1f);

            // Check the raycast result to update grounded
            if (!groundHit.collider)
            {
                if (_grounded)
                {
                    _grounded = false;
                    _frameLeftGrounded = Time.time;
                    GroundedChanged?.Invoke(false, 0); // Invoke event when becoming ungrounded
                    // Check if not grappling and not jumping to determine if in free fall
                    if (!_isLedgeClimbing && !_isWallClimbing && !_isWallSliding && !_isGrappling && !_grounded)
                    {
                        FreeFall?.Invoke(); // Invoke free fall event or method
                    }
                }
            }
            else
            {
                if (!_grounded)
                {
                    _grounded = true;
                    GroundedChanged?.Invoke(true, Mathf.Abs(_rb.velocity.y)); // Invoke event when grounded
                }
            }
        }
        /// <summary>
        /// </summary>
        private void WallCheck()
        {
            // Starting point of the raycast at the bottom of the player collider
            Vector2 rayStart = new Vector2(_col.bounds.center.x, _col.bounds.max.y);
            float rayLength = WalledDistance; // Distance to cast the ray forwards

            // Determine which way the player is facing
            Vector2 direction = playerParentSprite.localScale.x < 1 ? Vector2.left : Vector2.right;

            RaycastHit2D wallHit = Physics2D.Raycast(rayStart, direction, rayLength, ClimbableLayers);

            // Visualize Wall Check Raycast
            Debug.DrawRay(rayStart, direction * rayLength, Color.red, 0.1f);

            // Check the raycast result to update grounded
            if (wallHit.collider)
            {
                _walled = true;
            }
            else
            {
                _walled = false;
            }
        }
       

        private void LedgeCheck()
        {

            Vector2 direction = playerParentSprite.localScale.x < 1 ? Vector2.left : Vector2.right;


            Vector2 rayStartOffset = direction * _col.bounds.extents.x; // Move start point to the edge of the collider in the facing direction
            Vector2 rayStart = new Vector2(_col.bounds.center.x - rayStartOffset.x, _col.bounds.max.y + LedgeCheckOffset); // Move the start to the opposite side (player's back)

            float rayLength = LedgeCheckDistance; // Define LedgeCheckDistance based on your game scale

            Debug.DrawRay(rayStart, direction * rayLength, Color.yellow, 0.1f);

            RaycastHit2D ledgeHit = Physics2D.Raycast(rayStart, direction, rayLength, ~PlayerLayer);

            _canLedgeClimb = !ledgeHit.collider;

        }
        #endregion

        #region Jumping
        private bool _jumpAllowed = true; // Tracks if a jump is allowed
        private bool _jumpToConsume; // Indicates a jump input that needs to be processed
        private bool _bufferedJumpUsable; // Allows for jump input buffering
        private bool _endedJumpEarly; // Tracks if the jump was ended early by releasing the jump key
        private bool _coyoteUsable; // Allows for coyote time, a grace period for jumping after leaving the edge
        private float _timeJumpWasPressed; // The time when the jump button was last pressed

        private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + JumpBuffer;
        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + CoyoteTime;

        /// <summary>
        /// Determine if a buffered jump is available or if coyote time can be used, execute a jump if conditions are met
        /// </summary>
        private void HandleJump()
        {
            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.velocity.y > 0) _endedJumpEarly = true;

            if (!_jumpToConsume && !HasBufferedJump) return;

            if (_jumpAllowed && !_grounded && (_isWallClimbing || _isWallSliding)) WallJump();

            if (_jumpAllowed && !_walled && (_grounded || CanUseCoyote)) ExecuteJump();

            _jumpToConsume = false;
        }

        /// <summary>
        /// Execute a jump
        /// </summary>
        private void ExecuteJump()
        {
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            _frameVelocity.y = JumpPower;

            Jumped?.Invoke();
        }

        #endregion

        #region Grappling

        private bool _grappleToConsume; // Indicates a grapple input that needs to be processed
        private bool _stopGrappleToConsume; // Indicates a grapple release input that needs to be processed
        private bool _grappleUsable = true; // Allows grappling
        private bool _isGrappling = false; // Grapple is currently active
        private LineRenderer _lineRenderer; // Component used to render the grappling rope
        private DistanceJoint2D _joint; // Restricts the player's movement to within a certain distance from the grapple point
        private Vector2 _grapplePoint; // Game world grappling hook attach point
        private bool _allowedBoost;
        private int _yRelative;
        private float _currentDashWaitTime;

        /// <summary>
        /// Determines if the player can grapple, and grapples if conditions are met
        /// </summary>
        private void HandleGrapple()
        {
            if (!_grappleToConsume && !_stopGrappleToConsume) return;

            if (_grappleUsable && _grappleToConsume && !_hasPickaxe) StartGrapple();

            if ((_stopGrappleToConsume && !_hasPickaxe) || (_isGrappling && _hasPickaxe)) StopGrapple();

            _grappleToConsume = false;
            _stopGrappleToConsume = false;
        }

        /// <summary>
        /// Starts a grapple if a collision has occured, draws a line from the grapple position to the target hit point
        /// </summary>
        private void StartGrapple()
        {
            //RaycastHit2D hit = Physics2D.Raycast(player.transform.position, direction.up, GrappleDistance, Grappleable);
            //Convert mouse position to world position
            //Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            //Vector2 direction = (mousePosition - (Vector2)transform.position).normalized;

            //Send a raycast in the direction of the LookAtCursor, only detecting the grappleable layer
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction.up, GrappleDistance, GrappleableLayers);
            if (hit.collider != null)
            {
                _isGrappling = true;
                _grappleUsable = false;
                _jumpAllowed = false;
                _grapplePoint = hit.point;
                _joint = gameObject.AddComponent<DistanceJoint2D>();
                _joint.connectedAnchor = _grapplePoint;
                _joint.autoConfigureDistance = false;
                _joint.distance = Vector2.Distance(transform.position, _grapplePoint) * 0.8f; // Multiplier to control the slack of the rope
                _joint.maxDistanceOnly = true;
                _lineRenderer.positionCount = 2;
                Grappled?.Invoke();

                // Optionally adjust gravity scale and drag dynamically
                _rb.gravityScale = 1.0f; // Adjust as needed for swing dynamics
                _rb.drag = 0.5f; // Increase drag to slow down the swing and allow for longer swinging time
            }
        }

        /// <summary>
        /// Stops a grapple by removing the line render
        /// </summary>
        private void StopGrapple()
        {
            if (_joint != null)
            {
                Vector2 releaseVelocity = CalculateReleaseVelocity();
                _frameVelocity = releaseVelocity;

                Destroy(_joint);

                _rb.gravityScale = 0.5f; // Adjust this value as needed

                if (resetGravityCoroutine != null)
                {
                    StopCoroutine(resetGravityCoroutine); // Ensure we don't stack coroutines
                }
                resetGravityCoroutine = StartCoroutine(ResetGravityScaleAfterDelay(2f)); // Adjust delay as needed
            }
            if (!_grounded)
            {
                FreeFall?.Invoke(); // Invoke free fall event
            }
            _lineRenderer.positionCount = 0;
            _grappleUsable = true;
            _jumpAllowed = true;
            _isGrappling = false;
        }

        private Vector2 CalculateReleaseVelocity()
        {
            Vector2 tangentialVelocity = _rb.velocity;
            return tangentialVelocity;
        }

        /// <summary>
        /// Updates line render position
        /// </summary>
        private void UpdateGrappleRope()
        {
            //if (_lineRenderer.positionCount > 0)
            if (_joint)
            {
                _lineRenderer.SetPosition(0, transform.position);
                _lineRenderer.SetPosition(1, _grapplePoint);
                _isGrappling = true;
                _jumpAllowed = false;
            }
        }
        #endregion

        #region WallClimb
        private bool _wallClimbToConsume; // Indicates a wall climb input that needs to be processed
        private bool _stopWallClimbToConsume; // Indicates a wall climb release input that needs to be processed
        private bool _isWallClimbing = false; // WallClimb is currently active
        private bool _isWallSliding = false; // WallSlide is currently active
        private bool _isLedgeClimbing = false; // LedgeClimb is currently active
        private bool _canLedgeClimb = false; // If the player can ledge climb
        private float _lastWallJumpTime = -Mathf.Infinity;
        private Vector2 _ledgeClimbStartPos;

        /// <summary>
        /// </summary>
        private void HandleWallClimb()
        {
            // Prevent Loop
            if (_wallClimbToConsume && _isWallClimbing) _wallClimbToConsume = false;

            if ((_walled && _isWallClimbing && !_isWallSliding && _canLedgeClimb) || _isLedgeClimbing) StartLedgeClimb();

            if (_walled && !_isWallClimbing && (!_wallClimbToConsume || !_stopWallClimbToConsume) && _hasPickaxe && !_grounded && !_isLedgeClimbing) WallSlide();
            if (_walled && !_isWallClimbing && _isWallSliding && _hasPickaxe && !_grounded && !_isLedgeClimbing) WallSlide();

            if (_walled && _wallClimbToConsume && _hasPickaxe && !_isLedgeClimbing) StartWallClimb();

            if ((_walled && _stopWallClimbToConsume) || !_hasPickaxe) StopWallClimb();

            _wallClimbToConsume = false;
            _stopWallClimbToConsume = false;
        }

        /// <summary>
        /// </summary>
        private void StartWallClimb()
        {
                _isWallClimbing = true;
                _isWallSliding = false;
                _frameVelocity.y = WallClimbSpeed;
                WallClimbed?.Invoke();

        }

        /// <summary>
        /// </summary>
        private void WallSlide()
        {
            _isWallSliding = true;
            _frameVelocity.y = Mathf.MoveTowards(0, -MaxFallSpeed, WallSlideSpeed * 100 * Time.fixedDeltaTime);
            WallSlid?.Invoke();
        }

        /// <summary>
        /// </summary>
        private void WallJump()
        {
            if (_time - _lastWallJumpTime < WallJumpCoolDown) return;

            if (_isWallClimbing)
            {
                _frameVelocity.y = (JumpPower / 5) * 3;
            }
            else
            {
                _frameVelocity.y = JumpPower * 3; // Apply upward force
            }

            // Determine direction to apply horizontal jump force based on the player's direction
            float horizontalJumpDirection = playerParentSprite.localScale.x < 0 ? 1 : -1;

            // Apply the horizontal jump force
            _frameVelocity.x = HorizontalJumpPower * horizontalJumpDirection;

            WallJumped?.Invoke(); // Invoke the Jumped

            _lastWallJumpTime = _time;
        }

        /// <summary>
        /// </summary>
        private void StopWallClimb()
        {
            _isWallClimbing = false;
            if (_hasPickaxe) WallSlide();
        }

        private void StartLedgeClimb()
        {
            if (_isLedgeClimbing) return;

            _ledgeClimbStartPos = transform.position;
            currentPos = transform.position;


            _isLedgeClimbing = true;
            _isWallClimbing = false;
            _isWallSliding = false;
            _ignorePlayerInput = true;
            LedgeClimb?.Invoke(); // Invoke Ledge Climb

            _frameVelocity.y = 0;
            _frameVelocity.x = 0;

            StartCoroutine(WaitForAnimation(LedgeClimbDuration));
        }

        private IEnumerator WaitForAnimation(float duration)
        {
            // Wait for the animation to finish
            yield return new WaitForSeconds(duration);
            transform.position = _ledgeClimbStartPos;
            UpdatePlayerPositionPostClimb();
        }

        private void UpdatePlayerPositionPostClimb()
        {

            _isLedgeClimbing = false;
            _isWallClimbing = false;
            _isWallSliding = false;
            transform.position = _ledgeClimbStartPos;

            // Calculate the difference between the end and start positions
            Vector2 positionDifference = LedgeClimbOffsetEnd - LedgeClimbStart;

            // Determine the direction the player is facing
            positionDifference.x *= playerParentSprite.localScale.x < 0 ? -1f : 1f;

            // Calculate the new player position by adding the position difference to the current position
            Vector2 newPos = _ledgeClimbStartPos + positionDifference;

            // Apply the new position to the player's transform
            transform.position = newPos;

            _ignorePlayerInput = false;
            _isWallClimbing = false;
            _isWallSliding = false;

        }
        #endregion

        #region Boost
        private bool _boostToConsume; // Indicates a boost that needs to be processed
        private bool _stopBoostToConsume; // Indicates a boost release that needs to be processed
        private bool _boostActive = false; // Boost is currently active conditions are met
        /// <summary>
        /// Allows a boost if the boost is available and the key has been pressed
        /// </summary>
        private void HandleBoost()
        {
            if (!_boostToConsume && !_stopBoostToConsume) return;

            if ((_isGrappling && _boostToConsume) || (_isGrappling && _boostActive)) ApplyBoostForce(); //ApplyBoostForce();

            if (_stopBoostToConsume) StopBoost();

            _boostToConsume = false;
            _stopBoostToConsume = false;
        }

        /// <summary>
        /// applies a boost if the boost is available and the key has been pressed
        /// </summary>
        private void ApplyBoostForce()
        {
            BoostEffect.Play();
            _boostActive = true; // Indicate that boost is active

            // Calculate the normalized direction vector from the player to the grapple point
            Vector2 toGrapplePoint = (_grapplePoint - (Vector2)transform.position).normalized;

            // Determine if there's horizontal input
            bool hasHorizontalInput = _frameInput.Move.x != 0;

            // Determine the direction for the boost based on player input
            Vector2 boostDirection = Vector2.zero; // Default direction
            float angleOffset = 0; // Used to adjust the boost effect's orientation

            if (hasHorizontalInput)
            {
                // Calculate boost direction based on input for rotation around the grapple point
                float rotationAngle = _frameInput.Move.x < 0 ? 90f : -90f; // Rotate left for A key, right for D key
                Quaternion rotation = Quaternion.Euler(0, 0, rotationAngle);
                boostDirection = rotation * toGrapplePoint;
                angleOffset = _frameInput.Move.x < 0 ? 180 : 0; // Adjust effect orientation based on direction
            }
            else
            {
                // If no horizontal input and space is held, pull towards the grapple point
                boostDirection = toGrapplePoint;
                angleOffset = 180; // Effect should show opposite to the pull direction
            }

            // Apply the boost force in the determined direction
            _frameVelocity = boostDirection * BoostForce + new Vector2(0, _rb.velocity.y);

            // Correctly orient the boost effect based on the action
            float effectAngle = Mathf.Atan2(boostDirection.y, boostDirection.x) * Mathf.Rad2Deg;
            BoostEffect.transform.rotation = Quaternion.Euler(0, 0, effectAngle + angleOffset);

            // Handle boost deactivation
            if (_stopBoostToConsume || _stopGrappleToConsume)
            {
                BoostEffect.Stop();
                _boostActive = false;
                _stopBoostToConsume = false;
                _stopGrappleToConsume = false;
            }
        }

        /// <summary>
        /// Stops a boost if the key has been released
        /// </summary>
        private void StopBoost()
        {
            BoostEffect.Stop();
            _boostActive = false; // Indicate that boost is no longer active
        }
        #endregion

        #region Dash
        private bool _dashToConsume; // Indicates a dash that needs to be processed
        /// <summary>
        /// Allows a dash if the dash is available and the key has been pressed
        /// </summary>
        private void HandleDash()
        {
            if (!_dashToConsume) return;

            if (_isGrappling && _dashToConsume && ((_time - _currentDashWaitTime) >= DashCooldown)) ApplyDashForce();

            _dashToConsume = false;
        }

        /// <summary>
        /// Applies a dash if the dash is available and the key has been pressed
        /// </summary>
        private void ApplyDashForce()
        {
            Debug.Log("DASH");
            DashEffect.transform.rotation = direction.rotation;
            _rb.AddForce(direction.up * DashForce, ForceMode2D.Impulse);
            DashEffect.Play();
            _currentDashWaitTime = _time;
        }
        #endregion

        #region Horizontal Movement

        /// <summary>
        /// Handles horizontal movement based on player input, applying acceleration or deceleration as needed.
        /// </summary>
        private void HandleDirection()
        {
            if (_boostActive) return;

            if (_isGrappling)
            {
                _frameVelocity.x = (float)(_frameInput.Move.x * (MaxSpeed * (0.8)));
            }

            if (_frameInput.Move.x == 0)
            {
                // Applies deceleration when no input is given.
                var deceleration = _grounded ? GroundDeceleration : AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                // Applies acceleration towards the target speed based on input direction.
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * MaxSpeed, Acceleration * Time.fixedDeltaTime);
            }
        }

        #endregion

        #region Gravity
        /// <summary>
        /// Applies gravity effects to the player, adjusting vertical velocity based on whether they are grounded or in air.
        /// </summary>
        private void HandleGravity()
        {
            if (_isWallSliding || _isWallClimbing || _ignorePlayerInput)
            {
                return;
            }
            if (_grounded && _frameVelocity.y <= 0f)
            {
                _frameVelocity.y = GroundingForce; // Applies a small force to keep the player grounded.
            }
            else
            {
                var inAirGravity = FallAcceleration;
                if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= JumpEndEarlyGravityModifier;
                _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
        }

        #endregion

        #region ToolSwap
        private bool _toolSwapToConsume; // Indicates a tool swap input that needs to be processed
        private bool _hasPickaxe = true; // Whether the player is equipped with a pickaxe
        private void HandleToolSwap()
        {
            if (!_toolSwapToConsume) return;

            if (_toolSwapToConsume) ToolSwap();

            _toolSwapToConsume = false;
        }
        private void ToolSwap()
        {
            _hasPickaxe = !_hasPickaxe;
            // Toggle the active or inactive state of the ice pickaxes
            rightIcePickaxe.SetActive(_hasPickaxe);
            leftIcePickaxe.SetActive(_hasPickaxe);

            // Toggle the active or inactive state of the grappling gun
            grapplingGun.SetActive(!_hasPickaxe);
        }
        #endregion

        /// <summary>
        /// Applies the calculated movement to the player's Rigidbody.
        /// </summary>
        //private void ApplyMovement() => _rb.velocity = _frameVelocity;
        private void ApplyMovement()
        {
            //_frameVelocity.y = _frameInput.Move.y;
            _rb.velocity = new Vector2(_frameVelocity.x, _frameVelocity.y);
        }

        private int GetRelativeYPos()
        {
            return transform.position.y < _grapplePoint.y ? -1 : 1;
        }

        private IEnumerator ResetGravityScaleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _rb.gravityScale = originalGravityScale;
        }
    }
        public struct FrameInput
        {
            public bool JumpDown; // Indicates if the jump button was pressed this frame
            public bool JumpHeld; // Indicates if the jump button is being held down
            public Vector2 Move; // The movement input vector.
            public bool Grapple; // Indicates if the grapple button was pressed this frame
            public bool StopGrapple; // Indicates if the grapple release button was pressed this frame
            public bool Boost; // Indicates if the boost button was pressed this frame
            public bool StopBoost; // Indicates if the boost release button was pressed this frame
            public bool Dash; // Indicates if the dash button was pressed this frame
            public bool ToolSwap; // Indicates if the current tool needs to be swapped on this frame
            public bool WallClimb; // Indicates if the wall climb button was pressed this frame
            public bool StopWallClimb; // Indicates if the wall climb release button was pressed this frame
            public bool WallClimbHoldOnJump; // Indicates if the wall climb button is being pressed during during a wall jump
        }
  

    // Interface for events and properties for the player controller
    public interface IPlayerController
    {
        event Action<bool, float> GroundedChanged; // Event triggered when the grounded state changes
        event Action Jumped; // Event triggered when the player jumps
        event Action Grappled; // Event triggered when the player grapples
        event Action FreeFall; // Event triggered when the player is freefalling
        event Action WallSlid; // Event triggered when the player wall slides
        event Action WallClimbed; // Event triggered when the player wall climbs
        event Action WallJumped; // Event triggered when the player wall jumps
        event Action LedgeClimb; // Event triggered when the player ledge climbs
        Vector2 FrameInput { get; } // Current frame's input vector
    }


}