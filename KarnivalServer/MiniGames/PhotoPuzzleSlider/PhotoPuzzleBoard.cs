public enum PhotoPuzzleImageId : byte
{
    Cat = 1,
    Logo = 2,
}

public static class PhotoPuzzleBoard
{
    public const int SideLength = 3;
    public const int SlotCount = SideLength * SideLength;
    public const byte Empty = byte.MaxValue;

    public static byte[] CreateSolved(byte missingTileId)
    {
        byte[] board = new byte[SlotCount];
        for (byte tileId = 0; tileId < SlotCount; tileId++)
            board[tileId] = tileId == missingTileId ? Empty : tileId;

        return board;
    }

    public static bool AreAdjacent(int firstSlot, int secondSlot)
    {
        if (!IsSlot(firstSlot) || !IsSlot(secondSlot))
            return false;

        int rowDelta = Math.Abs((firstSlot / SideLength) - (secondSlot / SideLength));
        int columnDelta = Math.Abs((firstSlot % SideLength) - (secondSlot % SideLength));
        return rowDelta + columnDelta == 1;
    }

    public static int Find(byte[] board, byte value)
    {
        if (board == null)
            return -1;

        for (int slot = 0; slot < board.Length; slot++)
        {
            if (board[slot] == value)
                return slot;
        }

        return -1;
    }

    public static int CountCorrectTiles(byte[] board, byte missingTileId)
    {
        if (board == null || board.Length != SlotCount)
            return 0;

        int correctCount = 0;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (slot != missingTileId && board[slot] == slot)
                correctCount++;
        }

        return correctCount;
    }

    public static bool IsSolved(byte[] board, byte missingTileId)
    {
        return board != null &&
            board.Length == SlotCount &&
            board[missingTileId] == Empty &&
            CountCorrectTiles(board, missingTileId) == SlotCount - 1;
    }

    public static int GetManhattanDistance(byte[] board)
    {
        if (board == null || board.Length != SlotCount)
            return 0;

        int distance = 0;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            byte tileId = board[slot];
            if (tileId == Empty || tileId >= SlotCount)
                continue;

            distance += Math.Abs((slot / SideLength) - (tileId / SideLength));
            distance += Math.Abs((slot % SideLength) - (tileId % SideLength));
        }

        return distance;
    }

    public static int[] GetAdjacentSlots(int slot)
    {
        if (!IsSlot(slot))
            return Array.Empty<int>();

        List<int> adjacent = new(4);
        int row = slot / SideLength;
        int column = slot % SideLength;
        if (row > 0)
            adjacent.Add(slot - SideLength);
        if (row < SideLength - 1)
            adjacent.Add(slot + SideLength);
        if (column > 0)
            adjacent.Add(slot - 1);
        if (column < SideLength - 1)
            adjacent.Add(slot + 1);
        return adjacent.ToArray();
    }

    private static bool IsSlot(int slot)
    {
        return slot >= 0 && slot < SlotCount;
    }
}