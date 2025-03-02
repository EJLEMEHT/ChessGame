using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI : MonoBehaviour
{
    public static UI Instance { get; set; }
    void Awake()
    {
        Instance = this;
        Instance.gameObject.AddComponent<CanvasGroup>();
    }
}
