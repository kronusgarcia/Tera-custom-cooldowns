﻿using System;

namespace TCC.Data
{
    public class Section
    {
        public uint Id { get; }
        public uint NameId { get; }
        public string MapId { get; }
        public double Top { get; }
        public double Left { get; }
        public double Width { get; }
        public double Height { get; }
        public bool IsDungeon { get; }
        public double Scale
        {
            get
            {
                return Width / (Double)App.Current.FindResource("MapWidth");
            }
        }
        public Section(uint sId, uint sNameId, string mapId, double top, double left, double width, double height, bool dg)
        {
            Id = sId;
            NameId = sNameId;
            MapId = mapId;
            Top = top;
            Left = left;
            Width = width;
            Height = height;
            IsDungeon = dg;
        }

        public bool ContainsPoint(float x, float y)
        {
            var matchesY = y > Left &&  y < Width + Left;
            var matchesX = x < Top && x > Top - Height;
            if (matchesX & matchesY)
            {
                Console.WriteLine($"  |  X:{x}\n  |  T:{Top} - B:{Top-Height}");
                Console.WriteLine($"  |  Y:{y}\n  |  L:{Left} - R:{Left+Width}");
            }
            return matchesX && matchesY;
        }
    }
}
