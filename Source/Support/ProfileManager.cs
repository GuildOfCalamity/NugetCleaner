using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

using Con = System.Diagnostics.Debug;

namespace NugetCleaner;

// Custom attribute to mark sensitive properties
[AttributeUsage(AttributeTargets.Property)]
public class EncryptedAttribute : Attribute { }

/// <summary>
/// This module is dependent on the <see cref="System.Text.Json.JsonSerializer"/>.
/// Newtonsoft's JSON solution can also be used.
/// </summary>
public class AppSettings
{
    #region [Public Members]
    public string? Username { get; set; }
    [Encrypted]
    public string? Password { get; set; }
    [Encrypted]
    public string? ApiSecret { get; set; }
    public string? ApiKey { get; set; }
    public string? Metrics { get; set; }
    public DateTime? LastUse { get; set; }
    public int WindowTop { get; set; }
    public int WindowLeft { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public int LastCount { get; set; }
    public int Days { get; set; }
    public long LastSize { get; set; }
    public int WindowState { get; set; }
    public string? Version { get; set; }
    public string? Theme { get; set; } = "Dark";
    public bool FirstRun { get; set; } = true;
    public bool DebugMode { get; set; }

    #endregion

    #region [Private Members]
    static bool portable = false;
    static readonly string p1 = "Rubber";
    static readonly string p2 = "Bumper";
    static readonly string p3 = "Baby";
    static readonly byte[] entropyBytes = Encoding.UTF8.GetBytes($"{p1}{p3}{p2}");
    static string SettingsFilePath = string.Empty;
    #endregion

    /// <summary>
    /// Load settings profile with automatic decryption.
    /// </summary>
    /// <param name="portableEncryption">
    /// <para>If true, the profile will use a standard <see cref="System.Security.Cryptography.Aes"/>-128 encryption process for identified properties, and the
    /// settings will work if transferred to another machine.</para>
    /// <para>If false, the profile will use <see cref="Windows.Security.Cryptography.DataProtection.DataProtectionProvider"/> for encryption, 
    /// but this will only be compatible machine-wide and is incompatible once copied to another machine.</para>
    /// </param>
    /// <returns><see cref="AppSettings"/> object</returns>
    public static AppSettings Load(bool portableEncryption = true)
    {
        if (string.IsNullOrEmpty(SettingsFilePath))
        {
            if (App.IsPackaged)
                SettingsFilePath = Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "profile.json");
            else
                SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile.json");
        }

        portable = portableEncryption;
        if (!File.Exists(SettingsFilePath))
        {
            Con.WriteLine("[ERROR] Settings file not found, returning a new instance.");
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            settings?.DecryptSensitiveProperties();
            //settings?.ValidateEncryptedProperties(false); // Validate after loading (decrypted state)
            Con.WriteLine("[INFO] Settings successfully loaded.");
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Loading settings: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Save settings profile with automatic encryption. 
    /// After <see cref="AppSettings.Load(bool)"/> is called, the 'portable' setting is observed during encryption/decryption.
    /// </summary>
    public void Save()
    {
        if (string.IsNullOrEmpty(SettingsFilePath))
        {
            if (App.IsPackaged)
                SettingsFilePath = Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "profile.json");
            else
                SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile.json");
        }

        try
        {
            EncryptSensitiveProperties();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            File.WriteAllText(SettingsFilePath, json);
            Con.WriteLine("[INFO] Settings successfully saved.");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Encrypt all properties marked with [Encrypted]
    /// </summary>
    void EncryptSensitiveProperties()
    {
        foreach (var property in GetType().GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(EncryptedAttribute)))
            {
                var value = property.GetValue(this)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (!IsEncrypted(value))
                    {
                        property.SetValue(this, Encrypt(value));
                    }
                    else
                    {
                        Con.WriteLine($"[WARNING] Property '{property.Name}' is already encrypted.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Decrypt all properties marked with [Encrypted]
    /// </summary>
    void DecryptSensitiveProperties()
    {
        foreach (var property in GetType().GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(EncryptedAttribute)))
            {
                var value = property.GetValue(this)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (IsEncrypted(value))
                        property.SetValue(this, Decrypt(value));
                    else
                        property.SetValue(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Encrypt a string
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !portable)
            {
                return EncryptSync(plainText);
            }
            else
            {
                return EncryptPortable(plainText, $"{p1}{p3}{p2}");
            }
        }
        catch (COMException ex)
        {
            if (ex.HResult == -2146893819)
                Con.WriteLine($"[ERROR] Encrypting data: NTE_BAD_DATA");
            else
                Con.WriteLine($"[ERROR] Encrypting data(HRESULT={ex.HResult}): {ex.Message}");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Encrypting data: {ex.Message}");
        }
        return string.Empty;
    }

    /// <summary>
    /// Decrypt a string
    /// </summary>
    /// <param name="encryptedText"></param>
    /// <returns></returns>
    static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !portable)
            {
                return DecryptSync(encryptedText);
            }
            else
            {
                return DecryptPortable(encryptedText, $"{p1}{p3}{p2}");
            }
        }
        catch (COMException ex)
        {
            if (ex.HResult == -2146893819)
                Con.WriteLine($"[ERROR] Decrypting data: NTE_BAD_DATA");
            else
                Con.WriteLine($"[ERROR] Decrypting data(HRESULT={ex.HResult}): {ex.Message}");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Decrypting data: {ex.Message}");
        }
        return string.Empty;
    }

    /// <summary>
    /// Encrypts the given plaintext using <see cref="DataProtectionProvider"/>.
    /// </summary>
    /// <param name="plainText">The plaintext to encrypt.</param>
    /// <returns>The encrypted data as a Base64 string.</returns>
    public static async Task<string> EncryptAsync(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var plainBuffer = CryptographicBuffer.ConvertStringToBinary(plainText, BinaryStringEncoding.Utf8);
            var provider = new DataProtectionProvider("LOCAL=machine"); // "LOCAL=machine", "LOCAL=user", "LOCAL=app", "LOCAL=group", "CURRENT=user", or a valid SID
            IBuffer encryptedBuffer = await provider.ProtectAsync(plainBuffer);
            return CryptographicBuffer.EncodeToBase64String(encryptedBuffer);
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            Con.WriteLine($"[ERROR] Encrypting data: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Encrypting data: {ex.Message}");
        }
        return plainText;
    }

    /// <summary>
    /// Encrypts the given plaintext synchronously using <see cref="DataProtectionProvider"/>.
    /// </summary>
    /// <param name="plainText">The plaintext to encrypt.</param>
    /// <returns>The encrypted data as a Base64 string.</returns>
    public static string EncryptSync(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var plainBuffer = CryptographicBuffer.ConvertStringToBinary(plainText, BinaryStringEncoding.Utf8);
            var provider = new DataProtectionProvider("LOCAL=machine"); // "LOCAL=machine", "LOCAL=user", "LOCAL=app", "LOCAL=group", "CURRENT=user", or a valid SID
            IBuffer encryptedBuffer = provider.ProtectAsync(plainBuffer).AsTask().Result;
            return CryptographicBuffer.EncodeToBase64String(encryptedBuffer);
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            Con.WriteLine($"[ERROR] Encrypting data: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Encrypting data: {ex.Message}");
        }
        return plainText;
    }

    /// <summary>
    /// Decrypts the given encrypted Base64 string using <see cref="DataProtectionProvider"/>.
    /// </summary>
    /// <param name="encryptedData">The encrypted Base64 string to decrypt.</param>
    /// <returns>The decrypted plaintext.</returns>
    public static async Task<string> DecryptAsync(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            return string.Empty;

        try
        {
            var encryptedBuffer = CryptographicBuffer.DecodeFromBase64String(encryptedData);
            var provider = new DataProtectionProvider("LOCAL=machine"); // "LOCAL=machine", "LOCAL=user", "LOCAL=app", "LOCAL=group", "CURRENT=user", or a valid SID
            IBuffer decryptedBuffer = await provider.UnprotectAsync(encryptedBuffer);
            return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuffer);
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            Con.WriteLine($"[ERROR] Decrypting data: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Decrypting data: {ex.Message}");
        }
        return string.Empty;
    }

    /// <summary>
    /// Decrypts the given encrypted Base64 string synchronously using <see cref="DataProtectionProvider"/>.
    /// </summary>
    /// <param name="encryptedData">The encrypted Base64 string to decrypt.</param>
    /// <returns>The decrypted plaintext.</returns>
    public static string DecryptSync(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentNullException(nameof(encryptedData));

        try
        {
            var encryptedBuffer = CryptographicBuffer.DecodeFromBase64String(encryptedData);
            var provider = new DataProtectionProvider("LOCAL=machine"); // "LOCAL=machine", "LOCAL=user", "LOCAL=app", "LOCAL=group", "CURRENT=user", or a valid SID
            IBuffer decryptedBuffer = provider.UnprotectAsync(encryptedBuffer).AsTask().Result;
            return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuffer);
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            Con.WriteLine($"[ERROR] Decrypting data: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Decrypting data: {ex.Message}");
        }
        return string.Empty;
    }

    /// <summary>
    /// Portable method to encrypt a string via <see cref="System.Security.Cryptography.Aes"/>.
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns>encrypted text</returns>
    static string EncryptPortable(string plainText, string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length < 16)
            throw new ArgumentException($"The hash must be a minimum of 16 characters for AES-128.", $"{nameof(hash)}");

        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes($"{hash}"); // Must be 16 bytes for AES-128
            aes.IV = Encoding.UTF8.GetBytes($"{hash}");  // Must be 16 bytes
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (var writer = new StreamWriter(cryptoStream))
                        {
                            writer.Write(plainText); // write into the encryption stream
                        }
                        return Convert.ToBase64String(memoryStream.ToArray());
                    }
                }
            }
        }
    }

    /// <summary>
    /// Portable method to decrypt a string via <see cref="System.Security.Cryptography.Aes"/>.
    /// </summary>
    /// <param name="cipherText"></param>
    /// <returns>decrypted text</returns>
    static string DecryptPortable(string cipherText, string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length < 16)
            throw new ArgumentException($"The hash must be a minimum of 16 characters for AES-128.", $"{nameof(hash)}");

        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes($"{hash}"); // Must be 16 bytes for AES-128
            aes.IV = Encoding.UTF8.GetBytes($"{hash}");  // Must be 16 bytes
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            {
                using (var memoryStream = new MemoryStream(Convert.FromBase64String(cipherText)))
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (var reader = new StreamReader(cryptoStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Not secure, but better than clear text.
    /// </summary>
    static string FallbackEncryptDecrypt(string input, string key)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        StringBuilder output = new StringBuilder();
        int keyLength = key.Length;
        for (int i = 0; i < input.Length; i++)
        {
            char encryptedChar = (char)(input[i] ^ key[i % keyLength]);
            output.Append(encryptedChar);
        }
        return $"{output}";
    }

    #region [Helpers]
    /// <summary>
    /// Attempt to decode and decrypt. If it fails, it's not valid encrypted data.
    /// </summary>
    static bool IsEncrypted(string value)
    {
        try
        {
            if (!portable)
            {
                var encryptedBuffer = CryptographicBuffer.DecodeFromBase64String(value);
                var provider = new DataProtectionProvider("LOCAL=machine"); // "LOCAL=machine", "LOCAL=user", "LOCAL=app", "LOCAL=group", "CURRENT=user", or a valid SID
                IBuffer decryptedBuffer = provider.UnprotectAsync(encryptedBuffer).AsTask().Result;
                return true;
            }
            else
            {
                return IsAesEncrypted(value);
            }
        }
        catch (COMException ex)
        {
            if (ex.HResult == -2146893819)
                Con.WriteLine($"[WARNING] IsEncrypted: NTE_BAD_DATA");
        }
        catch (Exception)
        {
        }
        return false;
    }

    /// <summary>
    /// For portable encryption checking
    /// </summary>
    static bool IsAesEncrypted(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(input);
        }
        catch (FormatException)
        {
            return false; // Invalid Base64 string
        }
        catch (Exception)
        {
            return false; // Other issue
        }

        // Check if the length is a multiple of 16 (AES block size)
        if (decodedBytes.Length % 16 != 0)
            return false; // Not an AES block size

        //if (!input.EndsWith("=")) { return false; }

        return true; // Likely AES or Base64
    }

    /// <summary>
    /// Validate properties marked with [Encrypted]
    /// </summary>
    /// <param name="isEncrypted"></param>
    /// <exception cref="InvalidOperationException"></exception>
    void ValidateEncryptedProperties(bool isEncrypted)
    {
        foreach (var property in GetType().GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(EncryptedAttribute)))
            {
                var value = property.GetValue(this)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (isEncrypted && !IsEncrypted(value))
                    {
                        throw new InvalidOperationException($"Property '{property.Name}' is not encrypted but should be.");
                    }

                    if (!isEncrypted && IsEncrypted(value))
                    {
                        throw new InvalidOperationException($"Property '{property.Name}' is encrypted but should be plain text.");
                    }
                }
            }
        }
    }
    #endregion
}
