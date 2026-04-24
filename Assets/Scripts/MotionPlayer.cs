using System;
using System.Collections;
using Live2D.Cubism.Framework.Motion;
using UnityEngine;

using System.Collections;
using Live2D.Cubism.Framework.Motion;
using NUnit.Framework.Constraints;
using UnityEngine;
using Random = UnityEngine.Random;

public class MotionPlayer : MonoBehaviour
{
    private CubismMotionController _motionController;
    private Coroutine _fadeCoroutine;

    [Header("Transition Settings")]
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 1.5f;
    public float fadeOutStartMinDelay = 0f;
    public float fadeOutStartMaxDelay = 12f;

    private void Start()
    {
        _motionController = GetComponent<CubismMotionController>();
        if (_motionController != null) {
            _motionController.AnimationEndHandler += OnMotionEnded;
            
            // Start with Layer 1 invisible so we can fade it in
            _motionController.SetLayerWeight(1, 0f); 
        }
    }

    public void PlayMotion(AnimationClip animation)
    {
        if (_motionController == null || animation == null) return;

        // Stop any ongoing fade (in or out) to prevent flickering
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

        // 1. Play the animation on Layer 1 (ensure isLoop is false for the end handler to fire)
        _motionController.PlayAnimation(
            animation,
            layerIndex: 1, 
            priority: CubismMotionPriority.PriorityForce,
            isLoop: false
        );

        // 2. Start the Fade In
        _fadeCoroutine = StartCoroutine(FadeLayerWeight(1, 0f, 1f, fadeInDuration));
    }

    private void OnMotionEnded(int instanceId)
    {
        // 3. When the motion finishes, start the Fade Out
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeLayerWeight(1, 1f, 0f, fadeOutDuration));
    }
    
    private IEnumerator FadeLayerWeight(int layerIndex, float startWeight, float endWeight, float duration)
    {
        if (startWeight > endWeight) {
            yield return new WaitForSeconds(GetLowerBiasedRandom(fadeOutStartMinDelay,  fadeOutStartMaxDelay));
        }
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float currentWeight = Mathf.Lerp(startWeight, endWeight, elapsed / duration);
            _motionController.SetLayerWeight(layerIndex, currentWeight);
            yield return null;
        }

        _motionController.SetLayerWeight(layerIndex, endWeight);
        
        // If we just finished fading out, stop the animation to clean up the PlayableGraph
        if (endWeight <= 0f)
        {
            _motionController.StopAllAnimation(); 
        }
    }

    private void OnDestroy() {
        if (_motionController != null)
            _motionController.AnimationEndHandler -= OnMotionEnded;
    }
    
    public float GetLowerBiasedRandom(float min, float max, float exponent = 2f)
    {
        // higher the exponent, higher the bias towards 0.
        // 1. Get a raw value 0.0 to 1.0
        float raw = Random.value; 
    
        // 2. Apply the power. 
        // Since 0.1 * 0.1 = 0.01, small numbers stay very small.
        float biased = Mathf.Pow(raw, exponent); 
    
        // 3. Scale to your range
        return Mathf.Lerp(min, max, biased);
    }
}