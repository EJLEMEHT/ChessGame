using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class but_col : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [SerializeField] private Button but;
    [SerializeField] private Color old;
    [SerializeField] public Color neww;
    [SerializeField] public Color text_old;
    [SerializeField] public Color text_neww;

    public delegate void Action();
    public static event Action butonHovered;

    void Start()
    {
        but = this.GetComponent<Button>();
        old = but.GetComponent<Image>().color;
        text_old = but.GetComponentInChildren<Text>().color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        but.GetComponent<Image>().color = neww;
        but.GetComponentInChildren<Text>().color = text_neww;
        butonHovered();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        but.GetComponent<Image>().color = old;
        but.GetComponentInChildren<Text>().color = text_old;
    }
}