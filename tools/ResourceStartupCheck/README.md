# Resource startup regression check

Run with .NET 10 and your locally installed client archive:

```sh
dotnet run --project tools/ResourceStartupCheck -c Release -- /path/to/Root.wad
```

This runs the production `ResourceContainer`, then accesses every resource's
`Instance` and repeats discovery. Before the fix, startup constructed resources
outside their lazy singleton and subsequent spell-resource access threw a
duplicate-key exception. It also verifies that each resource uses its own
singleton type (including `MagicSchools`). No player database is accessed.

Client archives are not included in this repository.
