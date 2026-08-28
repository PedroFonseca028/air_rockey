using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerAI : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private Rigidbody2D puckRb2d;

    private Vector2 homePosition;
    private Vector2 currentTarget;

    // =========================================================
    // ESTADOS
    // =========================================================

    private enum AIState
    {
        Defense,
        Setup,
        Strike,
        WallSetup,
        WallStrike,
        Recover
    }

    private enum WallType
    {
        None,
        Top,
        Left,
        Right
    }

    private AIState currentState = AIState.Defense;
    private WallType currentWall = WallType.None;

    // =========================================================
    // REFERÊNCIAS
    // =========================================================

    [Header("Referências")]
    [SerializeField] private Transform puck;

    // =========================================================
    // MOVIMENTO
    // =========================================================

    [Header("Movimento")]

    [SerializeField]
    private float speed = 5f;

    [SerializeField]
    private float attackSpeedMultiplier = 1.30f;

    [SerializeField]
    private float wallAttackSpeedMultiplier = 1.20f;

    [SerializeField]
    private float reactionTime = 0.10f;

    // =========================================================
    // ATAQUE
    // =========================================================

    [Header("Ataque")]

    // A IA só ataca quando o puck está acima desse Y.
    [SerializeField]
    private float attackY = 0.65f;

    // Distância para ficar atrás do puck.
    [SerializeField]
    private float setupDistance = 0.58f;

    // Quanto atravessa o puck durante a batida.
    [SerializeField]
    private float strikeDistance = 0.95f;

    // Distância necessária para considerar
    // que chegou na posição de preparação.
    [SerializeField]
    private float setupTolerance = 0.16f;

    // Quanto tenta mandar a bola para o centro.
    [SerializeField]
    [Range(0f, 1f)]
    private float centerShotBias = 0.35f;

    // Se o puck se mover muito durante o posicionamento,
    // recalculamos a jogada.
    [SerializeField]
    private float replanDistance = 0.40f;

    // Tempo máximo tentando se posicionar.
    [SerializeField]
    private float setupTimeout = 1.1f;

    // Tempo máximo fazendo a batida.
    [SerializeField]
    private float strikeTimeout = 0.42f;

    // Tempo que continua avançando depois de tocar no puck.
    [SerializeField]
    private float followThroughTime = 0.08f;

    // =========================================================
    // DEFESA
    // =========================================================

    [Header("Defesa")]

    [SerializeField]
    private float defenseY = 3.25f;

    [SerializeField]
    [Range(0f, 1f)]
    private float defensiveTracking = 0.75f;

    [SerializeField]
    [Range(0f, 1f)]
    private float predictionStrength = 0.75f;

    [SerializeField]
    private float maxPredictionTime = 0.8f;

    // =========================================================
    // PAREDES
    // =========================================================

    [Header("Jogada na parede")]

    // Quando começamos a tratar o puck como próximo
    // da parede superior.
    [SerializeField]
    private float topWallMargin = 0.65f;

    [SerializeField]
    private float sideWallMargin = 0.55f;

    // Distância usada para preparar uma tacada de tabela.
    [SerializeField]
    private float wallSetupDistance = 0.65f;

    // Quanto atravessa a posição do puck.
    [SerializeField]
    private float wallStrikeDistance = 1.0f;

    // Inclinação horizontal da tacada
    // quando está na parede superior.
    [SerializeField]
    private float topWallHorizontalPower = 0.65f;

    // Inclinação vertical da tacada
    // quando está numa parede lateral.
    [SerializeField]
    private float sideWallDownPower = 0.70f;

    // Se o puck já está saindo da parede,
    // não precisamos tentar soltá-lo.
    [SerializeField]
    private float wallExitVelocity = 0.30f;

    // =========================================================
    // RECUPERAÇÃO APÓS BATIDA
    // =========================================================

    [Header("Recuperação após bater")]

    [SerializeField]
    private float recoverDuration = 0.28f;

    [SerializeField]
    [Range(0f, 1f)]
    private float recoverTracking = 0.30f;

    // =========================================================
    // CAMPO
    // =========================================================

    [Header("Limites")]

    [SerializeField]
    private float minX = -2.3f;

    [SerializeField]
    private float maxX = 2.3f;

    [SerializeField]
    private float centerY = 0f;

    [SerializeField]
    private float maxY = 4.5f;

    [SerializeField]
    private float malletRadius = 0.30f;

    // =========================================================
    // ESTADO INTERNO
    // =========================================================

    private float reactionTimer;
    private float stateTimer;

    private Vector2 plannedPuckPosition;
    private Vector2 plannedShotDirection;

    private Vector2 setupTarget;
    private Vector2 strikeTarget;

    private bool hitPuck;
    private float hitTime;

    private bool alternateTopWallSide;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();

        homePosition = rb2d.position;
        currentTarget = homePosition;

        rb2d.gravityScale = 0f;
        rb2d.angularVelocity = 0f;

        rb2d.constraints =
            RigidbodyConstraints2D.FreezeRotation;

        if (puck != null)
        {
            puckRb2d =
                puck.GetComponent<Rigidbody2D>();
        }
    }

    private void FixedUpdate()
    {
        if (puck == null)
            return;

        if (puckRb2d == null)
        {
            puckRb2d =
                puck.GetComponent<Rigidbody2D>();

            if (puckRb2d == null)
                return;
        }

        stateTimer +=
            Time.fixedDeltaTime;

        reactionTimer -=
            Time.fixedDeltaTime;

        // =====================================================
        // ESTADOS QUE PRECISAM SER EXECUTADOS TODO FIXEDUPDATE
        // =====================================================

        switch (currentState)
        {
            case AIState.Setup:
            case AIState.WallSetup:

                UpdateSetup();

                MoveAI();
                return;

            case AIState.Strike:
            case AIState.WallStrike:

                UpdateStrike();

                MoveAI();
                return;

            case AIState.Recover:

                UpdateRecover();

                MoveAI();
                return;
        }

        // =====================================================
        // NOVA DECISÃO
        // =====================================================

        if (reactionTimer <= 0f)
        {
            DecideAction();

            reactionTimer =
                reactionTime;
        }

        MoveAI();
    }

    // =========================================================
    // DECISÃO PRINCIPAL
    // =========================================================

    private void DecideAction()
    {
        // -----------------------------------------------------
        // PUCK NO CAMPO DO JOGADOR
        // -----------------------------------------------------

        if (puck.position.y <= attackY)
        {
            Defend();
            return;
        }

        // -----------------------------------------------------
        // PAREDE SUPERIOR
        // -----------------------------------------------------

        if (IsNearTopWall())
        {
            /*
             * Se o puck já está descendo,
             * NÃO tenta bater nele novamente.
             *
             * Ele já está indo para o jogador.
             */

            if (PuckLeavingTopWall())
            {
                Defend();
                return;
            }

            BeginWallAttack(
                WallType.Top
            );

            return;
        }

        // -----------------------------------------------------
        // PAREDE ESQUERDA
        // -----------------------------------------------------

        if (IsNearLeftWall())
        {
            if (PuckLeavingLeftWall())
            {
                BeginNormalAttack();
                return;
            }

            BeginWallAttack(
                WallType.Left
            );

            return;
        }

        // -----------------------------------------------------
        // PAREDE DIREITA
        // -----------------------------------------------------

        if (IsNearRightWall())
        {
            if (PuckLeavingRightWall())
            {
                BeginNormalAttack();
                return;
            }

            BeginWallAttack(
                WallType.Right
            );

            return;
        }

        // -----------------------------------------------------
        // ATAQUE NORMAL
        // -----------------------------------------------------

        BeginNormalAttack();
    }

    // =========================================================
    // DEFESA
    // =========================================================

    private void Defend()
    {
        currentState =
            AIState.Defense;

        currentWall =
            WallType.None;

        stateTimer = 0f;

        float targetX =
            puck.position.x;

        Vector2 velocity =
            puckRb2d.linearVelocity;

        /*
         * Se a bola está vindo para cima,
         * prevê onde cruzará a linha de defesa.
         */

        if (velocity.y > 0.1f)
        {
            float distanceY =
                defenseY -
                puck.position.y;

            float time =
                distanceY /
                velocity.y;

            time =
                Mathf.Clamp(
                    time,
                    0f,
                    maxPredictionTime
                );

            float predictedX =
                puck.position.x +
                velocity.x * time;

            predictedX =
                ReflectXInsideField(
                    predictedX
                );

            targetX =
                Mathf.Lerp(
                    puck.position.x,
                    predictedX,
                    predictionStrength
                );
        }
        else
        {
            /*
             * Se a bola está indo embora,
             * acompanha parcialmente.
             */

            targetX =
                Mathf.Lerp(
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

    // =========================================================
    // COMEÇA ATAQUE NORMAL
    // =========================================================

    private void BeginNormalAttack()
    {
        currentWall =
            WallType.None;

        plannedPuckPosition =
            puck.position;

        /*
         * Queremos mandar o puck:
         *
         * - para BAIXO
         * - levemente para o centro
         */

        float horizontal =
            -plannedPuckPosition.x *
            centerShotBias;

        plannedShotDirection =
            new Vector2(
                horizontal,
                -1f
            ).normalized;

        Vector2 rawSetup =
            plannedPuckPosition -
            plannedShotDirection *
            setupDistance;

        /*
         * Se precisar ficar acima do limite do campo
         * para conseguir essa tacada, significa que
         * a bola está próxima demais da parede superior.
         *
         * Nesse caso fazemos tabela.
         */

        float maximumY =
            maxY -
            malletRadius;

        if (rawSetup.y > maximumY)
        {
            BeginWallAttack(
                WallType.Top
            );

            return;
        }

        setupTarget =
            ClampPosition(
                rawSetup
            );

        strikeTarget =
            ClampPosition(
                plannedPuckPosition +
                plannedShotDirection *
                strikeDistance
            );

        currentState =
            AIState.Setup;

        stateTimer = 0f;

        hitPuck = false;

        SetTarget(
            setupTarget
        );
    }

    // =========================================================
    // COMEÇA JOGADA NA PAREDE
    // =========================================================

    private void BeginWallAttack(
        WallType wall
    )
    {
        currentWall = wall;

        plannedPuckPosition =
            puck.position;

        // -----------------------------------------------------
        // PAREDE SUPERIOR
        // -----------------------------------------------------

        if (wall == WallType.Top)
        {
            /*
             * Se estiver na direita:
             *
             * IA vem de baixo/esquerda
             * e manda o puck para cima/direita.
             *
             * A parede devolve ele para baixo.
             *
             * Se estiver na esquerda:
             * comportamento inverso.
             */

            float horizontalDirection;

            if (puck.position.x > 0.20f)
            {
                horizontalDirection =
                    topWallHorizontalPower;
            }
            else if (puck.position.x < -0.20f)
            {
                horizontalDirection =
                    -topWallHorizontalPower;
            }
            else
            {
                horizontalDirection =
                    alternateTopWallSide
                    ? topWallHorizontalPower
                    : -topWallHorizontalPower;

                alternateTopWallSide =
                    !alternateTopWallSide;
            }

            plannedShotDirection =
                new Vector2(
                    horizontalDirection,
                    1f
                ).normalized;
        }

        // -----------------------------------------------------
        // PAREDE ESQUERDA
        // -----------------------------------------------------

        else if (wall == WallType.Left)
        {
            /*
             * Manda o puck contra a parede esquerda
             * e para baixo.
             *
             * A parede devolve direita + baixo.
             */

            plannedShotDirection =
                new Vector2(
                    -1f,
                    -sideWallDownPower
                ).normalized;
        }

        // -----------------------------------------------------
        // PAREDE DIREITA
        // -----------------------------------------------------

        else
        {
            plannedShotDirection =
                new Vector2(
                    1f,
                    -sideWallDownPower
                ).normalized;
        }

        setupTarget =
            ClampPosition(
                plannedPuckPosition -
                plannedShotDirection *
                wallSetupDistance
            );

        strikeTarget =
            ClampPosition(
                plannedPuckPosition +
                plannedShotDirection *
                wallStrikeDistance
            );

        currentState =
            AIState.WallSetup;

        stateTimer = 0f;

        hitPuck = false;

        SetTarget(
            setupTarget
        );
    }

    // =========================================================
    // POSICIONAMENTO
    // =========================================================

    private void UpdateSetup()
    {
        // Bola foi para o outro campo.
        if (puck.position.y <= attackY)
        {
            Defend();
            return;
        }

        // -----------------------------------------------------
        // CANCELA SE A BOLA SAIU DA PAREDE
        // -----------------------------------------------------

        if (currentState == AIState.WallSetup)
        {
            if (WallSituationResolved())
            {
                currentState =
                    AIState.Defense;

                reactionTimer = 0f;
                stateTimer = 0f;

                return;
            }
        }

        // -----------------------------------------------------
        // PUCK MUDOU MUITO DE POSIÇÃO
        // -----------------------------------------------------

        float puckMovement =
            Vector2.Distance(
                puck.position,
                plannedPuckPosition
            );

        if (
            puckMovement >
            replanDistance
        )
        {
            currentState =
                AIState.Defense;

            stateTimer = 0f;
            reactionTimer = 0f;

            return;
        }

        // -----------------------------------------------------
        // TIMEOUT
        // -----------------------------------------------------

        if (stateTimer >
            setupTimeout)
        {
            currentState =
                AIState.Defense;

            stateTimer = 0f;
            reactionTimer = 0f;

            return;
        }

        // -----------------------------------------------------
        // CONTINUA INDO PARA POSIÇÃO
        // -----------------------------------------------------

        SetTarget(
            setupTarget
        );

        float distance =
            Vector2.Distance(
                rb2d.position,
                setupTarget
            );

        if (distance >
            setupTolerance)
        {
            return;
        }

        // =====================================================
        // CHEGOU NA POSIÇÃO
        // =====================================================

        /*
         * Recalcula somente o ponto final da batida
         * usando a posição ATUAL do puck.
         *
         * A direção permanece a mesma.
         */

        float throughDistance =
            currentState ==
            AIState.WallSetup
            ? wallStrikeDistance
            : strikeDistance;

        strikeTarget =
            ClampPosition(
                (Vector2)puck.position +
                plannedShotDirection *
                throughDistance
            );

        if (currentState ==
            AIState.WallSetup)
        {
            currentState =
                AIState.WallStrike;
        }
        else
        {
            currentState =
                AIState.Strike;
        }

        stateTimer = 0f;

        hitPuck = false;

        SetTarget(
            strikeTarget
        );
    }

    // =========================================================
    // BATIDA
    // =========================================================

    private void UpdateStrike()
    {
        /*
         * Durante a batida não ficamos mudando
         * o alvo constantemente.
         *
         * Isso é importante.
         *
         * A IA realmente COMETE a tacada.
         */

        SetTarget(
            strikeTarget
        );

        // -----------------------------------------------------
        // JÁ TOCOU NO PUCK
        // -----------------------------------------------------

        if (hitPuck)
        {
            if (
                stateTimer -
                hitTime >=
                followThroughTime
            )
            {
                BeginRecover();
                return;
            }
        }

        // -----------------------------------------------------
        // TIMEOUT
        // -----------------------------------------------------

        if (stateTimer >=
            strikeTimeout)
        {
            BeginRecover();
        }
    }

    // =========================================================
    // COLISÃO COM PUCK
    // =========================================================

    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        if (puckRb2d == null)
            return;

        if (
            collision.rigidbody !=
            puckRb2d
        )
        {
            return;
        }

        /*
         * Não recua imediatamente.
         *
         * Apenas registra o impacto.
         *
         * UpdateStrike continua avançando durante
         * followThroughTime.
         */

        if (
            currentState == AIState.Strike ||
            currentState == AIState.WallStrike
        )
        {
            if (!hitPuck)
            {
                hitPuck = true;
                hitTime = stateTimer;
            }
        }
    }

    // =========================================================
    // RECUPERA APÓS TACADA
    // =========================================================

    private void BeginRecover()
    {
        currentState =
            AIState.Recover;

        currentWall =
            WallType.None;

        stateTimer = 0f;

        /*
         * Volta para a região defensiva,
         * mas acompanha um pouco o X atual da bola.
         */

        float targetX =
            Mathf.Lerp(
                homePosition.x,
                puck.position.x,
                recoverTracking
            );

        SetTarget(
            new Vector2(
                targetX,
                defenseY
            )
        );
    }

    private void UpdateRecover()
    {
        float targetX =
            Mathf.Lerp(
                homePosition.x,
                puck.position.x,
                recoverTracking
            );

        SetTarget(
            new Vector2(
                targetX,
                defenseY
            )
        );

        if (
            stateTimer >=
            recoverDuration
        )
        {
            currentState =
                AIState.Defense;

            stateTimer = 0f;
            reactionTimer = 0f;
        }
    }

    // =========================================================
    // SITUAÇÃO DA PAREDE RESOLVIDA?
    // =========================================================

    private bool WallSituationResolved()
    {
        Vector2 velocity =
            puckRb2d.linearVelocity;

        switch (currentWall)
        {
            case WallType.Top:

                /*
                 * Já está descendo.
                 */

                return
                    velocity.y <
                    -wallExitVelocity;

            case WallType.Left:

                /*
                 * Já está indo para direita.
                 */

                return
                    velocity.x >
                    wallExitVelocity;

            case WallType.Right:

                /*
                 * Já está indo para esquerda.
                 */

                return
                    velocity.x <
                    -wallExitVelocity;
        }

        return false;
    }

    // =========================================================
    // PAREDES
    // =========================================================

    private bool IsNearTopWall()
    {
        return
            puck.position.y >
            maxY -
            topWallMargin;
    }

    private bool IsNearLeftWall()
    {
        return
            puck.position.x <
            minX +
            sideWallMargin;
    }

    private bool IsNearRightWall()
    {
        return
            puck.position.x >
            maxX -
            sideWallMargin;
    }

    private bool PuckLeavingTopWall()
    {
        return
            puckRb2d.linearVelocity.y <
            -wallExitVelocity;
    }

    private bool PuckLeavingLeftWall()
    {
        return
            puckRb2d.linearVelocity.x >
            wallExitVelocity;
    }

    private bool PuckLeavingRightWall()
    {
        return
            puckRb2d.linearVelocity.x <
            -wallExitVelocity;
    }

    // =========================================================
    // PREVISÃO DE REBOTE
    // =========================================================

    private float ReflectXInsideField(
        float x
    )
    {
        float left =
            minX +
            malletRadius;

        float right =
            maxX -
            malletRadius;

        float width =
            right -
            left;

        if (width <= 0f)
            return 0f;

        float repeated =
            Mathf.Repeat(
                x - left,
                width * 2f
            );

        if (repeated <= width)
        {
            return
                left +
                repeated;
        }

        return
            right -
            (repeated - width);
    }

    // =========================================================
    // LIMITES DA IA
    // =========================================================

    private Vector2 ClampPosition(
        Vector2 position
    )
    {
        float x =
            Mathf.Clamp(
                position.x,
                minX +
                malletRadius,
                maxX -
                malletRadius
            );

        /*
         * Nunca deixa a IA cruzar o meio.
         */

        float y =
            Mathf.Clamp(
                position.y,
                centerY +
                malletRadius,
                maxY -
                malletRadius
            );

        return
            new Vector2(
                x,
                y
            );
    }

    private void SetTarget(
        Vector2 target
    )
    {
        currentTarget =
            ClampPosition(
                target
            );
    }

    // =========================================================
    // MOVIMENTO
    // =========================================================

    private void MoveAI()
    {
        float currentSpeed =
            speed;

        if (
            currentState ==
            AIState.Strike
        )
        {
            currentSpeed *=
                attackSpeedMultiplier;
        }

        if (
            currentState ==
            AIState.WallStrike ||
            currentState ==
            AIState.WallSetup
        )
        {
            currentSpeed *=
                wallAttackSpeedMultiplier;
        }

        Vector2 newPosition =
            Vector2.MoveTowards(
                rb2d.position,
                currentTarget,
                currentSpeed *
                Time.fixedDeltaTime
            );

        rb2d.MovePosition(
            newPosition
        );
    }
}
