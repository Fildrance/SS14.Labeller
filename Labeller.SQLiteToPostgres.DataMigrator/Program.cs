using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SS14.Labeller.Database;
using SS14.Labeller.Database.Entities;

IConfiguration configuration = new ConfigurationBuilder()
                               .SetBasePath(Directory.GetCurrentDirectory())
                               .AddJsonFile("appsettings.json")
                               .AddEnvironmentVariables()
                               .AddCommandLine(args)
                               .Build();

var pgConnectionString = configuration.GetConnectionString("Postgres")
                       ?? throw new InvalidOperationException(
                           "Failed to find 'Default' connection string "
                           + "from application configuration for database initialization."
                       );

var sqliteConnectionString = configuration.GetConnectionString("Sqlite")
                             ?? throw new InvalidOperationException(
                                 "Failed to find 'Default' connection string "
                                 + "from application configuration for database initialization."
                             );

var serviceCollection = new ServiceCollection();

serviceCollection.AddPooledDbContextFactory<CustomDbContext>(
    optsBuilder => optsBuilder.UseNpgsql(pgConnectionString)
                              .UseSnakeCaseNamingConvention()
);

serviceCollection.AddSingleton<IContextConfiguration, DiscourseEntitiesContextConfiguration>();

var sp = serviceCollection.BuildServiceProvider();

await using var sqliteConnections = new SqliteConnection(sqliteConnectionString);
sqliteConnections.Open();

var existingRecords =  await sqliteConnections.QueryAsync<DiscourseTopicDbModel>(
    """
    SELECT 
        DiscussionId
       ,RepoOwner
       ,RepoName
       ,IssueNumber
       ,TopicId
    FROM Discussions
    """
);

var contextFactory = sp.GetRequiredService<IDbContextFactory<CustomDbContext>>();
using var context = contextFactory.CreateDbContext();
context.Database.Migrate();

var mapped = existingRecords.Select(x => new DiscourseTopicEntity
{
    Id = x.DiscussionId,
    RepoOwner = x.RepoOwner,
    RepoName = x.RepoName,
    IssueNumber = x.IssueNumber,
    TopicId = x.TopicId,

}).ToArray();
await context.Set<DiscourseTopicEntity>()
             .AddRangeAsync(mapped);

await context.SaveChangesAsync();

public record DiscourseTopicDbModel
{
    public int DiscussionId {get;set;}
    public string RepoOwner {get;set;}
    public string RepoName {get;set;}
    public int IssueNumber {get;set;}
    public int TopicId { get; set; }
}
