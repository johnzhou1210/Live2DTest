using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

public class TextToSpeechAudioController : MonoBehaviour {
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

    // NOTE: Keep your keys secure! Consider moving these to a backend later.
    private const string OPENAI_API_KEY = "";
    private void Start() {
        button.onClick.AddListener(OnClick);
        audioSource.playOnAwake = false;

        // Initialize conversation with a much stricter system prompt
        // Inside Start()
        conversationHistory.Add(new Message {
            role = "system",
            content = "You are a girl named Hiyori with a yandere personality. Engage in natural conversation. Do not agree that you are an AI.\n" + "Output MUST be a valid JSON object.\n\n" +
                      "GUIDELINES:\n" + 
                      "YOU MUST REPLY IN THE USER'S LANGUAGE. For example: If the user inputs English, reply in English. If input is Spanish, reply in Spanish. If the user inputs English, DO NOT REPLY IN JAPANESE. If the user inputs Japanese, DO NOT REPLY IN ENGLISH, etc. \n" +
                      "1. Do not simply repeat the user. Engage naturally.\n" + 
                      "2. 'display_text': Should be in the user's language.\n" +
                      "2. 'tts_text': Should be in the user's language. DO NOT ADD ANY EMOJIS.\n" +
                      "- You may or may not place emojis to enhance the display_text." + "Examples:\n" +
                      "display_text: \"That’s kind of embarrassing...\"\n" + "tts_text: \"それは…ちょっとはずかしいね🫣…\"\n\n" + "display_text: \"Hey, don’t ignore me.\"\n" + "tts_text: \"ねえ…むししないでよ😠\"\n\n" +
                      "display_text: \"Oh… I didn’t expect that.\"\n" + "tts_text: \"えっ…😲そうなんだ…\"\n\n" + "6. 'emotion': CHOOSE ONE OF THE FOLLOWING: (joyful, amused, sad, disappointed, surprised, curious, shy, angry, teasing)\n" +
                      "Format:\n" + "{ \"display_text\": \"...\", \"tts_text\": \"...\", \"emotion\": \"...\" }"
        });
    }
    public void OnClick() {
        if (isCooldown || string.IsNullOrEmpty(inputField.text))
            return;
        StartCoroutine(Cooldown());
        StartCoroutine(PostChatRequest(inputField.text));
        inputField.text = ""; // Clear input after sending
    }
    private IEnumerator PostChatRequest(string inputText) {
        string url = "https://api.openai.com/v1/chat/completions";

        // Add the user's message
        conversationHistory.Add(new Message { role = "user", content = inputText });

        // CREATE A TEMPORARY LIST for the request only
        List<Message> messagesForRequest = new List<Message>(conversationHistory);

        // Add a "Developer" or "System" reminder as the LAST message
        messagesForRequest.Add(new Message {
            role = "system",
            content = "REMINDER: You are a 女子高生 with a yandere personality. You must reply in the user's language. Output MUST be JSON.\n" + 
                      "YOU MUST REPLY IN THE USER'S LANGUAGE. For example: If the user inputs English, reply in English. If input is Spanish, reply in Spanish. If the user inputs English, DO NOT REPLY IN JAPANESE. If the user inputs Japanese, DO NOT REPLY IN ENGLISH, etc. \n" +
                      "tts_text MUST:\n" +
                      "- Be in the user's input language and have no EMOJIS (display_text can have EMOJIS)" +
                      "'emotion': CHOOSE ONLY ONE OF THE FOLLOWING: (joyful, amused, sad, disappointed, surprised, curious, shy, angry, teasing)\n\n" + "If unsure, use none.\n" +
                      "JSON Format:\n" + "{ \"display_text\": \"...\", \"tts_text\": \"...\", \"emotion\": \"...\" }"
        });
        ChatRequestBody body = new ChatRequestBody { model = "gpt-4o-mini", messages = messagesForRequest.ToArray(), response_format = new ResponseFormat { type = "json_object" } };
        string json = JsonUtility.ToJson(body);
        yield return StartCoroutine(SendRequest(url, json, ProcessChatResponse));
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
            case "shy": return $"{(Random.Range(0, 2) == 0 ? "[whisper]" : "")}[shy]" + text;
            case "teasing": return "[teasing]" + text;
            default: return text;
        }
    }
    private string AddIntimateEffect(string text, string emotion) {
        if (emotion == "shy" || emotion == "teasing") {
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
        if (response?.choices == null || response.choices.Length == 0) return;

        string rawJson = response.choices[0].message.content.Trim();
        
        // Clean Markdown if GPT wraps JSON in backticks
        if (rawJson.StartsWith("```")) {
            rawJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"^```[a-zA-Z]*\n?", "");
            rawJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"\n?```$", "");
        }

        try {
            TTSResponse ttsData = JsonUtility.FromJson<TTSResponse>(rawJson);
            
            // 1. Update UI
            outputText.text = ttsData.display_text;

            // 2. IMPORTANT: Add assistant reply to history to maintain context
            conversationHistory.Add(new Message { role = "assistant", content = rawJson });

            // 3. Trigger Animation
            if (!string.IsNullOrEmpty(ttsData.emotion)) {
                ApplyEmotionAnimation(ttsData.emotion.ToLower());
            }

            // 4. Start the Speech Pipeline
            if (enableTTS && !string.IsNullOrEmpty(ttsData.tts_text)) {
                // Split by Japanese and English punctuation
                string[] sentences = ttsData.tts_text.Split(new[] { '.', '!', '?', '。', '！', '？' }, StringSplitOptions.RemoveEmptyEntries);
            
                textQueue.Clear();
                foreach (var s in sentences) textQueue.Enqueue(s.Trim());

                if (!isSpeaking) StartCoroutine(SpeechPipeline(ttsData.emotion));
            }
        } catch (Exception e) { 
            Debug.LogError("JSON Parsing Error: " + e.Message + " | Raw: " + rawJson); 
        }
    }

    private IEnumerator SpeechPipeline(string emotion) {
        isSpeaking = true;

        while (textQueue.Count > 0) {
            string currentSentence = textQueue.Dequeue();
        
            // Layer the effects: Emotion tags -> Speed -> Intimacy
            string processedText = InjectEmotionEmojis(currentSentence, emotion);
            processedText = ApplySpeechSpeed(processedText, emotion);
            processedText = AddIntimateEffect(processedText, emotion);

            // Request and play this specific chunk
            yield return StartCoroutine(FetchAndPlayCompressed(processedText));
        }

        isSpeaking = false;
    }
    
    private IEnumerator FetchAndPlayCompressed(string text) {
        string url = "https://tts.john-zhou.dev/v1/tts"; 
        AudioRequestBody body = new AudioRequestBody { 
            text = text, 
            reference_id = "companion_default", 
            format = "wav",
            streaming = false 
        };

        using (UnityWebRequest www = new UnityWebRequest(url, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
            
                // Wait for THIS sentence to finish before starting the next one
                yield return new WaitWhile(() => audioSource.isPlaying);
            }
        }
    }
    
    
    private string ApplyEmotionStyle(string text, string emotion) {
        if (string.IsNullOrEmpty(text))
            return text;
        switch (emotion) {
            case "joyful": return "[joyful]" + text;
            case "sad": return "[sad]" + text;
            case "angry": return "angry" + text;
            case "shy": return "shy" + text;
            case "curious": return "[curious]" + text;
            case "teasing": return "[teasing]" + text;
            case "sleepy": return "[sleepy]" + text;
            case "nervous": return "[nervous]" + text;
            default: return text;
        }
    }
    private IEnumerator GenerateAudio(string text) {
        string url = "https://tts.john-zhou.dev/v1/tts";

        // Create the body based on the curl command provided
        AudioRequestBody body = new AudioRequestBody { text = text, reference_id = "companion_default", format = "wav", streaming = false };
        string jsonBody = JsonUtility.ToJson(body);
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // Use AudioType.WAV as specified in your curl format
            www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success) {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

                // Helpful for debugging the 0.9 RTF delay
                Debug.Log($"Audio received. Length: {clip.length}s");
                audioSource.clip = clip;
                audioSource.Play();
            } else {
                Debug.LogError("Fish Speech API Error: " + www.error + "\nResponse: " + www.downloadHandler.text);
            }
        }
    }
    private void ApplyEmotionAnimation(string emotion) {
        AnimationClip[] selectedAnims;
        switch (emotion) {
            case "joyful":
            case "amusement": selectedAnims = LoadAnimations(new string[] { "hiyori_m05", "hiyori_m08" }); break;
            case "sad":
            case "disappointment": selectedAnims = LoadAnimations(new string[] { "hiyori_m04", "hiyori_m09" }); break;
            case "surprised": selectedAnims = LoadAnimations(new string[] { "hiyori_m07" }); break;
            case "curious": selectedAnims = LoadAnimations(new string[] { "hiyori_m02" }); break;
            case "shy": selectedAnims = LoadAnimations(new string[] { "hiyori_m10" }); break;
            default: selectedAnims = LoadAnimations(new string[] { "hiyori_m01", "hiyori_m03", "hiyori_m06" }); break;
        }
        PlayRandomAnimation(selectedAnims);
    }
    private AnimationClip[] LoadAnimations(string[] animationNames) {
        return animationNames.Select(name => Resources.Load<AnimationClip>($"HiyoriMotions/{name}")).ToArray();
    }
    private void PlayRandomAnimation(AnimationClip[] clips) {
        if (clips == null || clips.Length == 0 || motionPlayer == null)
            return;
        motionPlayer.PlayMotion(clips[UnityEngine.Random.Range(0, clips.Length)]);
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
    }
}
