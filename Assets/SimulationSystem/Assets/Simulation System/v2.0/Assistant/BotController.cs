using System;
using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;
using SimulationSystem.V02.Simulation.Managers;

public class BotController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // ENUMS
    // ─────────────────────────────────────────────

    public enum BotState { Idle, Hovering, Moving, HelpCalled, PromptShown, UIOpen, UIClose, Discarding }
    public enum BotMode
    {
        Companion,   // Default mode: follows player, reacts to help calls and prompts
        Guide,       // Guide mode: used for BotGuideBehaviour, ignores help calls and prompts
    }
    public BotMode currentMode = BotMode.Companion;

    // ─────────────────────────────────────────────
    // ACTIONS AND EVENTS
    // ─────────────────────────────────────────────

    public static Action BotSummonedWithKey;
    public static Action BotDismissedWithKey;

    // ─────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Scene References")]
    public Animator animator;
    public AudioSource audioSource;
    public BotGuideBehaviour guideBehaviour;

    [Header("Position Targets")]
    [Tooltip("Active position — in front, slightly to the left of the player")]
    public GameObject frontLeftOffset;
    [Tooltip("Passive position — to the side, outside the player's FOV")]
    public GameObject sideOffset;

    [Header("Follow Behaviour")]
    public Transform panelHolder;
    public float followSmoothTime = 0.4f;
    public float maxFollowSpeed = 8f;
    public float rotationSpeed = 4f;

    [Header("Hover Float")]
    public float hoverAmplitude = 0.04f;
    public float hoverFrequency = 1.2f;

    [Header("VR Head Tracking")]
    [Tooltip("Assign your Main Camera / CenterEyeAnchor here")]
    public Transform vrHead;
    [Tooltip("Vertical offset from eye level. Negative = below eyes, positive = above.")]
    public float headHeightOffset = 0f;

    [Header("Sound Effects")]
    public AudioClip soundSummon;
    public AudioClip soundDismiss;
    public AudioClip soundUIOpen;
    public AudioClip soundUIClose;
    public AudioClip soundHelpCalled;
    public AudioClip soundPromptShown;
    public AudioClip soundDiscard;

    [Header("Animator Parameter Names")]
    public string animParamState = "BotState";
    public string animParamMoving = "IsMoving";
    public string animParamVisible = "IsVisible";

    private const int ANIM_IDLE = 0;
    private const int ANIM_HOVERING = 1;
    private const int ANIM_MOVING = 2;
    private const int ANIM_HELP = 3;
    private const int ANIM_PROMPT = 4;
    private const int ANIM_UI_OPEN = 5;
    private const int ANIM_UI_CLOSE = 6;
    private const int ANIM_DISCARDING = 7;

    // ─────────────────────────────────────────────
    // RUNTIME STATE
    // ─────────────────────────────────────────────

    private BotState _state = BotState.Idle;
    private GameObject _targetSlot;
    private bool _atFront = false;
    private bool _isVisible = false;
    private float _hoverTimer = 0f;
    private Vector3 _velocity = Vector3.zero;
    private const float MOVING_THRESHOLD = 0.15f;

    private bool _prevLeftPrimary = false;
    private bool _prevRightPrimary = false;
    private Coroutine _autoHideCoroutine;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────
    private void OnEnable()
    {
        BotGuideBehaviour.GuideFinished += OnGuideFinished;

        SimulationManager.Instance.SimulationStart.AddListener(() => TogglePosition());
    }

    private void OnDisable()
    {
        BotGuideBehaviour.GuideFinished -= OnGuideFinished;

        SimulationManager.Instance.SimulationStart.RemoveListener(() => TogglePosition());
    }

    private void Start()
    {
        if (frontLeftOffset == null || sideOffset == null)
        {
            Debug.LogError("[BotController] Missing required references (frontLeftOffset / sideOffset).");
            enabled = false;
            return;
        }

        _targetSlot = sideOffset;
        transform.position = _targetSlot.transform.position;

        SetVisible(false);
        SwitchMode(BotMode.Companion);
        SetState(BotState.Idle);

        TogglePosition();
    }

    private void Update()
    {
        PollInput();

        switch (currentMode)
        {
            case BotMode.Companion:
                FollowPlayer();
                FaceUser();
                break;

            case BotMode.Guide:
                // GuideBehaviour handles transform
                break;
        }
        if (!_isVisible) return;
    }


    // MODE SWITCHING (AUTHORITY)

    public void SwitchMode(BotMode newMode)
    {
        currentMode = newMode;

        switch (currentMode)
        {
            case BotMode.Companion:
                _velocity = Vector3.zero;
                SetState(BotState.Idle);
                break;

            case BotMode.Guide:
                _velocity = Vector3.zero;
                // FIX: Set Moving state so the bot exits whatever animation
                // it was in (e.g. HelpCalled). The guide behaviour drives
                // the transform directly — Moving is the correct visual state.
                SetState(BotState.Moving);
                break;
        }
    }

    // ─────────────────────────────────────────────
    // FOLLOW & ROTATION
    // ─────────────────────────────────────────────

    private void FollowPlayer()
    {
        _hoverTimer += Time.deltaTime;
        float hoverY = Mathf.Sin(_hoverTimer * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
        Vector3 desired = _targetSlot.transform.position;

        if (vrHead != null)
            desired.y = vrHead.position.y + headHeightOffset + hoverY;
        else
            desired.y = _targetSlot.transform.position.y + hoverY;

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity,
            followSmoothTime,
            maxFollowSpeed > 0f ? maxFollowSpeed : Mathf.Infinity);

        float dist = Vector3.Distance(transform.position, _targetSlot.transform.position);
        bool moving = dist > MOVING_THRESHOLD;
        animator?.SetBool(animParamMoving, moving);

        if (moving && _state == BotState.Hovering) SetState(BotState.Moving);
        else if (!moving && _state == BotState.Moving) SetState(BotState.Hovering);
    }

    private void FaceUser()
    {
        if (_atFront)
        {
            transform.LookAt(panelHolder);
        }
        else
        {
            transform.rotation = sideOffset.transform.rotation;
        }
    }

    // ─────────────────────────────────────────────
    // INPUT
    // ─────────────────────────────────────────────

    private void PollInput()
    {
        /*if (Input.GetKeyDown(KeyCode.Z))
        {

            TogglePosition();
        }
*/

        bool leftNow = GetVRButton(InputDeviceCharacteristics.Left, CommonUsages.primaryButton);
        if (leftNow && !_prevLeftPrimary)
        {
            if (!_atFront)
            {
                Debug.Log("[BotController] Toggle position input detected (X).");
                BotSummonedWithKey?.Invoke();
            }
            else BotDismissedWithKey?.Invoke();

            TogglePosition();

        }
        _prevLeftPrimary = leftNow;

        bool rightNow = GetVRButton(InputDeviceCharacteristics.Right, CommonUsages.primaryButton);
        _prevRightPrimary = rightNow;
    }

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    public void TogglePosition()
    {
        _atFront = !_atFront;
        _targetSlot = _atFront ? frontLeftOffset : sideOffset;
        if (_atFront && !_isVisible) SetVisible(true);

        if (_atFront) SoundManager.PlayBotSummon();
        else SoundManager.PlayBotDismiss();

        //PlaySound(_atFront ? soundSummon : soundDismiss);
        SetState(BotState.Moving);
        _velocity = Vector3.zero;
    }

    public void Summon()
    {
        _atFront = true;
        _targetSlot = frontLeftOffset;
        SetVisible(true);

        SoundManager.PlayBotSummon();
        //PlaySound(soundSummon);
        SetState(BotState.Moving);
        _velocity = Vector3.zero;


    }

    public void Dismiss()
    {
        _atFront = false;
        _targetSlot = sideOffset;

        SoundManager.PlayBotDismiss();

        //PlaySound(soundDismiss);
        SetState(BotState.Moving);
        _velocity = Vector3.zero;
    }

    public void OnHelpCalled()
    {
        SoundManager.PlayBotHelpCalled();

        //PlaySound(soundHelpCalled);
        SetState(BotState.HelpCalled);
        _velocity = Vector3.zero;
    }

    // CALLED ON GUIDANCE FINISH BY BotGuideBehaviour (AUTHORITY)
    public void OnGuideFinished()
    {
        _targetSlot = frontLeftOffset;
        SwitchMode(BotMode.Companion);
    }

    public void OnPromptShown() { SetState(BotState.PromptShown); SoundManager.PlayBotPromptShown();/*PlaySound(soundPromptShown);*/ }
    public void OnUIOpen() { SetState(BotState.UIOpen); SoundManager.PlayBotUIOpen(); /*PlaySound(soundUIOpen);*/ }
    public void OnUIClose()
    {
        SetState(BotState.UIClose);

        SoundManager.PlayBotUIClose();
        //PlaySound(soundUIClose);
        StartCoroutine(ReturnToHoverAfter(1.2f));
    }

    public void OnDiscard()
    {
        if (!_isVisible) SetVisible(true);

        SoundManager.PlayBotDiscard();
        //PlaySound(soundDiscard);
        SetState(BotState.Discarding);
    }

    public void OnDiscardComplete()
    {
        SetState(BotState.Hovering);
    }

    public void ShowBotUI(string message) { if (!_isVisible) Summon(); OnUIOpen(); }
    public void HideBotUI()
    {
        if (_autoHideCoroutine != null) { StopCoroutine(_autoHideCoroutine); _autoHideCoroutine = null; }
        OnUIClose();
    }

    public void SnapToTarget()
    {
        Quaternion savedRotation = transform.rotation;
        transform.position = _targetSlot.transform.position;
        _velocity = Vector3.zero;
        _hoverTimer = 0f;
        transform.rotation = frontLeftOffset.transform.rotation;
    }

    // ─────────────────────────────────────────────
    // INTERNALS
    // ─────────────────────────────────────────────

    private void SetState(BotState newState)
    {
        _state = newState;
        if (animator == null) return;
        int v = newState switch
        {
            BotState.Hovering => ANIM_HOVERING,
            BotState.Moving => ANIM_MOVING,
            BotState.HelpCalled => ANIM_HELP,
            BotState.PromptShown => ANIM_PROMPT,
            BotState.UIOpen => ANIM_UI_OPEN,
            BotState.UIClose => ANIM_UI_CLOSE,
            BotState.Discarding => ANIM_DISCARDING,
            _ => ANIM_IDLE
        };
        animator.SetInteger(animParamState, v);
    }

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        animator?.SetBool(animParamVisible, visible);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    private IEnumerator ReturnToHoverAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_state == BotState.UIClose) SetState(BotState.Hovering);
    }

    private bool GetVRButton(InputDeviceCharacteristics hand, InputFeatureUsage<bool> usage)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(hand, devices);
        foreach (var d in devices)
            if (d.TryGetFeatureValue(usage, out bool v) && v) return true;
        return false;
    }
}