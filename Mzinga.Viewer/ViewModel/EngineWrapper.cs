﻿// 
// EngineWrapper.cs
//  
// Author:
//       Jon Thysell <thysell@gmail.com>
// 
// Copyright (c) 2015, 2016, 2017 Jon Thysell <http://jonthysell.com>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Mzinga.Core;

namespace Mzinga.Viewer.ViewModel
{
    public delegate void BoardUpdatedEventHandler(Board board);

    public delegate void EngineTextUpdatedEventHandler(string engineText);

    public delegate void TargetPieceUpdatedEventHandler(PieceName pieceName);

    public delegate void TargetPositionUpdatedEventHandler(Position position);

    public class EngineWrapper
    {
        public Board Board
        {
            get
            {
                return _board;
            }
            private set
            {
                _board = value;
                OnBoardUpdate(Board);
            }
        }
        private Board _board = null;

        public MoveSet ValidMoves { get; private set; }

        public BoardHistory BoardHistory { get; private set; }

        public bool GameInProgress
        {
            get
            {
                return (null != Board && Board.GameInProgress);
            }
        }

        public bool GameIsOver
        {
            get
            {
                return (null != Board && Board.GameIsOver);
            }
        }

        public bool CurrentTurnIsHuman
        {
            get
            {
                return (null != Board &&
                        ((Board.CurrentTurnColor == Color.White && CurrentGameSettings.WhitePlayerType == PlayerType.Human) ||
                         (Board.CurrentTurnColor == Color.Black && CurrentGameSettings.BlackPlayerType == PlayerType.Human)));
            }
        }

        public PieceName TargetPiece
        {
            get
            {
                return _targetPiece;
            }
            set
            {
                PieceName oldValue = _targetPiece;

                _targetPiece = value;

                if (oldValue != value)
                {
                    OnTargetPieceUpdate(TargetPiece);
                }
            }
        }
        private PieceName _targetPiece = PieceName.INVALID;

        public Position TargetPosition
        {
            get
            {
                return _targetPosition;
            }
            set
            {
                Position oldValue = _targetPosition;

                _targetPosition = value;

                if (oldValue != value)
                {
                    OnTargetPositionUpdate(TargetPosition);
                }
            }
        }
        private Position _targetPosition = null;

        public Move TargetMove { get; private set; }

        public bool CanPlayTargetMove
        {
            get
            {
                return CanPlayMove(TargetMove) && !TargetMove.IsPass;
            }
        }

        public bool CanPass
        {
            get
            {
                return (GameInProgress && CurrentTurnIsHuman && null != ValidMoves && ValidMoves.Contains(Move.Pass));
            }
        }

        public bool CanUndoLastMove
        {
            get
            {
                return (null != Board && CanUndoMoveCount > 0);
            }
        }

        public int CanUndoMoveCount
        {
            get
            {
                int moves = 0;

                int historyCount = null != BoardHistory ? BoardHistory.Count : 0;

                if (null != Board && historyCount > 0)
                {
                    if (CurrentGameSettings.WhitePlayerType == PlayerType.Human && CurrentGameSettings.BlackPlayerType == PlayerType.Human)
                    {
                        moves = 1; // Can only undo one Human move
                    }
                    else if (CurrentGameSettings.WhitePlayerType != CurrentGameSettings.BlackPlayerType)
                    {
                        if (CurrentTurnIsHuman)
                        {
                            moves = 2; // Undo the previous move (AI) and the move before that (Human)
                        }
                        else if (GameIsOver)
                        {
                            moves = 1; // Undo the previous, game-ending move (Human)
                        }
                    }
                    else if (CurrentGameSettings.WhitePlayerType == PlayerType.EngineAI && CurrentGameSettings.BlackPlayerType == PlayerType.EngineAI)
                    {
                        moves = 0; // Can't undo an AI vs AI's moves
                    }
                }

                // Only undo moves if there are enough moves to undo
                if (moves <= historyCount)
                {
                    return moves;
                }

                return 0;
            }
        }

        public bool CanFindBestMove
        {
            get
            {
                return CurrentTurnIsHuman && GameInProgress && null != ValidMoves && ValidMoves.Count > 0;
            }
        }

        public bool CanPlayBestMove
        {
            get
            {
                return CurrentTurnIsHuman && GameInProgress && null != ValidMoves && ValidMoves.Count > 0;
            }
        }

        public string EngineText
        {
            get
            {
                return _engineText.ToString();
            }
        }
        private StringBuilder _engineText;

        public GameSettings CurrentGameSettings
        {
            get
            {
                if (null == _currentGameSettings)
                {
                    _currentGameSettings = new GameSettings();
                }

                return _currentGameSettings.Clone();
            }
            private set
            {
                _currentGameSettings = (null != value) ? value.Clone() : null;
            }
        }
        private GameSettings _currentGameSettings;

        public event BoardUpdatedEventHandler BoardUpdated;
        public event EngineTextUpdatedEventHandler EngineTextUpdated;

        public event TargetPieceUpdatedEventHandler TargetPieceUpdated;
        public event TargetPositionUpdatedEventHandler TargetPositionUpdated;

        private Process _process;
        private StreamReader _reader;
        private StreamWriter _writer;

        private const int AutoPlayMinMs = 1000;

        public EngineWrapper(string engineCommand)
        {
            _engineText = new StringBuilder();
            StartEngine(engineCommand);
        }

        private void StartEngine(string engineName)
        {
            if (string.IsNullOrWhiteSpace(engineName))
            {
                throw new ArgumentNullException("engineName");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = engineName;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;

            _engineText.Clear();

            _process = Process.Start(startInfo);
            _reader = _process.StandardOutput;
            _writer = _process.StandardInput;

            ReadEngineOutput(EngineCommand.Info);
        }

        public void Close()
        {
            _writer.WriteLine("exit");
            _process.WaitForExit();
            _process.Close();
            _process = null;
        }

        public void NewGame(GameSettings settings)
        {
            if (null == settings)
            {
                throw new ArgumentNullException("settings");
            }

            CurrentGameSettings = settings;

            SendCommand("newgame");
        }

        public void PlayTargetMove()
        {
            if (null == TargetMove)
            {
                throw new Exception("Please select a valid piece and destination first.");
            }

            if (TargetMove.IsPass)
            {
                Pass();
            }
            else
            {
                SendCommand("play {0}", TargetMove);
            }
        }

        public void Pass()
        {
            SendCommand("pass");
        }

        public void UndoLastMove()
        {
            int moves = CanUndoMoveCount;

            if (moves > 0)
            {
                SendCommand("undo {0}", moves);
            }
        }

        public void PlayBestMove()
        {
            AutoPlayBestMove();
        }

        public void FindBestMove()
        {
            SendCommand("bestmove");
        }

        public void SendCommand(string command, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException("command");
            }

            command = string.Format(command, args);

            EngineCommand cmd = IdentifyCommand(command);

            if (cmd == EngineCommand.Exit)
            {
                throw new Exception("Can't send exit command.");
            }

            EngineTextAppendLine(command);
            _writer.WriteLine(command);
            ReadEngineOutput(cmd);
        }

        private EngineCommand IdentifyCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException("command");
            }

            string[] split = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            string cmd = split[0].ToLower();

            switch (cmd)
            {
                case "info":
                    return EngineCommand.Info;
                case "?":
                case "help":
                    return EngineCommand.Help;
                case "board":
                    return EngineCommand.Board;
                case "newgame":
                    return EngineCommand.NewGame;
                case "play":
                    return EngineCommand.Play;
                case "pass":
                    return EngineCommand.Pass;
                case "validmoves":
                    return EngineCommand.ValidMoves;
                case "bestmove":
                    return EngineCommand.BestMove;
                case "undo":
                    return EngineCommand.Undo;
                case "history":
                    return EngineCommand.History;
                case "exit":
                    return EngineCommand.Exit;
                default:
                    return EngineCommand.Unknown;
            }
        }

        private void ReadEngineOutput(EngineCommand command)
        {
            StringBuilder sb = new StringBuilder();

            string errorMessage = "";
            string invalidMoveMessage = "";

            string line = null;
            while (null != (line = _reader.ReadLine()))
            {
                EngineTextAppendLine(line);
                if (line == "ok")
                {
                    break;
                }
                else
                {
                    if (line.StartsWith("err"))
                    {
                        errorMessage = line.Substring(line.IndexOf(' ') + 1);
                    }
                    else if (line.StartsWith("invalidmove"))
                    {
                        invalidMoveMessage = line.Substring(line.IndexOf(' ') + 1);
                    }
                    sb.AppendLine(line);
                }
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new EngineException(errorMessage);
            }

            if (!string.IsNullOrWhiteSpace(invalidMoveMessage))
            {
                throw new InvalidMoveException(invalidMoveMessage);
            }

            string[] outputSplit = sb.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            string firstLine = "";

            if (null != outputSplit && outputSplit.Length > 0)
            {
                firstLine = outputSplit[0];
            }

            // Update other properties
            switch (command)
            {
                case EngineCommand.Board:
                case EngineCommand.NewGame:
                case EngineCommand.Play:
                case EngineCommand.Pass:
                case EngineCommand.Undo:
                    Board = !string.IsNullOrWhiteSpace(firstLine) ? new Board(firstLine) : null;
                    break;
                case EngineCommand.ValidMoves:
                    ValidMoves = !string.IsNullOrWhiteSpace(firstLine) ? new MoveSet(firstLine) : null;
                    break;
                case EngineCommand.BestMove:
                    if (!string.IsNullOrWhiteSpace(firstLine))
                    {
                        Move bestMove = new Move(firstLine);

                        TargetPiece = bestMove.PieceName;
                        TargetPosition = bestMove.Position;
                        TargetMove = bestMove;
                    }
                    else
                    {
                        TargetPiece = PieceName.INVALID;
                    }
                    break;
                case EngineCommand.History:
                    BoardHistory = !string.IsNullOrWhiteSpace(firstLine) ? new BoardHistory(firstLine) : null;
                    break;
                case EngineCommand.Info:
                case EngineCommand.Help:
                default:
                    break;
            }
        }

        private void EngineTextAppendLine(string line)
        {
            _engineText.AppendLine(line);
            OnEngineTextUpdate(EngineText);
        }

        public PieceName GetPieceAt(double cursorX, double cursorY, double hexRadius)
        {
            Position position = Position.FromCursor(cursorX, cursorY, hexRadius);

            Piece topPiece = (null != Board) ? Board.GetPieceOnTop(position) : null;
            return ((null != topPiece) ? topPiece.PieceName : PieceName.INVALID);
        }

        public Position GetTargetPositionAt(double cursorX, double cursorY, double hexRadius)
        {
            Position bottomPosition = Position.FromCursor(cursorX, cursorY, hexRadius);

            Piece topPiece = (null != Board) ? Board.GetPieceOnTop(bottomPosition) : null;

            if (null == topPiece)
            {
                // No piece there, return position at bottom of the stack (stack == 0)
                return bottomPosition;
            }
            else
            {
                // Piece present, return position on top of the piece
                return topPiece.Position.GetShifted(0, 0, 0, 1);
            }
        }

        public bool CanPlayMove(Move move)
        {
            return (GameInProgress && CurrentTurnIsHuman && null != move && null != ValidMoves && ValidMoves.Contains(move));
        }

        private void OnBoardUpdate(Board board)
        {
            TargetPiece = PieceName.INVALID;
            ValidMoves = null;

            SendCommand("history");

            if (GameInProgress)
            {
                SendCommand("validmoves");
            }

            BoardUpdated?.Invoke(board);

            if (GameInProgress &&
                ((Board.CurrentTurnColor == Color.White && CurrentGameSettings.WhitePlayerType == PlayerType.EngineAI) ||
                 (Board.CurrentTurnColor == Color.Black && CurrentGameSettings.BlackPlayerType == PlayerType.EngineAI)))
            {
                AutoPlayBestMove();
            }
        }

        private void AutoPlayBestMove()
        {
            Task.Run(() =>
            {
                try
                {
                    DateTime start = DateTime.Now;

                    FindBestMove();

                    int timeToFindBestMoveMs = (int)(DateTime.Now - start).TotalMilliseconds;
                    int timeToWaitMs = Math.Max(AutoPlayMinMs - timeToFindBestMoveMs, (AutoPlayMinMs / 2));

                    if (timeToWaitMs > 0)
                    {
                        Thread.Sleep(timeToWaitMs);
                    }

                    PlayTargetMove();
                }
                catch (Exception ex)
                {
                    ExceptionUtils.HandleException(ex);
                }
            });
        }

        private void OnEngineTextUpdate(string engineText)
        {
            EngineTextUpdated?.Invoke(engineText);
        }

        private void OnTargetPieceUpdate(PieceName pieceName)
        {
            TargetPosition = null;

            TargetPieceUpdated?.Invoke(pieceName);
        }

        private void OnTargetPositionUpdate(Position position)
        {
            TargetMove = null;
            if (TargetPiece != PieceName.INVALID && null != TargetPosition)
            {
                TargetMove = new Move(TargetPiece, TargetPosition);
            }

            TargetPositionUpdated?.Invoke(position);
        }

        private enum EngineCommand
        {
            Unknown = -1,
            Info = 0,
            Help,
            Board,
            NewGame,
            Play,
            Pass,
            ValidMoves,
            BestMove,
            Undo,
            History,
            Exit
        }
    }
}
