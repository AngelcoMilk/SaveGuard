using UnityEngine.SceneManagement;

namespace HomeGuidance.Runtime;

public sealed class GuidanceLifecycle
{
    private readonly GuidanceController _controller;
    private int _roundToken;
    private int _roundSceneHandle;
    private bool _wasRoundActive;
    private bool _sceneSubscribed;

    public GuidanceLifecycle(GuidanceController controller)
    {
        _controller = controller;
    }

    public void Initialize()
    {
        if (!_sceneSubscribed)
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            _sceneSubscribed = true;
        }
    }

    public void Tick()
    {
        var gm = YAPYAP.GameManager.Instance;
        bool roundActive = gm != null && gm.RoundActive;

        if (roundActive && !_wasRoundActive)
        {
            // Round started
            _roundToken++;
            _roundSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            _controller.RoundGuidanceState.BeginRound(_roundToken);
            GuidanceLog.Info($"Round {_roundToken} started, scene handle={_roundSceneHandle}");
            _controller.OnRoundBegin(_roundToken);
        }

        _wasRoundActive = roundActive;
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (_wasRoundActive && previous.handle == _roundSceneHandle && previous.handle != current.handle)
        {
            // Confirmed scene teardown while round was active
            GuidanceLog.Info($"Scene teardown detected (scene {previous.handle} -> {current.handle}), ending round");
            _controller.RoundGuidanceState.EndRound();
            _controller.OnRoundEnd();
            _wasRoundActive = false;
        }
    }

    public void Shutdown()
    {
        if (_sceneSubscribed)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _sceneSubscribed = false;
        }
    }
}
