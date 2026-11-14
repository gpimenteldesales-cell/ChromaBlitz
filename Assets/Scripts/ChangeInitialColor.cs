using UnityEngine;

public class ChangeInitialColor : MonoBehaviour
{
    
    void Start()
    {
        GetComponent<Renderer>().material.color = Random.ColorHSV();
    }

}
