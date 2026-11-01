namespace NexusForever.Bot;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            object options = CliOptions.Parse(args);
            switch (options)
            {
                case PrepOptions prep:
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    Guid token = await BotDatabase.PrepAsync(prep, timeout.Token);
                    Console.WriteLine(token);
                    return 0;
                }
                case RunOptions run:
                {
                    var report = new RunReport { QuestId = run.QuestId };
                    await using var log = new BotLog(run.Log);
                    var runner = new BotRunner(run, report, log);
                    return await runner.RunAsync();
                }
                default:
                    throw new ArgumentException(CliOptions.Usage);
            }
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }
}
