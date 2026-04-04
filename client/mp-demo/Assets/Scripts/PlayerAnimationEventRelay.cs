using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponentInParent<PlayerController>();
    }

    public void AnimationEvent_PlayFootstep()
    {
        if (_playerController == null)
        {
            _playerController = GetComponentInParent<PlayerController>();
        }

        _playerController?.AnimationEvent_PlayFootstep();
    }
}
