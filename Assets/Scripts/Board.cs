using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Xml.Serialization;
using System.IO;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public enum Mode
{
    None = 0,
    Classic,
    FirstCheck,
    Atomic,
    Problems
}
public enum CameraAngle
{
    menu = 0,
    white = 1,
    black = 2,
    start = 3
}

public class Board : MonoBehaviour
{
    [Header("Art Stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float boardHeight = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject promotionMenu;
    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject[] cameraAngles;
    [SerializeField] private GameObject blackTimer;
    [SerializeField] private GameObject whiteTimer;
    [SerializeField] private GameObject inGameUI;
    [SerializeField] private GameObject gameUI;

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] sideMaterials;

    // Sound
    public delegate void Action();
    public static event Action buttonClicked;
    public static event Action checkMate;
    public static event Action check;
    public static event Action pieceMoved;
    public static event Action pieceKilled;
    public static event Action wrongMove;

    private Piece[,] pieces;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private Piece currentlyDragging;
    private List<Piece> deadWhites = new List<Piece>();
    private List<Piece> deadBlacks = new List<Piece>();
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover = -Vector2Int.one; // current mouse hovering tile
    private Vector3 bounds;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private Mode mode;
    private float timerValueBlack;
    private float timerValueWhite;
    private bool inGame;
    private bool isChoosingPiece;
    private int START_TIMER_VALUE = 900;

    // Problems mode
    List<string> solution;
    string problemName;

    private void Awake()
    {
        Promotion.pieceChoice += ProcessPromotion;

        inGameUI.SetActive(false);

        inGame = false;

        isWhiteTurn = true;

        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        GenerateAllPieces();
        PositionaAllPieces();
        StartCoroutine(ChangeCameraWithDelay(CameraAngle.menu));
    }

    private void Update()
    {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }
        if (!inGame)
        {
            return;
        }

        // Timer
        if (mode != Mode.Problems)
        {
            if (!isWhiteTurn)
            {
                if (timerValueBlack - Time.deltaTime > 0)
                {
                    timerValueBlack -= Time.deltaTime;
                    float min = Mathf.FloorToInt(timerValueBlack / 60);
                    float sec = Mathf.FloorToInt(timerValueBlack % 60);
                    blackTimer.GetComponent<UnityEngine.UI.Text>().text = string.Format("{0,00}:{1,00}", min, sec < 10 ? $"0{sec}" : sec);
                }
                else
                    CheckMate(0);
            }
            else
            {
                if (timerValueWhite - Time.deltaTime > 0)
                {
                    timerValueWhite -= Time.deltaTime;
                    float min = Mathf.FloorToInt(timerValueWhite / 60);
                    float sec = Mathf.FloorToInt(timerValueWhite % 60);
                    whiteTimer.GetComponent<UnityEngine.UI.Text>().text = string.Format("{0,00}:{1,00}", min, sec < 10 ? $"0{sec}" : sec);
                }
                else
                    CheckMate(1);
            }
        }

        if (isChoosingPiece)
            return;

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight", "Wrong")) && inGame)
        {
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            // if we're hovering a tile after not hovering any tiles
            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // if we were already hovering a tile, change the previous one
            if (currentHover != hitPosition && tiles[hitPosition.x, hitPosition.y].layer != LayerMask.NameToLayer("Wrong"))
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }
            // if we click on the mouse
            if (Input.GetMouseButtonDown(0))
            {
                if (currentlyDragging == null)
                {
                    if (pieces[hitPosition.x, hitPosition.y] != null)
                    {
                        if (pieces[hitPosition.x, hitPosition.y].side == 0 && isWhiteTurn || pieces[hitPosition.x, hitPosition.y].side == 1 && !isWhiteTurn)
                        {
                            currentlyDragging = pieces[hitPosition.x, hitPosition.y];

                            availableMoves = currentlyDragging.GetAvailableMoves(ref pieces, TILE_COUNT_X, TILE_COUNT_Y);
                            specialMove = currentlyDragging.GetSpecialMoves(ref pieces, ref moveList, ref availableMoves);
                            if (mode != Mode.Atomic)
                                PreventCheck();
                            HighlightTiles();
                        }
                    }
                }
                else
                {
                    Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                    bool validMove = MoveTo(currentlyDragging, hitPosition.x, hitPosition.y);

                    if (validMove && mode == Mode.Problems && solution.Count != 0) // auto move
                    {
                        Vector2Int[] solutionMove = NotationToMove(solution[0], currentlyDragging.side == 0 ? 1 : 0);
                        MoveTo(pieces[solutionMove[0].x, solutionMove[0].y], solutionMove[1].x, solutionMove[1].y, true);
                        solution.RemoveAt(0);
                    }

                    int curDragSide = currentlyDragging.side;
                    currentlyDragging = null;
                    RemoveHighlightTiles();
                    if (!validMove) 
                    {
                        if (pieces[hitPosition.x, hitPosition.y] != null && pieces[hitPosition.x, hitPosition.y].side == curDragSide)
                        {
                            currentlyDragging = pieces[hitPosition.x, hitPosition.y];
                            availableMoves = currentlyDragging.GetAvailableMoves(ref pieces, TILE_COUNT_X, TILE_COUNT_Y);
                            specialMove = currentlyDragging.GetSpecialMoves(ref pieces, ref moveList, ref availableMoves);
                            if (mode != Mode.Atomic)
                                PreventCheck();
                            HighlightTiles();
                        }
                    }
                }
            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }
            if (currentlyDragging != null && Input.GetMouseButtonDown(0))
            {
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }

        // if we're dragging a piece
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            if (horizontalPlane.Raycast(ray, out float distance))
                ray.GetPoint(distance);
        }
    }
    // Board Gen
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)


    {
        GameObject tileObject = new GameObject($"Tile[{x}, {y}]");
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    // Piece Gen
    private void GenerateAllPieces()
    {
        pieces = new Piece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteSide = 0;
        int blackSide = 1;

        // white
        pieces[0, 0] = GenerateSinglePiece(PieceType.Rook, whiteSide);
        pieces[1, 0] = GenerateSinglePiece(PieceType.Knight, whiteSide);
        pieces[2, 0] = GenerateSinglePiece(PieceType.Bishop, whiteSide);
        pieces[3, 0] = GenerateSinglePiece(PieceType.Queen, whiteSide);
        pieces[4, 0] = GenerateSinglePiece(PieceType.King, whiteSide);
        pieces[5, 0] = GenerateSinglePiece(PieceType.Bishop, whiteSide);
        pieces[6, 0] = GenerateSinglePiece(PieceType.Knight, whiteSide);
        pieces[7, 0] = GenerateSinglePiece(PieceType.Rook, whiteSide);
        for (int x = 0; x < TILE_COUNT_X; x++)
            pieces[x, 1] = GenerateSinglePiece(PieceType.Pawn, whiteSide);
        // black
        pieces[0, 7] = GenerateSinglePiece(PieceType.Rook, blackSide);
        pieces[1, 7] = GenerateSinglePiece(PieceType.Knight, blackSide);
        pieces[2, 7] = GenerateSinglePiece(PieceType.Bishop, blackSide);
        pieces[3, 7] = GenerateSinglePiece(PieceType.Queen, blackSide);
        pieces[4, 7] = GenerateSinglePiece(PieceType.King, blackSide);
        pieces[5, 7] = GenerateSinglePiece(PieceType.Bishop, blackSide);
        pieces[6, 7] = GenerateSinglePiece(PieceType.Knight, blackSide);
        pieces[7, 7] = GenerateSinglePiece(PieceType.Rook, blackSide);
        for (int x = 0; x < TILE_COUNT_X; x++)
            pieces[x, 6] = GenerateSinglePiece(PieceType.Pawn, blackSide);

    }
    private Piece GenerateSinglePiece(PieceType type, int side)
    {
        Piece piece = Instantiate(prefabs[(int)type - 1], transform).GetComponent<Piece>();

        piece.type = type;
        piece.side = side;
        piece.GetComponent<MeshRenderer>().material = sideMaterials[side];

        return piece;
    }

    // Positioning
    public void PositionaAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_X; y++)
                if (pieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
    }
    private void PositionSinglePiece(int x, int y, bool instant = false)
    {
        pieces[x, y].currentX = x;
        pieces[x, y].currentY = y;
        pieces[x, y].SetPosition(GetTileCenter(x, y), instant);
}
    private IEnumerator PositionSinglePieceWithDelay(int x, int y)
    {
        yield return new WaitForSeconds(0.3f);
        PositionSinglePiece(x, y);
        pieceMoved();
    }

    // Highlighting tiles
    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
    }
    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
            if (tiles[availableMoves[i].x, availableMoves[i].y].layer != LayerMask.NameToLayer("Wrong"))
                tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");

        availableMoves.Clear();
    }

    // Checkmate
    private void CheckMate(int side)
    {
        DisplayVictory(side);
        checkMate();
    }
    private bool CheckForCheckmate()
    {
        Vector2Int[] thisMove = moveList[moveList.Count - 1];
        int targetSide = (pieces[thisMove[1].x, thisMove[1].y].side == 0) ? 1 : 0;

        List<Piece> attackingPieces = new List<Piece>();
        List<Piece> defendingPieces = new List<Piece>();
        Piece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (pieces[x, y] != null)
                {
                    if (pieces[x, y].side == targetSide)
                    {
                        defendingPieces.Add(pieces[x, y]);
                        if (pieces[x, y].type == PieceType.King)
                            targetKing = pieces[x, y];
                    }
                    else
                        attackingPieces.Add(pieces[x, y]);
                }

        // Is the king attacked right now?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            List<Vector2Int> pieceMoves = attackingPieces[i].GetAvailableMoves(ref pieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
                currentAvailableMoves.Add(pieceMoves[b]);
        }
        // Are we in check right now?
        if (ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            if (mode == Mode.FirstCheck)
                CheckMate(pieces[thisMove[1].x, thisMove[1].y].side);

            // King is under atteck, can we move something to help him?
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref pieces, TILE_COUNT_X, TILE_COUNT_Y);
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0)
                {
                    check();
                    return false; // Check, but not a checkmate
                }
            }

            return true; // Checkmate
        }
        return false; // Not a check
    }
    private void PreventCheck()
    {
        Piece targetKing = FindKing();
        // Deleting all the moves that are putting us in check through ref availableMoves
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(Piece piece, ref List<Vector2Int> moves, Piece targetKing)
    {
        int currentX = piece.currentX;
        int currentY = piece.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPosition = new Vector2Int(targetKing.currentX, targetKing.currentY);
            if (piece.type == PieceType.King)
                kingPosition = new Vector2Int(simX, simY);

            Piece[,] simPieces = new Piece[TILE_COUNT_X, TILE_COUNT_Y];
            List<Piece> simAtackingPieces = new List<Piece>();
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (pieces[x, y] != null)
                    {
                        simPieces[x, y] = pieces[x, y];
                        if (simPieces[x, y].side != piece.side)
                            simAtackingPieces.Add(simPieces[x, y]);
                    }
                }
            }
            // Simulating the move
            simPieces[currentX, currentY] = null;
            piece.currentX = simX;
            piece.currentY = simY;
            simPieces[simX, simY] = piece;

            // Did one of the pieces got taken down during simulation
            Piece deadPiece = simAtackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
                simAtackingPieces.Remove(deadPiece);

            // Get all the simulated attacking pieces move
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simAtackingPieces.Count; a++)
            {
                List<Vector2Int> pieceMoves = simAtackingPieces[a].GetAvailableMoves(ref simPieces, TILE_COUNT_X, TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                    simMoves.Add(pieceMoves[b]);

            }

            // Is the king in trouble? If so, remove the move
            if (ContainsValidMove(ref simMoves, kingPosition))
            {
                movesToRemove.Add(moves[i]);
            }

            // Restore the actual piece position
            piece.currentX = currentX;
            piece.currentY = currentY;
        }

        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);
    }

    // Special Moves
    private void ProcessSpecialMove()
    {
        if (specialMove == SpecialMove.EnPassant)
        {
            Vector2Int[] thisMove = moveList[moveList.Count - 1];
            Piece thisPawn = pieces[thisMove[1].x, thisMove[1].y];
            Vector2Int[] targetPawnPostiton = moveList[moveList.Count - 2];
            Piece enemyPawn = pieces[targetPawnPostiton[1].x, targetPawnPostiton[1].y];

            if (thisPawn.currentX == enemyPawn.currentX)
            {
                if (thisPawn.currentY == enemyPawn.currentY - 1 || thisPawn.currentY == enemyPawn.currentY + 1)
                {
                    KillPiece(enemyPawn);
                    Debug.Log("unp kill");
                    pieceKilled();
                    pieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }

        }

        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] thisMove = moveList[moveList.Count - 1];
            Piece targetPawn = pieces[thisMove[1].x, thisMove[1].y];

            if (targetPawn.side == 0 && thisMove[1].y == 7 || targetPawn.side == 1 && thisMove[1].y == 0)
            {
                ActivatePromotionMenu(targetPawn.currentX, targetPawn.currentY, targetPawn.side);
            }
        }

        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] thisMove = moveList[moveList.Count - 1];

            // Left Rook
            if (thisMove[1].x == 2)
            {
                if (thisMove[1].y == 0) // White side
                {
                    Piece rook = pieces[0, 0];
                    pieces[3, 0] = rook;
                    PositionSinglePiece(3, 0);
                    pieces[0, 0] = null;
                }
                else if (thisMove[1].y == 7) // Black side
                {
                    Piece rook = pieces[0, 7];
                    pieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    pieces[0, 7] = null;
                }
            }

            // Right Rook
            else if (thisMove[1].x == 6)
            {
                if (thisMove[1].y == 0) // White side
                {
                    Piece rook = pieces[7, 0];
                    pieces[5, 0] = rook;
                    PositionSinglePiece(5, 0);
                    pieces[7, 0] = null;
                }
                else if (thisMove[1].y == 7) // Black side
                {
                    Piece rook = pieces[7, 7];
                    pieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    pieces[7, 7] = null;
                }
            }
        }
    }
    public void ActivatePromotionMenu(int x, int y, int side)
    {
        isChoosingPiece = true;

        promotionMenu.transform.position = GetTileCenter(x, y) + new Vector3(-tileSize, 2.3f * tileSize, 0);
        if (side == 0)
            for (int i = 0; i < 4; i++)
            {
                promotionMenu.transform.GetChild(1).GetChild(i).gameObject.SetActive(true);
                promotionMenu.transform.GetChild(1).GetChild(7 - i).gameObject.SetActive(false);
            }
        else
            for (int i = 4; i < 8; i++)
            {
                promotionMenu.transform.GetChild(1).GetChild(i).gameObject.SetActive(true);
                promotionMenu.transform.GetChild(1).GetChild(i - 4).gameObject.SetActive(false);
            }
        promotionMenu.transform.rotation = Quaternion.Euler((side == 0) ? new Vector3(60, 0, 0) : new Vector3(60, 180, 0));
        promotionMenu.SetActive(true);
    }
    public void ProcessPromotion(PieceType type, int side)
    {
        promotionMenu.gameObject.SetActive(false);

        Vector2Int[] thisMove = moveList[moveList.Count - 1];
        Piece targetPawn = pieces[thisMove[1].x, thisMove[1].y];
        Piece piece = GenerateSinglePiece(type, side);
        piece.transform.position = targetPawn.transform.position;
        Destroy(targetPawn.gameObject);
        pieces[thisMove[1].x, thisMove[1].y] = piece;
        PositionSinglePiece(thisMove[1].x, thisMove[1].y);

        if (CheckForCheckmate())
            CheckMate(piece.side);
        isWhiteTurn = !isWhiteTurn;
        StartCoroutine(ChangeCameraWithDelay(isWhiteTurn ? CameraAngle.white : CameraAngle.black));
        isChoosingPiece = false;
    }

    // Auxiliary operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int position)
    {
        for (int i = 0; i < moves.Count; i++)
            if (moves[i].x == position.x && moves[i].y == position.y)
                return true;
        return false;
    }
    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3((x) * tileSize, yOffset, (y) * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }
    private Vector2Int LookupTileIndex(GameObject tileObject)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (tiles[x, y] == tileObject)
                    return new Vector2Int(x, y);
        return -Vector2Int.one;
    }
    private bool MoveTo(Piece piece, int x, int y, bool auto = false)
    {
        if (!ContainsValidMove(ref availableMoves, new Vector2Int(x, y)) && !auto)
            return false;

        Vector2Int previousPosition = new Vector2Int(piece.currentX, piece.currentY);

        // Problem mode check
        if (mode == Mode.Problems && !auto)
        {
            Vector2Int[] thisMove = new Vector2Int[] { previousPosition, new Vector2Int(x, y) };
            Vector2Int[] solutionMove = NotationToMove(solution[0], currentlyDragging.side);
            if (!(solutionMove[0] == thisMove[0] && solutionMove[1] == thisMove[1]))
            {
                StartCoroutine(WrongMovwHighlighting(piece.currentX, piece.currentY, x, y));
                wrongMove();
                return false;
            }
            else
            {
                solution.RemoveAt(0);
            }
        }


        // if there's another piece on the target postition?
        bool pieceKill = false;

        if (pieces[x, y] != null)
        {
            Piece otherPiece = pieces[x, y];
            if (piece.side == otherPiece.side)
                return false;

            if (mode == Mode.Atomic)
            {
                if (piece.type == PieceType.King && otherPiece.type == PieceType.King)
                    CheckMate(2);
                AtomicExplosion(x, y);
                KillPiece(piece);
                pieceKilled();
                pieceKill = true;
            }
            // moving dead piece aside, if it is an auto, with delay
            if (auto && !isChoosingPiece)
            {
                StartCoroutine(KillPieceWithDelay(otherPiece));
            }
            else
            {
                KillPiece(otherPiece);
                pieceKilled();
            }
        }
        // Move the piece to desired tile
        if (pieces[x, y] == null && !pieceKill || mode != Mode.Atomic)
        {
            pieces[x, y] = piece;
            if (mode == Mode.Problems && auto)
            {
                StartCoroutine(PositionSinglePieceWithDelay(x, y));
            }
            else
            {
                PositionSinglePiece(x, y);
                pieceMoved();
            }
        }

        pieces[previousPosition.x, previousPosition.y] = null;
        
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });

        ProcessSpecialMove();

        if (!isChoosingPiece)
        {
            if (mode != Mode.Atomic && CheckForCheckmate())
                CheckMate(piece.side);

            if (mode != Mode.Problems)
            {
                isWhiteTurn = !isWhiteTurn;
                StartCoroutine(ChangeCameraWithDelay(isWhiteTurn ? CameraAngle.white : CameraAngle.black));
            }

        }
        return true;
    }
    private void KillPiece(Piece piece)
    {
        int count;
        float indent = 9.5f;

        // Atomic chess
        if (piece.type == PieceType.King)
        {
            CheckMate(piece.side == 0 ? 1 : 0);
        }

        if (piece.side == 0)
        {
            deadWhites.Add(piece);
            if (piece.type == PieceType.Pawn)
            {
                count = deadWhites.Where(p => p.type == PieceType.Pawn).Count();
            }
            else
            {
                indent += 1;
                count = deadWhites.Where(p => p.type != PieceType.Pawn).Count();
            }

            piece.SetPosition(
                new Vector3(indent * tileSize, -1 * boardHeight, -1 * tileSize)
                - bounds
                + new Vector3(tileSize / 2, 0, tileSize / 2)
                + (Vector3.forward * deathSpacing) * count
                );
        }
        else
        {
            deadBlacks.Add(piece);
            if (piece.type == PieceType.Pawn)
            {
                count = deadBlacks.Where(p => p.type == PieceType.Pawn).Count();
            }
            else
            {
                indent += 1;
                count = deadBlacks.Where(p => p.type != PieceType.Pawn).Count();
            }

            piece.SetPosition(-(
                new Vector3(indent * tileSize, boardHeight, -1 * tileSize)
                - bounds
                + new Vector3(tileSize / 2, 0, tileSize / 2)
                + (Vector3.forward * deathSpacing) * count
                ));
        }
    }
    private IEnumerator KillPieceWithDelay(Piece piece)
    {
        yield return new WaitForSeconds(0.3f);
        KillPiece(piece);
        pieceKilled();
    }
    private Piece FindKing()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (pieces[x, y] != null && pieces[x, y].type == PieceType.King)
                    if (pieces[x, y].side == currentlyDragging.side)
                        return pieces[x, y];
        return null;
    }
    private void ResetBoard()
    {
        // Time reset
        timerValueBlack = START_TIMER_VALUE;
        timerValueWhite = START_TIMER_VALUE;

        // Fields reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        // UI remove
        pauseScreen.transform.GetChild(0).gameObject.SetActive(false);
        pauseScreen.transform.GetChild(1).gameObject.SetActive(false);
        pauseScreen.SetActive(false);

        // Clear up
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (pieces[x, y] != null)
                    Destroy(pieces[x, y].gameObject);
                pieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);
        deadBlacks.Clear();
        deadWhites.Clear();

        GenerateAllPieces();
        PositionaAllPieces();
        isWhiteTurn = true;
    }

    // Atomic block
    private void AtomicExplosion(int x, int y)
    {
        // This
        pieces[x, y] = null;

        // Right
        if (x + 1 < TILE_COUNT_X)
        {
            // Right
            if (pieces[x + 1, y] != null && pieces[x+1, y].type != PieceType.Pawn)
            {
                KillPiece(pieces[x + 1, y]);
                pieces[x + 1, y] = null;
            }

            // Top Right
            if (y + 1 < TILE_COUNT_X)
            {
                if (pieces[x + 1, y + 1] != null && pieces[x + 1, y + 1].type != PieceType.Pawn)
                {
                    KillPiece(pieces[x + 1, y + 1]);
                    pieces[x + 1, y + 1] = null;
                }
            }

            // Bottom Right
            if (y - 1 >= 0)
            {
                if (pieces[x + 1, y - 1] != null && pieces[x + 1, y - 1].type != PieceType.Pawn)
                {
                    KillPiece(pieces[x + 1, y - 1]);
                    pieces[x + 1, y - 1] = null;
                }
            }
        }

        // Left
        if (x - 1 >= 0)
        {
            // Left
            if (pieces[x - 1, y] != null && pieces[x - 1, y].type != PieceType.Pawn)
            { 
                KillPiece(pieces[x - 1, y]);
                pieces[x - 1, y] = null;
            }

            // Top Left
            if (y + 1 < TILE_COUNT_X)
            {
                if (pieces[x - 1, y + 1] != null && pieces[x - 1, y + 1].type != PieceType.Pawn)
                {
                    KillPiece(pieces[x - 1, y + 1]);
                    pieces[x - 1, y + 1] = null;
                }
            }

            // Bottom Left
            if (y - 1 >= 0)
            {
                if (pieces[x - 1, y - 1] != null && pieces[x - 1, y - 1].type != PieceType.Pawn)
                {
                    KillPiece(pieces[x - 1, y - 1]);
                    pieces[x - 1, y - 1] = null;
                }
            }
        }

        // Up
        if (y + 1 < TILE_COUNT_X)
            if (pieces[x, y + 1] != null && pieces[x, y + 1].type != PieceType.Pawn)
            {
                KillPiece(pieces[x, y + 1]);
                pieces[x, y + 1] = null;
            }

        // Down
        if (y - 1 >= 0)
            if (pieces[x, y - 1] != null && pieces[x, y - 1].type != PieceType.Pawn)
            {
                KillPiece(pieces[x, y - 1]);
                pieces[x, y - 1] = null;
            }
    }

    // Problems block
    private void GenerateProblemBoard()
    {
        // Get random problem from file
        Problems problems;
        XmlSerializer s = new XmlSerializer(typeof(Problems));
        using (FileStream fs = new FileStream("C:\\Games\\Chess Game Mega complete\\Problems.xml", FileMode.Open))
        {
            problems = (Problems)s.Deserialize(fs);
        }
        string[] problem = problems.GetRandomProblem();
        name = problem[0];
        string[] position = problem[1].Split();
        solution = problem[2][..^2].Split().Where(m => !m.Contains('.')).ToList();

        // Board clearing
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (pieces[x, y] != null)
                    Destroy(pieces[x, y].gameObject);
                pieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);
        deadBlacks.Clear();
        deadWhites.Clear();

        // Position Setting
        int setX = 0;
        int setY = 7;
        foreach (char symb in position[0])
        {
            if (symb == '/') 
            {
                setY--;
                setX = 0;
            }
            else
            {
                int num = (int)char.GetNumericValue(symb);
                if (num == -1)
                {
                    pieces[setX, setY] = PieceFromLetter(symb);
                    setX += 1;
                }
                else setX += num;
            }
        }
        for (int i = 0; i < TILE_COUNT_X; i++)
            for (int j = 0; j < TILE_COUNT_Y; j++)
        PositionaAllPieces();
        isWhiteTurn = position[1] == "w";
        if (position[2] == "-")
        {
            // Remove castling moves
            moveList.Add(new Vector2Int[] {new Vector2Int(0, 0), Vector2Int.zero});
            moveList.Add(new Vector2Int[] { new Vector2Int(0, 7), Vector2Int.zero});
            moveList.Add(new Vector2Int[] { new Vector2Int(7, 0), Vector2Int.zero});
            moveList.Add(new Vector2Int[] { new Vector2Int(7, 7), Vector2Int.zero});
        }
        else
        {
            if (!position[2].Contains("k"))
                moveList.Add(new Vector2Int[] { new Vector2Int(7, 7), Vector2Int.zero });
            if (!position[2].Contains("q"))
                moveList.Add(new Vector2Int[] { new Vector2Int(0, 7), Vector2Int.zero });
            if (!position[2].Contains("K"))
                moveList.Add(new Vector2Int[] { new Vector2Int(7, 0), Vector2Int.zero });
            if (!position[2].Contains("Q"))
                moveList.Add(new Vector2Int[] { new Vector2Int(0, 0), Vector2Int.zero });
        }
    }
    public IEnumerator WrongMovwHighlighting(int thisX, int thisY, int x, int y)
    {

        tiles[x, y].layer = LayerMask.NameToLayer("Wrong");
        tiles[thisX, thisY].layer = LayerMask.NameToLayer("Wrong");
        yield return new WaitForSeconds(0.2f);
        tiles[x, y].layer = LayerMask.NameToLayer("Tile");
        tiles[thisX, thisY].layer = LayerMask.NameToLayer("Tile");
        yield return new WaitForSeconds(0.2f);
        tiles[x, y].layer = LayerMask.NameToLayer("Wrong");
        tiles[thisX, thisY].layer = LayerMask.NameToLayer("Wrong");
        yield return new WaitForSeconds(0.2f);
        tiles[x, y].layer = LayerMask.NameToLayer("Tile");
        tiles[thisX, thisY].layer = LayerMask.NameToLayer("Tile");
    }
    public Piece PieceFromLetter(char letter)
    {
        if (Char.ToLower(letter) == 'r')
            return GenerateSinglePiece(PieceType.Rook, letter == 'r' ? 1 : 0);
        if (Char.ToLower(letter) == 'n')
            return GenerateSinglePiece(PieceType.Knight, letter == 'n' ? 1 : 0);
        if (Char.ToLower(letter) == 'b')
            return GenerateSinglePiece(PieceType.Bishop, letter == 'b' ? 1 : 0);
        if (Char.ToLower(letter) == 'q')
            return GenerateSinglePiece(PieceType.Queen, letter == 'q' ? 1 : 0);
        if (Char.ToLower(letter) == 'k')
            return GenerateSinglePiece(PieceType.King, letter == 'k' ? 1 : 0);
        return GenerateSinglePiece(PieceType.Pawn, letter == 'p' ? 1 : 0);
    }
    public Vector2Int[] NotationToMove(string nMove, int side)
    {
        Vector2Int[] move = new Vector2Int[2];
        string curPos; // Start position
        string desPos; // New postion
        int additionX = -1;
        int additionY = -1;

        bool promotion = false;
        char promotionPiece;

        if (nMove.Contains('+') || nMove.Contains("#"))
            nMove = nMove[..^1];
        if (nMove.Contains("="))
            promotion = true;
        if (nMove.Contains('x'))
        {
            curPos = nMove[..nMove.IndexOf('x')];
            if (!promotion)
                desPos = nMove[(nMove.IndexOf('x') + 1)..];
            else
                desPos = nMove[(nMove.IndexOf('x') + 1)..nMove.IndexOf('=')];
        }
        else
        {
            if (!promotion)
            {
                curPos = nMove[..^2];
                desPos = nMove[^2..];
            }
            else
            {
                curPos = "";
                desPos = nMove[..nMove.IndexOf('=')];
            }

        }
        if (curPos.Length == 2)
        {
            if ((int)Char.GetNumericValue(curPos[1]) == -1)
                additionX = VerticalToInt(curPos[1]);
            else
                additionY = (int)char.GetNumericValue(curPos[1]);
        }
        List<Vector2Int> PossiblePiecePos = new List<Vector2Int>();
        if (curPos.Length == 0 || Char.IsLower(curPos[0])) // Pawn moved
        {
            int nMoveY = (int)Char.GetNumericValue(desPos[1]) - 1;
            int direction = side == 0 ? 1 : -1;

            if (curPos.Length == 0)
            {
                if (pieces[VerticalToInt(desPos[0]), nMoveY - direction] != null)
                    move[0] = new Vector2Int(VerticalToInt(desPos[0]), nMoveY - direction);
                else
                    move[0] = new Vector2Int(VerticalToInt(desPos[0]), nMoveY - 2 * direction);
            }
            else
                move[0] = new Vector2Int(VerticalToInt(curPos[0]), nMoveY - direction);
            move[1] = new Vector2Int(VerticalToInt(desPos[0]), nMoveY);
            return move;
        }
        if (curPos[0] == 'R')
        {
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0;  y < TILE_COUNT_Y; y++)
                {
                    if (pieces[x, y] != null && pieces[x, y].type == PieceType.Rook && pieces[x, y].side == side)
                        PossiblePiecePos.Add(new Vector2Int(x, y));
                }
            }
        }
        else if (curPos[0] == 'N')
        {
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (pieces[x, y] != null && pieces[x, y].type == PieceType.Knight && pieces[x, y].side == side)
                        PossiblePiecePos.Add(new Vector2Int(x, y));
                }
            }
        }
        else if (curPos[0] == 'B')
        {
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (pieces[x, y] != null && pieces[x, y].type == PieceType.Bishop && pieces[x, y].side == side)
                        PossiblePiecePos.Add(new Vector2Int(x, y));
                }
            }
        }
        else if (curPos[0] == 'Q')
        {
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (pieces[x, y] != null && pieces[x, y].type == PieceType.Queen && pieces[x, y].side == side)
                        PossiblePiecePos.Add(new Vector2Int(x, y));
                }
            }
        }
        else
        {
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (pieces[x, y] != null && pieces[x, y].type == PieceType.King && pieces[x, y].side == side)
                        PossiblePiecePos.Add(new Vector2Int(x, y));
                }
            }
        }
        move[1] = new Vector2Int(VerticalToInt(desPos[0]), (int)Char.GetNumericValue(desPos[1]) - 1);

        if (PossiblePiecePos.Count == 1)
            move[0] = PossiblePiecePos[0];
        else if ((additionY == -1 || additionX == -1) && !(additionY == -1 && additionX == -1))
        {
            if (additionY != -1)
                move[0] = PossiblePiecePos.Where(m => m.y == additionY).First();
            else
            {
                move[0] = PossiblePiecePos.Where(m => m.x == additionX).First();
            }
        }
        else
        {
            List<Vector2Int> moves1 = pieces[PossiblePiecePos[0].x, PossiblePiecePos[0].y].GetAvailableMoves(ref pieces, TILE_COUNT_X, TILE_COUNT_Y);
            if (ContainsValidMove(ref moves1, move[1]))
            {
                move[0] = new Vector2Int(PossiblePiecePos[0].x, PossiblePiecePos[0].y);
            }
            else
                move[0] = new Vector2Int(PossiblePiecePos[1].x, PossiblePiecePos[1].y);
        }
        return move;

    }
    public int VerticalToInt(char v)
    {
        switch (v)
        {
            case 'a': return 0;
            case 'b': return 1;
            case 'c': return 2;
            case 'd': return 3;
            case 'e': return 4;
            case 'f': return 5;
            case 'g': return 6;
            case 'h': return 7;
            default: return -1;
        }
    }

    // UI
    private IEnumerator ChangeCameraWithDelay(CameraAngle index)
    {
        yield return new WaitForSeconds(0.5f);
        ChangeCamera(index);
    }
    public void OnClassicGameButton()
    {
        inGame = true;
        ChangeCamera(CameraAngle.white);
        gameUI.gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
        ResetBoard();
        mode = Mode.Classic;
        inGameUI.SetActive(true);
        whiteTimer.SetActive(true);
        blackTimer.SetActive(true);
        buttonClicked();
    }
    public void OnFirstCheckButton()
    {
        inGame = true;
        ChangeCamera(CameraAngle.white);
        gameUI.gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
        ResetBoard();
        mode = Mode.FirstCheck;
        inGameUI.SetActive(true);
        whiteTimer.SetActive(true);
        blackTimer.SetActive(true);
        buttonClicked();
    }
    public void OnAtomicChessButton()
    {
        inGame = true;
        ChangeCamera(CameraAngle.white);
        gameUI.gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
        ResetBoard();
        mode = Mode.Atomic;
        inGameUI.SetActive(true);
        whiteTimer.SetActive(true);
        blackTimer.SetActive(true);
        buttonClicked();
    }
    public void OnProblensButton()
    {
        GenerateProblemBoard();
        inGame = true;
        ChangeCamera(isWhiteTurn ? CameraAngle.white : CameraAngle.black);
        gameUI.gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
        mode = Mode.Problems;
        blackTimer.SetActive(false);
        whiteTimer.SetActive(false);
        inGameUI.SetActive(true);
        pauseScreen.gameObject.transform.GetChild(3).GetChild(2).gameObject.SetActive(false);
        pauseScreen.gameObject.transform.GetChild(3).GetChild(0).gameObject.SetActive(true);
        buttonClicked();
    }
    public void OnExitButton()
    {
        buttonClicked();
        UnityEngine.Application.Quit();
    }
    public void ChangeCamera(CameraAngle index)
    {
        if (inGame || index == CameraAngle.menu)
        {
            for (int i = 0; i < cameraAngles.Length; i++)
                cameraAngles[i].SetActive(false);

            cameraAngles[(int)index].SetActive(true);
        }
    }
    public void OnPauseButton()
    {
        pauseScreen.SetActive(true);
        backButton.SetActive(true);
        inGame = false;
        buttonClicked();
    }
    public void OnBackButton()
    {
        pauseScreen.SetActive(false);
        inGame = true;
        buttonClicked();
    }
    private void DisplayVictory(int winningTeam)
    {
        if (!pauseScreen.activeInHierarchy)
        {
            pauseScreen.SetActive(true);
            backButton.SetActive(false);
            pauseScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
            inGame = false;
        }
    }
    public void OnResetButton()
    {
        inGame = true;
        ResetBoard();
        if (isWhiteTurn)
        {
            ChangeCamera(isWhiteTurn ? CameraAngle.white : CameraAngle.black);
        }
        timerValueBlack = START_TIMER_VALUE;
        timerValueWhite = START_TIMER_VALUE;
        inGameUI.SetActive(true);
        buttonClicked();
    }
    public void OnMenuButton()
    {
        pauseScreen.SetActive(false);
        ChangeCamera(CameraAngle.menu);
        inGame = false;
        gameUI.gameObject.GetComponent<CanvasGroup>().blocksRaycasts = true;
        inGameUI.SetActive(false);
        // Problems
        pauseScreen.gameObject.transform.GetChild(3).GetChild(2).gameObject.SetActive(true);
        pauseScreen.gameObject.transform.GetChild(3).GetChild(0).gameObject.SetActive(false);

        //if (mode == Mode.Classic || mode == Mode.FirstCheck || mode == Mode.Atomic)
        //    SaveGame();

        buttonClicked();
    }
    public void OnNextProblemButton()
    {
        inGame = true;
        GenerateProblemBoard();
        StartCoroutine(ChangeCameraWithDelay(isWhiteTurn ? CameraAngle.white : CameraAngle.black));
        pauseScreen.transform.GetChild(0).gameObject.SetActive(false);
        pauseScreen.transform.GetChild(1).gameObject.SetActive(false);
        pauseScreen.SetActive(false);
        buttonClicked();
    }
}
