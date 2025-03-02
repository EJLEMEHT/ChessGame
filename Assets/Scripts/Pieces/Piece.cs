using UnityEngine;
using System.Collections.Generic;

public enum PieceType
{
    None = 0, 
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6
}

public class Piece : MonoBehaviour
{
    public int side;
    public int currentX;
    public int currentY;
    public PieceType type;
    private Vector3 desiredPosition;

    private void Start()
    {
        transform.rotation = Quaternion.Euler((side == 0) ? new Vector3(0, -90, 0) : new Vector3(0, 90, 0));
    }
    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 10);
    }

    public virtual List<Vector2Int> GetAvailableMoves(ref Piece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        r.Add(new Vector2Int(3, 3));
        r.Add(new Vector2Int(4, 4));

        return r;
    }

    public virtual SpecialMove GetSpecialMoves(ref Piece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        return SpecialMove.None;
    }
    public virtual void SetPosition(Vector3 position, bool instant = false)
    {
        desiredPosition = position;
        if (instant)
        {
            transform.position = desiredPosition;
        }
    }
}
