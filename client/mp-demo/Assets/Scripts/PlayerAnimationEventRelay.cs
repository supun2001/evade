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

    public void AnimationEvent_PlayCrouchFootstep()
    {
        if (_playerController == null)
        {
            _playerController = GetComponentInParent<PlayerController>();
        }

        _playerController?.AnimationEvent_PlayCrouchFootstep();
    }

    public void AnimationEvent_PlayJumpStartFootstep()
    {
        if (_playerController == null)
        {
            _playerController = GetComponentInParent<PlayerController>();
        }

        _playerController?.AnimationEvent_PlayJumpStartFootstep();
    }

    public void AnimationEvent_PlayLandingFootstep()
    {
        if (_playerController == null)
        {
            _playerController = GetComponentInParent<PlayerController>();
        }

        _playerController?.AnimationEvent_PlayLandingFootstep();
    }
}
