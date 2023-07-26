using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Diagnostics;

public class MyBot : IChessBot
{
    private readonly ILogger<MyBot> logger;

    private int myColor = -1; // -1: unknown, 0: black, 1: white

    private Dictionary<PieceType,double> pieceValues = new() {
            {PieceType.None, 0.0},
            {PieceType.Pawn, 1.0},
            {PieceType.Knight, 3.0},
            {PieceType.Bishop, 3.5},
            {PieceType.Rook, 5.0},
            {PieceType.Queen, 9.0},
            {PieceType.King, 0}
        };

    public Move Think(Board board, Timer timer)
    {
        // if necessary determine color
        if(myColor == -1)
            myColor = board.IsWhiteToMove ? 1 : 0;

        // Determine who's turn it is
        bool myTurn = myColor == (board.IsWhiteToMove ? 1 : 0);

        // Get moves
        Move[] moves = board.GetLegalMoves();
        Dictionary<Move, double> allMoves = new();
        Dictionary<Move,double> checks = new();
        Dictionary<Move, double> captures = new();
        Dictionary<Move, double> promotions = new();
        Dictionary<Move, double> attacks = new();
        Dictionary<Move, double> other = new();

        // Calculate board score
        double boardScore = CalculateBoardScore(board);

        // Sort moves
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double subScore = CalculateBoardScore(board);
            allMoves.Add(move, subScore);

            logger.LogInformation(AppLogEvents.Read, "Move ({0}->{1}) score = {2}", move.StartSquare.Name, move.TargetSquare.Name, subScore);

            if(board.IsInCheck()) checks.Add(move, subScore);
            else if(move.IsCapture || move.IsEnPassant) captures.Add(move, subScore);
            else if(move.IsPromotion) promotions.Add(move, subScore);
            else other.Add(move, subScore);
            board.UndoMove(move);
        }

        // Pick best move
        var sortedMoves = from entry in allMoves orderby entry.Value descending select entry.Key;

        return sortedMoves.First<Move>();
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

        return myColor == 0 ? black - white : white - black; // Positive number: I'm stronger, Negative number: I'm weaker
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