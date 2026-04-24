using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;

public class ARPlacementController : MonoBehaviour
{
    public GameObject hiyoriModel; // Drag your Live2D Prefab here
    private ARRaycastManager _raycastManager;
    private static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
    {
        // 1. Check for finger touch
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (!EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) {
                    // 2. Raycast from touch position against detected planes
                    if (_raycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon))
                    {
                        // 3. Get the "Pose" (Position + Rotation) of the hit point
                        Pose hitPose = s_Hits[0].pose;

                        // 4. Move Hiyori to that spot
                        hiyoriModel.transform.position = hitPose.position;
                    
                        // Keep her upright but facing the direction of the plane's 'forward'
                        hiyoriModel.transform.rotation = hitPose.rotation;
                    
                        // 5. Trigger a greeting motion (2,5, or 8)
                        AnimationClip[] animations = TextToSpeechAudioController.LoadAnimations(new string[] { "hiyori_m02", "hiyori_m05", "hiyori_m08" });
                        hiyoriModel.GetComponent<MotionPlayer>().PlayMotion(animations[Random.Range(0, animations.Length)]);
                    }
                }
                
               
            }
        }
    }
}
