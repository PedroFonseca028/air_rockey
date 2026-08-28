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

    [SerializeField] private float reactionTime = 0.12f;

    [SerializeField] private float aimError = 0.10f;

    [Header("Ataque")]

    [SerializeField] private float attackY = 0.7f;

    [SerializeField] private float behindPuckDistance = 0.55f;

 
    [SerializeField] private float hitThroughDistance = 0.75f;

  
    [SerializeField] private float shotCenterBias = 0.25f;

    [SerializeField] private float positioningTolerance = 0.20f;

    [Header("Defesa")]
    [SerializeField] private float defenseY = 3.3f;

    [SerializeField]
    [Range(0f, 1f)]
    private float defensiveTracking = 0.75f;


    [SerializeField]
    [Range(0f, 1f)]
    private float predictionStrength = 0.75f;

    [SerializeField] private float maxPredictionTime = 0.8f;

    [Header("Anti-Travamento")]

    [SerializeField] private float wallSafeMargin = 0.55f;

    [SerializeField] private float puckContactDistance = 0.65f;

    
    [SerializeField] private float stuckDetectionTime = 0.20f;

    [SerializeField] private float retreatTime = 0.40f;

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

        if (puckRb2d == null)
        {
            puckRb2d = puck.GetComponent<Rigidbody2D>();
        }

        CheckIfPuckIsStuck();

        if (retreatTimer > 0f)
        {
            retreatTimer -= Time.fixedDeltaTime;

            currentState = AIState.Retreat;

            MoveAI();
            return;
        }

        reactionTimer -= Time.fixedDeltaTime;

        if (reactionTimer <= 0f)
        {
            DecideAction();

            reactionTimer = reactionTime;
        }

        MoveAI();
    }


    private void DecideAction()
    {
        if (puck.position.y <= attackY)
        {
            Defend();
            return;
        }

        if (IsNearTopWall())
        {
            HandleTopWall();
            return;
        }

        if (IsNearSideWall() && !PuckIsLeavingSideWall())
        {
            HandleSideWall();
            return;
        }

        AttackPuck();
    }

    private void Defend()
    {
        currentState = AIState.Defense;

        float targetX = puck.position.x;

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

    private void AttackPuck()
    {

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

        currentState = AIState.Attack;

        float centerDirection = 0f;

        if (Mathf.Abs(puck.position.x) > 0.15f)
        {
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

    private void HandleSideWall()
    {
        currentState = AIState.Positioning;

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

    private void HandleTopWall()
    {
        currentState = AIState.Defense;

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

    private void StartRetreat()
    {
        retreatTimer = retreatTime;

        currentState = AIState.Retreat;

        float retreatX = Mathf.MoveTowards(
            rb2d.position.x,
            0f,
            retreatDistance
        );

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

        if (puck.position.x <
            minX + wallSafeMargin)
        {
            return puckRb2d.linearVelocity.x > 0.2f;
        }

        if (puck.position.x >
            maxX - wallSafeMargin)
        {
            return puckRb2d.linearVelocity.x < -0.2f;
        }

        return true;
    }

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


    private void MoveAI()
    {
        float currentSpeed = speed;
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
