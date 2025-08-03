using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using UnityEngine;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;
    [SerializeField] private NetworkPrefabRef _hostAvatarPrefab;
    private NetworkObject _hostAvatarObject;

    // 5초 자동 Host 접속 기능
    [Header("Auto Host Settings")]
    [SerializeField] private float autoHostDelay = 5f;
    private float autoHostTimer;
    private bool isAutoTimerActive = false;

    private void Start()
    {
        // 앱 시작 시 자동 Host 타이머 시작
        StartAutoHostTimer();
    }

    private void Update()
    {
        // 자동 Host 타이머 업데이트
        if (isAutoTimerActive && _runner == null)
        {
            autoHostTimer -= Time.deltaTime;
            
            if (autoHostTimer <= 0f)
            {
                Debug.Log("[BasicSpawner] Auto-connecting to Host after 5 seconds");
                StopAutoHostTimer();
                StartGame(GameMode.Host);
            }
        }
    }

    private void StartAutoHostTimer()
    {
        autoHostTimer = autoHostDelay;
        isAutoTimerActive = true;
        Debug.Log($"[BasicSpawner] Auto Host timer started - will connect in {autoHostDelay} seconds");
    }

    private void StopAutoHostTimer()
    {
        isAutoTimerActive = false;
        Debug.Log("[BasicSpawner] Auto Host timer stopped");
    }

    // Host
    async void StartGame(GameMode mode)
    {
        // 사용자가 수동으로 선택했을 때 타이머 정지
        if (isAutoTimerActive)
        {
            StopAutoHostTimer();
        }

        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // Create the NetworkSceneInfo from the current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid) {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // Start or join (depends on gamemode) a session with a specific name
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }
    
    private void OnGUI()
    {
        if (_runner == null)
        {
            // 타이머 표시
            if (isAutoTimerActive)
            {
                string timerText = $"Auto Host in: {Mathf.Ceil(autoHostTimer)}s";
                GUI.Label(new Rect(10, 90, 300, 20), timerText);
                GUI.Label(new Rect(10, 110, 300, 20), "Redirecting to Host in 5 seconds");
            }

            if (GUI.Button(new Rect(0,0,200,40), "Host"))
            {
                StartGame(GameMode.Host);
            }
            if (GUI.Button(new Rect(0,40,200,40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
    }
    
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // AOI (Area of Interest) 처리 - 필요시 구현
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // AOI (Area of Interest) 처리 - 필요시 구현
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            if (player.Equals(runner.LocalPlayer))
            {
                // Host가 세션에 참가하면 Host 아바타 스폰
                if (_hostAvatarObject == null)
                {
                    Vector3 spawnPosition = Vector3.zero;
                    _hostAvatarObject = runner.Spawn(_hostAvatarPrefab, spawnPosition, Quaternion.identity, player);
                    Debug.Log($"[BasicSpawner] Host avatar spawned for player {player}");
                }
            }
            else
            {
                Debug.Log($"[BasicSpawner] Client player {player} joined as spectator");
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[BasicSpawner] Player {player} left the session");
        
        // Host가 떠나면 Host 아바타도 제거
        if (_hostAvatarObject != null && _hostAvatarObject.InputAuthority == player)
        {
            if (runner.IsServer)
            {
                runner.Despawn(_hostAvatarObject);
                _hostAvatarObject = null;
                Debug.Log($"[BasicSpawner] Host avatar despawned");
            }
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[BasicSpawner] Shutdown: {shutdownReason}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[BasicSpawner] Disconnected from server: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        // 연결 요청 허용
        request.Accept();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[BasicSpawner] Connection failed: {reason}");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        // 사용자 정의 메시지 처리 - 필요시 구현
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        // 신뢰성 있는 데이터 수신 - 필요시 구현
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        // 신뢰성 있는 데이터 진행률 - 필요시 구현
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 입력 처리 - VR에서는 트래커 데이터가 자동으로 처리됨
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        // 입력 누락 처리
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[BasicSpawner] Connected to server");
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        // 세션 목록 업데이트 - 필요시 구현
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        // 커스텀 인증 응답 - 필요시 구현
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("[BasicSpawner] Host migration occurred");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[BasicSpawner] Scene load completed");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("[BasicSpawner] Scene load started");
    }
}
