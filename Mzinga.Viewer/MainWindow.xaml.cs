﻿// 
// MainWindow.xaml.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using Mzinga.Core;
using Mzinga.Viewer.ViewModel;

namespace Mzinga.Viewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel VM
        {
            get
            {
                return DataContext as MainViewModel;
            }
        }

        public Point CanvasCursorPosition
        {
            get
            {
                Point point = MouseUtils.CorrectGetPosition(BoardCanvas);
                point.X -= CanvasOffsetX;
                point.Y -= CanvasOffsetY;
                return point;
            }
        }

        private double HexRadiusRatio = 1.0 / (EnumUtils.NumPieceNames + 1);

        private double PieceCanvasMargin = 3.0;

        private double CanvasOffsetX = 0.0;
        private double CanvasOffsetY = 0.0;

        private double StackShiftRatio = 0.1;

        private Board LastBoard;

        private SolidColorBrush WhiteBrush;
        private SolidColorBrush BlackBrush;

        private SolidColorBrush HighlightEdgeBrush;
        private SolidColorBrush HighlightBodyBrush;

        private SolidColorBrush QueenBeeBrush;
        private SolidColorBrush SpiderBrush;
        private SolidColorBrush BeetleBrush;
        private SolidColorBrush GrasshopperBrush;
        private SolidColorBrush SoldierAntBrush;

        public MainWindow()
        {
            InitializeComponent();

            Closing += MainWindow_Closing;

            // Init brushes
            WhiteBrush = new SolidColorBrush(Colors.White);
            BlackBrush = new SolidColorBrush(Colors.Black);

            HighlightEdgeBrush = new SolidColorBrush(Colors.Orange);
            HighlightBodyBrush = new SolidColorBrush(Colors.Aqua);
            HighlightBodyBrush.Opacity = 0.25;

            QueenBeeBrush = new SolidColorBrush(Colors.Gold);
            SpiderBrush = new SolidColorBrush(Colors.Brown);
            BeetleBrush = new SolidColorBrush(Colors.Purple);
            GrasshopperBrush = new SolidColorBrush(Colors.Green);
            SoldierAntBrush = new SolidColorBrush(Colors.Blue);

            // Bind board updates to VM
            if (null != VM)
            {
                VM.PropertyChanged += VM_PropertyChanged;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            EngineConsoleWindow.Instance.Close();
        }

        private void VM_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Board":
                case "TargetMove":
                    AppViewModel.Instance.DoOnUIThread(() =>
                        {
                            DrawBoard(VM.Board);
                        });
                    break;
            }
        }

        private void DrawBoard(Board board)
        {
            BoardCanvas.Children.Clear();
            WhiteHandStackPanel.Children.Clear();
            BlackHandStackPanel.Children.Clear();

            CanvasOffsetX = 0.0;
            CanvasOffsetX = 0.0;

            if (null != board)
            {
                Point minPoint = new Point(double.MaxValue, double.MaxValue);
                Point maxPoint = new Point(double.MinValue, double.MinValue);

                int maxStack;
                int numPieces;
                Dictionary<int, List<Piece>> piecesInPlay = GetPiecesOnBoard(board, out numPieces, out maxStack);

                double size = HexRadiusRatio * Math.Min(BoardCanvas.ActualHeight, BoardCanvas.ActualWidth);

                WhiteHandStackPanel.MinHeight = board.WhiteHand.Count() > 0 ? (size + PieceCanvasMargin) * 2 : 0;
                BlackHandStackPanel.MinHeight = board.BlackHand.Count() > 0 ? (size + PieceCanvasMargin) * 2 : 0;

                PieceName selectedPieceName = VM.AppVM.EngineWrapper.TargetPiece;
                Position targetPosition = VM.AppVM.EngineWrapper.TargetPosition;

                // Draw the pieces in play
                for (int stack = 0; stack <= maxStack; stack++)
                {
                    if (piecesInPlay.ContainsKey(stack))
                    {
                        foreach (Piece piece in piecesInPlay[stack])
                        {
                            Position position = piece.Position;

                            if (piece.PieceName == selectedPieceName && null != targetPosition)
                            {
                                position = targetPosition;
                            }

                            Point center = GetPoint(position, size, true);

                            HexType hexType = (piece.Color == Core.Color.White) ? HexType.WhitePiece : HexType.BlackPiece;

                            Polygon hex = GetHex(center, size, hexType);
                            BoardCanvas.Children.Add(hex);

                            TextBlock hexText = GetHexText(center, size, piece.PieceName);
                            BoardCanvas.Children.Add(hexText);

                            minPoint = Min(center, size, minPoint);
                            maxPoint = Max(center, size, maxPoint);
                        }
                    }
                }

                // Draw the pieces in white's hand
                foreach (Piece piece in board.WhiteHand)
                {
                    if (piece.PieceName != selectedPieceName || (piece.PieceName == selectedPieceName && null == targetPosition))
                    {
                        Canvas pieceCanvas = GetPieceCanvas(piece, size);
                        WhiteHandStackPanel.Children.Add(pieceCanvas);
                    }
                }

                // Draw the pieces in black's hand
                foreach (Piece piece in board.BlackHand)
                {
                    if (piece.PieceName != selectedPieceName || (piece.PieceName == selectedPieceName && null == targetPosition))
                    {
                        Canvas pieceCanvas = GetPieceCanvas(piece, size);
                        BlackHandStackPanel.Children.Add(pieceCanvas);
                    }
                }

                // Highlight the selected piece
                if (selectedPieceName != PieceName.INVALID)
                {
                    Piece selectedPiece = board.GetPiece(selectedPieceName);

                    if (selectedPiece.InPlay)
                    {
                        Point center = GetPoint(selectedPiece.Position, size, true);

                        Polygon hex = GetHex(center, size, HexType.SelectedPiece);
                        BoardCanvas.Children.Add(hex);

                        minPoint = Min(center, size, minPoint);
                        maxPoint = Max(center, size, maxPoint);
                    }
                }

                // Draw the valid moves for that piece
                MoveSet validMoves = VM.AppVM.EngineWrapper.ValidMoves;

                if (selectedPieceName != PieceName.INVALID && null != validMoves)
                {
                    foreach (Move validMove in validMoves)
                    {
                        if (validMove.PieceName == selectedPieceName)
                        {
                            Point center = GetPoint(validMove.Position, size);

                            Polygon hex = GetHex(center, size, HexType.ValidMove);
                            BoardCanvas.Children.Add(hex);

                            minPoint = Min(center, size, minPoint);
                            maxPoint = Max(center, size, maxPoint);
                        }
                    }
                }

                // Highlight the target position
                if (null != targetPosition)
                {
                    Point center = GetPoint(targetPosition, size, true);

                    Polygon hex = GetHex(center, size, HexType.SelectedMove);
                    BoardCanvas.Children.Add(hex);

                    minPoint = Min(center, size, minPoint);
                    maxPoint = Max(center, size, maxPoint);
                }

                // Translate everything on the board
                double boardWidth = Math.Abs(maxPoint.X - minPoint.X);
                double boardHeight = Math.Abs(maxPoint.Y - minPoint.Y);

                double boardCenterX = minPoint.X + (boardWidth / 2);
                double boardCenterY = minPoint.Y + (boardHeight / 2);

                double canvasCenterX = BoardCanvas.ActualWidth / 2;
                double canvasCenterY = BoardCanvas.ActualHeight / 2;

                double offsetX = canvasCenterX - boardCenterX;
                double offsetY = canvasCenterY - boardCenterY;

                foreach (UIElement child in BoardCanvas.Children)
                {
                    if (null != (child as TextBlock)) // Hex labels
                    {
                        Canvas.SetLeft(child, Canvas.GetLeft(child) + offsetX);
                        Canvas.SetTop(child, Canvas.GetTop(child) + offsetY);
                    }
                    else if (null != (child as Polygon)) // Hexes
                    {
                        Polygon hex = (Polygon)child;
                        PointCollection oldPoints = new PointCollection(hex.Points);

                        hex.Points.Clear();

                        foreach (Point oldPoint in oldPoints)
                        {
                            hex.Points.Add(new Point(oldPoint.X + offsetX, oldPoint.Y + offsetY));
                        }
                    }
                }

                CanvasOffsetX = offsetX;
                CanvasOffsetY = offsetY;

                VM.CanvasHexRadius = size;
            }

            LastBoard = board;
        }

        private Point Min(Point center, double size, Point minPoint)
        {
            double minX = Math.Min(minPoint.X, center.X - size);
            double minY = Math.Min(minPoint.Y, center.Y - size);

            return new Point(minX, minY);
        }

        private Point Max(Point center, double size, Point maxPoint)
        {
            double maxX = Math.Max(maxPoint.X, center.X + size);
            double maxY = Math.Max(maxPoint.Y, center.Y + size);

            return new Point(maxX, maxY);
        }

        private Point GetPoint(Position position, double size, bool stackShift = false)
        {
            if (null == position)
            {
                throw new ArgumentNullException("position");
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("size");
            }

            double x = size * 1.5 * position.Q;
            double y = size * Math.Sqrt(3.0) * (position.R + (0.5 * position.Q));

            if (stackShift && position.Stack > 0)
            {
                x += size * 1.5 * StackShiftRatio * position.Stack;
                y -= size * Math.Sqrt(3.0) * StackShiftRatio * position.Stack;
            }

            return new Point(x, y);
        }

        private Dictionary<int, List<Piece>> GetPiecesOnBoard(Board board, out int numPieces, out int maxStack)
        {
            if (null == board)
            {
                throw new ArgumentNullException("board");
            }

            numPieces = 0;
            maxStack = -1;

            Dictionary<int, List<Piece>> pieces = new Dictionary<int, List<Piece>>();
            pieces[0] = new List<Piece>();

            PieceName targetPieceName = VM.AppVM.EngineWrapper.TargetPiece;
            Position targetPosition = VM.AppVM.EngineWrapper.TargetPosition;

            bool targetPieceInPlay = false;

            // Add pieces already on the board
            foreach (Piece piece in board.PiecesInPlay)
            {
                Position position = piece.Position;

                if (piece.PieceName == targetPieceName)
                {
                    if (null != targetPosition)
                    {
                        position = targetPosition;
                    }
                    targetPieceInPlay = true;
                }

                int stack = position.Stack;
                maxStack = Math.Max(maxStack, stack);

                if (!pieces.ContainsKey(stack))
                {
                    pieces[stack] = new List<Piece>();
                }

                pieces[stack].Add(piece);
                numPieces++;
            }

            // Add piece being placed on the board
            if (!targetPieceInPlay && null != targetPosition)
            {
                int stack = targetPosition.Stack;
                maxStack = Math.Max(maxStack, stack);

                if (!pieces.ContainsKey(stack))
                {
                    pieces[stack] = new List<Piece>();
                }

                pieces[stack].Add(new Piece(targetPieceName, targetPosition));
                numPieces++;
            }

            return pieces;
        }

        private Polygon GetHex(Point center, double size, HexType hexType)
        {
            if (null == center)
            {
                throw new ArgumentNullException("center");
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("size");
            }

            Polygon hex = new Polygon();
            hex.StrokeThickness = 2;

            switch (hexType)
            {
                case HexType.WhitePiece:
                    hex.Fill = WhiteBrush;
                    hex.Stroke = BlackBrush;
                    break;
                case HexType.BlackPiece:
                    hex.Fill = BlackBrush;
                    hex.Stroke = BlackBrush;
                    break;
                case HexType.ValidMove:
                    hex.Fill = HighlightBodyBrush;
                    break;
                case HexType.SelectedPiece:
                    hex.Stroke = HighlightEdgeBrush;
                    break;
                case HexType.SelectedMove:
                    hex.Fill = HighlightBodyBrush;
                    hex.Stroke = HighlightEdgeBrush;
                    break;
            }

            PointCollection points = new PointCollection();

            for (int i = 0; i < 6; i++)
            {
                double angle_deg = 60.0 * i;
                double angle_rad = Math.PI / 180 * angle_deg;
                points.Add(new Point(center.X + size * Math.Cos(angle_rad), center.Y + size * Math.Sin(angle_rad)));
            }

            hex.Points = points;

            return hex;
        }

        private TextBlock GetHexText(Point center, double size, PieceName pieceName)
        {
            if (null == center)
            {
                throw new ArgumentNullException("center");
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("size");
            }

            TextBlock hexText = new TextBlock();
            hexText.Text = EnumUtils.GetShortName(pieceName).Substring(1);
            hexText.FontFamily = new FontFamily("Lucida Console");

            switch (pieceName)
            {
                case PieceName.WhiteQueenBee:
                case PieceName.BlackQueenBee:
                    hexText.Foreground = QueenBeeBrush;
                    break;
                case PieceName.WhiteSpider1:
                case PieceName.WhiteSpider2:
                case PieceName.BlackSpider1:
                case PieceName.BlackSpider2:
                    hexText.Foreground = SpiderBrush;
                    break;
                case PieceName.WhiteBeetle1:
                case PieceName.WhiteBeetle2:
                case PieceName.BlackBeetle1:
                case PieceName.BlackBeetle2:
                    hexText.Foreground = BeetleBrush;
                    break;
                case PieceName.WhiteGrasshopper1:
                case PieceName.WhiteGrasshopper2:
                case PieceName.WhiteGrassHopper3:
                case PieceName.BlackGrasshopper1:
                case PieceName.BlackGrasshopper2:
                case PieceName.BlackGrassHopper3:
                    hexText.Foreground = GrasshopperBrush;
                    break;
                case PieceName.WhiteSoldierAnt1:
                case PieceName.WhiteSoldierAnt2:
                case PieceName.WhiteSoldierAnt3:
                case PieceName.BlackSoldierAnt1:
                case PieceName.BlackSoldierAnt2:
                case PieceName.BlackSoldierAnt3:
                    hexText.Foreground = SoldierAntBrush;
                    break;
            }

            hexText.FontSize = size;

            Canvas.SetLeft(hexText, center.X - (hexText.Text.Length * (hexText.FontSize / 3.5)));
            Canvas.SetTop(hexText, center.Y - (hexText.FontSize / 2.0));

            return hexText;
        }

        private enum HexType
        {
            WhitePiece,
            BlackPiece,
            ValidMove,
            SelectedPiece,
            SelectedMove
        }

        private Canvas GetPieceCanvas(Piece piece, double size)
        {
            Point center = new Point(size, size);

            HexType hexType = (piece.Color == Core.Color.White) ? HexType.WhitePiece : HexType.BlackPiece;

            Polygon hex = GetHex(center, size, hexType);
            TextBlock hexText = GetHexText(center, size, piece.PieceName);

            Canvas pieceCanvas = new Canvas();
            pieceCanvas.Height = size * 2;
            pieceCanvas.Width = size * 2;
            pieceCanvas.Margin = new Thickness(PieceCanvasMargin);
            pieceCanvas.Background = (piece.Color == Core.Color.White) ? WhiteHandStackPanel.Background : BlackHandStackPanel.Background;

            pieceCanvas.Name = EnumUtils.GetShortName(piece.PieceName);

            pieceCanvas.Children.Add(hex);
            pieceCanvas.Children.Add(hexText);

            // Add highlight if the piece is selected
            if (VM.AppVM.EngineWrapper.TargetPiece == piece.PieceName)
            {
                Polygon highlightHex = GetHex(center, size, HexType.SelectedPiece);
                pieceCanvas.Children.Add(highlightHex);
            }

            pieceCanvas.MouseLeftButtonDown += PieceCanvas_MouseLeftButtonDown;

            return pieceCanvas;
        }

        private void PieceCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Canvas pieceCanvas = sender as Canvas;

            if (null != pieceCanvas)
            {
                PieceName clickedPiece = EnumUtils.ParseShortName(pieceCanvas.Name);
                VM.PieceClick(clickedPiece);
            }
        }

        private void BoardCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = CanvasCursorPosition;
            VM.CanvasClick(p.X, p.Y);
        }

        private DateTime LastRedrawOnSizeChange = DateTime.Now;

        private void BoardCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DateTime.Now - LastRedrawOnSizeChange > TimeSpan.FromMilliseconds(20))
            {
                DrawBoard(LastBoard);
                LastRedrawOnSizeChange = DateTime.Now;
            }
        }
    }
}
