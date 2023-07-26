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

    private Move? Analyze(Board board, Timer timer, bool myTurn, int deapth, int deapthLimit, int processingTimeMS, out double moveRating, out bool whiteWins, out bool blackWins)
    {
        // Check deapth limit
        if(deapth >= deapthLimit)
        {
            // Calculate the score of the board as is (This is the rating for the move that got us here)
            moveRating = CalculateBoardScore(board, myTurn);
            return null;
        }

        // Get the list of legal moves
        Move[] moves = board.GetLegalMoves();
        Dictionary<Move, double> scores = new();

        // Rate each legal move
        foreach(Move move in moves)
        {
            board.MakeMove(move);
            double subRating;
            bool blackWon, whiteWon;
            Analyze(board, timer, !myTurn, deapth+1, deapthLimit, int processingTimeMs / moves.Length, out subRating, out whiteWon, out blackWon); // We already know which move was analized, so we don't care about the returned move
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
        PieceList[] pieces = board.GetAllPieceLists();

        // Total strength
        relativeScores.pieceStrength = CalculateTotalPieceStrength(pieces, out blackScores.pieceStrength, out whiteScores.pieceStrength);

        // Flanks
        // Find all the peices in collumns A-C
s
        return relativeScores.TotalScore();
    }

    private double CalculateTotalPieceStrength(PieceList[] pieces, out double black, out double white)
    {
        black = 0.0;
        white = 0.0;
        foreach(PieceList plist in pieces)
        {
            if(plist.IsWhitePieceList)
            {
                white += plist.Count * pieceValues[plist.TypeOfPieceInList];
            }
            else
            {
                black += plist.Count * pieceValues[plist.TypeOfPieceInList];
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

        public readonly double TotalScore()
        {
            return pieceStrength;
        }
    }

}