public enum PotatoFacePartVariant : byte
{
    None = 0,
    One = 1,
    Two = 2,
}

public readonly record struct PotatoFacePosition(float X, float Y);

public readonly record struct PotatoFaceTarget(
    PotatoFacePartVariant EyesVariant,
    PotatoFacePartVariant MouthVariant,
    PotatoFacePartVariant NoseVariant);
