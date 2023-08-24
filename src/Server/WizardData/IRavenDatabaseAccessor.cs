using System.Security.Cryptography.X509Certificates;

namespace Imlight.Server.WizardData;

public interface IRavenDatabaseAccessor
{
    protected X509Certificate2 Certificate { get; }
    protected string DatabaseName { get; }
    protected string Url { get; }
}