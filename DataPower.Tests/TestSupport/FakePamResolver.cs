using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace DataPower.Tests.TestSupport
{
    // Pass-through PAM resolver for tests - returns whatever value it was given
    // rather than resolving anything through a real PAM provider.
    public class FakePamResolver : IPAMSecretResolver
    {
        public string Resolve(string instanceInfo) => instanceInfo;
    }
}
