using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMouse : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private Camera mainCamera;

    [Header("Movimento")]
    [SerializeField] private float speed = 15f;

    [Header("Limites")]
    [SerializeField] private float minX = -2.2f;
    [SerializeField] private float maxX = 2.2f;

    [SerializeField] private float minY = -4.3f;
    [SerializeField] private float maxY = -0.3f;

    private Vector2 targetPosition;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;

        targetPosition = rb2d.position;
    }



    void Update()
    {
        if (mainCamera == null)
            return;

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(Input.mousePosition);

        float x = Mathf.Clamp(
            mouseWorldPosition.x,
            minX,
            maxX
        );

        float y = Mathf.Clamp(
            mouseWorldPosition.y,
            minY,
            maxY
        );

        targetPosition = new Vector2(x, y);
    }

    void FixedUpdate()
    {
        Vector2 newPosition = Vector2.MoveTowards(
            rb2d.position,
            targetPosition,
            speed * Time.fixedDeltaTime
        );

        rb2d.MovePosition(newPosition);
    }
}


