using Live2D.Cubism.Core;
using UnityEngine;

public sealed class MouthLipSyncPriority : MonoBehaviour
{
    [SerializeField] private AudioSource audioInput;
    [SerializeField] private string mouthParameterId = "ParamMouthOpenY";
    [SerializeField] private float gain = 10f;
    [SerializeField] private float silenceThreshold = 0.02f;
    [SerializeField] private float smoothing = 0.05f;

    private CubismModel model;
    private CubismParameter mouthParameter;
    private float[] samples;
    private float currentValue;
    private float velocity;

    private void Awake()
    {
        model = GetComponent<CubismModel>();
        samples = new float[256];

        if (model != null)
        {
            foreach (var p in model.Parameters)
            {
                if (p.Id == mouthParameterId)
                {
                    mouthParameter = p;
                    break;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (audioInput == null || mouthParameter == null)
            return;

        float motionValue = mouthParameter.Value;

        audioInput.GetOutputData(samples, 0);

        float total = 0f;
        for (int i = 0; i < samples.Length; i++)
            total += samples[i] * samples[i];

        float rms = Mathf.Sqrt(total / samples.Length) * gain;
        rms = Mathf.Clamp01(rms);

        bool audible = audioInput.isPlaying && rms > silenceThreshold;

        float targetValue = audible ? rms : motionValue;
        currentValue = Mathf.SmoothDamp(currentValue, targetValue, ref velocity, smoothing);

        mouthParameter.Value = currentValue;
    }
}
