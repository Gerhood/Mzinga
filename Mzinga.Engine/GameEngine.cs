﻿// 
// GameEngine.cs
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

using Mzinga.Core;
using Mzinga.Core.AI;

namespace Mzinga.Engine
{
    public delegate void ConsoleOut(string format, params object[] arg);

    public class GameEngine
    {
        public string ID { get; private set; }
        public ConsoleOut ConsoleOut { get; private set; }
        public GameEngineConfig Config { get; private set; }

        private GameBoard GameBoard;

        private GameAI _gameAI;

        private Move _cachedBestMove;

        public bool ExitRequested { get; private set; }

        public GameEngine(string id, GameEngineConfig config, ConsoleOut consoleOut)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }

            if (null == config)
            {
                throw new ArgumentNullException("config");
            }

            if (null == consoleOut)
            {
                throw new ArgumentNullException("consoleOut");
            }

            ID = id;
            Config = config;
            ConsoleOut = consoleOut;

            _gameAI = Config.GetGameAI();

            ExitRequested = false;
        }

        public void ParseCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException("command");
            }

            string[] split = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                string cmd = split[0].ToLower();

                int paramCount = split.Length - 1;

                switch (cmd)
                {
                    case "info":
                        Info();
                        break;
                    case "?":
                    case "help":
                        Help();
                        break;
                    case "board":
                        PrintBoard();
                        break;
                    case "newgame":
                        NewGame();
                        break;
                    case "play":
                        if (paramCount == 0)
                        {
                            PlayBestMove();
                        }
                        else
                        {
                            Play(split[1]);
                        }
                        break;
                    case "pass":
                        Pass();
                        break;
                    case "validmoves":
                        if (paramCount == 0)
                        {
                            ValidMoves();
                        }
                        else
                        {
                            ValidMoves(split[1]);
                        }
                        break;
                    case "bestmove":
                        BestMove();
                        break;
                    case "undo":
                        if (paramCount == 0)
                        {
                            Undo();
                        }
                        else
                        {
                            Undo(int.Parse(split[1]));
                        }
                        
                        break;
                    case "history":
                        History();
                        break;
                    case "exit":
                        Exit();
                        break;
                    default:
                        throw new Exception("Unknown command.");
                }
            }
            catch (InvalidMoveException ex)
            {
                ConsoleOut("invalidmove {0}", ex.Message);
            }
            catch (Exception ex)
            {
                ConsoleOut("err {0}", ex.Message.Replace("\r\n", " "));
            }
            ConsoleOut("ok");
        }

        private void Info()
        {
            ConsoleOut("id {0}", ID);
        }

        private void Help()
        {
            ConsoleOut("Available commands: ");
            ConsoleOut("info");
            ConsoleOut("help");
            ConsoleOut("board");
            ConsoleOut("newgame");
            ConsoleOut("play");
            ConsoleOut("pass");
            ConsoleOut("validmoves");
            ConsoleOut("bestmove");
            ConsoleOut("undo");
            ConsoleOut("history");
            ConsoleOut("exit");
        }

        private void PrintBoard()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            ConsoleOut(GameBoard.ToString());
        }

        private void NewGame()
        {
            GameBoard = new GameBoard();
            _cachedBestMove = null;

            GameBoard.BoardChanged += () =>
            {
                _cachedBestMove = null;
            };

            _gameAI.ResetCaches();

            ConsoleOut(GameBoard.ToString());
        }

        private void PlayBestMove()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (GameBoard.GameIsOver)
            {
                throw new GameIsOverException();
            }

            Move bestMove = GetBestMove();

            GameBoard.Play(bestMove);
            ConsoleOut(GameBoard.ToString());
        }

        private void Play(string moveString)
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (GameBoard.GameIsOver)
            {
                throw new GameIsOverException();
            }

            GameBoard.Play(new Move(moveString));
            ConsoleOut(GameBoard.ToString());
        }

        private void Pass()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (GameBoard.GameIsOver)
            {
                throw new GameIsOverException();
            }

            GameBoard.Pass();
            ConsoleOut(GameBoard.ToString());
        }

        private void ValidMoves()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (GameBoard.GameIsOver)
            {
                throw new GameIsOverException();
            }

            MoveSet validMoves = GameBoard.GetValidMoves();
            ConsoleOut(validMoves.ToString());
        }

        private void ValidMoves(string pieceName)
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (string.IsNullOrWhiteSpace(pieceName))
            {
                throw new ArgumentNullException(pieceName);
            }

            if (GameBoard.GameIsOver)
            {
                throw new GameIsOverException();
            }

            MoveSet validMoves = GameBoard.GetValidMoves(EnumUtils.ParseShortName(pieceName));
            ConsoleOut(validMoves.ToString());
        }

        private void BestMove()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (GameBoard.GameIsOver)
            {
                throw new GameIsOverException();
            }

            Move bestMove = GetBestMove();

            if (null != bestMove)
            {
                ConsoleOut(bestMove.ToString());
            }
        }

        private void Undo(int moves = 1)
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (moves < 1)
            {
                throw new UndoTooFewMoves();
            }
            else if (moves > GameBoard.BoardHistoryCount)
            {
                throw new UndoTooManyMoves();
            }

            for (int i = 0; i < moves; i++)
            {
                GameBoard.UndoLastMove();
            }
            ConsoleOut(GameBoard.ToString());
        }

        private void History()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            BoardHistory history = new BoardHistory(GameBoard.BoardHistory);
            ConsoleOut(history.ToString());
        }

        private void Exit()
        {
            ExitRequested = true;
        }

        private Move GetBestMove()
        {
            if (null == GameBoard)
            {
                throw new NoBoardException();
            }

            if (null == _cachedBestMove)
            {
                _cachedBestMove = _gameAI.GetBestMove(GameBoard);
            }

            return _cachedBestMove;
        }
    }

    public class NoBoardException : Exception
    {
        public NoBoardException() : base("You must start a game before you can do that.") { }
    }

    public class GameIsOverException : Exception
    {
        public GameIsOverException() : base("You can't do that, the game is over.") { }
    }

    public class UndoTooFewMoves : Exception
    {
        public UndoTooFewMoves() : base("You must undo at least one move.") { }
    }

    public class UndoTooManyMoves : Exception
    {
        public UndoTooManyMoves() : base("You cannot undo that many moves.") { }
    }
}
