using System.Text;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace IntegrationTestsRepo.IntegrationTests;

public class KeycloakMockMessagePublisher
{
    public void PublishMessage(string message, string hostname, int port)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostname,
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        var queueName = "example_queue";

// Declare een queue
        channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

// Bind de queue aan de exchange met de routing key
        channel.QueueBind(queue: queueName, exchange: "amq.topic", routingKey: "KK.EVENT.ADMIN.organizations.SUCCESS.ORGANIZATION.CREATE");
        var body = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish(exchange: "amq.topic", routingKey: "KK.EVENT.ADMIN.organizations.SUCCESS.ORGANIZATION.CREATE", mandatory: true, basicProperties: null, body: body);
        
        channel.BasicReturn += (sender, args) =>
        {
            Console.WriteLine($"Message was not delivered. ReplyText: {args.ReplyText}");
        };
    }
}