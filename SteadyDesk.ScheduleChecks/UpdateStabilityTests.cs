using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class UpdateStabilityTests
{
    private static readonly Uri FeedUri = AppUpdateClient.ManifestUri;

    public static void Run(StabilityTestRunner runner)
    {
        runner.Suite("更新版本三段号比较", () => VerifyVersionComparison(runner));
        runner.Suite("更新清单签名与字段绑定", () => VerifySignatureAndPayload(runner));
        runner.Suite("更新来源与异常清单约束", () => VerifyManifestConstraints(runner));
        runner.Suite("更新包下载校验与失败清理", () => VerifyPackageDownload(runner).GetAwaiter().GetResult());
    }

    private static void VerifyVersionComparison(StabilityTestRunner runner)
    {
        var current = AppUpdateClient.NormalizeVersion(new Version(1, 2, 0, 0));
        var available = AppUpdateClient.NormalizeVersion(new Version(1, 2, 1));
        runner.Equal("程序集四段版本折算成三段发布版本", new Version(1, 2, 0), current);
        runner.Check("高一个补丁版本会被识别为更新", available > current);
        runner.Check("同一版本号不提示更新", AppUpdateClient.NormalizeVersion(new Version(1, 2, 0)) == current);
    }

    private static void VerifySignatureAndPayload(StabilityTestRunner runner)
    {
        using var rsa = RSA.Create(3072);
        var manifest = CreateManifest();
        Sign(manifest, rsa);

        var json = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var verified = UpdateManifestVerifier.ParseAndVerify(json, rsa.ExportRSAPublicKeyPem(), FeedUri);
        runner.Equal("签名清单版本可读取", "1.2.0", verified.Version);
        runner.Equal("签名清单大小可读取", manifest.PackageSizeBytes, verified.PackageSizeBytes);

        manifest.PackageSizeBytes++;
        runner.Throws<InvalidDataException>(
            "清单字段被改动后签名失效",
            () => UpdateManifestVerifier.ParseAndVerify(
                JsonSerializer.SerializeToUtf8Bytes(manifest),
                rsa.ExportRSAPublicKeyPem(),
                FeedUri));

        var malformedSignature = CreateManifest();
        malformedSignature.Signature = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        runner.Throws<InvalidDataException>(
            "错误签名不能通过",
            () => UpdateManifestVerifier.ParseAndVerify(
                JsonSerializer.SerializeToUtf8Bytes(malformedSignature),
                rsa.ExportRSAPublicKeyPem(),
                FeedUri));
    }

    private static void VerifyManifestConstraints(StabilityTestRunner runner)
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportRSAPublicKeyPem();

        var wrongHost = Sign(CreateManifest(
            packageUrl: "https://updates.example.invalid/releases/v1.2.0/steady-desk-update-win-x64.zip"), rsa);
        runner.Throws<InvalidDataException>(
            "不同源站被拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(JsonSerializer.SerializeToUtf8Bytes(wrongHost), publicKey, FeedUri));

        var http = Sign(CreateManifest(
            packageUrl: "http://my-1253786342.cos.ap-shanghai.myqcloud.com/releases/v1.2.0/steady-desk-update-win-x64.zip"), rsa);
        runner.Throws<InvalidDataException>(
            "非 HTTPS 更新包被拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(JsonSerializer.SerializeToUtf8Bytes(http), publicKey, FeedUri));

        var wrongPath = Sign(CreateManifest(
            packageUrl: "https://my-1253786342.cos.ap-shanghai.myqcloud.com/releases/latest.zip"), rsa);
        runner.Throws<InvalidDataException>(
            "非版本化更新包路径被拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(JsonSerializer.SerializeToUtf8Bytes(wrongPath), publicKey, FeedUri));

        var oversizedPackage = Sign(CreateManifest(packageSize: UpdateManifestVerifier.MaximumPackageBytes + 1), rsa);
        runner.Throws<InvalidDataException>(
            "超出上限的更新包被拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(JsonSerializer.SerializeToUtf8Bytes(oversizedPackage), publicKey, FeedUri));

        const string nullFields = "{\"SchemaVersion\":1,\"Product\":\"SteadyDesk\",\"Version\":\"1.2.0\",\"PackageUrl\":null,\"PackageSizeBytes\":1,\"Sha256\":null,\"Signature\":null}";
        runner.Throws<InvalidDataException>(
            "空字段被安全拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(Encoding.UTF8.GetBytes(nullFields), publicKey, FeedUri));

        runner.Throws<InvalidDataException>(
            "超大清单被拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(new byte[33 * 1024], publicKey, FeedUri));

        runner.Throws<InvalidDataException>(
            "无效公钥被安全拒绝",
            () => UpdateManifestVerifier.ParseAndVerify(
                JsonSerializer.SerializeToUtf8Bytes(Sign(CreateManifest(), rsa)),
                "not a PEM key",
                FeedUri));
    }

    private static async Task VerifyPackageDownload(StabilityTestRunner runner)
    {
        var packageBytes = Enumerable.Range(0, 64 * 1024)
            .Select(index => (byte)(index % 251))
            .ToArray();
        var manifest = CreateManifest(packageSize: packageBytes.Length);
        manifest.Sha256 = Convert.ToHexString(SHA256.HashData(packageBytes));

        Directory.CreateDirectory(AppStorage.UpdatesDirectory);
        var partialPath = Path.Combine(
            AppStorage.UpdatesDirectory,
            "1.2.0-" + manifest.Sha256.ToLowerInvariant() + ".zip.part");
        var partialLength = packageBytes.Length / 3;
        File.WriteAllBytes(partialPath, packageBytes[..partialLength]);

        var handler = new StaticResponseHandler(packageBytes);
        using var httpClient = new HttpClient(handler);
        var updateClient = new AppUpdateClient(httpClient, "unused-in-download-only-test");
        var packagePath = await updateClient.DownloadPackageAsync(manifest);
        try
        {
            runner.Check(
                "断点续传后通过长度与 SHA-256 校验",
                File.ReadAllBytes(packagePath).SequenceEqual(packageBytes));
            runner.Equal("续传请求从已有字节处开始", 1, handler.RangeRequestCount);
        }
        finally
        {
            AppStorage.DeleteIfExists(packagePath);
        }

        manifest.Sha256 = new string('0', 64);
        var existingFiles = Directory.GetFiles(AppStorage.UpdatesDirectory)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        await ExpectInvalidDataAsync(
            () => updateClient.DownloadPackageAsync(manifest),
            "错误 SHA-256 会拒绝更新包",
            runner);

        var newFiles = Directory.GetFiles(AppStorage.UpdatesDirectory)
            .Where(path => !existingFiles.Contains(path))
            .ToArray();
        runner.Equal("校验失败不留下临时或完成文件", 0, newFiles.Length);
    }

    private static async Task ExpectInvalidDataAsync(
        Func<Task<string>> action,
        string name,
        StabilityTestRunner runner)
    {
        try
        {
            var path = await action();
            AppStorage.DeleteIfExists(path);
            runner.Check(name, false);
        }
        catch (InvalidDataException)
        {
            runner.Check(name, true);
        }
        catch (Exception exception)
        {
            runner.Check(name + " (threw " + exception.GetType().Name + ")", false);
        }
    }

    private static UpdateManifest Sign(UpdateManifest manifest, RSA rsa)
    {
        manifest.Signature = Convert.ToBase64String(rsa.SignData(
            UpdateManifestVerifier.CreateSignaturePayload(manifest),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));
        return manifest;
    }

    private static UpdateManifest CreateManifest(string? packageUrl = null, long packageSize = 4096)
    {
        return new UpdateManifest
        {
            SchemaVersion = 1,
            Product = "SteadyDesk",
            Version = "1.2.0",
            PackageUrl = packageUrl ?? "https://my-1253786342.cos.ap-shanghai.myqcloud.com/releases/v1.2.0/steady-desk-update-win-x64.zip",
            PackageSizeBytes = packageSize,
            Sha256 = new string('a', 64),
            Signature = string.Empty
        };
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        public int RangeRequestCount { get; private set; }

        public StaticResponseHandler(byte[] content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestedStart = request.Headers.Range?.Ranges.SingleOrDefault()?.From;
            if (requestedStart is long start)
            {
                RangeRequestCount++;
                var suffix = _content[checked((int)start)..];
                var partialResponse = new HttpResponseMessage(System.Net.HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(suffix)
                };
                partialResponse.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(
                    start,
                    _content.LongLength - 1,
                    _content.LongLength);
                return Task.FromResult(partialResponse);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            });
        }
    }
}
