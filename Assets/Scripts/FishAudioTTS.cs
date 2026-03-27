using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class FishAudioTTS : MonoBehaviour
{
    [Header("Audio & Settings")]
    public AudioSource audioSource;
    [TextArea] public string textToSpeak = "Hello, welcome to Unity!";
    public string apiKey = "7d25349f8593479f8ff25e697611ed35"; // ⚠️ Use a secure method in production
    public string model = "s2-pro";
    public string format = "mp3";

    public void Speak()
    {
        StartCoroutine(RequestTTS(textToSpeak));
    }

    private IEnumerator RequestTTS(string text)
    {
        string url = "https://api.fish.audio/v1/tts";

        // Prepare JSON body
        string jsonBody = JsonUtility.ToJson(new
        {
            text = text,
            format = format,
            model = model
        });

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();

            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Error: " + www.error + "\n" + www.downloadHandler.text);
            }
            else
            {
                byte[] audioData = www.downloadHandler.data;

                // Convert MP3 to AudioClip using NAudio / UnityWebRequestMultimedia (Unity 2022+)
                StartCoroutine(PlayAudioFromData(audioData));
            }
        }
    }

    private IEnumerator PlayAudioFromData(byte[] audioData)
    {
        // Save to temporary file
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "tts.mp3");
        System.IO.File.WriteAllBytes(tempPath, audioData);

        using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("AudioClip load failed: " + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}