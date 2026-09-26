using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기존 Day/Time TMP와 Phase 1 버튼을 StoreSession 상태에 맞춘다.
// MoneyText는 이 스크립트가 갱신하지 않는다.
public class StoreHud : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text phaseText;
    [SerializeField] TMP_Text resultText;

    [SerializeField] Button startBusinessButton;
    [SerializeField] Button showResultButton;
    [SerializeField] Button nextDayButton;
    [SerializeField] Button pauseButton;
    [SerializeField] Button speed1Button;
    [SerializeField] Button speed2Button;
    [SerializeField] Button speed3Button;
    [SerializeField] Button skipTimeButton;

    [SerializeField] GameObject speedControls;
    [SerializeField] GameObject resultPanel;

    StorePhase displayedPhase;
    TMP_Text skipTimeLabel;

    void Awake()
    {
        if (session == null)
        {
            Debug.LogError("StoreHud: StoreSession이 연결되지 않았습니다.", this);
        }

        if (skipTimeButton != null)
        {
            skipTimeLabel = skipTimeButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    void OnEnable()
    {
        Bind(startBusinessButton, OnStartBusiness);
        Bind(showResultButton, OnShowResult);
        Bind(nextDayButton, OnNextDay);
        Bind(pauseButton, OnTogglePause);
        Bind(speed1Button, OnSpeed1);
        Bind(speed2Button, OnSpeed2);
        Bind(speed3Button, OnSpeed3);
        Bind(skipTimeButton, OnSkipTime);
    }

    void Start()
    {
        Refresh(forceVisibility: true);
    }

    void OnDisable()
    {
        Unbind(startBusinessButton, OnStartBusiness);
        Unbind(showResultButton, OnShowResult);
        Unbind(nextDayButton, OnNextDay);
        Unbind(pauseButton, OnTogglePause);
        Unbind(speed1Button, OnSpeed1);
        Unbind(speed2Button, OnSpeed2);
        Unbind(speed3Button, OnSpeed3);
        Unbind(skipTimeButton, OnSkipTime);
    }

    void Update()
    {
        Refresh(forceVisibility: false);
    }

    void Refresh(bool forceVisibility)
    {
        if (session == null)
        {
            return;
        }

        SetText(dayText, session.DayLabel);
        SetText(timeText, session.TimeLabel);
        SetText(phaseText, session.Phase.ToString());
        SetText(skipTimeLabel, session.SkipTimeLabel);

        if (session.Phase == StorePhase.Result)
        {
            SetText(resultText, $"{session.DayLabel} 종료");
        }

        if (!forceVisibility && session.Phase == displayedPhase)
        {
            return;
        }

        displayedPhase = session.Phase;
        SetButtonVisible(startBusinessButton, session.Phase == StorePhase.Preparation);
        SetButtonVisible(showResultButton, session.Phase == StorePhase.Closing);
        SetObjectVisible(speedControls, session.Phase == StorePhase.Open);
        SetObjectVisible(resultPanel, session.Phase == StorePhase.Result);
    }

    void OnStartBusiness()
    {
        session.StartBusiness();
        Refresh(forceVisibility: true);
    }

    void OnShowResult()
    {
        session.ShowResult();
        Refresh(forceVisibility: true);
    }

    void OnNextDay()
    {
        session.AdvanceToNextDay();
        Refresh(forceVisibility: true);
    }

    void OnTogglePause()
    {
        session.TogglePause();
    }

    void OnSpeed1()
    {
        session.SetSpeed(StoreSession.NormalTimeScale);
    }

    void OnSpeed2()
    {
        session.SetSpeed(StoreSession.DoubleTimeScale);
    }

    void OnSpeed3()
    {
        session.SetSpeed(StoreSession.TripleTimeScale);
    }

    void OnSkipTime()
    {
        session.JumpToSkipTime();
        Refresh(forceVisibility: true);
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
        {
            text.text = value;
        }
    }

    static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    static void SetObjectVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }
}
