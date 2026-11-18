namespace Brain.Data.Collection.Space;

internal readonly record struct Cell3D(int X, int Y, int Z)
{
    public static Cell3D operator +(Cell3D l, Cell3D r)
    {
        return new Cell3D(l.X + r.X, l.Y + r.Y, l.Z + r.Z);
    }
}