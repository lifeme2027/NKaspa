namespace NKaspa.Samples.Tutorials
{
    /// <summary>
    /// Offline checks for the English tutorials under docs/tutorials.
    /// No socket is opened. <see cref="SendTutorial.SendOnTestnetAsync"/> compiles the broadcast path and is not called here.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            _ = args;
            var writer = Console.Out;
            if (AddressTutorial.Run(writer) != 0)
            {
                return 1;
            }

            if (SendTutorial.RunOffline(writer) != 0)
            {
                return 1;
            }

            writer.WriteLine("ok");
            return 0;
        }
    }
}
