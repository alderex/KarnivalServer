public readonly struct CupAndBallSwap
{
    public CupAndBallSwap(byte firstSlot, byte secondSlot)
    {
        FirstSlot = firstSlot;
        SecondSlot = secondSlot;
    }

    public byte FirstSlot { get; }
    public byte SecondSlot { get; }
}
