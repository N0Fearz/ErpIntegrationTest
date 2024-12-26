using System.Net.Http.Headers;
using Npgsql;

namespace IntegrationTestsRepo.IntegrationTests;

[Collection("MicroserviceTests")]
[TestCaseOrderer(ordererTypeName: "IntegrationTestsRepo.IntegrationTests.PriorityOrderer", "IntegrationTestsRepo")]
public class OrganizationIntegrationTests
{
    private readonly MicroservicesSetup _microservicesSetup;
    private readonly KeycloakMockMessagePublisher _keycloakMockMessagePublisher;
    
    public OrganizationIntegrationTests(MicroservicesSetup microservicesSetup)
    {
        _microservicesSetup = microservicesSetup;
        _keycloakMockMessagePublisher = new KeycloakMockMessagePublisher();
    }
    
    [Fact, TestPriority(1)]
    public async Task Tes1_OrganizationService_Should_Process_Keycloak_Organization_Creation_Event()
    {
        // Arrange
        await _microservicesSetup.StartServicesAsync();

        const string testMessage = "{\n\"@class\" : \"com.github.aznamier.keycloak.event.provider.EventAdminNotificationMqMsg\",\n\"time\" : 1734471669619,\n\"realmId\" : \"f7976e0d-14ab-4ea0-8a87-032f9c16151f\",\n\"authDetails\" : {\n\"realmId\" : \"d5061ec1-18e9-4430-89fe-068f08c9b5ff\",\n\"realmName\" : \"master\",\n\"clientId\" : \"03a6c6de-20e5-43b5-9bca-da81d4fef626\",\n\"userId\" : \"f4ff4e89-6fe2-45f8-9ecf-384f9bb0ab8d\",\n\"ipAddress\" : \"10.42.0.1\"\n},\n\"resourceType\" : \"ORGANIZATION\",\n\"operationType\" : \"CREATE\",\n\"resourcePath\" : \"organizations/2b36c94f-25c5-4d03-83c6-1429a7371413\",\n\"representation\" : \"{\\\"id\\\":\\\"0ba94660-861a-45af-a01a-344c04fbfc1b\\\",\\\"name\\\":\\\"TestOrganization\\\",\\\"alias\\\":\\\"test1234\\\",\\\"enabled\\\":true,\\\"description\\\":\\\"\\\",\\\"redirectUrl\\\":\\\"\\\",\\\"attributes\\\":{},\\\"domains\\\":[{\\\"name\\\":\\\"idk.com\\\",\\\"verified\\\":false}]}\",\n\"resourceTypeAsString\" : \"ORGANIZATION\"\n}";

        // Act 
        await Task.Delay(5000);
        _keycloakMockMessagePublisher.PublishMessage(
            testMessage, 
            _microservicesSetup.RabbitMqContainer.Hostname, 
            _microservicesSetup.RabbitMqContainer.GetMappedPublicPort(5672)
            );

        // Wait
        await Task.Delay(TimeSpan.FromSeconds(20));

        // Assert
        bool isProcessed = await CheckIfSchemaExists();
        await Task.Delay(TimeSpan.FromSeconds(20));
        Assert.True(isProcessed, "Het bericht is niet correct verwerkt.");
    }
    
    [Fact, TestPriority(4)]
    public async Task Test2_ArticleService_Should_Get_Schema_For_Correct_Organization()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJwQzktZFk0eE9rT1E1b09ac3g2SFFFRi1JbVJkSWRZLWdVWjNOdkVBcl9nIn0.eyJleHAiOjE3MzIzMDU4NDUsImlhdCI6MTczMjMwNTU0NSwiYXV0aF90aW1lIjoxNzMyMzA1NTQ0LCJqdGkiOiJiMGY5NDZkMC03ZTBiLTQ1YzktYmRkZC03OWQxMzZlMTI0NjEiLCJpc3MiOiJodHRwOi8vMTkyLjE2OC4yLjE1Mjo4MDg2L3JlYWxtcy9PcmdhbmlzYXRpb25zIiwiYXVkIjoiYWNjb3VudCIsInN1YiI6IjEwMWU4YzZjLWYxYzItNDQwZi1iMjY5LTgxZGY2YWIwMzkzMyIsInR5cCI6IkJlYXJlciIsImF6cCI6ImZyb250ZW5kLWVycCIsInNpZCI6ImI1YWUwYTMzLTM2OGYtNGU0MC05OWVhLTRjY2E2NjJlOWQwYyIsImFjciI6IjEiLCJhbGxvd2VkLW9yaWdpbnMiOlsiaHR0cDovL2xvY2FsaG9zdDozMDAwIl0sInJlYWxtX2FjY2VzcyI6eyJyb2xlcyI6WyJvZmZsaW5lX2FjY2VzcyIsImRlZmF1bHQtcm9sZXMtb3JnYW5pc2F0aW9ucyIsInVtYV9hdXRob3JpemF0aW9uIl19LCJyZXNvdXJjZV9hY2Nlc3MiOnsiYWNjb3VudCI6eyJyb2xlcyI6WyJtYW5hZ2UtYWNjb3VudCIsIm1hbmFnZS1hY2NvdW50LWxpbmtzIiwidmlldy1wcm9maWxlIl19fSwic2NvcGUiOiJvcGVuaWQgZW1haWwgcHJvZmlsZSBvcmdhbml6YXRpb24iLCJlbWFpbF92ZXJpZmllZCI6ZmFsc2UsIm9yZ2FuaXphdGlvbiI6eyJUZXN0T3JnYW5pemF0aW9uIjp7ImlkIjoiMGJhOTQ2NjAtODYxYS00NWFmLWEwMWEtMzQ0YzA0ZmJmYzFiIn19LCJuYW1lIjoiVGVzdCBUZXN0ZXIiLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJ0ZXN0IiwiZ2l2ZW5fbmFtZSI6IlRlc3QiLCJmYW1pbHlfbmFtZSI6IlRlc3RlciIsImVtYWlsIjoidGVzdEBtYWlsLmNvbSJ9.lqYrXfOxsu2Ef9-rvw2TPASGhgvKpjvboWWADXA4KQteN8q4bBeYDVKCT_HHLeurmF766TJMZCsrn8fGWhdPaWP0SW_DZWtnuSLhsifNsfaxSM7c5xXOiRGk_zMt08YckQphUs4eBkM4yd_vSp5J2_HxH8oiNeaiOC2zh5oHZWw6zBx_Fhzxfu0OOF_zngaaoadGm_5_xTXJlPQMLo07_ffehe7TTzQPvqrG2-cCkU0-YJcsVWY55ieqprCAQQ0LBqhBNBDpKwDTwrJAPc-aX6cFv1NVj-BLW2PurGhDlZkX3gHIBWTTSgwNUGbyMbwi70Ncr3Eqj6zbpNHr3UwJRw");
        
        //Act
        var response = await client.GetAsync($"http://{_microservicesSetup.ArticleService.Hostname}:{_microservicesSetup.ArticleService.GetMappedPublicPort(8080)}/api/Article");
        
        //Assert
        Assert.True(response.IsSuccessStatusCode);
    }
    private async Task<bool> CheckIfSchemaExists()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_microservicesSetup.DatabaseArticlesConnectionStringLocal);
            await connection.OpenAsync();
            const string sql = "SELECT EXISTS (SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'schema_testorganization');";

            await using var command = new NpgsqlCommand(sql, connection);

            var result = await command.ExecuteScalarAsync();

            await Task.Delay(5000); // Simuleer DB-check
            // De query retourneert een bool: true als het schema bestaat, anders false.
            return result is bool exists && exists;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking schema existence: {ex.Message}");
            return false;
        }
    }
}
