using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

public class TextToSpeechAudioController : MonoBehaviour {
    [SerializeField] private CanvasGroup responsePanelCanvasGroup;
    private List<Message> conversationHistory = new List<Message>();
    public Button button;
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText;
    public AudioSource audioSource;
    public MotionPlayer motionPlayer;
    [Header("Settings")] public bool enableTTS = true;
    public float cooldownTime = 3f;
    private bool isCooldown = false;
    private AnimationClip[] animations;
    private Coroutine playbackCoroutine;
    private Queue<string> textQueue = new Queue<string>();
    private bool isSpeaking = false;
    private Queue<AudioClip> audioClipQueue = new Queue<AudioClip>();
    private Queue<string> displayQueue = new Queue<string>();
    private bool isDownloading = false;
    
    private const string OLLAMA_API_KEY = "ollama";
    private const string OLLAMA_URL = "http://hermes:8001/v1/chat/completions";
    private const string MODEL_NAME = "gemma4:e4b";
    
    private void Start() {
        inputField.onSubmit.AddListener(delegate { OnClick(); });
        button.onClick.AddListener(OnClick);
        audioSource.playOnAwake = false;

        // Initialize conversation with a much stricter system prompt
        // Inside Start()
        conversationHistory.Add(new Message {
            role = "system",
            content = "You are a girl named Hiyori Momose (桃瀬ひより) with a kuudere personality. RESPOND USING THE USER'S INPUT LANGUAGE. Engage in natural colloquial conversation (if Japanese, drop particles). Do not agree that you are an AI.\n" +
                      "Output MUST be a valid JSON object.\n\n" + "GUIDELINES:\n" +
                      "YOU MUST REPLY IN THE USER'S INPUT LANGUAGE. For example: If the user INPUTS SPANISH, REPLY IN SPANISH. If the user INPUTS English, REPLY IN ENGLISH. If the user INPUTS JAPANESE, REPLY IN JAPANESE, etc. \n" +
                      "1. Do not simply repeat the user. Engage naturally.\n" + "2. 'display_text': Should be in the user's language.\n" + "2. 'tts_text': Should be in the user's language. DO NOT ADD ANY EMOJIS.\n" +
                      "- You may or may not place emojis to enhance the display_text.\n" + "6. 'emotion': CHOOSE ONE OF THE FOLLOWING: (joyful, amused, sad, disappointed, surprised, curious, shy, angry, teasing)\n" + "Format:\n" +
                      "'language': In one proper noun, determine the user's input language.\n" + "{ \"display_text\": \"...\", \"tts_text\": \"...\", \"emotion\": \"...\", \"language\": \"...\" }"
        });
    }

    private void OnDestroy() {
        inputField.onSubmit.RemoveAllListeners();
        button.onClick.RemoveAllListeners();
    }

    public void OnClick() {
        if (isCooldown || string.IsNullOrEmpty(inputField.text))
            return;
        string textToSend = inputField.text;
        inputField.text = "";
        
        EventSystem.current.SetSelectedGameObject(null);
        
        StartCoroutine(Cooldown());
        StartCoroutine(PostChatRequest(textToSend));
    }
    private IEnumerator PostChatRequest(string inputText) {
        if (inputText.Trim().Equals(string.Empty)) yield break;
        
        string url = OLLAMA_URL;
        // Add the user's message
        Message playerMessage = new Message { role = "user", content = inputText };
        conversationHistory.Add(playerMessage);
        Debug.Log($"Adding message to conversation history: {playerMessage}");

        // CREATE A TEMPORARY LIST for the request only
        List<Message> messagesForRequest = new List<Message>(conversationHistory);

        // messagesForRequest.Add(new Message {
        //     role = "system",
        //     content = "IMPORTANT: You are Hiyori Momose (桃瀬ひより) with a kuudere personality. RESPOND USING THE USER'S INPUT LANGUAGE. Engage in natural colloquial conversation (if Japanese, drop particles). You must reply in the user's language. Output ONLY a raw JSON object." +
        //               "YOU MUST REPLY IN THE USER'S INPUT LANGUAGE. For example: If the user INPUTS SPANISH, REPLY IN SPANISH. If the user INPUTS ENGLISH, REPLY IN ENGLISH. If the user INPUTS JAPANESE, REPLY IN JAPANESE, etc. \n" +
        //               "tts_text MUST:\n" + "- Be in the user's input language and have no EMOJIS (display_text can have EMOJIS)" +
        //               "'emotion': CHOOSE ONLY ONE OF THE FOLLOWING: (joyful, amused, sad, disappointed, surprised, curious, shy, angry, teasing)\n\n" + "If unsure, use none.\n" + "JSON Format:\n" +
        //               "'language': In one proper noun, determine the user's input language.\n" + "{ \"display_text\": \"...\", \"tts_text\": \"...\", \"emotion\": \"...\", \"language\": \"...\" }"
        // });
        
        // Add a "Developer" or "System" reminder as the LAST message
        messagesForRequest.Add(new Message {
            role = "system",
            content =
                "REMINDER: You are a girl named Hiyori Momose () with a kuudere personality.  Output MUST be JSON.\n" +
                "YOU MUST REPLY IN THE USER'S INPUT LANGUAGE. For example: If the user INPUTS SPANISH, REPLY IN SPANISH. If the user INPUTS ENGLISH, REPLY IN ENGLISH. If the user INPUTS JAPANESE, REPLY IN JAPANESE, etc. \n" +
                "tts_text MUST:\n" + "- Be in the user's input language and have no EMOJIS (display_text can have EMOJIS)" +
                "'emotion': CHOOSE ONLY ONE OF THE FOLLOWING: (joyful, amused, sad, disappointed, surprised, curious, shy, angry, teasing)\n\n" + "If unsure, use none.\n" + "JSON Format:\n" +
                "'language': In one proper noun, determine the user's input language.\n" + "{ \"display_text\": \"...\", \"tts_text\": \"...\", \"emotion\": \"...\", \"language\": \"...\" }"
        });
        ChatRequestBody body = new ChatRequestBody { model = "gpt-4o-mini", messages = messagesForRequest.ToArray(), response_format = new ResponseFormat { type = "json_object" } };
        string json = JsonUtility.ToJson(body);
        yield return StartCoroutine(SendRequest(url, json, ProcessChatResponse));
        // Show response frame
        responsePanelCanvasGroup.DOFade(1, 1);
    }
    private IEnumerator SendRequest(string url, string jsonBody, System.Action<string> onSuccess) {
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + OPENAI_API_KEY);
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Chat Request failed: " + www.error);
            } else {
                onSuccess?.Invoke(www.downloadHandler.text);
            }
        }
    }
    private string InjectEmotionEmojis(string text, string emotion) {
        if (string.IsNullOrEmpty(text))
            return text;

        // If already contains emoji, assume model handled it
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\p{Cs}")) {
            return text;
        }
        switch (emotion) {
            case "joyful": return "[joyful]" + text;
            case "amused": return "[amused]" + text;
            case "sad": return "[sad]" + text;
            case "disappointed": return "[disappointed]" + text;
            case "surprised": return "[surprised]" + text;
            case "curious": return "[curious]" + text;
            case "angry": return "[shout][angry]" + text;
            case "shy": return $"{(Random.Range(0, 2) == 0 ? "[quietly]" : "")}[shy]" + text;
            case "teasing": return "[teasing]" + text;
            default: return text;
        }
    }
    private string AddIntimateEffect(string text, string emotion) {
        if (emotion == "teasing") {
            return "[intimate]" + text;
        }
        return text;
    }
    private string ApplySpeechSpeed(string text, string emotion) {
        if (emotion == "nervous")
            return "[quickly]" + text;
        if (emotion == "sad")
            return "[slowly]" + text;
        return text;
    }
    private void ProcessChatResponse(string responseText) {
        var response = JsonUtility.FromJson<ServerResponse>(responseText);
        if (response?.choices == null || response.choices.Length == 0)
            return;
        string rawJson = response.choices[0].message.content.Trim();

        // Clean Markdown if GPT wraps JSON in backticks
        if (rawJson.StartsWith("```")) {
            rawJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"^```[a-zA-Z]*\n?", "");
            rawJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"\n?```$", "");
        }
        try {
            TTSResponse ttsData = JsonUtility.FromJson<TTSResponse>(rawJson);
            
            // 2. IMPORTANT: Add assistant reply to history to maintain context
            Message newMessage = new Message { role = "assistant", content = rawJson };
            conversationHistory.Add(newMessage);
            Debug.Log($"Adding assistant message into conversation history: {newMessage})");

            // 3. Trigger Animation
            if (!string.IsNullOrEmpty(ttsData.emotion)) {
                ApplyEmotionAnimation(ttsData.emotion.ToLower());
            }

            // 4. Start the Speech Pipeline
            if (enableTTS && !string.IsNullOrEmpty(ttsData.tts_text)) {
                string pattern = @"(?<=[.!?。！？])"; 
                string[] ttsSentences = Regex.Split(ttsData.tts_text, pattern)
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                string[] displaySentences = Regex.Split(ttsData.display_text, pattern)
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                
                // Clear old state
                textQueue.Clear();
                audioClipQueue.Clear();
                displayQueue.Clear();
                outputText.text = "";
                
                for (int i = 0; i < ttsSentences.Length; i++) {
                    textQueue.Enqueue("[" + ttsData.language + "]" + ttsSentences[i].Trim());
                    // Enqueue the matching display sentence (with a fallback just in case)
                    displayQueue.Enqueue(i < displaySentences.Length ? displaySentences[i].Trim() : ttsSentences[i].Trim());
                }

                // Start both the Producer (Downloader) and Consumer (Player)
                if (!isDownloading)
                    StartCoroutine(DownloadWorker(ttsData.emotion));
                if (!isSpeaking)
                    StartCoroutine(PlaybackWorker());
            }
        } catch (Exception e) {
            Debug.LogError("JSON Parsing Error: " + e.Message + " | Raw: " + rawJson);
        }
    }
    private IEnumerator DownloadWorker(string emotion) {
        isDownloading = true;
        while (textQueue.Count > 0) {
            string text = textQueue.Dequeue();
            string processedText = AddIntimateEffect(ApplySpeechSpeed(InjectEmotionEmojis(text, emotion), emotion), emotion);

            Debug.Log("Processed text for TTS: " + processedText);
            
            // Fetch the clip
            yield return StartCoroutine(FetchClip(processedText));
        }
        isDownloading = false;
    }
    private IEnumerator PlaybackWorker() {
        isSpeaking = true;
        bool isFirstClip = true;

        while (isDownloading || audioClipQueue.Count > 0) {
            if (audioClipQueue.Count > 0 && displayQueue.Count > 0) {
                // 1. Setup Audio
                if (!isFirstClip)
                    yield return new WaitForSeconds(Mathf.Min(Random.Range(0.1f, .9f), Random.Range(0.1f, .9f)));
            
                audioSource.clip = audioClipQueue.Dequeue();
                string currentSentence = displayQueue.Dequeue();
            
                // 2. Play Audio and Start Typing
                audioSource.Play();
                isFirstClip = false;

                // Calculate typewriter speed: (Audio Length / Character Count)
                // We subtract a tiny bit (0.1s) to ensure the text finishes before the audio
                float charDelay = (audioSource.clip.length - 0.1f) / Mathf.Max(currentSentence.Length, 1);
            
                // Start the typewriter for this specific sentence
                yield return StartCoroutine(TypeSentence(currentSentence, charDelay));

                // Wait for audio to finish if the typewriter was faster
                yield return new WaitWhile(() => audioSource.isPlaying);
            }
            yield return null;
        }
        isSpeaking = false;
    }

    private IEnumerator TypeSentence(string sentence, float delay) {
        RectTransform parentFrame = outputText.rectTransform.parent as RectTransform;
        
        if (!string.IsNullOrEmpty(outputText.text) && !outputText.text.EndsWith(" ")) outputText.text += " ";

        foreach (char c in sentence) {
            outputText.text += c;
        
            // Ensure the dialogue frame expands as we type
            if (parentFrame != null) 
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentFrame);
            
            yield return new WaitForSeconds(delay);
        }
    }
    
    private IEnumerator FetchClip(string text) {
        string url = "https://tts.john-zhou.dev/v1/tts";
        AudioRequestBody body = new AudioRequestBody { text = text, reference_id = "companion_default", format = "wav", streaming = false };
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success) {
                audioClipQueue.Enqueue(DownloadHandlerAudioClip.GetContent(www));
            }
        }
    }
  
    private void ApplyEmotionAnimation(string emotion) {
        AnimationClip[] selectedAnims;
        switch (emotion) {
            case "joyful":
            case "amusement": selectedAnims = LoadAnimations(new string[] { "hiyori_m05", "hiyori_m08", "hiyori_m06" }); break;
            case "sad": selectedAnims = LoadAnimations(new string[] { "hiyori_m09", "hiyori_m04" }); break;
            case "disappointment": selectedAnims = LoadAnimations(new string[] { "hiyori_m04", "hiyori_m09" }); break;
            case "surprised": selectedAnims = LoadAnimations(new string[] { "hiyori_m07" }); break;
            case "curious": selectedAnims = LoadAnimations(new string[] { "hiyori_m02" }); break;
            case "shy": selectedAnims = LoadAnimations(new string[] { "hiyori_m10" }); break;
            default: selectedAnims = LoadAnimations(new string[] { "hiyori_m01", "hiyori_m03" }); break;
        }
        PlayRandomAnimation(selectedAnims);
    }
    public static AnimationClip[] LoadAnimations(string[] animationNames) {
        return animationNames.Select(name => Resources.Load<AnimationClip>($"HiyoriMotions/{name}")).ToArray();
    }
    private void PlayRandomAnimation(AnimationClip[] clips) {
        if (clips == null || clips.Length == 0 || motionPlayer == null)
            return;
        motionPlayer.PlayMotion(clips[Random.Range(0, clips.Length)]);
    }
    private IEnumerator Cooldown() {
        isCooldown = true;
        button.interactable = false;
        yield return new WaitForSeconds(cooldownTime);
        button.interactable = true;
        isCooldown = false;
    }

    // Data Classes
    [Serializable]
    private class ServerResponse {
        public Choice[] choices;
    }

    [Serializable]
    private class Choice {
        public Message message;
    }

    [Serializable]
    public class Message {
        public string role;
        public string content;
        public override string ToString() {
            return $"role: {role}, content: {content}";
        }
    }

    [Serializable]
    public class ChatRequestBody {
        public string model;
        public Message[] messages;
        public ResponseFormat response_format;
    }

    [Serializable]
    public class ResponseFormat {
        public string type = "json_object";
    }

    [Serializable]
    public class AudioRequestBody {
        public string text;
        public string reference_id; // Changed from voice
        public string format; // Added
        public bool streaming; // Added
    }

    [Serializable]
    private class TTSResponse {
        public string display_text;
        public string tts_text;
        public string emotion;
        public string language;
    }
    
   
}
