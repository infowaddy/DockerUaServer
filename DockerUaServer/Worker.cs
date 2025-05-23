namespace DockerUaServer
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private IDockerUaServer dockerUaServer;

        public Worker(ILogger<Worker> _logger, IDockerUaServer dockerUaServer)
        {
            this.logger = _logger;
            this.dockerUaServer = dockerUaServer;
        }


        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting OPC UA Server...");
            await dockerUaServer.Start(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping OPC UA Server...");
            dockerUaServer.Stop(cancellationToken);
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Docker Ua Server is running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}
