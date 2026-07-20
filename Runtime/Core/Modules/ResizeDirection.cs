using System;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Which edge(s) a resize grip drives. Edges are single bits; corners combine one edge per axis, so a handler
    /// can test each axis independently (<c>(dir &amp; Left) != 0</c>).
    /// </summary>
    [Flags]
    public enum ResizeDirection
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Top = 1 << 2,
        Bottom = 1 << 3,

        TopLeft = Top | Left,
        TopRight = Top | Right,
        BottomLeft = Bottom | Left,
        BottomRight = Bottom | Right,
    }
}
