using Dut00.AppInfo.ConnectionStrings.Internal;

namespace Dut00.AppInfo.ConnectionStrings.Tests;

public sealed class ConnectionStringMaskerTests
{
    private static readonly ISet<string> DefaultKeys = new ConnectionStringsOptions().SensitiveKeys;

    private static string Mask(string value) => ConnectionStringMasker.Mask(value, DefaultKeys, "***");

    [Fact]
    public void SqlServer_MasksUserAndPasswordAndKeepsKeyCasing()
    {
        Mask("Data Source=db.company.com;Initial Catalog=Billing;User ID=sa;Password=S3cr3t!")
            .ShouldBe("Data Source=db.company.com;Initial Catalog=Billing;User ID=***;Password=***");
    }

    [Fact]
    public void SqlServer_UidAndPwd_AreMasked()
    {
        Mask("Server=tcp:db,1433;Database=app;UID=admin;PWD=S3cr3t!")
            .ShouldBe("Server=tcp:db,1433;Database=app;UID=***;PWD=***");
    }

    [Fact]
    public void PostgreSql_MasksUsernameAndPassword()
    {
        Mask("Host=db.company.com;Port=5432;Database=app;Username=bob;Password=S3cr3t!")
            .ShouldBe("Host=db.company.com;Port=5432;Database=app;Username=***;Password=***");
    }

    [Fact]
    public void AzureStorage_MasksAccountKey()
    {
        Mask("DefaultEndpointsProtocol=https;AccountName=billing;AccountKey=bXlTZWNyZXRLZXk=;EndpointSuffix=core.windows.net")
            .ShouldBe("DefaultEndpointsProtocol=https;AccountName=billing;AccountKey=***;EndpointSuffix=core.windows.net");
    }

    [Fact]
    public void AzureServiceBus_MasksSharedAccessKeyAndKeepsEndpoint()
    {
        var masked = Mask("Endpoint=sb://billing.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=c2VjcmV0=");

        masked.ShouldStartWith("Endpoint=sb://billing.servicebus.windows.net/;");
        masked.ShouldEndWith("SharedAccessKey=***");
        masked.ShouldNotContain("c2VjcmV0");
    }

    [Fact]
    public void SasUrlInEndpoint_IsMasked()
    {
        Mask("BlobEndpoint=https://billing.blob.core.windows.net/?sv=2024-01-01&sig=SIGNATURE;AccountName=billing")
            .ShouldBe("BlobEndpoint=***;AccountName=billing");
    }

    [Fact]
    public void UrlWithCredentialsInValue_IsMasked()
    {
        Mask("Proxy=http://bob:S3cr3t@proxy:8080;Host=db").ShouldBe("Proxy=***;Host=db");
    }

    [Fact]
    public void NestedPasswordInValue_IsMasked()
    {
        Mask("Provider=Microsoft.ACE.OLEDB.12.0;Extended Properties=\"Excel 12.0;Password=S3cr3t\"")
            .ShouldBe("Provider=Microsoft.ACE.OLEDB.12.0;Extended Properties=***");
    }

    [Theory]
    [InlineData("Proxy Password=S3cr3t;Host=db", "Proxy Password=***;Host=db")]
    [InlineData("SSL Password=S3cr3t;Host=db", "SSL Password=***;Host=db")]
    [InlineData("ClientSecret=S3cr3t;Host=db", "ClientSecret=***;Host=db")]
    public void KeyContainingASensitiveWord_IsMasked(string value, string expected)
    {
        Mask(value).ShouldBe(expected);
    }

    [Fact]
    public void SensitiveKeys_MatchIgnoringCase()
    {
        Mask("host=db;PASSWORD=S3cr3t;user id=sa").ShouldBe("host=db;PASSWORD=***;user id=***");
    }

    [Fact]
    public void QuotedValueWithSeparators_IsMaskedWhole()
    {
        Mask("Host=db;Password=\"S3c;r3t=x\"").ShouldBe("Host=db;Password=***");
        Mask("Host=db;Password='it''s'").ShouldBe("Host=db;Password=***");
    }

    [Fact]
    public void DuplicateKey_IsMasked()
    {
        var masked = Mask("Host=db;Password=first;password=second");

        masked.ShouldNotContain("first");
        masked.ShouldNotContain("second");
    }

    [Theory]
    [InlineData("Server=db;Password=p@ss;word")]
    [InlineData("Server=db;Password")]
    [InlineData("=no-key")]
    [InlineData("just-a-token")]
    public void UnparseableValue_IsMaskedWhole(string value)
    {
        Mask(value).ShouldBe("***");
    }

    [Theory]
    [InlineData("postgres://user:S3cr3t@db.company.com/app")]
    [InlineData("mongodb://user:S3cr3t@db.company.com/app?authSource=admin")]
    [InlineData("redis://:S3cr3t@cache:6379")]
    [InlineData("  amqp://user:S3cr3t@mq/")]
    public void UriStyleValue_IsMaskedWhole(string value)
    {
        Mask(value).ShouldBe("***");
    }

    [Theory]
    [InlineData("cache:6379,password=S3cr3t,ssl=True")]
    [InlineData("cache:6379,ssl=True,password=S3cr3t")]
    public void ValueNotInKeyValueFormat_IsMaskedWhole(string value)
    {
        Mask(value).ShouldBe("***");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyValue_IsReturnedAsIs(string value)
    {
        Mask(value).ShouldBe(value);
    }

    [Fact]
    public void ValueWithoutSecrets_IsKept()
    {
        Mask("Data Source=db.company.com;Initial Catalog=Billing;Integrated Security=True")
            .ShouldBe("Data Source=db.company.com;Initial Catalog=Billing;Integrated Security=True");
    }

    [Theory]
    [InlineData("Driver={ODBC Driver 18 for SQL Server};Server=db", "Driver=\"{ODBC Driver 18 for SQL Server}\";Server=db")]
    [InlineData("Endpoint=https://myai.openai.azure.com/;Deployment=gpt", "Endpoint=https://myai.openai.azure.com/;Deployment=gpt")]
    [InlineData("Server=tcp:db.company.com,1433;Database=app", "Server=tcp:db.company.com,1433;Database=app")]
    [InlineData("Driver={Odd}}Name};Server=db", "Driver={Odd}}Name};Server=db")]
    [InlineData("Application Name=Billing API (prod) #2;Host=db", "Application Name=\"Billing API (prod) #2\";Host=db")]
    public void BalancedBracesAndPlainAddresses_AreKept(string value, string expected)
    {
        Mask(value).ShouldBe(expected);
    }

    [Fact]
    public void BracedSecretSplitByTheParser_IsMaskedWhole()
    {
        Mask("Driver={ODBC Driver 18 for SQL Server};Server=db;UID=u;PWD={ab;cd=S3cr3t}").ShouldBe("***");
    }

    [Fact]
    public void HostNameContainingASensitiveWord_IsKept()
    {
        Mask("Host=userdb.company.com;Database=tokens").ShouldBe("Host=userdb.company.com;Database=tokens");
    }

    [Fact]
    public void CustomSensitiveKeyAndMask_AreUsed()
    {
        var keys = new HashSet<string>(DefaultKeys, StringComparer.OrdinalIgnoreCase) { "Tenant" };

        ConnectionStringMasker.Mask("Host=db;Tenant=contoso;Password=S3cr3t", keys, "<hidden>")
            .ShouldBe("Host=db;Tenant=<hidden>;Password=<hidden>");
    }

    [Fact]
    public void MaskWithSeparators_IsQuoted()
    {
        ConnectionStringMasker.Mask("Host=db;Password=S3cr3t", DefaultKeys, "a;b").ShouldBe("Host=db;Password=\"a;b\"");
    }

    [Fact]
    public void BlankSensitiveKeys_AreIgnored()
    {
        ConnectionStringMasker.Mask("Host=db;Password=S3cr3t", new HashSet<string> { "", "  ", "Password" }, "***")
            .ShouldBe("Host=db;Password=***");
    }

    [Fact]
    public void NoSensitiveKeys_StructuralRulesStillApply()
    {
        ConnectionStringMasker.Mask("postgres://user:S3cr3t@db/app", new HashSet<string>(), "***").ShouldBe("***");
        ConnectionStringMasker.Mask("Host=db;Password=visible", new HashSet<string>(), "***").ShouldBe("Host=db;Password=visible");
    }

    [Theory]
    [MemberData(nameof(SecretBearingValues))]
    public void Secret_NeverAppearsInOutput(string value)
    {
        Mask(value).ShouldNotContain("S3cr3t", Case.Insensitive);
    }

    public static TheoryData<string> SecretBearingValues() =>
    [
        "Data Source=db;User ID=S3cr3t;Password=S3cr3t",
        "Server=db;Pwd=S3cr3t",
        "AccountName=a;AccountKey=S3cr3t",
        "Endpoint=sb://ns/;SharedAccessKeyName=n;SharedAccessKey=S3cr3t",
        "SharedAccessSignature=sv=1&sig=S3cr3t;BlobEndpoint=https://a/",
        "BlobEndpoint=https://a/?sig=S3cr3t",
        "Extended Properties=\"Password = S3cr3t\"",
        "Host=db;ApiKey=S3cr3t",
        "Host=db;Token=S3cr3t",
        "Host=db;AccessKey=S3cr3t",
        "Host=db;Secret=S3cr3t",
        "cache:6379,password=S3cr3t",
        "mongodb://u:S3cr3t@h/db?x=y",
        "Server=db;Password=S3cr3t;broken",
        "Host=db;Password=\"S3cr3t\"",

        // ODBC braces are not quoting for the parser, so braced secrets get split.
        "Driver={ODBC Driver 18 for SQL Server};Server=db;UID=u;PWD={ab;cd=S3cr3t}",
        "Driver={Simba Spark ODBC Driver};Host=x;UID=token;PWD={dapi;Auth_Flow=S3cr3t}",
        "Server=db;PWD=x{a;b=S3cr3t}",

        // Credentials in addresses that System.Uri can't parse.
        "Proxy=http://bob:S3/cr3t@proxy:8080;Host=db",
        "Url=http://bob:p@S3cr3t@host/",
        "Url=http://bob:S3cr3t@host:badport/",
        "Url=https://bob:S3cr3t@[bad/",
        "Server=mongodb://u:S3cr3t@h1:27017,h2:27017/db;Host=x",
        "Url=amqps://bob:S3cr3t@h1,h2/vhost",
        "Url=file://bob:S3cr3t@host/share",
        "Url=tcp:bob:S3cr3t@host",

        // ODBC "}}" escape: "{ab}}" is still an open braced value.
        "Driver={ODBC Driver 18 for SQL Server};Server=db;UID=u;PWD={ab}};cd={S3cr3t}",
        "Driver={ODBC Driver 18 for SQL Server};Server=db;PWD={ab}};cd=S3cr3tMID;ef={S3cr3tTAIL}",

        // Credentials that don't sit behind a plain scheme at the start of the value.
        "Proxy=bob_smith:S3cr3t@proxy:8080",
        "Proxy=42:S3cr3t@proxy:8080",
        "Nodes=10.0.0.1:9200,http://elastic:S3cr3t@b:9200",
        "Server=db,https://bob:S3cr3t@h",
        "Url=//bob:S3cr3t@host/",
        "Url=<https://bob:S3cr3t@host/>",
        "Url=\u200Bhttps://bob:S3cr3t@host/",
        "Url=\uFF48ttps://bob:S3cr3t@host/",
        "Data Source=bob/S3cr3t@host:1521/svc",
        "Url=//host/path?sig=S3cr3t",
        "Host=db;Authorization=Basic S3cr3t",
        "Host=db;Signature=S3cr3t;Bearer=S3cr3t",

        // Real-world keys covered by the defaults.
        "Endpoint=https://myai.openai.azure.com/;Key=S3cr3t",
        "Data Source=https://kusto.windows.net;Application Key=S3cr3t;AppKey=S3cr3t",
        "Host=db;Username=u;PSW=S3cr3t",
        "account=acct;user=u;private_key=S3cr3t;passcode=S3cr3t",
        "Host=db;Credentials=user:S3cr3t;Passphrase=S3cr3t",

        // Other nested spellings.
        "Host=db;Extended Properties='\"Password\"=S3cr3t'",
        "Host=db;Extended Properties=\"Password:S3cr3t\"",
    ];
}
