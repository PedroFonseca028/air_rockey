using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Puck : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private CircleCollider2D circleCollider;
    private AudioSource audioSource;

    [Header("Movimento")]
    [SerializeField] private float initialSpeed = 5f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Limites do campo")]
    [SerializeField] private float minX = -2.3f;
    [SerializeField] private float maxX = 2.3f;
    [SerializeField] private float minY = -4.5f;
    [SerializeField] private float maxY = 4.5f;

    [Header("Gol")]
    [SerializeField] private float goalHalfWidth = 0.7f;

    [Header("Som")]
    [SerializeField] private AudioClip collisionSound;
    [SerializeField] private float minimumImpactForSound = 0.5f;

    [Header("Reset")]
    [SerializeField] private float resetDelay = 1f;

    private Vector2 initialPosition;
    private bool resetting = false;

    private ScoreManager scoreManager;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        circleCollider = GetComponent<CircleCollider2D>();
        audioSource = GetComponent<AudioSource>();

        scoreManager = FindFirstObjectByType<ScoreManager>();

        initialPosition = rb2d.position;

        rb2d.gravityScale = 0f;
        rb2d.linearDamping = 0f;
        rb2d.angularDamping = 0f;

        rb2d.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
    }

    void Start()
    {
        LaunchPuck();
    }

    void FixedUpdate()
    {
        if (resetting)
            return;

        LimitSpeed();
        KeepInsideField();
    }

    private void LimitSpeed()
    {
        if (rb2d.linearVelocity.magnitude > maxSpeed)
        {
            rb2d.linearVelocity =
                rb2d.linearVelocity.normalized * maxSpeed;
        }
    }

    private void KeepInsideField()
    {
        Vector2 position = rb2d.position;
        Vector2 velocity = rb2d.linearVelocity;

        float radiusX = circleCollider.bounds.extents.x;
        float radiusY = circleCollider.bounds.extents.y;

        if (position.x - radiusX < minX)
        {
            position.x = minX + radiusX;
            velocity.x = Mathf.Abs(velocity.x);
        }

        if (position.x + radiusX > maxX)
        {
            position.x = maxX - radiusX;
            velocity.x = -Mathf.Abs(velocity.x);
        }

        bool canEnterGoal =
            Mathf.Abs(position.x) + radiusX <= goalHalfWidth;

        if (!canEnterGoal)
        {
            if (position.y - radiusY < minY)
            {
                position.y = minY + radiusY;
                velocity.y = Mathf.Abs(velocity.y);
            }

            if (position.y + radiusY > maxY)
            {
                position.y = maxY - radiusY;
                velocity.y = -Mathf.Abs(velocity.y);
            }
        }

        rb2d.position = position;
        rb2d.linearVelocity = velocity;
    }

    private void LaunchPuck()
    {
        float randomX = Random.Range(-0.5f, 0.5f);

        float directionY =
            Random.value > 0.5f ? 1f : -1f;

        Vector2 direction =
            new Vector2(randomX, directionY).normalized;

        rb2d.linearVelocity =
            direction * initialSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log(
            "Puck colidiu com: " +
            collision.gameObject.name
        );

        PlayCollisionSound(collision);
    }

    private void PlayCollisionSound(Collision2D collision)
    {
        if (audioSource == null || collisionSound == null)
            return;

        if (collision.relativeVelocity.magnitude <
            minimumImpactForSound)
        {
            return;
        }

        audioSource.PlayOneShot(collisionSound);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (resetting)
            return;

        if (other.CompareTag("GoalTop"))
        {
            Debug.Log("Gol do jogador!");

            if (scoreManager != null)
            {
                scoreManager.PlayerScored();
            }

            StartCoroutine(ResetPuck());
        }

        else if (other.CompareTag("GoalBottom"))
        {
            Debug.Log("Gol da IA!");

            if (scoreManager != null)
            {
                scoreManager.AIScored();
            }

            StartCoroutine(ResetPuck());
        }
    }

    private IEnumerator ResetPuck()
    {
        resetting = true;

        rb2d.linearVelocity = Vector2.zero;
        rb2d.angularVelocity = 0f;

        rb2d.position = initialPosition;

        yield return new WaitForSeconds(resetDelay);

        LaunchPuck();

        resetting = false;
    }
}
