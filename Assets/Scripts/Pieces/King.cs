using System.Collections.Generic;
using UnityEngine;

public class King : Piece
{
    public override List<Vector2Int> GetAvailableMoves(ref Piece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        // Right
        if (currentX + 1 < tileCountX)
        {
            // Right
            if (board[currentX + 1, currentY] == null)
                r.Add(new Vector2Int(currentX + 1, currentY));
            else if (board[currentX + 1, currentY].side != side)
                r.Add(new Vector2Int(currentX + 1, currentY));

            // Top Right
            if (currentY + 1 < tileCountY)
            {
                if (board[currentX + 1, currentY + 1] == null)
                    r.Add(new Vector2Int(currentX + 1, currentY + 1));
                else if (board[currentX + 1, currentY + 1].side != side)
                    r.Add(new Vector2Int(currentX + 1, currentY + 1));
            }

            // Bottom Right
            if (currentY - 1 >= 0)
            {
                if (board[currentX + 1, currentY - 1] == null)
                    r.Add(new Vector2Int(currentX + 1, currentY - 1));
                else if (board[currentX + 1, currentY - 1].side != side)
                    r.Add(new Vector2Int(currentX + 1, currentY - 1));
            }
        }

        // Left
        if (currentX - 1 >= 0)
        {
            // Left
            if (board[currentX - 1, currentY] == null)
                r.Add(new Vector2Int(currentX - 1, currentY));
            else if (board[currentX - 1, currentY].side != side)
                r.Add(new Vector2Int(currentX - 1, currentY));

            // Top Left
            if (currentY + 1 < tileCountY)
            {
                if (board[currentX - 1, currentY + 1] == null)
                    r.Add(new Vector2Int(currentX - 1, currentY + 1));
                else if (board[currentX - 1, currentY + 1].side != side)
                    r.Add(new Vector2Int(currentX - 1, currentY + 1));
            }

            // Bottom Left
            if (currentY - 1 >= 0)
            {
                if (board[currentX - 1, currentY - 1] == null)
                    r.Add(new Vector2Int(currentX - 1, currentY - 1));
                else if (board[currentX - 1, currentY - 1].side != side)
                    r.Add(new Vector2Int(currentX - 1, currentY - 1));
            }
        }

        // Up
        if (currentY + 1 < tileCountY)
            if (board[currentX, currentY + 1] == null)
                r.Add(new Vector2Int(currentX, currentY + 1));
            else if (board[currentX, currentY + 1].side != side)
                r.Add(new Vector2Int(currentX, currentY + 1));

        // Down
        if (currentY - 1 >= 0)
            if (board[currentX, currentY - 1] == null)
                r.Add(new Vector2Int(currentX, currentY - 1));
            else if (board[currentX, currentY - 1].side != side)
                r.Add(new Vector2Int(currentX, currentY - 1));

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref Piece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        SpecialMove r = SpecialMove.None;
        bool isKingMoved = moveList.Find(m => m[0].x == 4 && m[0].y == ((side == 0) ? 0 : 7)) != null;
        bool isLeftRookMoved = moveList.Find(m => m[0].x == 0 && m[0].y == ((side == 0) ? 0 : 7)) != null;
        bool isRightRookMoved = moveList.Find(m => m[0].x == 7 && m[0].y == ((side == 0) ? 0 : 7)) != null;
        if (!isKingMoved)
        {
            // White team
            if (side  == 0)
            {
                // Left Rook
                if (!isLeftRookMoved)
                    if (board[3, 0] == null && board[2, 0] == null && board[1, 0] == null)
                    {
                        availableMoves.Add(new Vector2Int(2, 0));
                        r = SpecialMove.Castling;
                    }

                // Right Rook
                if (!isRightRookMoved)
                    if (board[5, 0] == null && board[6, 0] == null)
                    {
                        availableMoves.Add(new Vector2Int(6, 0));
                        r = SpecialMove.Castling;
                    }

            }
            else
            {
                // Left Rook
                if (!isLeftRookMoved)
                    if (board[3, 7] == null && board[2, 7] == null && board[1, 7] == null)
                    {
                        availableMoves.Add(new Vector2Int(2, 7));
                        r = SpecialMove.Castling;
                    }

                // Right Rook
                if (!isRightRookMoved)
                    if (board[5, 7] == null && board[6, 7] == null)
                    {
                        availableMoves.Add(new Vector2Int(6, 7));
                        r = SpecialMove.Castling;
                    }
            }
        }

        return r;
    }
}
