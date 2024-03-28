using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public class RangedEnemy : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The enemy animator")]
    [SerializeField] private Animator _anim;
    [Tooltip("The enemey projectile ex. snowball")]
    [SerializeField] public GameObject projectilePrefab;
    [Tooltip("Where the projectile should be launched from")]
    [SerializeField] public Transform projectileSpawnPoint;
    [Tooltip("All ground layers for enemy collision and efge detection")]
    [SerializeField] public LayerMask groundLayer;
    [Tooltip("Edge detection point")]
    [SerializeField] public Transform groundDetectionPoint;
    [Tooltip("Ground detection distance")]
    [SerializeField] public float groundDetectionDistance = 2f;
    [Tooltip("The player object")]
    [SerializeField] public Transform player;


    [Header("Projectile Properties")]
    [Tooltip("The projectiles speed")]
    [SerializeField] public float projectileSpeed = 5f;
    [Tooltip("The enemy's fire rate")]
    [SerializeField] public float fireRate = 2f;
    [Tooltip("The enemy's throw delay from the start of the throw anamation")]
    [SerializeField] public float throwDelay;

    [Header("Movement Properties")]
    [Tooltip("The enemy movement spped")]
    [SerializeField] public float moveSpeed = 2f;
    [Tooltip("The enemy detection distance")]
    [SerializeField] public float detectionRadius = 10f;
    [Tooltip("The player layer")]
    [SerializeField] public LayerMask playerLayer;

    [Header("Death Properties00")]
    [Tooltip("Death Animation Duration")]
    [SerializeField] public float deathAnimationTime = 4f;

    private float _nextFireTime;
    private bool _throwing = false;
    private bool _foundPlayer = false;
    private bool _dead = false;
    private Rigidbody2D _rigidbody;


    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        _anim.SetBool("Throwing", _throwing);
        _anim.SetBool("Patroling",!_foundPlayer);
        _anim.SetBool("Dead", _dead);

        if (!_dead)
        {
            CheckForPlayer();
            if (_foundPlayer)
            {
                FacePlayer();

                if (Time.time >= _nextFireTime && !_throwing)
                {
                    _nextFireTime = Time.time + 1f / fireRate;
                    _anim.SetTrigger(YetiThrow);
                    StartCoroutine(FireProjectileAfterAnimation(throwDelay));
                    _throwing = true;
                }
            }
            else
            {
                Move();
                CheckForEdgeOrObstacle();
            }
        }
    }


    void Move()
    {
        float moveDirection = transform.localScale.x < 0 ? 1f : -1f; // Flipped logic based on left natural facing direction
        transform.Translate(Vector2.right * moveDirection * moveSpeed * Time.deltaTime);
    }

    void FacePlayer()
    {
        float directionToPlayer = player.position.x - transform.position.x;

        // Flip only if the player is on the opposite side from the facing direction
        if ((directionToPlayer > 0 && transform.localScale.x > 0) || (directionToPlayer < 0 && transform.localScale.x < 0))
        {
            Flip();
        }
    }
    public void DeathSequence()
    {
        _dead = true;
        _anim.SetTrigger("Die");
        _rigidbody.isKinematic = true;
        _rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;

        StartCoroutine(WaitAndDestroy(deathAnimationTime));
    }

    IEnumerator WaitAndDestroy(float delay)
    {
        // Wait for the defined duration
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    IEnumerator FireProjectileAfterAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        FireProjectile();
        _throwing = false;
    }

    void FireProjectile()
    {
        Vector2 direction = (player.position - projectileSpawnPoint.position).normalized;
        GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);

        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        rb.velocity = direction * projectileSpeed;
    }

    void CheckForPlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, playerLayer);
        _foundPlayer = hits.Length > 0;
    }

    void CheckForEdgeOrObstacle()
    {
        Vector2 forwardDirection = transform.localScale.x < 0 ? Vector2.right : Vector2.left;
        bool isGroundAhead = Physics2D.Raycast(groundDetectionPoint.position, Vector2.down, groundDetectionDistance, groundLayer);

        if (!isGroundAhead || Physics2D.Raycast(groundDetectionPoint.position, forwardDirection, groundDetectionDistance, groundLayer))
        {
            Flip();
        }
    }

    // NOT USED
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            Flip();
        }
    }

    void Flip()
    {
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Vector2 forwardDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector3 detectionDirection = forwardDirection * (transform.eulerAngles.y >= 180 ? -1 : 1); // Adjust based on enemy's facing direction
        Gizmos.color = Color.blue; // Use a different color to distinguish this line
        Gizmos.DrawLine(groundDetectionPoint.position, groundDetectionPoint.position + detectionDirection * groundDetectionDistance);
    }

    private static readonly int YetiThrow = Animator.StringToHash("YetiThrow");
}