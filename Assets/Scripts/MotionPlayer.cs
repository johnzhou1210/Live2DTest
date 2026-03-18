using System;
using UnityEngine;

public class MotionPlayer : MonoBehaviour
{
    private Animator animator;

    private void Awake() {
        animator = GetComponent<Animator>();
    }

    public void PlayMotion(AnimationClip clip) {
        animator.Play(clip.name);
    }
}
