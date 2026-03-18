using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class TextToSpeechAudioController : MonoBehaviour
{
    private List<Message> conversationHistory = new List<Message>();
    
    public Button button;
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText;
    public AudioSource audioSource;
    public MotionPlayer motionPlayer;
    
    [Header("Settings")]
    public bool enableTTS = false; // 🔥 disable for testing to avoid 429
    public float cooldownTime = 3f;

    private bool isCooldown = false;
    private AnimationClip[] animations;

    private const string OPENAI_API_KEY = "redacted";

    private void Start()
    {
        Debug.Log("Start called. Button: " + button);
        button.onClick.AddListener(OnClick);
        audioSource.playOnAwake = false;
        
        // Init animations
        animations = new AnimationClip[]
        {
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m01"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m02"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m03"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m04"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m05"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m06"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m07"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m08"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m09"),
            Resources.Load<AnimationClip>("HiyoriMotions/hiyori_m10")
        };

        // Initialize conversation with a system prompt
        conversationHistory.Add(new Message
        {
            role = "system",
            content = "You are a helpful assistant. Remember previous messages in this conversation."
        });
    }

    public void OnClick()
    {
        Debug.Log("Button clicked!");
        if (isCooldown) return;

        StartCoroutine(Cooldown());

        string input = inputField.text;
        Debug.Log("Input: " + input);
        StartCoroutine(PostChatRequest(input));
    }

    // =========================
    // CHAT REQUEST
    // =========================
    private IEnumerator PostChatRequest(string inputText)
    {
        string url = "https://api.openai.com/v1/chat/completions";

        // Add the user's new message to the history
        conversationHistory.Add(new Message
        {
            role = "user",
            content = inputText
        });

        ChatRequestBody body = new ChatRequestBody
        {
            model = "gpt-3.5-turbo",
            messages = conversationHistory.ToArray()
        };

        string json = JsonUtility.ToJson(body);

        yield return StartCoroutine(SendRequest(url, json, ProcessChatResponse));
    }

    // =========================
    // GENERIC REQUEST HANDLER
    // =========================
    private IEnumerator SendRequest(string url, string jsonBody, System.Action<string> onSuccess, int retryCount = 0)
    {
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();

            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + OPENAI_API_KEY);

            Debug.Log("Sending request to: " + url);
            yield return www.SendWebRequest();

            Debug.Log($"Request finished. Response code: {www.responseCode}");
            Debug.Log("Response body: " + www.downloadHandler.text);

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Request failed: " + www.error);
            }
            else
            {
                Debug.Log("Request succeeded. Passing to ProcessChatResponse");
                onSuccess?.Invoke(www.downloadHandler.text);
            }
        }
    }

    // =========================
    // PROCESS CHAT RESPONSE
    // =========================
    private void ProcessChatResponse(string responseText)
    {
        Debug.Log("=== RAW API RESPONSE ===");
        Debug.Log(responseText);

        var response = JsonUtility.FromJson<ServerResponse>(responseText);

        if (response != null &&
            response.choices != null &&
            response.choices.Length > 0 &&
            response.choices[0].message != null)
        {
            string text = response.choices[0].message.content;

            // Add assistant response to history
            conversationHistory.Add(new Message
            {
                role = "assistant",
                content = text
            });
            
            // Show emotion
            StartCoroutine(PostEmotion(text));

            // Start TTS coroutine
            if (enableTTS)
                StartCoroutine(GenerateAudioAndShowText(text));
            else
                outputText.text = text;
        }
        else
        {
            Debug.LogError("Invalid response format.");
        }
    }

    // =========================
    // TTS
    // =========================
    private IEnumerator GenerateAudioAndShowText(string text)
    {
        string url = "https://api.openai.com/v1/audio/speech";

        AudioRequestBody body = new AudioRequestBody
        {
            model = "tts-1",
            input = text,
            voice = "nova"
        };

        string jsonBody = JsonUtility.ToJson(body);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);

            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + OPENAI_API_KEY);

            Debug.Log("Requesting TTS...");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Error: " + www.error + "\n" + www.downloadHandler.text);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

            audioSource.clip = clip;
            audioSource.pitch = 1.0f;
            audioSource.Play();

            outputText.text = text; // Show text at the same time audio starts
        }
    }
    
    // ======================
    // EMOTIONS
    /*
     * 1. Ambivalent
     * 2. Curious
     * 3. Contemplative
     * 4. Sad
     * 5. Joyful
     * 6. Validation
     * 7. Surprised
     * 8. Amusement
     * 9. Disappointment
     * 10. Shy
     * 
     */
    // ======================
    private IEnumerator PostEmotion(string inputText) {
        string chatApiUrl = "https://api.openai.com/v1/chat/completions";
        ChatRequestBody requestBody = new ChatRequestBody {
            model = "gpt-3.5-turbo",
            messages = new Message[] {
                new Message {
                    role = "user",
                    content =
                        "Please select the emotion you can think of from this text from the emotion list. ###Output format:{the word of chosen emotion}###emotion list=[ambivalent,curious,contemplative,sad,joyful,validation,surprised,amusement,disappointment,shy]###this sentence=" +
                        inputText
                }
            }
        };
        Debug.Log($"Sent emotion classification request: {JsonUtility.ToJson(requestBody)}");
        using (UnityWebRequest www = new UnityWebRequest(chatApiUrl, "POST")) {
          byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
          www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
          www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
          www.SetRequestHeader("Content-Type", "application/json");
          www.SetRequestHeader("Authorization", "Bearer " + OPENAI_API_KEY);
          yield return www.SendWebRequest();
          if (www.result != UnityWebRequest.Result.Success) {
              Debug.LogError("Chat API Error: " + www.error);
              Debug.LogError("Response: " + www.downloadHandler.text);
          } else {
              var serverResponse = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);
              string outputText = serverResponse.choices[0].message.content.ToLower();
              Debug.Log("Emotion chosen: " + outputText);
              if (animations != null && animations.Length > 0) {
                  switch (outputText) {
                      case var _ when outputText.Contains("ambivalent"):
                          animations = LoadAnimations(new string[] { "hiyori_m01", "hiyori_m03"});
                          break;
                      case var _ when outputText.Contains("curious"):
                          animations = LoadAnimations(new string[] { "hiyori_m02" });
                          break;
                      case var _ when outputText.Contains("contemplative"):
                          animations = LoadAnimations(new string[]{"hiyori_m01", "hiyori_m03"});
                          break;
                      case var _ when outputText.Contains("sad"):
                          animations = LoadAnimations(new string[] { "hiyori_m04" });
                          break;
                      case var _ when outputText.Contains("joyful"):
                          animations = LoadAnimations(new string[] { "hiyori_m05", "hiyori_m08" });
                          break;
                      case var _ when outputText.Contains("validation"):
                          animations = LoadAnimations(new string[]{"hiyori_m06"});
                          break;
                      case var _ when outputText.Contains("surprised"):
                          animations = LoadAnimations(new string[] { "hiyori_m07" });
                          break;
                      case var _ when outputText.Contains("amusement"):
                          animations = LoadAnimations(new string[] { "hiyori_m08" });
                          break;
                      case var _ when outputText.Contains("disappointment"):
                          animations = LoadAnimations(new string[] { "hiyori_m09" });
                          break;
                      case var _ when outputText.Contains("shy"):
                          animations = LoadAnimations(new string[] { "hiyori_m10" });
                          break;
                  }
                  PlayRandomAnimation(animations);
                  yield return null;
              } else {
                  Debug.LogError("Animations not loaded or empty in ButtonClickHandler.");
                  yield return null;
              }
          }
        } 
    }

    private AnimationClip[] LoadAnimations(string[] animationNames) {
        return animationNames.Select(name => Resources.Load<AnimationClip>($"HiyoriMotions/{name}")).ToArray();
    }

    private void PlayRandomAnimation(AnimationClip[] animationClips) {
        if (animations == null || animations.Length == 0)
        {
            Debug.LogError("Animations array is null or empty.");
            return;
        }
        if (motionPlayer == null)
        {
            Debug.LogError("MotionPlayer is not initialized.");
            return;
        }
        int randomIndex = UnityEngine.Random.Range(0, animations.Length);
        motionPlayer.PlayMotion(animations[randomIndex]);
    }

    // =========================
    // COOLDOWN
    // =========================
    private IEnumerator Cooldown()
    {
        isCooldown = true;
        button.interactable = false;

        yield return new WaitForSeconds(cooldownTime);

        button.interactable = true;
        isCooldown = false;
    }

    // =========================
    // DATA CLASSES
    // =========================
    [System.Serializable]
    private class ServerResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public Message message;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ChatRequestBody
    {
        public string model;
        public Message[] messages;
    }

    [System.Serializable]
    public class AudioRequestBody
    {
        public string model;
        public string input;
        public string voice;
    }
}