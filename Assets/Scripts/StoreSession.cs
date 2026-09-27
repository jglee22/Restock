using UnityEngine;

public enum StorePhase
{
    Preparation,
    Open,
    Closing,
    Result
}

// 하루의 상태, 게임 시각, 영업 속도를 한곳에서 관리한다.
// Open일 때만 시각이 흐르고, 그 속도는 Time.timeScale을 따른다.
public class StoreSession : MonoBehaviour
{
    public const float PausedTimeScale = 0f;
    public const float NormalTimeScale = 1f;
    public const float DoubleTimeScale = 2f;
    public const float TripleTimeScale = 3f;

    const int MinutesPerHour = 60;
    const int MaxHour = 23;
    const int MaxMinute = 59;
    const float MinimumRealSecondsPerGameMinute = 0.01f;

    [SerializeField] int openingHour = 8;
    [SerializeField] int openingMinute = 0;
    [SerializeField] int closingHour = 22;
    [SerializeField] int closingMinute = 0;
    [SerializeField] int skipHour = 21;
    [SerializeField] int skipMinute = 50;
    [SerializeField] float realSecondsPerGameMinute = 1f;

    StorePhase phase = StorePhase.Preparation;
    int day = 1;
    float currentGameMinutes;
    float playingTimeScale = NormalTimeScale;
    bool isPaused;

    public event System.Action<int> DayStarted;

    public StorePhase Phase => phase;
    public int Day => day;
    public bool IsPaused => isPaused;
    public float PlayingTimeScale => playingTimeScale;

    public string DayLabel => $"Day {day}";

    public string SkipTimeLabel => $"{skipHour:00}:{skipMinute:00}";

    public string TimeLabel
    {
        get
        {
            int totalMinutes = Mathf.FloorToInt(currentGameMinutes);
            int hour = totalMinutes / MinutesPerHour;
            int minute = totalMinutes % MinutesPerHour;
            return $"{hour:00}:{minute:00}";
        }
    }

    void Awake()
    {
        day = 1;
        phase = StorePhase.Preparation;
        isPaused = false;
        playingTimeScale = NormalTimeScale;
        currentGameMinutes = OpeningTotalMinutes;
        Time.timeScale = NormalTimeScale;

        if (!HasValidSchedule)
        {
            Debug.LogError(
                $"StoreSession: 폐점 시각({closingHour:00}:{closingMinute:00})이 개점 시각({openingHour:00}:{openingMinute:00})보다 늦어야 합니다.",
                this);
        }
    }

    void OnDisable()
    {
        // Play Mode를 벗어나거나 오브젝트가 꺼질 때 다른 씬에 0/2/3 배속이 남지 않게 한다.
        if (Application.isPlaying)
        {
            Time.timeScale = NormalTimeScale;
        }
    }

    void Update()
    {
        if (phase != StorePhase.Open || !HasValidSchedule)
        {
            return;
        }

        currentGameMinutes += Time.deltaTime / realSecondsPerGameMinute;
        if (currentGameMinutes < ClosingTotalMinutes)
        {
            return;
        }

        currentGameMinutes = ClosingTotalMinutes;
        EnterClosing();
    }

    public void StartBusiness()
    {
        if (phase != StorePhase.Preparation)
        {
            Debug.LogWarning($"StoreSession: Preparation에서만 영업을 시작할 수 있습니다. 현재 상태: {phase}", this);
            return;
        }

        if (!HasValidSchedule)
        {
            Debug.LogError("StoreSession: 개점/폐점 시각이 올바르지 않아 영업을 시작하지 않습니다.", this);
            return;
        }

        phase = StorePhase.Open;
        isPaused = false;
        Time.timeScale = playingTimeScale;
    }

    public void ShowResult()
    {
        if (phase != StorePhase.Closing)
        {
            Debug.LogWarning($"StoreSession: Closing에서만 결산으로 넘어갈 수 있습니다. 현재 상태: {phase}", this);
            return;
        }

        phase = StorePhase.Result;
        Time.timeScale = NormalTimeScale;
    }

    public void AdvanceToNextDay()
    {
        if (phase != StorePhase.Result)
        {
            Debug.LogWarning($"StoreSession: Result에서만 다음 날로 넘어갈 수 있습니다. 현재 상태: {phase}", this);
            return;
        }

        day += 1;
        currentGameMinutes = OpeningTotalMinutes;
        phase = StorePhase.Preparation;
        isPaused = false;
        Time.timeScale = NormalTimeScale;
        DayStarted?.Invoke(day);
    }

    public void TogglePause()
    {
        if (phase != StorePhase.Open)
        {
            return;
        }

        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = playingTimeScale;
            return;
        }

        isPaused = true;
        Time.timeScale = PausedTimeScale;
    }

    public void JumpToSkipTime()
    {
        if (phase != StorePhase.Open)
        {
            Debug.LogWarning($"StoreSession: Open에서만 시각을 건너뛸 수 있습니다. 현재 상태: {phase}", this);
            return;
        }

        if (!HasValidSchedule)
        {
            Debug.LogError("StoreSession: 개점/폐점 시각이 올바르지 않아 시각을 건너뛰지 않습니다.", this);
            return;
        }

        int targetMinutes = skipHour * MinutesPerHour + skipMinute;
        if (targetMinutes <= OpeningTotalMinutes)
        {
            currentGameMinutes = OpeningTotalMinutes;
            return;
        }

        if (targetMinutes >= ClosingTotalMinutes)
        {
            currentGameMinutes = ClosingTotalMinutes;
            EnterClosing();
            return;
        }

        currentGameMinutes = targetMinutes;
    }

    public void SetSpeed(float timeScale)
    {
        if (phase != StorePhase.Open)
        {
            return;
        }

        if (!IsSupportedPlayingSpeed(timeScale))
        {
            Debug.LogWarning($"StoreSession: 지원하지 않는 속도입니다. {timeScale}", this);
            return;
        }

        playingTimeScale = timeScale;
        isPaused = false;
        Time.timeScale = timeScale;
    }

    void EnterClosing()
    {
        if (phase != StorePhase.Open)
        {
            return;
        }

        phase = StorePhase.Closing;
        isPaused = false;
        Time.timeScale = NormalTimeScale;
    }

    bool HasValidSchedule => ClosingTotalMinutes > OpeningTotalMinutes;

    int OpeningTotalMinutes => openingHour * MinutesPerHour + openingMinute;

    int ClosingTotalMinutes => closingHour * MinutesPerHour + closingMinute;

    static bool IsSupportedPlayingSpeed(float timeScale)
    {
        return Mathf.Approximately(timeScale, NormalTimeScale)
            || Mathf.Approximately(timeScale, DoubleTimeScale)
            || Mathf.Approximately(timeScale, TripleTimeScale);
    }

    void OnValidate()
    {
        openingHour = Mathf.Clamp(openingHour, 0, MaxHour);
        openingMinute = Mathf.Clamp(openingMinute, 0, MaxMinute);
        closingHour = Mathf.Clamp(closingHour, 0, MaxHour);
        closingMinute = Mathf.Clamp(closingMinute, 0, MaxMinute);
        skipHour = Mathf.Clamp(skipHour, 0, MaxHour);
        skipMinute = Mathf.Clamp(skipMinute, 0, MaxMinute);
        realSecondsPerGameMinute = Mathf.Max(MinimumRealSecondsPerGameMinute, realSecondsPerGameMinute);
    }
}
