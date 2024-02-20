using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
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
        [Tooltip("Set this to the layer your player is on")]
        public LayerMask PlayerLayer;
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

        [Tooltip("The detection distance for grounding and roof detection"), Range(0f, 0.5f)]
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
        [Tooltip("Set this to the layers considered grappleable surfaces")]
        public LayerMask GrappleableLayers;
        [Tooltip("Maximum distance the grappling hook can travel from the player to attach to a grappleable surface")]
        public float GrappleDistance = 10f;
        [Tooltip("Position from where the grapple is shot, typically the player's hand or a grappling device")]
        public UnityEngine.Transform direction;
        #endregion

        #endregion

        private Rigidbody2D _rb; // Rigidbody component used for physics
        private CapsuleCollider2D _col; // Collider component used for collision detection
        private FrameInput _frameInput; // Stores current frame's input data
        private Vector2 _frameVelocity; // Calculated velocity to apply to the Rigidbody
        private bool _cachedQueryStartInColliders;

        // Grappling Mechanic Fields and Properties
        private LineRenderer _lineRenderer; // Component used to render the grappling rope
        private DistanceJoint2D _joint; // Restricts the player's movement to within a certain distance from the grapple point
        private Vector2 _grapplePoint; // Game world grappling hook attach point

        // Boost Mechanic
        private bool _allowedBoost;
        private int _yRelative;
        public float boostForce; //Move to Settings Store
        public ParticleSystem boostEffect;
        public float maxSpeed;

        // Aerial Dash Mechanic
        public ParticleSystem dashEffect;
        public float dashCooldown;
        public float dashForce;
        private float currentDashWaitTime;

        // Exposed properties and events through IPlayerController interface
        public Vector2 FrameInput => _frameInput.Move;
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        public event Action Grappled;

        private float _time; // Tracks elapsed time, used for input buffering and coyote time calculations

        private void Awake()
        {
            // Initialize components and create inital cache
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
            _lineRenderer = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
        }

        // Start is called before the first frame update
        private void Start()
        {
            //Time since last dash
            currentDashWaitTime = Time.time;
        }

        private void Update()
        {
            // Update time and gather player input for each frame
            _time += Time.deltaTime;
            GatherInput();
        }

        /// <summary>
        /// Gather and processe player input, updating the FrameInput struct
        /// </summary>
        private void GatherInput()
        {
            _frameInput = new FrameInput
            {
                JumpDown = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W),
                JumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.W),
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Grapple = Input.GetMouseButtonDown(0),
                StopGrapple = Input.GetMouseButtonUp(0),
                Boost = Input.GetKey(KeyCode.Space),
                StopBoost = Input.GetKeyUp(KeyCode.Space)
            };

            // Apply input snapping to avoid unintentional small movements
            if (SnapInput)
            {
                _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
                _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
            }

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
        }

        private void FixedUpdate()
        {
            // Perform physics and movement calculations at a fixed intervals
            CheckCollisions();
            HandleJump();
            HandleGrapple();
            HandleDirection();
            HandleGravity();
            ApplyMovement();
            HandleBoost();

            if (_grounded && _joint)
            {
                // Allow the joint distance to decrease while the player is grounded so they don't get stuck
                _joint.maxDistanceOnly = true;
            }
        }

        private void LateUpdate()
        {
            //  Visually update grappling rope's position
            UpdateGrappleRope();
        }

        #region Collisions

        private float _frameLeftGrounded = float.MinValue; // Tracks the time when the player last left the ground
        private bool _grounded; // Indicates whether the player is currently grounded

        /// <summary>
        /// Check for collisions with the ground and ceiling using capsule casts
        /// Update the grounded state and invokes GroundedChanged events if collisions occur
        /// </summary>
        private void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;

            // Capsule casts to detect ground and ceiling collisions
            bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, GrounderDistance, ~PlayerLayer);
            bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, GrounderDistance, ~PlayerLayer);

            // Veiling collisions
            if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

            // Ground detection
            if (!_grounded && groundHit)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
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

            if (_jumpAllowed && (_grounded || CanUseCoyote)) ExecuteJump();

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

        /// <summary>
        /// Determines if the player can grapple, and grapples if conditions are met
        /// </summary>
        private void HandleGrapple()
        {
            if (!_grappleToConsume && !_stopGrappleToConsume) return;

            if (_grappleUsable && _grappleToConsume) StartGrapple();

            if (_stopGrappleToConsume) StopGrapple();

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
                _grapplePoint = hit.point;
                _joint = gameObject.AddComponent<DistanceJoint2D>();
                _joint.connectedAnchor = _grapplePoint;
                _joint.autoConfigureDistance = false;
                _joint.distance = Vector2.Distance(transform.position, _grapplePoint) * 0.8f; // Multiplier to control the slack of the rope
                _joint.maxDistanceOnly = true;
                _lineRenderer.positionCount = 2;
            }
            Grappled?.Invoke();
            _grappleUsable = false;
            _jumpAllowed = false;
            _isGrappling = true;
        }

        /// <summary>
        /// Stops a grapple by removing the line render
        /// </summary>
        private void StopGrapple()
        {
            if (_joint != null)
            {
                Destroy(_joint);
            }
            //StopGrapple?.Invoke();
            _lineRenderer.positionCount = 0;
            _grappleUsable = true;
            _jumpAllowed = true;
            _isGrappling = false;
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
            }
        }
        #endregion

        #region Boost
        private bool _boostToConsume; // Indicates a boost that needs to be processed
        private bool _stopBoostToConsume; // Indicates a boost release that needs to be processed
        private bool _boostUsable; // Allows boost
        private bool _boostActive = false; // Boost is currently active conditions are met
        /// <summary>
        /// Allows a boost if the boost is available and the key has been pressed
        /// </summary>
        private void HandleBoost()
        {
            if (!_boostToConsume && !_stopBoostToConsume) return;

            if (_isGrappling && _boostToConsume) AllowBoost();

            if (_stopBoostToConsume) StopBoost();

            _boostToConsume = false;
            _stopBoostToConsume = false;
        }
        /// <summary>
        /// Allows a boost if the boost is available and the key has been pressed
        /// </summary>
        private void AllowBoost()
        {
            _boostUsable = true;
        }

        private void StartBoost(float rawHorizontalInput)
        {
            _yRelative = getRelativeYPos();
            boostEffect.Play();

            Vector2 ropeVector = (_grapplePoint - (Vector2)transform.position).normalized;

            // Create a quaternion representing the 90 degree rotation
            Quaternion rotation = Quaternion.Euler(0, 0, rawHorizontalInput * -90.0f * Mathf.Deg2Rad);

            Vector2 normalVector = rotation * ropeVector;


            float angle = Mathf.Atan2(ropeVector.y, ropeVector.x) * Mathf.Rad2Deg + 90f;

            //BUG
            //Horizontal Movement Not Working
            //Debug.Log(" " + ropeVector + " " + normalVector + " " + boostForce);
            //_frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, rawHorizontalInput * MaxSpeed, Acceleration * Time.fixedDeltaTime);
            //_rb.velocity = new Vector2(rawHorizontalInput * MaxSpeed, _rb.velocity.y);

            if (rawHorizontalInput != 0)
            {
                if (_yRelative == 0)
                {
                    _rb.AddForce(normalVector * boostForce, ForceMode2D.Impulse);

                    boostEffect.transform.rotation = Quaternion.AngleAxis((angle + rawHorizontalInput * -90f), Vector3.forward);
                }
                else
                {
                    _rb.AddForce(-normalVector * boostForce, ForceMode2D.Impulse);

                    boostEffect.transform.rotation = Quaternion.AngleAxis((angle + rawHorizontalInput * 90f), Vector3.forward);
                }
            }

            _boostToConsume = false;
            _boostActive = true;
        }

        /// <summary>
        /// Stops a boost if the key has been released
        /// </summary>
        private void StopBoost()
        {
            boostEffect.Stop();
            _boostUsable = false;
            _boostActive = false;
            _stopBoostToConsume = false;
        }
        #endregion

        #region Horizontal Movement

        /// <summary>
        /// Handles horizontal movement based on player input, applying acceleration or deceleration as needed.
        /// </summary>
        private void HandleDirection()
        {
            if (_frameInput.Move.x == 0)
            {
                // Applies deceleration when no input is given.
                var deceleration = _grounded ? GroundDeceleration : AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
            }
            else if(_isGrappling && _boostUsable){
                StartBoost(_frameInput.Move.x);
            }
            else if (!_isGrappling)
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

        /// <summary>
        /// Applies the calculated movement to the player's Rigidbody.
        /// </summary>
        private void ApplyMovement() => _rb.velocity = _frameVelocity;

        private int getRelativeYPos()
        {
            if (transform.position.y <= _grapplePoint.y)
            {
                return 0;
            }
            else
            {
                return 1;
            }
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
        }

    // Interface for events and properties for the player controller
    public interface IPlayerController
    {
        event Action<bool, float> GroundedChanged; // Event triggered when the grounded state changes
        event Action Jumped; // Event triggered when the player jumps
        event Action Grappled; // Event triggered when the player grapples
        Vector2 FrameInput { get; } // Current frame's input vector
    }
}
