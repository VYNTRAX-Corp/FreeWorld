using UnityEngine;

// tiny helper to rotate an object around the Y axis
public class SimpleRotator : MonoBehaviour
{
    public float speed = 30f;
    void Update() => transform.Rotate(0f, speed * Time.deltaTime, 0f);
}
