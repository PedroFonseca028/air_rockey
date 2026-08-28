using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Puck : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private CircleCollider2D circleCollider;
    private AudioSource audioSource;

    // =========================================================
    // MOVIMENTO
    // =========================================================

    [Header("Movimento")]

    [SerializeField]
    private float initialSpeed = 5f;

    [SerializeField]
    private float minSpeed = 1.5f;

    [SerializeField]
    private float maxSpeed = 12f;

    // =========================================================
    // ANTI-TRAVAMENTO
    // =========================================================

    [Header("Anti-Travamento")]

    // Velocidade abaixo da qual consideramos
    // que o puck pode estar travado.
    [SerializeField]
    private float stuckSpeedThreshold = 0.40f;

    // Quanto tempo ele precisa ficar praticamente
    // parado perto da parede.
    [SerializeField]
    private float stuckTime = 0.25f;

    // Distância usada para considerar que está
    // muito próximo de uma parede/canto.
    [SerializeField]
    private float cornerDetectionDistance = 0.25f;

    // Velocidade aplicada quando destravamos o puck.
    [SerializeField]
    private float escapeSpeed = 4.5f;

    // Pequeno deslocamento para retirar fisicamente
    // o puck de dentro do canto.
    [SerializeField]
    private float escapePushDistance = 0.10f;

    // Evita deixar o collider exatamente em cima
    // do limite matemático da parede.
    [SerializeField]
    private float boundaryInset = 0.01f;

    // =========================================================
    // LIMITES
    // =========================================================

    [Header("Limites do campo")]

    [SerializeField]
    private float minX = -2.3f;

    [SerializeField]
    private float maxX = 2.3f;

    [SerializeField]
    private float minY = -4.5f;

    [SerializeField]
    private float maxY = 4.5f;

    // =========================================================
    // GOL
    // =========================================================

    [Header("Gol")]

    [SerializeField]
    private float goalHalfWidth = 0.7f;

    // =========================================================
    // SOM
    // =========================================================

    [Header("Som")]

    [SerializeField]
    private AudioClip collisionSound;

    [SerializeField]
    private float minimumImpactForSound = 0.5f;

    // =========================================================
    // RESET
    // =========================================================

    [Header("Reset")]

    [SerializeField]
    private float resetDelay = 1f;

    // =========================================================
    // ESTADO
    // =========================================================

    private Vector2 initialPosition;

    private bool resetting = false;

    private float stuckTimer = 0f;

    private ScoreManager scoreManager;

    // =========================================================
    // UNITY
    // =========================================================

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        circleCollider = GetComponent<CircleCollider2D>();
        audioSource = GetComponent<AudioSource>();

        scoreManager =
            FindFirstObjectByType<ScoreManager>();

        initialPosition =
            rb2d.position;

        // Air Hockey:
        // não queremos gravidade nem perda natural
        // de velocidade.
        rb2d.gravityScale = 0f;
        rb2d.linearDamping = 0f;
        rb2d.angularDamping = 0f;

        // Evita atravessar paredes em alta velocidade.
        rb2d.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;

        // Movimento visual mais suave.
        rb2d.interpolation =
            RigidbodyInterpolation2D.Interpolate;

        // Não deixa o Rigidbody "dormir".
        rb2d.sleepMode =
            RigidbodySleepMode2D.NeverSleep;
    }

    void Start()
    {
        LaunchPuck();
    }

    void FixedUpdate()
    {
        if (resetting)
            return;

        // Primeiro garante que o puck não escapou
        // dos limites do campo.
        KeepInsideField();

        // Depois verifica situações de travamento.
        PreventStuck();

        // Por último mantém a velocidade saudável.
        LimitSpeed();
    }

    // =========================================================
    // VELOCIDADE
    // =========================================================

    private void LimitSpeed()
    {
        Vector2 velocity =
            rb2d.linearVelocity;

        float speed =
            velocity.magnitude;

        // =============================================
        // VELOCIDADE MÁXIMA
        // =============================================

        if (speed > maxSpeed)
        {
            rb2d.linearVelocity =
                velocity.normalized *
                maxSpeed;

            return;
        }

        // =============================================
        // VELOCIDADE MÍNIMA
        // =============================================

        /*
         * Se estiver quase completamente parado,
         * NÃO fazemos nada aqui.
         *
         * PreventStuck() é responsável por retirar
         * o puck dessas situações.
         */

        if (
            speed > stuckSpeedThreshold &&
            speed < minSpeed
        )
        {
            rb2d.linearVelocity =
                velocity.normalized *
                minSpeed;
        }
    }

    // =========================================================
    // MANTÉM O PUCK DENTRO DO CAMPO
    // =========================================================

    private void KeepInsideField()
    {
        Vector2 position =
            rb2d.position;

        Vector2 velocity =
            rb2d.linearVelocity;

        // Tamanho físico REAL do puck.
        float radiusX =
            circleCollider.bounds.extents.x;

        float radiusY =
            circleCollider.bounds.extents.y;

        bool hitHorizontalWall = false;
        bool hitVerticalWall = false;

        // =============================================
        // PAREDE ESQUERDA
        // =============================================

        if (
            position.x - radiusX <
            minX
        )
        {
            position.x =
                minX +
                radiusX +
                boundaryInset;

            // Só corrige a velocidade se ela ainda
            // estiver apontando PARA a parede.
            if (velocity.x < 0f)
            {
                velocity.x =
                    Mathf.Abs(
                        velocity.x
                    );
            }

            hitHorizontalWall = true;
        }

        // =============================================
        // PAREDE DIREITA
        // =============================================

        if (
            position.x + radiusX >
            maxX
        )
        {
            position.x =
                maxX -
                radiusX -
                boundaryInset;

            if (velocity.x > 0f)
            {
                velocity.x =
                    -Mathf.Abs(
                        velocity.x
                    );
            }

            hitHorizontalWall = true;
        }

        // =============================================
        // ABERTURA DO GOL
        // =============================================

        bool canEnterGoal =
            Mathf.Abs(position.x) +
            radiusX <= goalHalfWidth;

        // =============================================
        // PAREDES SUPERIOR / INFERIOR
        // =============================================

        if (!canEnterGoal)
        {
            // PAREDE INFERIOR
            if (
                position.y - radiusY <
                minY
            )
            {
                position.y =
                    minY +
                    radiusY +
                    boundaryInset;

                if (velocity.y < 0f)
                {
                    velocity.y =
                        Mathf.Abs(
                            velocity.y
                        );
                }

                hitVerticalWall = true;
            }

            // PAREDE SUPERIOR
            if (
                position.y + radiusY >
                maxY
            )
            {
                position.y =
                    maxY -
                    radiusY -
                    boundaryInset;

                if (velocity.y > 0f)
                {
                    velocity.y =
                        -Mathf.Abs(
                            velocity.y
                        );
                }

                hitVerticalWall = true;
            }
        }

        // =============================================
        // CANTO
        // =============================================

        /*
         * Se o puck ultrapassou duas paredes ao mesmo
         * tempo, ele chegou efetivamente no canto.
         *
         * Não deixamos a física decidir sozinha uma
         * velocidade quase zero.
         *
         * Mandamos o puck diagonalmente para dentro.
         */

        if (
            hitHorizontalWall &&
            hitVerticalWall
        )
        {
            Vector2 inwardDirection =
                GetDirectionTowardField(
                    position
                );

            float newSpeed =
                Mathf.Max(
                    velocity.magnitude,
                    escapeSpeed
                );

            newSpeed =
                Mathf.Min(
                    newSpeed,
                    maxSpeed
                );

            velocity =
                inwardDirection *
                newSpeed;
        }

        rb2d.position =
            position;

        rb2d.linearVelocity =
            velocity;
    }

    // =========================================================
    // DETECÇÃO DE TRAVAMENTO
    // =========================================================

    private void PreventStuck()
    {
        float speed =
            rb2d.linearVelocity.magnitude;

        bool nearDangerousArea =
            IsNearSideWall() ||
            IsNearTopOrBottomWall();

        // =============================================
        // PUCK LENTO PERTO DE UMA PAREDE
        // =============================================

        if (
            nearDangerousArea &&
            speed <= stuckSpeedThreshold
        )
        {
            stuckTimer +=
                Time.fixedDeltaTime;
        }

        // =============================================
        // PUCK MUITO LENTO NUM CANTO
        // =============================================

        else if (
            IsNearCorner() &&
            speed < minSpeed
        )
        {
            stuckTimer +=
                Time.fixedDeltaTime;
        }

        else
        {
            stuckTimer = 0f;
        }

        // =============================================
        // DESTRAVA
        // =============================================

        if (
            stuckTimer >=
            stuckTime
        )
        {
            EscapeFromStuckPosition();

            stuckTimer = 0f;
        }
    }

    // =========================================================
    // ESCAPA DE UMA POSIÇÃO TRAVADA
    // =========================================================

    private void EscapeFromStuckPosition()
    {
        Vector2 position =
            rb2d.position;

        Vector2 direction =
            GetDirectionTowardField(
                position
            );

        /*
         * Evita trajetórias perfeitamente verticais.
         *
         * Elas podem fazer o puck voltar exatamente
         * para o mesmo ponto.
         */

        if (
            Mathf.Abs(direction.x) <
            0.15f
        )
        {
            direction.x =
                Random.value > 0.5f
                ? 0.25f
                : -0.25f;
        }

        direction.Normalize();

        // =============================================
        // RETIRA FISICAMENTE DO CANTO
        // =============================================

        Vector2 newPosition =
            position +
            direction *
            escapePushDistance;

        newPosition =
            ClampInsideField(
                newPosition
            );

        rb2d.position =
            newPosition;

        // =============================================
        // DÁ VELOCIDADE PARA DENTRO
        // =============================================

        rb2d.linearVelocity =
            direction *
            escapeSpeed;

        rb2d.angularVelocity =
            0f;
    }

    // =========================================================
    // DIREÇÃO PARA O INTERIOR DO CAMPO
    // =========================================================

    private Vector2 GetDirectionTowardField(
        Vector2 position
    )
    {
        Vector2 direction =
            Vector2.zero;

        // =============================================
        // HORIZONTAL
        // =============================================

        if (position.x > 0.05f)
        {
            direction.x = -1f;
        }
        else if (position.x < -0.05f)
        {
            direction.x = 1f;
        }
        else
        {
            direction.x =
                Random.value > 0.5f
                ? 0.30f
                : -0.30f;
        }

        // =============================================
        // VERTICAL
        // =============================================

        if (position.y > 0.05f)
        {
            direction.y = -1f;
        }
        else if (position.y < -0.05f)
        {
            direction.y = 1f;
        }
        else
        {
            direction.y =
                Random.value > 0.5f
                ? 0.30f
                : -0.30f;
        }

        return direction.normalized;
    }

    // =========================================================
    // VERIFICAÇÕES DAS PAREDES
    // =========================================================

    private bool IsNearSideWall()
    {
        float radiusX =
            circleCollider.bounds.extents.x;

        bool nearLeft =
            rb2d.position.x -
            radiusX <=
            minX +
            cornerDetectionDistance;

        bool nearRight =
            rb2d.position.x +
            radiusX >=
            maxX -
            cornerDetectionDistance;

        return
            nearLeft ||
            nearRight;
    }

    private bool IsNearTopOrBottomWall()
    {
        float radiusX =
            circleCollider.bounds.extents.x;

        float radiusY =
            circleCollider.bounds.extents.y;

        // Dentro do gol não queremos tratar
        // a borda vertical como parede.
        bool canEnterGoal =
            Mathf.Abs(
                rb2d.position.x
            ) +
            radiusX <=
            goalHalfWidth;

        if (canEnterGoal)
            return false;

        bool nearTop =
            rb2d.position.y +
            radiusY >=
            maxY -
            cornerDetectionDistance;

        bool nearBottom =
            rb2d.position.y -
            radiusY <=
            minY +
            cornerDetectionDistance;

        return
            nearTop ||
            nearBottom;
    }

    // =========================================================
    // VERIFICA SE ESTÁ NO CANTO
    // =========================================================

    private bool IsNearCorner()
    {
        return
            IsNearSideWall() &&
            IsNearTopOrBottomWall();
    }

    // =========================================================
    // CLAMP DE SEGURANÇA
    // =========================================================

    private Vector2 ClampInsideField(
        Vector2 position
    )
    {
        float radiusX =
            circleCollider.bounds.extents.x;

        float radiusY =
            circleCollider.bounds.extents.y;

        position.x =
            Mathf.Clamp(
                position.x,
                minX +
                    radiusX +
                    boundaryInset,
                maxX -
                    radiusX -
                    boundaryInset
            );

        bool canEnterGoal =
            Mathf.Abs(position.x) +
            radiusX <=
            goalHalfWidth;

        if (!canEnterGoal)
        {
            position.y =
                Mathf.Clamp(
                    position.y,
                    minY +
                        radiusY +
                        boundaryInset,
                    maxY -
                        radiusY -
                        boundaryInset
                );
        }

        return position;
    }

    // =========================================================
    // LANÇAMENTO
    // =========================================================

    private void LaunchPuck()
    {
        stuckTimer = 0f;

        float randomX =
            Random.Range(
                -0.5f,
                0.5f
            );

        float directionY =
            Random.value > 0.5f
            ? 1f
            : -1f;

        Vector2 direction =
            new Vector2(
                randomX,
                directionY
            ).normalized;

        rb2d.linearVelocity =
            direction *
            initialSpeed;
    }

    // =========================================================
    // COLISÃO
    // =========================================================

    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        Debug.Log(
            "Puck colidiu com: " +
            collision.gameObject.name
        );

        PlayCollisionSound(
            collision
        );
    }

    // =========================================================
    // SOM
    // =========================================================

    private void PlayCollisionSound(
        Collision2D collision
    )
    {
        if (
            audioSource == null ||
            collisionSound == null
        )
        {
            return;
        }

        if (
            collision.relativeVelocity.magnitude <
            minimumImpactForSound
        )
        {
            return;
        }

        audioSource.PlayOneShot(
            collisionSound
        );
    }

    // =========================================================
    // GOL
    // =========================================================

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        if (resetting)
            return;

        // PLAYER FEZ GOL
        if (
            other.CompareTag(
                "GoalTop"
            )
        )
        {
            Debug.Log(
                "Gol do jogador!"
            );

            if (scoreManager != null)
            {
                scoreManager.PlayerScored();
            }

            StartCoroutine(
                ResetPuck()
            );
        }

        // IA FEZ GOL
        else if (
            other.CompareTag(
                "GoalBottom"
            )
        )
        {
            Debug.Log(
                "Gol da IA!"
            );

            if (scoreManager != null)
            {
                scoreManager.AIScored();
            }

            StartCoroutine(
                ResetPuck()
            );
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    private IEnumerator ResetPuck()
    {
        resetting = true;

        stuckTimer = 0f;

        rb2d.linearVelocity =
            Vector2.zero;

        rb2d.angularVelocity =
            0f;

        rb2d.position =
            initialPosition;

        yield return new WaitForSeconds(
            resetDelay
        );

        LaunchPuck();

        resetting = false;
    }
}
