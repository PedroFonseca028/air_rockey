using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerAI : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private Rigidbody2D puckRb2d;

    private Vector2 homePosition;
    private Vector2 currentTarget;

    private enum AIState
    {
        Defense,
        Positioning,
        Attack,
        Retreat
    }

    private AIState currentState = AIState.Defense;

    [Header("Referências")]
    [SerializeField] private Transform puck;

    [Header("Movimento da IA")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float attackSpeedMultiplier = 1.15f;

    // Quanto tempo a IA leva para recalcular sua decisão.
    [SerializeField] private float reactionTime = 0.12f;

    // Pequeno erro para ela não ser perfeita.
    [SerializeField] private float aimError = 0.10f;

    [Header("Ataque")]
    // A IA só começa a atacar quando a bola entra nessa região.
    [SerializeField] private float attackY = 0.7f;

    // Distância que ela tenta ficar "atrás" da bola antes de bater.
    [SerializeField] private float behindPuckDistance = 0.55f;

    // Até onde ela tenta passar depois da bola.
    [SerializeField] private float hitThroughDistance = 0.75f;

    // Quanto tenta mandar a bola para o centro.
    [SerializeField] private float shotCenterBias = 0.25f;

    // Distância considerada suficiente para estar posicionada.
    [SerializeField] private float positioningTolerance = 0.20f;

    [Header("Defesa")]
    [SerializeField] private float defenseY = 3.3f;

    [SerializeField]
    [Range(0f, 1f)]
    private float defensiveTracking = 0.75f;

    // Quanto a IA tenta prever a trajetória da bola.
    [SerializeField]
    [Range(0f, 1f)]
    private float predictionStrength = 0.75f;

    [SerializeField] private float maxPredictionTime = 0.8f;

    [Header("Anti-Travamento")]
    // Distância da parede em que a IA fica mais cuidadosa.
    [SerializeField] private float wallSafeMargin = 0.55f;

    // Distância entre IA e puck considerada contato próximo.
    [SerializeField] private float puckContactDistance = 0.65f;

    // Quanto tempo precisa ficar pressionando para recuar.
    [SerializeField] private float stuckDetectionTime = 0.20f;

    // Quanto tempo fica recuando.
    [SerializeField] private float retreatTime = 0.40f;

    // Quanto recua horizontalmente para o centro.
    [SerializeField] private float retreatDistance = 0.8f;

    [Header("Limites do campo")]
    [SerializeField] private float minX = -2.3f;
    [SerializeField] private float maxX = 2.3f;
    [SerializeField] private float centerY = 0f;
    [SerializeField] private float maxY = 4.5f;

    [Header("Tamanho da palheta")]
    [SerializeField] private float malletRadius = 0.30f;

    private float reactionTimer;
    private float retreatTimer;
    private float stuckTimer;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();

        homePosition = rb2d.position;
        currentTarget = homePosition;

        if (puck != null)
        {
            puckRb2d = puck.GetComponent<Rigidbody2D>();
        }
    }

    void FixedUpdate()
    {
        if (puck == null)
            return;

        // Caso o puck seja colocado depois no Inspector.
        if (puckRb2d == null)
        {
            puckRb2d = puck.GetComponent<Rigidbody2D>();
        }

        // =============================
        // VERIFICA SE PRENDEU A BOLA
        // =============================

        CheckIfPuckIsStuck();

        // =============================
        // RECUO
        // =============================

        if (retreatTimer > 0f)
        {
            retreatTimer -= Time.fixedDeltaTime;

            currentState = AIState.Retreat;

            MoveAI();
            return;
        }

        // =============================
        // TEMPO DE REAÇÃO
        // =============================

        reactionTimer -= Time.fixedDeltaTime;

        if (reactionTimer <= 0f)
        {
            DecideAction();

            reactionTimer = reactionTime;
        }

        MoveAI();
    }

    // ============================================
    // DECISÃO PRINCIPAL DA IA
    // ============================================

    private void DecideAction()
    {
        // Bola está no campo do jogador.
        if (puck.position.y <= attackY)
        {
            Defend();
            return;
        }

        // Se a bola estiver encostada na parede superior,
        // não tenta prensá-la.
        if (IsNearTopWall())
        {
            HandleTopWall();
            return;
        }

        // Se estiver encostada numa lateral e ainda não estiver
        // saindo dela, a IA espera numa posição segura.
        if (IsNearSideWall() && !PuckIsLeavingSideWall())
        {
            HandleSideWall();
            return;
        }

        AttackPuck();
    }

    // ============================================
    // DEFESA
    // ============================================

    private void Defend()
    {
        currentState = AIState.Defense;

        float targetX = puck.position.x;

        // Se a bola estiver vindo em direção à IA,
        // tenta prever onde ela chegará.
        if (puckRb2d != null &&
            puckRb2d.linearVelocity.y > 0.1f)
        {
            float distanceY =
                defenseY - puck.position.y;

            float timeToDefense =
                distanceY / puckRb2d.linearVelocity.y;

            timeToDefense = Mathf.Clamp(
                timeToDefense,
                0f,
                maxPredictionTime
            );

            float predictedX =
                puck.position.x +
                puckRb2d.linearVelocity.x *
                timeToDefense;

            predictedX = ReflectXInsideField(predictedX);

            targetX = Mathf.Lerp(
                puck.position.x,
                predictedX,
                predictionStrength
            );
        }
        else
        {
            // Se a bola estiver indo para longe,
            // acompanha parcialmente.
            targetX = Mathf.Lerp(
                homePosition.x,
                puck.position.x,
                defensiveTracking
            );
        }

        SetTarget(
            new Vector2(
                targetX,
                defenseY
            )
        );
    }

    // ============================================
    // ATAQUE
    // ============================================

    private void AttackPuck()
    {
        /*
         * Para mandar a bola para baixo:
         *
         *      IA
         *      🔴
         *
         *      ⚫
         *     puck
         *
         * ----------------
         * campo do jogador
         *
         * Primeiro a IA tenta chegar ACIMA da bola.
         */

        Vector2 behindPosition = new Vector2(
            puck.position.x,
            puck.position.y + behindPuckDistance
        );

        behindPosition.y = Mathf.Min(
            behindPosition.y,
            maxY - malletRadius
        );

        float distanceToBehindPosition =
            Vector2.Distance(
                rb2d.position,
                behindPosition
            );

        // Ainda não está bem posicionada.
        if (distanceToBehindPosition >
            positioningTolerance)
        {
            currentState = AIState.Positioning;

            float errorX =
                Random.Range(-aimError, aimError);

            behindPosition.x += errorX;

            SetTarget(behindPosition);

            return;
        }

        // Já ficou atrás do puck.
        // Agora realmente bate.
        currentState = AIState.Attack;

        float centerDirection = 0f;

        if (Mathf.Abs(puck.position.x) > 0.15f)
        {
            // Se está na direita, tenta mandar um pouco para esquerda.
            // Se está na esquerda, tenta mandar um pouco para direita.
            centerDirection =
                -Mathf.Sign(puck.position.x) *
                shotCenterBias;
        }

        Vector2 strikePosition = new Vector2(
            puck.position.x + centerDirection,
            puck.position.y - hitThroughDistance
        );

        SetTarget(strikePosition);
    }

    // ============================================
    // PAREDE LATERAL
    // ============================================

    private void HandleSideWall()
    {
        currentState = AIState.Positioning;

        /*
         * Não vai para cima do puck.
         *
         * Fica mais para dentro da quadra esperando
         * o puck sair da parede.
         */

        float safeX = puck.position.x;

        if (puck.position.x < 0f)
        {
            safeX =
                minX +
                wallSafeMargin +
                malletRadius;
        }
        else
        {
            safeX =
                maxX -
                wallSafeMargin -
                malletRadius;
        }

        float safeY = Mathf.Clamp(
            puck.position.y + behindPuckDistance,
            centerY + malletRadius,
            maxY - malletRadius
        );

        SetTarget(
            new Vector2(
                safeX,
                safeY
            )
        );
    }

    // ============================================
    // PAREDE SUPERIOR
    // ============================================

    private void HandleTopWall()
    {
        currentState = AIState.Defense;

        /*
         * MUITO IMPORTANTE:
         *
         * Como a IA está na parte superior,
         * para se afastar da parede superior
         * ela precisa DIMINUIR o Y.
         *
         * Na sua versão anterior o retreat aumentava Y,
         * o que podia piorar o travamento.
         */

        float safeX = Mathf.MoveTowards(
            puck.position.x,
            0f,
            wallSafeMargin
        );

        float safeY =
            Mathf.Min(
                defenseY,
                puck.position.y - wallSafeMargin
            );

        safeY = Mathf.Max(
            safeY,
            centerY + malletRadius
        );

        SetTarget(
            new Vector2(
                safeX,
                safeY
            )
        );
    }

    // ============================================
    // DETECÇÃO DE TRAVAMENTO
    // ============================================

    private void CheckIfPuckIsStuck()
    {
        bool nearWall =
            IsNearSideWall() ||
            IsNearTopWall();

        float distance =
            Vector2.Distance(
                rb2d.position,
                puck.position
            );

        bool touchingPuck =
            distance < puckContactDistance;

        if (nearWall && touchingPuck)
        {
            stuckTimer += Time.fixedDeltaTime;

            if (stuckTimer >= stuckDetectionTime)
            {
                StartRetreat();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    // ============================================
    // RECUO
    // ============================================

    private void StartRetreat()
    {
        retreatTimer = retreatTime;

        currentState = AIState.Retreat;

        // Sempre recua horizontalmente em direção ao centro.
        float retreatX = Mathf.MoveTowards(
            rb2d.position.x,
            0f,
            retreatDistance
        );

        /*
         * E recua verticalmente PARA BAIXO,
         * ou seja, para longe da parede superior.
         */
        float retreatY = rb2d.position.y - retreatDistance;

        retreatY = Mathf.Clamp(
            retreatY,
            centerY + malletRadius,
            maxY - malletRadius
        );

        SetTarget(
            new Vector2(
                retreatX,
                retreatY
            )
        );
    }

    // ============================================
    // VERIFICAÇÕES DE PAREDE
    // ============================================

    private bool IsNearSideWall()
    {
        return
            puck.position.x <
            minX + wallSafeMargin
            ||
            puck.position.x >
            maxX - wallSafeMargin;
    }

    private bool IsNearTopWall()
    {
        return
            puck.position.y >
            maxY - wallSafeMargin;
    }

    private bool PuckIsLeavingSideWall()
    {
        if (puckRb2d == null)
            return false;

        // Está na parede esquerda e indo para direita.
        if (puck.position.x <
            minX + wallSafeMargin)
        {
            return puckRb2d.linearVelocity.x > 0.2f;
        }

        // Está na parede direita e indo para esquerda.
        if (puck.position.x >
            maxX - wallSafeMargin)
        {
            return puckRb2d.linearVelocity.x < -0.2f;
        }

        return true;
    }

    // ============================================
    // PREVISÃO DE REBOTE LATERAL
    // ============================================

    private float ReflectXInsideField(float x)
    {
        float left =
            minX + malletRadius;

        float right =
            maxX - malletRadius;

        float width = right - left;

        if (width <= 0f)
            return 0f;

        float repeated =
            Mathf.Repeat(
                x - left,
                width * 2f
            );

        if (repeated <= width)
        {
            return left + repeated;
        }

        return right -
               (repeated - width);
    }

    // ============================================
    // DEFINE ALVO
    // ============================================

    private void SetTarget(Vector2 targetPosition)
    {
        float targetX = Mathf.Clamp(
            targetPosition.x,
            minX + malletRadius,
            maxX - malletRadius
        );

        float targetY = Mathf.Clamp(
            targetPosition.y,
            centerY + malletRadius,
            maxY - malletRadius
        );

        currentTarget =
            new Vector2(
                targetX,
                targetY
            );
    }

    // ============================================
    // MOVIMENTO
    // ============================================

    private void MoveAI()
    {
        float currentSpeed = speed;

        // Pequeno boost somente quando está batendo.
        if (currentState == AIState.Attack)
        {
            currentSpeed *=
                attackSpeedMultiplier;
        }

        Vector2 newPosition =
            Vector2.MoveTowards(
                rb2d.position,
                currentTarget,
                currentSpeed *
                Time.fixedDeltaTime
            );

        rb2d.MovePosition(newPosition);
    }
}
