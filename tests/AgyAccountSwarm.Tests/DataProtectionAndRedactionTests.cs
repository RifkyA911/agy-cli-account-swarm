using System;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class DataProtectionAndRedactionTests
{
    [Fact]
    public void DataProtectionService_RoundTrip_EncryptsAndDecryptsAccurately()
    {
        var service = new DataProtectionService();
        string plainSecret = "{\"access_token\":\"ya29.a0AfH6SMB_secret_key\",\"id_token\":\"eyJhbGciOiJub25lIn0.eyJlbWFpbCI6InRlc3RAZXhhbXBsZS5jb20ifQ.sig\"}";

        // Act
        string protectedText = service.Protect(plainSecret);

        // Assert
        Assert.NotNull(protectedText);
        Assert.StartsWith("dpapi::", protectedText);
        Assert.NotEqual(plainSecret, protectedText);
        Assert.True(service.IsProtected(protectedText));

        // Decrypt
        string unprotected = service.Unprotect(protectedText);
        Assert.Equal(plainSecret, unprotected);
    }

    [Fact]
    public void DataProtectionService_LegacyPlainText_ReturnsAsIsForMigration()
    {
        var service = new DataProtectionService();
        string legacyJson = "{\"id_token\":\"eyJhbGciOiJub25lIn0.eyJlbWFpbCI6ImxlZ2FjeUBleGFtcGxlLmNvbSJ9.sig\"}";

        // Assert
        Assert.False(service.IsProtected(legacyJson));
        string result = service.Unprotect(legacyJson);
        Assert.Equal(legacyJson, result);
    }

    [Fact]
    public void Logger_RedactSensitive_RedactsJwtAndOAuthTokens()
    {
        string rawJwt = "Exception reading token: eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJlbWFpbCI6InRlc3RAZ21haWwuY29tIn0.abcdef123456";
        string rawOAuth = "Using token ya29.a0AfH6SMB_this_is_a_secret_token_value_12345";

        string redactedJwt = Logger.RedactSensitive(rawJwt);
        string redactedOAuth = Logger.RedactSensitive(rawOAuth);

        Assert.DoesNotContain("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9", redactedJwt);
        Assert.Contains("[REDACTED_JWT_TOKEN]", redactedJwt);

        Assert.DoesNotContain("ya29.a0AfH6SMB_this_is_a_secret_token_value_12345", redactedOAuth);
        Assert.Contains("[REDACTED_OAUTH_TOKEN]", redactedOAuth);
    }

    [Fact]
    public void Logger_RedactSensitive_LeavesNonSensitiveMessagesUntouched()
    {
        string normalMsg = "Normal log message with profile 'Worker Alpha' running in Net9";
        string result = Logger.RedactSensitive(normalMsg);
        Assert.Equal(normalMsg, result);
    }
}
