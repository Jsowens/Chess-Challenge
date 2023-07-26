using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Diagnostics;

public class MyBot : IChessBot
{
    private bool isWhite = true;
    private bool firstMove = true;
    private Dictionary<PieceType,double> pieceValues = new() {
            {PieceType.None, 0.0},
            {PieceType.Pawn, 1.0},
            {PieceType.Knight, 3.0},
            {PieceType.Bishop, 3.5},
            {PieceType.Rook, 5.0},
            {PieceType.Queen, 9.0},
            {PieceType.King, 0}
        };

    private List<Move> predictions = new();

    public Move Think(Board board, Timer timer)
    {
        if(firstMove)
            isWhite = board.IsWhiteToMove;
        predictions.Clear();
        double moveRating;
        return Analyze(board, timer, true, 0, 5, timer.MillisecondsRemaining / 40, out moveRating);
    }

    private Move Analyze(Board board, Timer timer, bool myTurn, int deapth, int deapthLimit, int processingTimeMS, out double moveRating)
    {
        // Get the list of legal moves
        Move[] moves = board.GetLegalMoves();
        Dictionary<Move, double> scores = new();

        // Check deapth limit
        if(deapth >= deapthLimit || moves.Length <= 0)
        {
            // Calculate the score of the board as is (This is the rating for the move that got us here)
            moveRating = CalculateBoardScore(board);
            return new Move("b1c3", board);
        }

        // Rate each legal move
        foreach(Move move in moves)
        {
            board.MakeMove(move);
            double subRating;
            if(myTurn && board.IsInCheckmate()) // This move just won me the game, set points to 10000
                {moveRating = 10000; board.UndoMove(move); return move;}
            else if(!myTurn && board.IsInCheckmate()) // This move just won the opponent the game, set points to -10000
                {moveRating = -10000; board.UndoMove(move); return move;}
            else    // Nobody wins immediatly, so analize the board
                Analyze(board, timer, !myTurn, deapth+1, deapthLimit, processingTimeMS / moves.Length, out subRating); // We already know which move was analized, so we don't care about the returned move
            scores.Add(move, subRating);
            board.UndoMove(move);
        }

        // Determine the best move
        Move bestMove;
        if(myTurn)
        {
            // I want the most positive score
            var sortedMoves = from entry in scores orderby entry.Value descending select entry.Key;
            bestMove = sortedMoves.First<Move>();
        }
        else
        {
            // My opponent wants the most negative score
            var sortedMoves = from entry in scores orderby entry.Value ascending select entry.Key;
            bestMove = sortedMoves.First<Move>();
        }
        moveRating = scores[bestMove];
        return bestMove; // This doesn't really matter until the top layer resolves
    }

    private double CalculateBoardScore(Board board)
    {
        // Calculate the raw peice strength on the board
        StrategyScores whiteScores, blackScores, relativeScores = new();
        PieceList[] pieceLists = board.GetAllPieceLists();

        // Very inefficient transformation
        List<Piece> pieces = new();
        foreach (PieceList pList in pieceLists)
        {
            foreach (Piece p in pList)
            {
                pieces.Add(p);
            }
        }

        // Total strength
        relativeScores.pieceStrength = CalculatePieceStrength(pieces, 0, 7, 0, 7, out blackScores.pieceStrength, out whiteScores.pieceStrength);

        // Flanks
        relativeScores.leftFlank = CalculatePieceStrength(pieces, 0, 7, 0, 2, out blackScores.leftFlank, out whiteScores.leftFlank);
        relativeScores.center = CalculatePieceStrength(pieces, 0, 7, 3, 4, out blackScores.center, out whiteScores.center);
        relativeScores.rightFlank = CalculatePieceStrength(pieces, 0, 7, 5, 7, out blackScores.rightFlank, out whiteScores.rightFlank);

        // Sides
        relativeScores.leftSide = CalculatePieceStrength(pieces, 0, 7, 0, 3, out blackScores.leftSide, out whiteScores.leftSide);
        relativeScores.rightSide = CalculatePieceStrength(pieces, 0, 7, 4, 7, out blackScores.rightSide, out whiteScores.rightSide);


        double finalScore = relativeScores.pieceStrength +
            relativeScores.center;
        return finalScore;
    }

    private double CalculatePieceStrength(List<Piece> pieces, int minRank, int maxRank, int minFile, int maxFile, out double black, out double white)
    {
        black = 0.0;
        white = 0.0;
        foreach(Piece piece in pieces)
        {
            Square sq = piece.Square;
            if(sq.Rank < minRank || sq.Rank > maxRank || sq.File < minFile || sq.File > maxFile) continue; // Skip if out of bounds

            if(piece.IsWhite)
            {
                white += pieceValues[piece.PieceType];
            }
            else
            {
                black += pieceValues[piece.PieceType];
            }
        }

        return isWhite ? white - black : black - white; // Positive number: I'm stronger, Negative number: I'm weaker
    }

    struct StrategyScores
    {
        public StrategyScores() {}
        // General
        public double pieceStrength = 0;
        public int longestPawnChain = 0;
        public int pawnIslands = 0;
        public int pawnCount = 0;
        public bool kingInCenter = true;
        public double kingSafety = 0;
        public double attackRating = 0;
        public double defenseRating = 0;
        public int space = 0; // How many squares are held by the pawn wall

        //                                                       _              _
        //     [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        // 8   [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        //     [     ]#####[     ]#####[     ]#####[     ]#####  | Black        |
        //      #####[     ]#####[     ]#####[     ]#####[     ] | Land         |
        // 7    #####[     ]#####[     ]#####[     ]#####[     ] |              |
        //      #####[     ]#####[     ]#####[     ]#####[     ] _              | Black
        //     [     ]#####[     ]#####[     ]#####[     ]#####  |              | Base
        // 6   [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        //     [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        //      #####[     ]#####[     ]#####[     ]#####[     ] |              |
        // 5    #####[     ]#####[     ]#####[     ]#####[     ] |              |
        //      #####[     ]#####[     ]#####[     ]#####[     ] | Noman's      _
        //     [     ]#####[     ]#####[     ]#####[     ]#####  | Land         |
        // 4   [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        //     [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        //      #####[     ]#####[     ]#####[     ]#####[     ] |              |
        // 3    #####[     ]#####[     ]#####[     ]#####[     ] |              |
        //      #####[     ]#####[     ]#####[     ]#####[     ] _              | White
        //     [     ]#####[     ]#####[     ]#####[     ]#####  |              | Base
        // 2   [     ]#####[     ]#####[     ]#####[     ]#####  |              |
        //     [     ]#####[     ]#####[     ]#####[     ]#####  | White        |
        //      #####[     ]#####[     ]#####[     ]#####[     ] | Land         |
        // 1    #####[     ]#####[     ]#####[     ]#####[     ] |              |
        //      #####[     ]#####[     ]#####[     ]#####[     ] _              _
        //        A     B     C     D     E     F     G     H 
        //
        //     |-----------------|-----------|-----------------|
        //          Left Flank      Center      Right Flank
        //     |-----------------------|-----------------------|
        //          Left Side                   Right Side

        // 3 sector Left to Right force concentrations
        public double leftFlank = 0;
        public double center = 0;
        public double rightFlank = 0;

        // 2 sector Left and Right force concentrations
        public double leftSide = 0;
        public double rightSide = 0;

        // 3 sector advancement concentrations
        public double whiteLand = 0;
        public double nomansLand = 0;
        public double blackLand = 0;

        // 2 sector advancement concentrations
        public double whiteBase = 0;
        public double blackBase = 0;
    }

}