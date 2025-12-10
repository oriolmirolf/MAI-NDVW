using UnityEngine;

public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => dir
        };
    }

    public static Vector2Int ToVector(this Direction dir)
    {
        return dir switch
        {
            Direction.North => Vector2Int.up,
            Direction.South => Vector2Int.down,
            Direction.East => Vector2Int.right,
            Direction.West => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }

    public static float ToRotation(this Direction dir)
    {
        return dir switch
        {
            Direction.North => 90f,
            Direction.South => -90f,
            Direction.East => 0f,
            Direction.West => 180f,
            _ => 0f
        };
    }
}

[System.Flags]
public enum DoorMask
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
    All = North | East | South | West
}

public static class DoorMaskExtensions
{
    public static bool Has(this DoorMask mask, Direction dir)
    {
        return dir switch
        {
            Direction.North => (mask & DoorMask.North) != 0,
            Direction.East => (mask & DoorMask.East) != 0,
            Direction.South => (mask & DoorMask.South) != 0,
            Direction.West => (mask & DoorMask.West) != 0,
            _ => false
        };
    }

    public static DoorMask Add(this DoorMask mask, Direction dir)
    {
        return dir switch
        {
            Direction.North => mask | DoorMask.North,
            Direction.East => mask | DoorMask.East,
            Direction.South => mask | DoorMask.South,
            Direction.West => mask | DoorMask.West,
            _ => mask
        };
    }

    public static DoorMask FromDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => DoorMask.North,
            Direction.South => DoorMask.South,
            Direction.East => DoorMask.East,
            Direction.West => DoorMask.West,
            _ => DoorMask.None
        };
    }
}
