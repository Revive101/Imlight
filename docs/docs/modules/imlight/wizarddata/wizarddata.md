# WizardData

The **WizardData** system provides persistent storage for all player and game data using RavenDB with a dual-database architecture.

Data is separated into two distinct databases based on access patterns and data ownership:
- **PlayerDatabase**: Individual player accounts and character data
- **WorldDatabase**: Shared game configuration data used by development teams

::: tip
The WorldDatabase fills gaps in client data. Despite the [shared data systems](../concepts/shared-data-systems.md) philosophy, some data is left server-side only. Some examples include drop tables, NPC inventories, and zone transfer data.
:::

### RavenDB Singleton Pattern

Both databases use the `RavenDatabaseSingleton<T>` pattern for connection management:

```csharp
public class PlayerDatabase : RavenDatabaseSingleton<PlayerDatabase> {
    protected override string DatabaseName { get; } 
        = ConfigurationManager.Settings["Database.PlayerDatabaseName"];
    protected override string Url { get; } 
        = ConfigurationManager.Settings["Database.PlayerDatabaseUrl"];
}
```

**Embedded vs Remote**
- **Embedded**: Local database file for development and small deployments
- **Remote**: External RavenDB server for production environments
- Automatic fallback to embedded when no URL is configured

## Collection Pattern

Data access is handled through static collection classes that provide CRUD operations and encapsulate database logic.

### Collection Structure

```csharp
public static class AccountCollection {
    public const string CollectionName = "Accounts";
    private static readonly IDocumentStore s_store;
    
    static AccountCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }
    
    public static bool CreateAccount(Account account) {
        using var session = s_store.OpenSession();
        
        if (session.Query<Account>(collectionName: CollectionName)
                   .Any(c => c.Username == account.Username)) {
            return false;
        }
        
        session.Store(account);
        var metadata = session.Advanced.GetMetadataFor(account);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        session.SaveChanges();
        return true;
    }
}
```

## Creating New Collections

When adding new data types, follow the established collection pattern:

### 1. Determine Database Target
- **PlayerDatabase**: User-specific data that varies per player
- **WorldDatabase**: Shared configuration data managed by developers

### 2. Create Collection Class

```csharp
public static class ExampleCollection {
    public const string CollectionName = "Examples";
    private static readonly IDocumentStore s_store;
    
    static ExampleCollection() {
        s_store = PlayerDatabase.Instance.Store; // or WorldDatabase.Instance.Store
    }
    
    public static bool CreateExample(ExampleModel example) {
        using var session = s_store.OpenSession();
        
        session.Store(example);
        var metadata = session.Advanced.GetMetadataFor(example);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        session.SaveChanges();
        return true;
    }
    
    public static ExampleModel GetExample(string id) {
        using var session = s_store.OpenSession();
        return session.Load<ExampleModel>(id);
    }
}
```

### 3. Follow Conventions
- **Static class** with collection name constant
- **Store assignment** in static constructor
- **Metadata setting** for proper RavenDB collection organization
- **Session disposal** using `using` statements
- **Consistent method naming** (Create, Get, Update, Delete patterns)

## Database Utilities

The `DatabaseUtilities` class provides common operations:

```csharp
// Create accounts with hashed passwords
var account = DatabaseUtilities.CreateEmbeddedDatabaseAccount(
    username: "player123",
    email: "player@example.com", 
    plaintextPassword: "password",
    auth: AuthLevel.None
);
```