using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>
/// Photon Fusion을 사용한 기본 네트워크 스포너 클래스
/// Host와 Client 모드를 지원하며, 플레이어 아바타의 스폰/디스폰을 관리합니다.
/// INetworkRunnerCallbacks 인터페이스를 구현하여 네트워크 이벤트를 처리합니다.
/// </summary>
public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    // 네트워크 러너 - Fusion의 핵심 컴포넌트로 네트워크 세션을 관리
    private NetworkRunner _runner;
    
    // Host 플레이어의 아바타 프리팹 참조 (Inspector에서 설정)
    [SerializeField] private NetworkPrefabRef _hostAvatarPrefab;
    
    // 현재 스폰된 Host 아바타 오브젝트의 참조
    private NetworkObject _hostAvatarObject;

    /// <summary>
    /// 네트워크 게임 세션을 시작하는 메서드
    /// Host 또는 Client 모드로 게임을 시작할 수 있습니다.
    /// </summary>
    /// <param name="mode">게임 모드 (Host, Client, Server 등)</param>
    async void StartGame(GameMode mode)
    {
        // Fusion 러너를 현재 GameObject에 추가하고 사용자 입력 제공을 활성화
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true; // 이 클라이언트가 입력을 제공할 것임을 명시

        // 현재 활성화된 씬으로부터 NetworkSceneInfo 생성
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        
        // 씬이 유효한 경우 씬 정보에 추가 (Additive 모드로 로드)
        if (scene.IsValid) {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // 지정된 게임 모드와 세션 이름으로 게임 시작 또는 참가
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,              // Host, Client, Server 등의 모드
            SessionName = "TestRoom",     // 세션 이름 (같은 이름의 룸에 참가)
            Scene = scene,                // 로드할 씬
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>() // 기본 씬 매니저 추가
        });
    }
    
    /// <summary>
    /// Unity의 Start 메서드 - 게임 시작 시 자동으로 호출됩니다.
    /// 현재는 자동으로 클라이언트 모드로 접속하도록 설정되어 있습니다.
    /// </summary>
    private void Start()
    {
        // 자동으로 클라이언트로 접속
        StartGame(GameMode.Client);
    }
    
    /// <summary>
    /// 오브젝트가 플레이어의 관심 영역(AOI)에서 벗어날 때 호출되는 콜백
    /// 네트워크 최적화를 위해 멀리 있는 오브젝트는 동기화하지 않습니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="obj">AOI에서 벗어난 네트워크 오브젝트</param>
    /// <param name="player">해당 플레이어</param>
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // AOI (Area of Interest) 처리 - 필요시 구현
    }

    /// <summary>
    /// 오브젝트가 플레이어의 관심 영역(AOI)에 진입할 때 호출되는 콜백
    /// 플레이어 근처의 오브젝트만 동기화하여 네트워크 성능을 최적화합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="obj">AOI에 진입한 네트워크 오브젝트</param>
    /// <param name="player">해당 플레이어</param>
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // AOI (Area of Interest) 처리 - 필요시 구현
    }

    /// <summary>
    /// 새로운 플레이어가 세션에 참가했을 때 호출되는 콜백
    /// Host인 경우 자신의 아바타를 스폰하고, Client는 관전자로 참가합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="player">참가한 플레이어의 참조</param>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // 현재 인스턴스가 서버(Host)인 경우에만 오브젝트 스폰 권한이 있음
        if (runner.IsServer)
        {
            // 참가한 플레이어가 로컬 플레이어(Host)인 경우
            if (player.Equals(runner.LocalPlayer))
            {
                // Host가 세션에 참가하면 Host 아바타 스폰
                if (_hostAvatarObject == null)
                {
                    // 스폰 위치를 원점으로 설정
                    Vector3 spawnPosition = Vector3.zero;
                    
                    // Host 아바타를 지정된 위치에 스폰하고 플레이어에게 권한 부여
                    _hostAvatarObject = runner.Spawn(_hostAvatarPrefab, spawnPosition, Quaternion.identity, player);
                    Debug.Log($"[BasicSpawner] Host avatar spawned for player {player}");
                }
            }
            else
            {
                // Host가 아닌 다른 플레이어는 관전자로 참가
                Debug.Log($"[BasicSpawner] Client player {player} joined as spectator");
            }
        }
    }

    /// <summary>
    /// 플레이어가 세션을 떠났을 때 호출되는 콜백
    /// Host가 떠나는 경우 해당 아바타를 제거합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="player">떠난 플레이어의 참조</param>
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[BasicSpawner] Player {player} left the session");
        
        // Host가 떠나면 Host 아바타도 제거
        if (_hostAvatarObject != null && _hostAvatarObject.InputAuthority == player)
        {
            // 서버에서만 오브젝트 제거 권한이 있음
            if (runner.IsServer)
            {
                // 네트워크에서 아바타 오브젝트 제거
                runner.Despawn(_hostAvatarObject);
                _hostAvatarObject = null;
                Debug.Log($"[BasicSpawner] Host avatar despawned");
            }
        }
    }

    /// <summary>
    /// 네트워크 러너가 종료될 때 호출되는 콜백
    /// 세션 종료, 연결 끊김 등의 이유로 종료될 수 있습니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="shutdownReason">종료 이유</param>
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[BasicSpawner] Shutdown: {shutdownReason}");
    }

    /// <summary>
    /// 서버와의 연결이 끊어졌을 때 호출되는 콜백
    /// 네트워크 문제, 서버 종료 등의 이유로 발생할 수 있습니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="reason">연결 끊김 이유</param>
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[BasicSpawner] Disconnected from server: {reason}");
    }

    /// <summary>
    /// 다른 클라이언트가 세션에 연결을 요청할 때 호출되는 콜백
    /// 연결 허용/거부를 결정할 수 있습니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="request">연결 요청 정보</param>
    /// <param name="token">인증 토큰 (있는 경우)</param>
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        // 연결 요청 허용 (모든 클라이언트 허용)
        request.Accept();
    }

    /// <summary>
    /// 서버 연결에 실패했을 때 호출되는 콜백
    /// 서버가 없거나, 네트워크 문제 등으로 인해 발생합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="remoteAddress">연결하려던 서버 주소</param>
    /// <param name="reason">연결 실패 이유</param>
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[BasicSpawner] Connection failed: {reason}");
    }

    /// <summary>
    /// 사용자 정의 시뮬레이션 메시지를 받았을 때 호출되는 콜백
    /// 커스텀 네트워크 메시지를 처리할 때 사용합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="message">받은 메시지</param>
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        // 사용자 정의 메시지 처리 - 필요시 구현
    }

    /// <summary>
    /// 신뢰성 있는 데이터를 받았을 때 호출되는 콜백
    /// TCP처럼 데이터가 확실히 전달되는 것을 보장하는 채널입니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="player">데이터를 보낸 플레이어</param>
    /// <param name="key">데이터 식별 키</param>
    /// <param name="data">받은 데이터</param>
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        // 신뢰성 있는 데이터 수신 - 필요시 구현
    }

    /// <summary>
    /// 신뢰성 있는 데이터 전송 진행률이 업데이트될 때 호출되는 콜백
    /// 대용량 데이터 전송 시 진행 상황을 모니터링할 수 있습니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="player">데이터를 받는 플레이어</param>
    /// <param name="key">데이터 식별 키</param>
    /// <param name="progress">전송 진행률 (0.0 ~ 1.0)</param>
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        // 신뢰성 있는 데이터 진행률 - 필요시 구현
    }

    /// <summary>
    /// 네트워크 입력을 수집할 때 호출되는 콜백
    /// 플레이어의 입력(키보드, 마우스, VR 컨트롤러 등)을 네트워크로 전송합니다.
    /// VR 환경에서는 트래킹 데이터가 자동으로 처리됩니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="input">입력 데이터 구조체</param>
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 입력 처리 - VR에서는 트래커 데이터가 자동으로 처리됨
    }

    /// <summary>
    /// 플레이어의 입력이 누락되었을 때 호출되는 콜백
    /// 네트워크 지연이나 패킷 손실로 인해 입력이 제때 도착하지 않을 때 발생합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="player">입력이 누락된 플레이어</param>
    /// <param name="input">누락된 입력 데이터</param>
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        // 입력 누락 처리
    }

    /// <summary>
    /// 서버에 성공적으로 연결되었을 때 호출되는 콜백
    /// 클라이언트가 서버와 연결을 완료한 후 호출됩니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[BasicSpawner] Connected to server");
    }

    /// <summary>
    /// 사용 가능한 세션 목록이 업데이트되었을 때 호출되는 콜백
    /// 로비 시스템에서 방 목록을 표시할 때 사용할 수 있습니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="sessionList">업데이트된 세션 목록</param>
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        // 세션 목록 업데이트 - 필요시 구현
    }

    /// <summary>
    /// 커스텀 인증 시스템의 응답을 받았을 때 호출되는 콜백
    /// 외부 인증 서버와 연동할 때 사용합니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="data">인증 응답 데이터</param>
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        // 커스텀 인증 응답 - 필요시 구현
    }

    /// <summary>
    /// Host 마이그레이션이 발생했을 때 호출되는 콜백
    /// 현재 Host가 떠났을 때 다른 클라이언트가 새로운 Host가 되는 과정입니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    /// <param name="hostMigrationToken">Host 마이그레이션 토큰</param>
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("[BasicSpawner] Host migration occurred");
    }

    /// <summary>
    /// 씬 로딩이 완료되었을 때 호출되는 콜백
    /// 모든 플레이어가 새로운 씬을 로드 완료한 후 호출됩니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[BasicSpawner] Scene load completed");
    }

    /// <summary>
    /// 씬 로딩이 시작되었을 때 호출되는 콜백
    /// 새로운 씬으로 전환을 시작할 때 호출됩니다.
    /// </summary>
    /// <param name="runner">네트워크 러너</param>
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("[BasicSpawner] Scene load started");
    }
}
