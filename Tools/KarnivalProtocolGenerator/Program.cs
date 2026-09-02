using Karnival.ProtocolGeneration;

if (args.Length is < 3 or > 4 ||
    (args.Length == 4 && args[3] != "--verify"))
{
    Console.Error.WriteLine(
        "Usage: KarnivalProtocolGenerator <schema> <server-output> <client-output> [--verify]");
    return 2;
}

try
{
    ProtocolSchema schema = ProtocolCodeGenerator.ReadSchema(args[0]);
    string generated = ProtocolCodeGenerator.Render(schema);
    if (args.Length == 4)
    {
        bool serverMatches = ProtocolCodeGenerator.Matches(args[1], generated);
        bool clientMatches = ProtocolCodeGenerator.Matches(args[2], generated);
        if (!serverMatches)
            Console.Error.WriteLine($"Generated server protocol is stale: {args[1]}");
        if (!clientMatches)
            Console.Error.WriteLine($"Generated client protocol is stale: {args[2]}");
        return serverMatches && clientMatches ? 0 : 1;
    }

    ProtocolCodeGenerator.Write(args[1], generated);
    ProtocolCodeGenerator.Write(args[2], generated);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
