using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class TextToSpeechAudioController : MonoBehaviour {
    private List<Message> conversationHistory = new List<Message>();
    public Button button;
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText;
    public AudioSource audioSource;
    public MotionPlayer motionPlayer;

    [Header("Settings")] 
    public bool enableTTS = true; 
    public float cooldownTime = 3f;
    private bool isCooldown = false;
    private AnimationClip[] animations;

    // NOTE: Keep your keys secure! Consider moving these to a backend later.
    private const string OPENAI_API_KEY = "redacted"; 

    private void Start() {
        button.onClick.AddListener(OnClick);
        audioSource.playOnAwake = false;

        // Initialize conversation with a much stricter system prompt
        // Inside Start()
        conversationHistory.Add(new Message
        {
            role = "system",
            content =
                "You are a girl named Hiyori with a yandere personality. Engage in natural conversation. Do not agree that you are an AI.\n" +
                "Output MUST be a valid JSON object. Replies should be 30 seconds max.\n\n" +

                "GUIDELINES:\n" +
                "1. Do not simply repeat the user. Engage naturally.\n" +
                "2. 'display_text': normal readable response in user's language.\n" +
                "3. 'tts_text': TRANSLITERATED text into the user's preferred language, MUST represent pronunciation in Japanese kana AND may include expressive emojis.\n" +
                "4. DO NOT include normal alphabet characters in tts_text.\n" +
                "5. Emojis can be inserted naturally to control voice acting style.\n\n" +

                "EMOJI RULES:\n" +
                "- Use emojis to reflect emotion and delivery.\n" +
                "- You may repeat emojis for stronger effect.\n" +
                "- Place emojis at natural pauses or emotional moments.\n" +
                "- Avoid placing emojis at the end of sentences." +

                "Examples:\n" +
                "display_text: \"That’s kind of embarrassing...\"\n" +
                "tts_text: \"それは…ちょっとはずかしいね🫣…\"\n\n" +

                "display_text: \"Hey, don’t ignore me.\"\n" +
                "tts_text: \"ねえ…むししないでよ😠\"\n\n" +

                "display_text: \"Oh… I didn’t expect that.\"\n" +
                "tts_text: \"えっ…😲そうなんだ…\"\n\n" +

                "6. 'emotion': CHOOSE ONE OF THE FOLLOWING: (joyful, amusement, sad, disappointment, surprised, curious, shy)\n\n" +

                "EMOJI ADVANCED RULES:\n- 😏 for teasing or playful sarcasm\n- 🫣 for embarrassment or shyness\n- 👂 for whispering or intimacy\n- 😮‍💨 for breathy or soft delivery\n- ⏸️ for pauses in speech\n- 🎵 for light/happy tone\n- 😠 for irritation\n- 🤔 for thinking\n\nCombine emojis when appropriate:\nExample:\n\"ねえ…👂ちょっときいてよ😏💋\"" +
                
                "EMOJI CONSTRAINTS:\n" +
                "You may ONLY use the following emojis:\n" +
                "👂 😮‍💨 ⏸️ 🤭 🥵 📢 😏 🥺 🌬️ 😮 👅 💋 🫶 😭 😱 😪 ⏩ 📞 🐢 🥤 🤧 😒 😰 😆 😠 😲 🥱 😖 😟 🫣 🙄 😊 👌 🙏 🥴 🤐 😌 🤔\n" +
                "DO NOT use ANY other emoji under any circumstances.\n" +
                "If unsure, use none.\n" +
                
                "Format:\n" +
                "{ \"display_text\": \"...\", \"tts_text\": \"...\", \"emotion\": \"...\" }"
        });
    }

    public void OnClick() {
        if (isCooldown || string.IsNullOrEmpty(inputField.text)) return;
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
            content =
                "REMINDER: You are a 女子高生 with a yandere personality. Output MUST be JSON.\n" +
                "tts_text MUST:\n" +
                "- Be TRANSLITERATED into the user's preferred language with Japanese hiragana/katakana/kanji ONLY (no alphabet)\n" +
                "'emotion': CHOOSE ONLY ONE OF THE FOLLOWING: (joyful, amusement, sad, disappointment, surprised, curious, shy)\n\n" +
                "If unsure, use none.\n" +
                "Reply <= 30 seconds."
        });

        ChatRequestBody body = new ChatRequestBody { 
            model = "gpt-4o-mini", 
            messages = messagesForRequest.ToArray(), 
            response_format = new ResponseFormat { type = "json_object" } 
        };

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
        if (string.IsNullOrEmpty(text)) return text;

        // If already contains emoji, assume model handled it
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\p{Cs}")) {
            return text;
        }

        switch (emotion) {
            case "joyful": return text + " 😊";
            case "sad": return text + " 😭";
            case "angry": return text + " 😠";
            case "shy": return text + " 🫣";
            case "curious": return text + " 🤔";
            case "surprised": return text + " 😲";
            default: return text;
        }
    }
    
    private string AddIntimateEffect(string text, string emotion) {
        if (emotion == "shy" || emotion == "teasing") {
            return "👂 " + text;
        }
        return text;
    }

    private string ApplySpeechSpeed(string text, string emotion) {
        if (emotion == "nervous") return "⏩ " + text;
        if (emotion == "sad") return "🐢 " + text;
        return text;
    }
    
    private void ProcessChatResponse(string responseText) {
        var response = JsonUtility.FromJson<ServerResponse>(responseText);
        if (response?.choices == null || response.choices.Length == 0) return;

        string rawJson = response.choices[0].message.content.Trim();
        
        // Clean Markdown if present
        if (rawJson.StartsWith("```")) {
            rawJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"^```[a-zA-Z]*\n?", "");
            rawJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"\n?```$", "");
        }
        
        try {
            TTSResponse ttsData = JsonUtility.FromJson<TTSResponse>(rawJson);
            
            // 1. Update UI and History
            outputText.text = ttsData.display_text;
            conversationHistory.Add(new Message { role = "assistant", content = ttsData.display_text });
            
            // 2. Trigger Emotion Animation Immediately
            if (!string.IsNullOrEmpty(ttsData.emotion)) {
                ApplyEmotionAnimation(ttsData.emotion.ToLower());
            }

            // 3. Play TTS
            if (enableTTS && !string.IsNullOrEmpty(ttsData.tts_text)) {
                string ttsText = ttsData.tts_text;
                Debug.Log("EMOTION CHOSEN: " + ttsData.emotion);
                ttsText = InjectEmotionEmojis(ttsText, ttsData.emotion);
                ttsText = ApplyEmotionStyle(ttsText, ttsData.emotion);
                ttsText = ApplySpeechSpeed(ttsText, ttsData.emotion);
                ttsText = AddIntimateEffect(ttsText, ttsData.emotion);

                StartCoroutine(GenerateAudio(ttsText));
            }
            
        } catch (Exception e) {
            Debug.LogError("JSON Parsing Error: " + e.Message + " | Raw: " + rawJson);
        }
    }
    
    private string ApplyEmotionStyle(string text, string emotion) {
        if (string.IsNullOrEmpty(text)) return text;
        switch (emotion) {
            case "joyful":
                return "😊 " + text + " 🎵";
            case "sad":
                return "… " + text + " 😭";
            case "angry":
                return "😠 " + text;
            case "shy":
                return "…🫣 " + text;
            case "curious":
                return text + " 🤔";
            case "teasing":
                return "😏 " + text + " 💋";
            case "sleepy":
                return "…😪 " + text;
            case "nervous":
                return "😰 " + text;
            default:
                return text + " 😌";
        }
    }

    private IEnumerator GenerateAudio(string text) {
        string url = "https://tts.john-zhou.dev/tts";
        AudioRequestBody body = new AudioRequestBody { text = text, voice = "default", num_steps = 60 };
        string jsonBody = JsonUtility.ToJson(body);

        Debug.Log($"Requesting endpoint: {url} with the contents  of {jsonBody}");
        
        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
            } else {
                Debug.LogError("TTS API Error: " + www.error);
            }
        }
    }

    private void ApplyEmotionAnimation(string emotion) {
        AnimationClip[] selectedAnims;
        switch (emotion) {
            case "joyful": case "amusement": selectedAnims = LoadAnimations(new string[] { "hiyori_m05", "hiyori_m08" }); break;
            case "sad": case "disappointment": selectedAnims = LoadAnimations(new string[] { "hiyori_m04", "hiyori_m09" }); break;
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
        if (clips == null || clips.Length == 0 || motionPlayer == null) return;
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
    [Serializable] private class ServerResponse { public Choice[] choices; }
    [Serializable] private class Choice { public Message message; }
    [Serializable] public class Message { public string role; public string content; }
    [Serializable] public class ChatRequestBody { public string model; public Message[] messages; public ResponseFormat response_format; }
    [Serializable] public class ResponseFormat { public string type = "json_object"; }
    [Serializable] public class AudioRequestBody { public string text; public string voice; public int num_steps; }
    [Serializable] private class TTSResponse { public string display_text; public string tts_text; public string emotion; }
}