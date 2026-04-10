using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public float initialGameSpeed = 5f;
    public float gameSpeedIncrease = 0.1f;
    public float gameSpeed { get; private set; }

    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI hiscoreText;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button retryButton;

    private Player player;
    private Spawner spawner;
    private AnimatedSprite playerAnim;

    private float score;
    public float Score => score;

    private float persistentHiscore;
    private float sessionHiscore;
    [SerializeField] private TextMeshProUGUI sessionHiscoreText;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private AudioClip dieSfx;
    [SerializeField] private AudioClip pointSfx;
    [SerializeField] private int pointSfxEvery = 500;
    private int nextPointSfxAt;

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Start()
    {
        EnsureSceneReferences();
        EnsureAudioWired();

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(NewGame);
        }

        persistentHiscore = PlayerPrefs.GetFloat("hiscore", 0f);
        sessionHiscore = 0f;

        NewGame();
    }

    public void NewGame()
    {
        EnsureSceneReferences();
        EnsureAudioWired();

        Obstacle[] obstacles = FindObjectsOfType<Obstacle>(true);

        foreach (var obstacle in obstacles) {
            if (obstacle != null) Destroy(obstacle.gameObject);
        }

        score = 0f;
        // Session hiscore persists across retries but resets on scene reload (Start()).
        nextPointSfxAt = pointSfxEvery;
        gameSpeed = initialGameSpeed;
        enabled = true;

        // Make sure UI is visible even if someone edited the scene.
        ForceCanvasVisible();
        if (scoreText != null) { scoreText.enabled = true; scoreText.gameObject.SetActive(true); }
        if (hiscoreText != null) { hiscoreText.enabled = true; hiscoreText.gameObject.SetActive(true); }
        if (sessionHiscoreText != null) { sessionHiscoreText.enabled = true; sessionHiscoreText.gameObject.SetActive(true); }

        if (player != null) {
            player.gameObject.SetActive(true);
            player.enabled = true;
        }
        if (playerAnim != null) playerAnim.enabled = true;

        if (spawner != null) spawner.gameObject.SetActive(true);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (retryButton != null) retryButton.gameObject.SetActive(false);

        UpdateHiscore();
        UpdateSessionHiscore();
    }

    public void GameOver()
    {
        gameSpeed = 0f;
        enabled = false;

        PlayDieSfx();

        // Keep dino visible; stop input + animation.
        if (player != null) player.enabled = false;
        if (playerAnim != null) playerAnim.enabled = false;

        if (spawner != null) spawner.gameObject.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(true);
        if (retryButton != null) retryButton.gameObject.SetActive(true);

        UpdateHiscore();
    }

    private void Update()
    {
        gameSpeed += gameSpeedIncrease * Time.deltaTime;
        score += gameSpeed * Time.deltaTime;

        if (scoreText != null) {
            scoreText.text = Mathf.FloorToInt(score).ToString("D5");
        }

        if (score > sessionHiscore)
        {
            sessionHiscore = score;
            UpdateSessionHiscore();
        }

        if (score > persistentHiscore)
        {
            persistentHiscore = score;
            PlayerPrefs.SetFloat("hiscore", persistentHiscore);
            UpdateHiscore();
        }

        // Night mode after 500 points (and stay on).
        bool isNight = score >= 500f;
        if (Camera.main != null) {
            Color bg = isNight ? Color.black : Color.white;
            Camera.main.backgroundColor = Color.Lerp(Camera.main.backgroundColor, bg, Time.deltaTime * 2f);
        }
        if (scoreText != null) scoreText.color = Color.Lerp(scoreText.color, isNight ? Color.white : Color.black, Time.deltaTime * 2f);
        if (hiscoreText != null) hiscoreText.color = Color.Lerp(hiscoreText.color, isNight ? Color.white : Color.gray, Time.deltaTime * 2f);
        if (sessionHiscoreText != null) sessionHiscoreText.color = Color.Lerp(sessionHiscoreText.color, isNight ? Color.white : Color.gray, Time.deltaTime * 2f);
        if (gameOverText != null) gameOverText.color = Color.Lerp(gameOverText.color, isNight ? Color.white : Color.black, Time.deltaTime * 2f);

        int scoreInt = Mathf.FloorToInt(score);
        if (pointSfx != null && pointSfxEvery > 0 && scoreInt >= nextPointSfxAt) {
            nextPointSfxAt = ((scoreInt / pointSfxEvery) + 1) * pointSfxEvery;
            PlayPointSfx();
        }
    }

    private void UpdateHiscore()
    {
        if (hiscoreText != null) {
            hiscoreText.text = Mathf.FloorToInt(persistentHiscore).ToString("D5");
        }
    }

    private void UpdateSessionHiscore()
    {
        if (sessionHiscoreText != null)
        {
            // Ephemeral hiscore: resets on scene reload, persists across retries.
            sessionHiscoreText.text = $"CURR {Mathf.FloorToInt(sessionHiscore):D5}";
        }
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    public void PlayJumpSfx()
    {
        if (sfxSource != null && jumpSfx != null) sfxSource.PlayOneShot(jumpSfx);
    }

    private void PlayDieSfx()
    {
        if (sfxSource != null && dieSfx != null) sfxSource.PlayOneShot(dieSfx);
    }

    private void PlayPointSfx()
    {
        if (sfxSource != null && pointSfx != null) sfxSource.PlayOneShot(pointSfx);
    }

    private void EnsureSceneReferences()
    {
        if (player == null) player = FindObjectOfType<Player>(true);
        if (spawner == null) spawner = FindObjectOfType<Spawner>(true);
        if (playerAnim == null && player != null) playerAnim = player.GetComponent<AnimatedSprite>();

        // If serialized refs got lost, recover from known object names in Game scene.
        if (scoreText == null) {
            var go = GameObject.Find("Score");
            if (go != null) scoreText = go.GetComponent<TextMeshProUGUI>();
        }
        if (hiscoreText == null) {
            var go = GameObject.Find("Hiscore");
            if (go != null) hiscoreText = go.GetComponent<TextMeshProUGUI>();
        }
        if (sessionHiscoreText == null) {
            var go = GameObject.Find("SessionHiscore");
            if (go != null) sessionHiscoreText = go.GetComponent<TextMeshProUGUI>();
        }
        if (gameOverText == null) {
            var go = GameObject.Find("Game Over");
            if (go != null) gameOverText = go.GetComponent<TextMeshProUGUI>();
        }
        if (retryButton == null) {
            var go = GameObject.Find("Retry");
            if (go != null) retryButton = go.GetComponent<Button>();
        }
    }

    private void ForceCanvasVisible()
    {
        var canvas = FindObjectOfType<Canvas>(true);
        if (canvas == null) return;
        var rt = canvas.GetComponent<RectTransform>();
        if (rt != null && rt.localScale == Vector3.zero) {
            rt.localScale = Vector3.one;
        }
        canvas.enabled = true;
        canvas.gameObject.SetActive(true);

        EnsureSessionHiscoreUI(canvas);
    }

    private void EnsureSessionHiscoreUI(Canvas canvas)
    {
        if (sessionHiscoreText != null) return;

        // Create a TMP text under the same Canvas, positioned below the persistent hiscore.
        var go = new GameObject("SessionHiscore");
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);

        sessionHiscoreText = go.AddComponent<TextMeshProUGUI>();
        sessionHiscoreText.enableWordWrapping = false;
        sessionHiscoreText.overflowMode = TextOverflowModes.Overflow;
        sessionHiscoreText.alignment = TextAlignmentOptions.TopRight;
        sessionHiscoreText.fontSize = 28;
        sessionHiscoreText.color = Color.gray;

        // Try to match font from existing hiscore
        if (hiscoreText != null)
        {
            sessionHiscoreText.font = hiscoreText.font;
            sessionHiscoreText.fontSize = hiscoreText.fontSize;
            sessionHiscoreText.fontStyle = hiscoreText.fontStyle;
        }

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(192, 50);

        // Place directly under the persistent hiscore line.
        rt.anchoredPosition = new Vector2(-256, -64);
    }

    private void EnsureAudioWired()
    {
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

#if UNITY_EDITOR
        if (jumpSfx == null) jumpSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/jump.wav");
        if (dieSfx == null) dieSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/die.wav");
        if (pointSfx == null) pointSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/point.wav");
#endif
    }
}
