using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    private int playerScore = 0;
    private int aiScore = 0;

    private TMP_Text scoreText;
    private TMP_Text popupText;

    private GameObject popupPanel;

    private Coroutine popupCoroutine;

    [Header("Popup")]
    [SerializeField] private float popupDuration = 1.2f;

    void Awake()
    {
        CreateUI();
        UpdateScore();
    }

    // ============================
    // PONTO DO JOGADOR
    // ============================

    public void PlayerScored()
    {
        playerScore++;

        UpdateScore();

        ShowPopup("GOL DO JOGADOR!");
    }

    // ============================
    // PONTO DA IA
    // ============================

    public void AIScored()
    {
        aiScore++;

        UpdateScore();

        ShowPopup("GOL DA IA!");
    }

    // ============================
    // ATUALIZA O PLACAR
    // ============================

    private void UpdateScore()
    {
        if (scoreText == null)
            return;

        scoreText.text =
            $"IA   {aiScore}   x   {playerScore}   VOCÊ";
    }

    // ============================
    // CRIA TODA A INTERFACE
    // ============================

    private void CreateUI()
    {
        // Canvas
        GameObject canvasObject = new GameObject(
            "GameCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 100;


        // Canvas Scaler
        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1000, 2000);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;


        // ============================
        // PAINEL DO PLACAR
        // ============================

        GameObject scorePanel =
            new GameObject(
                "ScorePanel",
                typeof(RectTransform),
                typeof(Image)
            );

        scorePanel.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform scorePanelRect =
            scorePanel.GetComponent<RectTransform>();

        scorePanelRect.anchorMin =
            new Vector2(0.5f, 1f);

        scorePanelRect.anchorMax =
            new Vector2(0.5f, 1f);

        scorePanelRect.pivot =
            new Vector2(0.5f, 1f);

        scorePanelRect.sizeDelta =
            new Vector2(650f, 120f);

        scorePanelRect.anchoredPosition =
            new Vector2(0f, -40f);


        Image scoreBackground =
            scorePanel.GetComponent<Image>();

        scoreBackground.color =
            new Color(0f, 0f, 0f, 0.65f);

        scoreBackground.raycastTarget = false;


        // Texto do placar
        GameObject scoreTextObject =
            new GameObject(
                "ScoreText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );

        scoreTextObject.transform.SetParent(
            scorePanel.transform,
            false
        );

        scoreText =
            scoreTextObject.GetComponent<TextMeshProUGUI>();

        ConfigureText(scoreText);

        scoreText.fontSize = 52f;
        scoreText.fontStyle = FontStyles.Bold;

        scoreText.alignment =
            TextAlignmentOptions.Center;

        RectTransform scoreRect =
            scoreTextObject.GetComponent<RectTransform>();

        Stretch(scoreRect);


        // ============================
        // POPUP DE GOL
        // ============================

        popupPanel =
            new GameObject(
                "GoalPopup",
                typeof(RectTransform),
                typeof(Image)
            );

        popupPanel.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform popupRect =
            popupPanel.GetComponent<RectTransform>();

        popupRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        popupRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        popupRect.pivot =
            new Vector2(0.5f, 0.5f);

        popupRect.sizeDelta =
            new Vector2(800f, 280f);

        popupRect.anchoredPosition =
            Vector2.zero;


        Image popupBackground =
            popupPanel.GetComponent<Image>();

        popupBackground.color =
            new Color(0f, 0f, 0f, 0.80f);

        popupBackground.raycastTarget = false;


        // Texto popup
        GameObject popupTextObject =
            new GameObject(
                "PopupText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );

        popupTextObject.transform.SetParent(
            popupPanel.transform,
            false
        );

        popupText =
            popupTextObject.GetComponent<TextMeshProUGUI>();

        ConfigureText(popupText);

        popupText.fontSize = 70f;
        popupText.fontStyle = FontStyles.Bold;

        popupText.alignment =
            TextAlignmentOptions.Center;

        RectTransform popupTextRect =
            popupTextObject.GetComponent<RectTransform>();

        Stretch(popupTextRect);


        // Começa escondido
        popupPanel.SetActive(false);
    }

    // ============================
    // CONFIGURA TEXTO TMP
    // ============================

    private void ConfigureText(TMP_Text text)
    {
        TMP_FontAsset font =
            TMP_Settings.defaultFontAsset;

        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>(
                "Fonts & Materials/LiberationSans SDF"
            );
        }

        text.font = font;

        text.color = Color.white;

        text.enableWordWrapping = false;

        text.raycastTarget = false;

        text.outlineWidth = 0.15f;

        text.outlineColor = Color.black;
    }

    // ============================
    // FAZ OBJETO OCUPAR O PAI
    // ============================

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ============================
    // MOSTRA POPUP
    // ============================

    private void ShowPopup(string message)
    {
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        popupCoroutine =
            StartCoroutine(
                ShowPopupCoroutine(message)
            );
    }

    private IEnumerator ShowPopupCoroutine(
        string message
    )
    {
        popupText.text =
            message +
            "\n\n" +
            $"IA {aiScore}  x  {playerScore} VOCÊ";

        popupPanel.SetActive(true);

        yield return new WaitForSeconds(
            popupDuration
        );

        popupPanel.SetActive(false);

        popupCoroutine = null;
    }

    // ============================
    // RESET OPCIONAL
    // ============================

    public void ResetScore()
    {
        playerScore = 0;
        aiScore = 0;

        UpdateScore();
    }
}
