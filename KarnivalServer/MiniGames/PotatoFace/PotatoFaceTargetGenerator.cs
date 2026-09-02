public static class PotatoFaceTargetGenerator
{
    public static PotatoFaceTarget Generate(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return new PotatoFaceTarget(
            NextVariant(random),
            NextVariant(random),
            NextVariant(random));
    }

    private static PotatoFacePartVariant NextVariant(Random random) =>
        random.Next(2) == 0
            ? PotatoFacePartVariant.One
            : PotatoFacePartVariant.Two;

}
