using System.Runtime.InteropServices;
using Gmsd.Aos.Contracts.Secrets;

namespace Gmsd.Aos.Engine.Secrets;

/// <summary>
/// Windows Credential Manager implementation of ISecretStore.
/// Stores secrets securely using the Windows Credential Manager API.
/// </summary>
public class WindowsCredentialManagerSecretStore : ISecretStore
{
    private const string TargetNamePrefix = "GMSD_";

    public async Task SetSecretAsync(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Secret value cannot be null or empty.", nameof(value));

        var targetName = TargetNamePrefix + name;

        await Task.Run(() =>
        {
            var credential = new NativeCredential
            {
                TargetName = targetName,
                CredentialBlob = value,
                CredentialBlobSize = (uint)System.Text.Encoding.UTF8.GetByteCount(value),
                Type = CredentialType.Generic,
                Persist = CredentialPersist.LocalMachine,
                UserName = Environment.UserName
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to store secret '{name}' in Windows Credential Manager. Error code: {errorCode}");
            }
        });
    }

    public async Task<string> GetSecretAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));

        var targetName = TargetNamePrefix + name;

        return await Task.Run(() =>
        {
            IntPtr credentialPtr = IntPtr.Zero;
            try
            {
                if (!NativeMethods.CredRead(targetName, CredentialType.Generic, 0, out credentialPtr))
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == 1168) // ERROR_NOT_FOUND
                        throw new SecretNotFoundException(name);

                    throw new InvalidOperationException(
                        $"Failed to retrieve secret '{name}' from Windows Credential Manager. Error code: {errorCode}. " +
                        "Check that you have permission to access the credential store.");
                }

                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
                if (credential.CredentialBlob == null)
                    throw new InvalidOperationException($"Secret '{name}' has no value.");

                return credential.CredentialBlob;
            }
            finally
            {
                if (credentialPtr != IntPtr.Zero)
                    NativeMethods.CredFree(credentialPtr);
            }
        });
    }

    public async Task DeleteSecretAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));

        var targetName = TargetNamePrefix + name;

        await Task.Run(() =>
        {
            if (!NativeMethods.CredDelete(targetName, CredentialType.Generic, 0))
            {
                var errorCode = Marshal.GetLastWin32Error();
                if (errorCode == 1168) // ERROR_NOT_FOUND
                    throw new SecretNotFoundException(name);

                throw new InvalidOperationException(
                    $"Failed to delete secret '{name}' from Windows Credential Manager. Error code: {errorCode}");
            }
        });
    }

    public async Task<bool> SecretExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));

        try
        {
            await GetSecretAsync(name);
            return true;
        }
        catch (SecretNotFoundException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyCollection<string>> ListSecretsAsync()
    {
        return await Task.Run(() =>
        {
            var secrets = new List<string>();
            IntPtr credentialsPtr = IntPtr.Zero;
            uint count = 0;

            try
            {
                if (!NativeMethods.CredEnumerate(TargetNamePrefix, 0, out count, out credentialsPtr))
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == 1168) // ERROR_NOT_FOUND
                        return secrets.AsReadOnly();

                    throw new InvalidOperationException(
                        $"Failed to enumerate secrets from Windows Credential Manager. Error code: {errorCode}");
                }

                if (count == 0)
                    return secrets.AsReadOnly();

                var credentialPtrs = new IntPtr[count];
                Marshal.Copy(credentialsPtr, credentialPtrs, 0, (int)count);

                foreach (var credPtr in credentialPtrs)
                {
                    var credential = Marshal.PtrToStructure<NativeCredential>(credPtr);
                    if (credential.TargetName != null && credential.TargetName.StartsWith(TargetNamePrefix))
                    {
                        var secretName = credential.TargetName.Substring(TargetNamePrefix.Length);
                        secrets.Add(secretName);
                    }
                }

                return secrets.AsReadOnly();
            }
            finally
            {
                if (credentialsPtr != IntPtr.Zero)
                    NativeMethods.CredFree(credentialsPtr);
            }
        });
    }
}

internal enum CredentialType : uint
{
    Generic = 1,
    DomainPassword = 2,
    DomainCertificate = 3,
    DomainVisiblePassword = 4,
    GenericCertificate = 5,
    DomainExtended = 6
}

internal enum CredentialPersist : uint
{
    Session = 1,
    LocalMachine = 2,
    Enterprise = 3
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeCredential
{
    public uint Flags;
    public CredentialType Type;
    public string TargetName;
    public string Comment;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
    public uint CredentialBlobSize;
    public string CredentialBlob;
    public CredentialPersist Persist;
    public uint AttributeCount;
    public IntPtr Attributes;
    public string TargetAlias;
    public string UserName;
}

internal static class NativeMethods
{
    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CredRead(string target, CredentialType type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CredDelete(string target, CredentialType type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CredEnumerate(string filter, uint flags, out uint count, out IntPtr credentials);

    [DllImport("Advapi32.dll", SetLastError = true)]
    internal static extern void CredFree(IntPtr buffer);
}
