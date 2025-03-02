using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [SerializeField] private AudioClip[] sounds;
    public AudioSource a;
    public void Awake()
    {
        but_col.butonHovered += HoverSound;
        Board.buttonClicked += ClickSound;
        Board.check += CheckSound;
        Board.checkMate += MateSound;
        Board.pieceKilled += KillSound;
        Board.wrongMove += WrongSound;
        Board.pieceMoved += MoveSound;
    }
    public void CheckSound()
    {
        a.clip = sounds[0];
        a.Play();
    }
    public void MateSound()
    {
        a.clip = sounds[1];
        a.Play();
    }
    public void KillSound()
    {
        a.clip = sounds[2];
        a.Play();
    }
    public void WrongSound()
    {
        if (!a.isPlaying)
        {
            a.clip = sounds[3];
            a.Play();
        }
    }
    public void MoveSound()
    {
        if (!a.isPlaying)
        {
            a.clip = sounds[4];
            a.Play();
        }
    }
    public void HoverSound()
    {
        if (!a.isPlaying)
        {
            a.clip = sounds[5];
            a.Play();
        }
    }
    public void ClickSound()
    {
        if (!a.isPlaying)
        {
            a.clip = sounds[6];
            a.Play();
        }
    }
}
