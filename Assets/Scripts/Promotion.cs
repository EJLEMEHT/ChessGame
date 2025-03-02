using UnityEngine;

public class Promotion : MonoBehaviour
{
    public delegate void Action(PieceType pieceType, int side);
    public static event Action pieceChoice;
    public void OnWhiteQueenButton()
    {
        pieceChoice(PieceType.Queen, 0);
    }
    public void OnBlackQueenButton()
    {
        pieceChoice(PieceType.Queen, 1);
    }
    public void OnWhiteKnightButton()
    {
        pieceChoice(PieceType.Knight, 0);
    }
    public void OnBlackKnightButton()
    {
        pieceChoice(PieceType.Knight, 1);
    }
    public void OnWhiteBishopButton()
    {
        pieceChoice(PieceType.Bishop, 0);
    }
    public void OnBlackBishopButton()
    {
        pieceChoice(PieceType.Bishop, 1);
    }
    public void OnWhiteRookButton()
    {
        pieceChoice(PieceType.Rook, 0);
    }
    public void OnBlackRookButton()
    {
        pieceChoice(PieceType.Rook, 1);
    }
}
