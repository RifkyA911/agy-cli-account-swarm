using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AgyAccountSwarm.Services;

public interface IDataProtectionService
{
    string Protect(string plainText);
    string Unprotect(string encryptedOrPlainText);
    bool IsProtected(string text);
}

/// <summary>
/// Provides secure at-rest credential encryption using Windows DPAPI (Data Protection API)
/// scoped to the CurrentUser, with seamless transparent migration from legacy plain-text formats.
/// </summary>
public class DataProtectionService : IDataProtectionService
{
    private const string DpapiPrefix = "dpapi::";
    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        StringBuilder? ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public bool IsProtected(string text)
    {
        return !string.IsNullOrWhiteSpace(text) && text.StartsWith(DpapiPrefix, StringComparison.Ordinal);
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        if (!OperatingSystem.IsWindows())
        {
            // Non-windows graceful fallback
            return plainText;
        }

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = ProtectBytes(plainBytes);
            return DpapiPrefix + Convert.ToBase64String(cipherBytes);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[DataProtection] DPAPI encryption failed: {ex.Message}");
            return plainText;
        }
    }

    public string Unprotect(string encryptedOrPlainText)
    {
        if (string.IsNullOrWhiteSpace(encryptedOrPlainText)) return encryptedOrPlainText;

        // If not DPAPI protected, treat as legacy plain-text (seamless backward compatibility)
        if (!encryptedOrPlainText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            return encryptedOrPlainText;
        }

        if (!OperatingSystem.IsWindows())
        {
            return encryptedOrPlainText;
        }

        try
        {
            string base64 = encryptedOrPlainText.Substring(DpapiPrefix.Length);
            byte[] cipherBytes = Convert.FromBase64String(base64);
            byte[] plainBytes = UnprotectBytes(cipherBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[DataProtection] DPAPI decryption failed: {ex.Message}");
            return encryptedOrPlainText;
        }
    }

    private static byte[] ProtectBytes(byte[] data)
    {
        var inBlob = new DATA_BLOB
        {
            cbData = data.Length,
            pbData = Marshal.AllocHGlobal(data.Length)
        };
        Marshal.Copy(data, 0, inBlob.pbData, data.Length);

        var outBlob = new DATA_BLOB();
        var entropyBlob = new DATA_BLOB();

        try
        {
            if (!CryptProtectData(ref inBlob, "AgyAccountSwarmCredential", ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outBlob))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CryptProtectData failed with win32 error code {error}");
            }

            byte[] result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }

    private static byte[] UnprotectBytes(byte[] cipherData)
    {
        var inBlob = new DATA_BLOB
        {
            cbData = cipherData.Length,
            pbData = Marshal.AllocHGlobal(cipherData.Length)
        };
        Marshal.Copy(cipherData, 0, inBlob.pbData, cipherData.Length);

        var outBlob = new DATA_BLOB();
        var entropyBlob = new DATA_BLOB();

        try
        {
            if (!CryptUnprotectData(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outBlob))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CryptUnprotectData failed with win32 error code {error}");
            }

            byte[] result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }
}
