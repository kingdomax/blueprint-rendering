using UnityEngine;

public class Rotateable : MonoBehaviour
{
    void Update()
    {
        this.transform.Rotate(Vector3.forward * Time.deltaTime * 10);
    }
}
