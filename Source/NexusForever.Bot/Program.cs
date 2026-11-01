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
                    uint accountId = await BotDatabase.PrepAsync(prep, timeout.Token);
                    Console.WriteLine($"prepared account {accountId} with SRP6 credentials and no game token");
                    return 0;
                }
                case InfoOptions info:
                    return QuestInfo.Run(info);
                case RunOptions run:
                {
                    var report = new RunReport { QuestId = run.QuestIds.FirstOrDefault() };
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
