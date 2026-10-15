using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>본인 시점과 사망 후 다른 플레이어의 관전 시점을 전환합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerManager))]
public sealed class CinemachineCameraController : MonoBehaviour
{
    private PlayerManager playerManager;
    private _PlayerNetworkSync localPlayer;
    private PlayerStateMachine localStateMachine;

    private GameObject currentCamera;
    private int currentActorNumber;
    private bool hasStarted;

    /// <summary>같은 객체의 플레이어 관리자를 확보합니다.</summary>
    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
    }

    /// <summary>플레이어 등록·해제 알림을 구독합니다.</summary>
    private void OnEnable()
    {
        playerManager.D_PlayerRegistered += HandlePlayerRegistered;
        playerManager.D_PlayerUnregistered += HandlePlayerUnregistered;

        if (hasStarted)
        {
            InitializeCameras();
        }
    }

    /// <summary>이미 등록된 플레이어까지 포함해 최초 시점을 설정합니다.</summary>
    private void Start()
    {
        hasStarted = true;
        InitializeCameras();
    }

    /// <summary>사망 중 Tab을 누르면 다음 플레이어를 관전합니다.</summary>
    private void Update()
    {
        if (localStateMachine == null || localStateMachine.IsAlive
            || !Application.isFocused)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
        {
            SwitchToNextPlayer();
        }
    }

    /// <summary>이벤트 구독과 현재 카메라를 정리합니다.</summary>
    private void OnDisable()
    {
        playerManager.D_PlayerRegistered -= HandlePlayerRegistered;
        playerManager.D_PlayerUnregistered -= HandlePlayerUnregistered;

        BindLocalPlayer(null);
    }

    /// <summary>기존 카메라를 끄고 본인의 현재 상태에 맞는 시점을 설정합니다.</summary>
    private void InitializeCameras()
    {
        foreach (int actorNumber in playerManager.ActorNumberList)
        {
            _PlayerNetworkSync player = playerManager.GetPlayer(actorNumber);

            if (player != null)
            {
                DisablePlayerCameras(player);
            }
        }

        BindLocalPlayer(playerManager.LocalPlayerSync);
    }

    /// <summary>새 플레이어의 카메라를 끄고 본인이라면 상태를 연결합니다.</summary>
    private void HandlePlayerRegistered(_PlayerNetworkSync player)
    {
        DisablePlayerCameras(player);

        if (player == playerManager.LocalPlayerSync)
        {
            BindLocalPlayer(player);
        }
    }

    /// <summary>본인 또는 현재 관전 대상이 제거됐을 때 시점을 정리합니다.</summary>
    private void HandlePlayerUnregistered(_PlayerNetworkSync player)
    {
        if (ReferenceEquals(player, localPlayer))
        {
            BindLocalPlayer(null);
            return;
        }

        if (player.ActorNumber == currentActorNumber)
        {
            SwitchToNextPlayer();
        }
    }

    /// <summary>본인 플레이어의 상태 변경 구독을 교체합니다.</summary>
    private void BindLocalPlayer(_PlayerNetworkSync player)
    {
        if (localStateMachine != null)
        {
            localStateMachine.StateChanged -= HandleLocalStateChanged;
        }

        localPlayer = player;
        localStateMachine = player != null ? player.StateMachine : null;

        if (localStateMachine != null)
        {
            localStateMachine.StateChanged += HandleLocalStateChanged;
        }

        ApplyLocalView();
    }

    /// <summary>사망하거나 부활했을 때 본인 시점으로 전환합니다.</summary>
    private void HandleLocalStateChanged(
        PlayerState previousState,
        PlayerState currentState)
    {
        if (currentState == PlayerState.Dead
            || previousState == PlayerState.Dead)
        {
            ApplyLocalView();
        }
    }

    /// <summary>생존 중에는 본인 1인칭, 사망 중에는 본인 3인칭을 적용합니다.</summary>
    private void ApplyLocalView()
    {
        if (localPlayer == null || localStateMachine == null)
        {
            currentActorNumber = 0;
            SwitchCamera(null);
            return;
        }

        bool isAlive = localStateMachine.IsAlive;

        localPlayer.SetLocalModelVisible(!isAlive);
        currentActorNumber = localPlayer.ActorNumber;

        SwitchCamera(isAlive
            ? localPlayer.PlayerViewCamera
            : localPlayer.ThirdPersonViewCamera);
    }

    /// <summary>현재 번호보다 큰 최소 번호를 선택하고, 없으면 처음으로 순환합니다.</summary>
    private void SwitchToNextPlayer()
    {
        if (localPlayer == null || localStateMachine == null
            || localStateMachine.IsAlive)
        {
            return;
        }

        _PlayerNetworkSync firstPlayer = null;
        _PlayerNetworkSync nextPlayer = null;

        foreach (int actorNumber in playerManager.ActorNumberList)
        {
            _PlayerNetworkSync player = playerManager.GetPlayer(actorNumber);

            if (player == null || !player.isActiveAndEnabled
                || player.ThirdPersonViewCamera == null)
            {
                continue;
            }

            // 목록의 등록 순서와 관계없이 가장 작은 번호를 찾습니다.
            if (firstPlayer == null
                || actorNumber < firstPlayer.ActorNumber)
            {
                firstPlayer = player;
            }

            if (actorNumber > currentActorNumber
                && (nextPlayer == null
                    || actorNumber < nextPlayer.ActorNumber))
            {
                nextPlayer = player;
            }
        }

        _PlayerNetworkSync target =
            nextPlayer != null ? nextPlayer : firstPlayer;

        if (target == null)
        {
            currentActorNumber = 0;
            SwitchCamera(null);
            return;
        }

        currentActorNumber = target.ActorNumber;
        SwitchCamera(target.ThirdPersonViewCamera);
    }

    /// <summary>플레이어의 두 카메라를 모두 비활성화합니다.</summary>
    private void DisablePlayerCameras(_PlayerNetworkSync player)
    {
        if (player.PlayerViewCamera != null)
        {
            player.PlayerViewCamera.SetActive(false);
        }

        if (player.ThirdPersonViewCamera != null)
        {
            player.ThirdPersonViewCamera.SetActive(false);
        }
    }

    /// <summary>이전 카메라를 끈 뒤 선택한 카메라를 활성화합니다.</summary>
    private void SwitchCamera(GameObject nextCamera)
    {
        if (currentCamera != nextCamera && currentCamera != null)
        {
            currentCamera.SetActive(false);
        }

        currentCamera = nextCamera;

        if (currentCamera != null)
        {
            currentCamera.SetActive(true);
        }
    }
}