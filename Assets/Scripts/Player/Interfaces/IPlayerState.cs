// IPlayerState.cs
using UnityEngine;

public interface IPlayerState
{
    // *** CHANGE PARAMETER TYPE HERE TO IBasePlayerController ***
    void EnterState(IBasePlayerController controller);

    // *** CHANGE PARAMETER TYPE HERE TO IBasePlayerController ***
    void ExitState(IBasePlayerController controller);

    // *** CHANGE PARAMETER TYPE HERE TO IBasePlayerController ***
    void HandleInput(IBasePlayerController controller);

    // *** CHANGE PARAMETER TYPE HERE TO IBasePlayerController ***
    void UpdatePhysics(IBasePlayerController controller);

    // *** CHANGE PARAMETER TYPE HERE TO IBasePlayerController ***
    void UpdateLogic(IBasePlayerController controller);
}