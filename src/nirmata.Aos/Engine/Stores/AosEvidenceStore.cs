namespace nirmata.Aos.Engine.Stores;

internal sealed class AosEvidenceStore : AosJsonStoreBase
{
    public AosEvidenceStore(string aosRootPath)
        : base(aosRootPath, ".aos/evidence/")
    {
    }
}

