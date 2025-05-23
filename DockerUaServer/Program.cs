namespace DockerUaServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.Configure<DockerUaServerSettings>(builder.Configuration.GetSection("DockerUaServerSettings"));

            builder.Services.AddSingleton<IDockerUaServer, DockerUaServer>();

            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}