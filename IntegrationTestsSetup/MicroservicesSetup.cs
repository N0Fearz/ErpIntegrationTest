
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.RabbitMq;
using Testcontainers.PostgreSql;

namespace IntegrationTestsRepo.IntegrationTests;

public class MicroservicesSetup : IAsyncDisposable
{
    private INetwork _network;

    public IContainer ArticleService { get; private set; }
    public IContainer OrganizationService { get; private set; }
    public RabbitMqContainer RabbitMqContainer { get; private set; }
    public string HostName => RabbitMqContainer.Hostname;
    public PostgreSqlContainer PostgresContainer { get; private set; }
    public string DatabaseOrdersConnectionString { get; private set; }
    public string DatabaseArticlesConnectionString { get; private set; }
    public string DatabaseOrganizationConnectionString { get; private set; }
    public string DatabaseArticlesConnectionStringLocal { get; private set; }
    public int ArticleServicePort { get; private set; }
    private string _articleDbString;
    public async Task StartServicesAsync()
    {
        _network = new NetworkBuilder()
            .WithName("test-network")
            .WithReuse(true)
            .Build();

        await _network.CreateAsync();
        
        RabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management") 
            .WithUsername("guest")
            .WithPassword("guest")
            .WithPortBinding(5672, false)
            .WithPortBinding(15672, false)
            .WithNetwork(_network)
            .WithNetworkAliases("rabbitmq")
            .Build();
        await RabbitMqContainer.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(10));
        
        Console.WriteLine($"RabbitMQ Host: {RabbitMqContainer.Hostname}");
        Console.WriteLine($"RabbitMQ Port: {RabbitMqContainer.GetMappedPublicPort(5672)}");
        
        PostgresContainer = new PostgreSqlBuilder()
            .WithPortBinding(5432, false)
            .WithDatabase("organizations")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithNetwork(_network)
            .WithNetworkAliases("postgres")
            .Build();
        await PostgresContainer.StartAsync();
        await CreateAdditionalDatabasesAsync();
        
        ArticleService = new ContainerBuilder()
            .WithImage("casgoorman/articleservice:latest")
            .WithExposedPort(8080)
            .WithPortBinding(0, 8080)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("DISABLE_AUTH", "true")
            .WithEnvironment("RabbitMQ__HostName", "rabbitmq")
            .WithEnvironment("RabbitMQ__UserName", "guest")
            .WithEnvironment("RabbitMQ__Password", "guest")
            .WithEnvironment("ConnectionStrings__ArticleDB", _articleDbString)
            .WithNetwork(_network)
            .WithNetworkAliases("articleservice")
            .Build();
            
        OrganizationService = new ContainerBuilder()
            .WithImage("casgoorman/organizationservice:latest")
            .WithExposedPort(8080)
            .WithPortBinding(0, 8080)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("RabbitMQ__HostName", "rabbitmq")
            .WithEnvironment("RabbitMQ__UserName", "guest")
            .WithEnvironment("RabbitMQ__Password", "guest")
            .WithEnvironment("ConnectionStrings__OrderDB", DatabaseOrdersConnectionString)
            .WithEnvironment("ConnectionStrings__ErpDB", DatabaseArticlesConnectionString)
            .WithEnvironment("ConnectionStrings__OrganizationsDB", DatabaseOrganizationConnectionString)
            .WithNetwork(_network)
            .WithNetworkAliases("organizationservice")
            .Build();

        await Task.WhenAll(ArticleService.StartAsync(), OrganizationService.StartAsync());
        ArticleServicePort = ArticleService.GetMappedPublicPort(8080);
    }
    
    private async Task CreateAdditionalDatabasesAsync()
    {
        // Maak verbinding met de 'postgres' standaarddatabase
        var adminConnectionString = PostgresContainer.GetConnectionString();
        DatabaseArticlesConnectionStringLocal = adminConnectionString.Replace("organizations", "articles");
        
        const string createDbSql = @"
            CREATE DATABASE orders;
            CREATE DATABASE articles;
        ";

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(createDbSql, connection);
        await command.ExecuteNonQueryAsync();

        var connectionStrongCorrectHost = adminConnectionString.Replace("127.0.0.1", "postgres");
        DatabaseOrdersConnectionString = connectionStrongCorrectHost.Replace("organizations", "orders");
        DatabaseArticlesConnectionString = connectionStrongCorrectHost.Replace("organizations", "articles");
        DatabaseOrganizationConnectionString = connectionStrongCorrectHost;
        _articleDbString = DatabaseArticlesConnectionString + ";SearchPath=";
    }

    public async Task StopAsync()
    {
        PostgresContainer.DisposeAsync();
        RabbitMqContainer.DisposeAsync();
        ArticleService.DisposeAsync();
        OrganizationService.DisposeAsync();
        _network.DisposeAsync();
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}