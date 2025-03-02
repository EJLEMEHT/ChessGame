using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    public override List<Vector2Int> GetAvailableMoves(ref Piece[,] board, int tileCountX, int tileCountY)
    {
        int direction = (side == 0) ? 1 : -1;

        List<Vector2Int> r = new List<Vector2Int>();

        // One step front
        if (board[currentX, currentY + direction] == null)
            r.Add(new Vector2Int(currentX, currentY + direction));

        // Two step front
        if (board[currentX, currentY + direction] == null)
        {
            if (side == 0 && currentY == 1 && board[currentX, currentY + direction * 2] == null)
                r.Add(new Vector2Int(currentX, currentY + direction * 2));
            if (side == 1 && currentY == 6 && board[currentX, currentY + direction * 2] == null)
                r.Add(new Vector2Int(currentX, currentY + direction * 2));
        }

        // Kill move
        if (currentX != tileCountX - 1)
            if (board[currentX + 1, currentY + direction] != null && board[currentX + 1, currentY + direction].side != side)
                r.Add(new Vector2Int(currentX + 1, currentY + direction));
        if (currentX != 0)
            if (board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].side != side)
                r.Add(new Vector2Int(currentX - 1, currentY + direction));
        return r;
    }

    public override SpecialMove GetSpecialMoves(ref Piece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        int direction = (side == 0) ? 1 : -1;
        if ((side == 0 && currentY == 6) || (side == 1 && currentY == 1))
            return SpecialMove.Promotion;

        // En Passant
        if (moveList.Count > 0)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            if (board[lastMove[1].x, lastMove[1].y] != null && board[lastMove[1].x, lastMove[1].y].type == PieceType.Pawn) // If the last piece moved was a pawn
            {
                if (Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2) // If the last move was a +2 in either direction
                {
                    if (board[lastMove[1].x, lastMove[1].y].side != side) // If pawn is an enemy
                    {
                        if (lastMove[1].y == currentY) // If enemy pawn is on the same Y
                        {
                            if (lastMove[1].x == currentX - 1) // Landed Left
                            {
                                availableMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                            if (lastMove[1].x == currentX + 1) // Landed Right
                            {
                                availableMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                        }
                    }
                }
            }
        }
        return SpecialMove.None;
    }
}
