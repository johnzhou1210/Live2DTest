using UnityEngine;

public class FaceARCamera : MonoBehaviour
{
    void Update() {
        Vector3 target = Camera.main.transform.position;
        target.y = transform.position.y;
        transform.LookAt(target);
    }
}
