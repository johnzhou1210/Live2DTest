using Live2D.Cubism.Framework.Motion;
using UnityEngine;

public class MotionPlayer : MonoBehaviour
{
    private CubismMotionController _motionController;

    private void Start()
    {
        _motionController = GetComponent<CubismMotionController>();
    }

    public void PlayMotion(AnimationClip animation)
    {
        if (_motionController == null || animation == null)
        {
            return;
        }

        _motionController.PlayAnimation(
            animation,
            layerIndex: 0,
            priority: CubismMotionPriority.PriorityForce,
            isLoop: false
        );
    }
}
