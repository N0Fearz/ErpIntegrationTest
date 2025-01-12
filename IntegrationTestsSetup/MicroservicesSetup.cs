
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
    public IContainer OrderService { get; private set; }
    public IContainer OrganizationService { get; private set; }
    public RabbitMqContainer RabbitMqContainer { get; private set; }
    public string HostName => RabbitMqContainer.Hostname;
    public PostgreSqlContainer PostgresContainer { get; private set; }
    public string DatabaseOrdersConnectionString { get; private set; }
    public string DatabaseArticlesConnectionString { get; private set; }
    public string DatabaseOrganizationConnectionString { get; private set; }
    public string DatabaseArticlesConnectionStringLocal { get; private set; }
    public int ArticleServicePort { get; private set; }
    public int OrderServicePort { get; private set; }
    public async Task StartServicesAsync()
    {
        _network = new NetworkBuilder()
            .WithName("test-network")
            .WithReuse(true)
            .Build();

        await _network.CreateAsync();
        
        RabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management") 
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithNetwork(_network)
            .WithNetworkAliases("rabbitmq")
            .WithWaitStrategy((Wait.ForUnixContainer().UntilPortIsAvailable(5672)))
            .Build();
        await RabbitMqContainer.StartAsync();
        
        Console.WriteLine($"RabbitMQ Host: {RabbitMqContainer.Hostname}");
        Console.WriteLine($"RabbitMQ Port: {RabbitMqContainer.GetMappedPublicPort(5672)}");
        
        PostgresContainer = new PostgreSqlBuilder()
            .WithPortBinding(5432, true)
            .WithDatabase("organizations")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithNetwork(_network)
            .WithNetworkAliases("postgres")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("database system is ready to accept connections"))
            .Build();
        await PostgresContainer.StartAsync();
        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
        await CreateAdditionalDatabasesAsync();
        
        OrganizationService = new ContainerBuilder()
            .WithImage("casgoorman/organizationservice:latest")
            // .WithExposedPort(8080)
            .WithPortBinding(0, 8080)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("RabbitMQ__HostName", "rabbitmq")
            .WithEnvironment("RabbitMQ__UserName", "testuser")
            .WithEnvironment("RabbitMQ__Password", "testpassword")
            .WithEnvironment("ConnectionStrings__OrganizationsDB", DatabaseOrganizationConnectionString)
            .WithNetwork(_network)
            .WithNetworkAliases("organizationservice")
            .WithWaitStrategy((Wait.ForUnixContainer().UntilPortIsAvailable(8080)))
            .Build();
        await OrganizationService.StartAsync();
        ArticleService = new ContainerBuilder()
            .WithImage("casgoorman/articleservice:latest")
            .WithExposedPort(8080)
            .WithPortBinding(0, 8080)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("DISABLE_AUTH", "true")
            .WithEnvironment("RabbitMQ__HostName", "rabbitmq")
            .WithEnvironment("RabbitMQ__UserName", "testuser")
            .WithEnvironment("RabbitMQ__Password", "testpassword")
            .WithEnvironment("ConnectionStrings__ArticleDB", DatabaseArticlesConnectionString)
            .WithNetwork(_network)
            .WithNetworkAliases("articleservice")
            .WithWaitStrategy((Wait.ForUnixContainer().UntilPortIsAvailable(8080)))
            .Build();
        await ArticleService.StartAsync();
        ArticleServicePort = ArticleService.GetMappedPublicPort(8080);
        OrderService = new ContainerBuilder()
            .WithImage("casgoorman/orderservice:latest")
            .WithExposedPort(8080)
            .WithPortBinding(0, 8080)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("DISABLE_AUTH", "true")
            .WithEnvironment("RabbitMQ__HostName", "rabbitmq")
            .WithEnvironment("RabbitMQ__UserName", "testuser")
            .WithEnvironment("RabbitMQ__Password", "testpassword")
            .WithEnvironment("ConnectionStrings__OrderDB", DatabaseOrdersConnectionString)
            .WithNetwork(_network)
            .WithNetworkAliases("orderservice")
            .WithWaitStrategy((Wait.ForUnixContainer().UntilPortIsAvailable(8080)))
            .Build();
        await OrderService.StartAsync();
        OrderServicePort = OrderService.GetMappedPublicPort(8080);
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

        
        DatabaseOrdersConnectionString = "Server=postgres;Port=5432;Database=orders;User Id=postgres;Password=postgres;";
        DatabaseArticlesConnectionString = "Server=postgres;Port=5432;Database=articles;User Id=postgres;Password=postgres;";
        DatabaseOrganizationConnectionString = "Server=postgres;Port=5432;Database=organizations;User Id=postgres;Password=postgres;";
    }

    public async Task StopAsync()
    {
        PostgresContainer.DisposeAsync();
        RabbitMqContainer.DisposeAsync();
        ArticleService.DisposeAsync();
        OrganizationService.DisposeAsync();
        OrderService.DisposeAsync();
        _network.DisposeAsync();
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
