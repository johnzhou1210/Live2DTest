using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ResponseTextListener : MonoBehaviour {
    private string _previousText;
    private TextMeshProUGUI _textComponent;
    [SerializeField] private CanvasGroup containerCanvasGroup;
    [SerializeField] private float delayBeforeFade = 10f;

    private float _timer = 0f;
    private bool _visible = false;

    private void Awake() {
        _textComponent = GetComponent<TextMeshProUGUI>();
    }

    private void Start() {
        _previousText = _textComponent.text;
    }

    private void Update() {
        if (_textComponent == null) return;
        _timer = Mathf.Max(0f, _timer - Time.deltaTime);
        if (_timer == 0f && _visible) {
            _visible = false;
            containerCanvasGroup.DOFade(0, 1);
        }
        if (_previousText != _textComponent.text) {
            _previousText = _textComponent.text;
            _timer = delayBeforeFade;
            _visible = true;
        }
    }
}
