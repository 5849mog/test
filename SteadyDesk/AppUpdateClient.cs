using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SteadyDesk;

internal sealed class UpdateManifest
{
    public int SchemaVersion { get; set; }
    public string Product { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string PackageUrl { get; set; } = string.Empty;
    public long PackageSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

internal sealed record UpdateCheckResult(Version CurrentVersion, Version AvailableVersion, UpdateManifest Manifest)
{
    public bool IsUpdateAvailable => AvailableVersion > CurrentVersion;
}

internal static class UpdateManifestVerifier
{
    private const string ProductName = "SteadyDesk";
    private const int MaximumManifestBytes = 32 * 1024;
    internal const long MaximumPackageBytes = 256L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static UpdateManifest ParseAndVerify(ReadOnlySpan<byte> manifestBytes, string publicKeyPem, Uri feedUri)
    {
        if (manifestBytes.Length is 0 or > MaximumManifestBytes)
        {
            throw new InvalidDataException("更新清单为空或过大。");
        }

        var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestBytes, JsonOptions)
            ?? throw new InvalidDataException("更新清单格式无效。");

        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.Product, ProductName, StringComparison.Ordinal)
            || !Version.TryParse(manifest.Version, out _)
            || manifest.Version.Split('.').Length != 3
            || string.IsNullOrWhiteSpace(manifest.PackageUrl)
            || string.IsNullOrWhiteSpace(manifest.Sha256)
            || string.IsNullOrWhiteSpace(manifest.Signature)
            || manifest.PackageSizeBytes is <= 0 or > MaximumPackageBytes
            || manifest.Sha256.Length != 64
            || !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("更新清单字段无效。");
        }

        ValidatePackageLocation(manifest, feedUri);

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(manifest.Signature);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("更新清单签名格式无效。", exception);
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            if (rsa.KeySize < 2048)
            {
                throw new InvalidDataException("更新公钥长度不足，至少需要 2048 位 RSA 密钥。");
            }

            if (!rsa.VerifyData(
                    CreateSignaturePayload(manifest),
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss))
            {
                throw new InvalidDataException("更新清单签名验证失败。");
            }
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("更新公钥无效，无法验证更新清单。", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("更新公钥无效，无法验证更新清单。", exception);
        }

        return manifest;
    }

    public static byte[] CreateSignaturePayload(UpdateManifest manifest)
    {
        var canonical = string.Join("\n", new[]
        {
            manifest.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            manifest.Product,
            manifest.Version,
            manifest.PackageUrl,
            manifest.PackageSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            manifest.Sha256.ToLowerInvariant()
        });
        return Encoding.UTF8.GetBytes(canonical);
    }

    internal static void ValidatePackageLocation(UpdateManifest manifest, Uri feedUri)
    {
        if (!Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out var packageUri)
            || !string.Equals(packageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(packageUri.Host, feedUri.Host, StringComparison.OrdinalIgnoreCase)
            || packageUri.Port != feedUri.Port
            || !string.IsNullOrEmpty(packageUri.UserInfo)
            || !string.IsNullOrEmpty(packageUri.Query)
            || !string.IsNullOrEmpty(packageUri.Fragment)
            || !string.Equals(
                packageUri.AbsolutePath,
                $"/releases/v{manifest.Version}/steady-desk-update-win-x64.zip",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("更新包必须使用受信 COS 站点的固定 HTTPS 版本路径。");
        }
    }
}

internal sealed class AppUpdateClient
{
    public static readonly Uri ManifestUri = new(
        "https://my-1253786342.cos.ap-shanghai.myqcloud.com/releases/stable/latest.json");

    private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PackageTimeout = TimeSpan.FromMinutes(30);
    private readonly HttpClient _httpClient;
    private readonly string _publicKeyPem;

    public AppUpdateClient(HttpClient? httpClient = null, string? publicKeyPem = null)
    {
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _publicKeyPem = publicKeyPem ?? ReadEmbeddedPublicKey();
    }

    public static bool IsTrustConfigured => !string.IsNullOrWhiteSpace(ReadEmbeddedPublicKey());

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_publicKeyPem))
        {
            throw new InvalidOperationException("更新签名公钥尚未配置。");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ManifestTimeout);
        using var response = await _httpClient.GetAsync(
            ManifestUri,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > 32 * 1024)
        {
            throw new InvalidDataException("更新清单超过允许大小。");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var content = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token);
                if (read == 0)
                {
                    break;
                }

                if (content.Length + read > 32 * 1024)
                {
                    throw new InvalidDataException("更新清单超过允许大小。");
                }

                content.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var manifest = UpdateManifestVerifier.ParseAndVerify(content.ToArray(), _publicKeyPem, ManifestUri);
        var currentVersion = NormalizeVersion(
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0));
        var availableVersion = NormalizeVersion(Version.Parse(manifest.Version));
        return new UpdateCheckResult(currentVersion, availableVersion, manifest);
    }

    internal static Version NormalizeVersion(Version version)
    {
        return new Version(
            version.Major,
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0));
    }

    public async Task<string> DownloadPackageAsync(
        UpdateManifest manifest,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (manifest is null
            || string.IsNullOrWhiteSpace(manifest.Sha256)
            || manifest.PackageSizeBytes is <= 0 or > UpdateManifestVerifier.MaximumPackageBytes
            || manifest.Sha256.Length != 64
            || !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("更新包信息无效。");
        }

        UpdateManifestVerifier.ValidatePackageLocation(manifest, ManifestUri);
        var packageUri = new Uri(manifest.PackageUrl, UriKind.Absolute);

        Directory.CreateDirectory(AppStorage.UpdatesDirectory);
        var packageStem = manifest.Version + "-" + manifest.Sha256.ToLowerInvariant();
        var partialPath = Path.Combine(AppStorage.UpdatesDirectory, packageStem + ".zip.part");
        var completedPath = Path.Combine(AppStorage.UpdatesDirectory, packageStem + ".zip");

        if (File.Exists(completedPath))
        {
            if (MatchesExpectedPackage(completedPath, manifest))
            {
                return completedPath;
            }

            AppStorage.DeleteIfExists(completedPath);
        }

        var offset = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        if (offset >= manifest.PackageSizeBytes)
        {
            if (offset == manifest.PackageSizeBytes && MatchesExpectedPackage(partialPath, manifest))
            {
                File.Move(partialPath, completedPath);
                return completedPath;
            }

            AppStorage.DeleteIfExists(partialPath);
            offset = 0;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PackageTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, packageUri);
            if (offset > 0)
            {
                request.Headers.Range = new RangeHeaderValue(offset, null);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (response.StatusCode == HttpStatusCode.OK && offset > 0)
            {
                // Some compatible object endpoints ignore Range; restart safely from byte zero.
                AppStorage.DeleteIfExists(partialPath);
                offset = 0;
            }
            else if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                ValidateContentRange(response, offset, manifest.PackageSizeBytes);
            }
            else if (response.StatusCode != HttpStatusCode.OK)
            {
                response.EnsureSuccessStatusCode();
                throw new InvalidDataException("更新服务器返回了不支持的 HTTP 状态。");
            }

            if (response.Content.Headers.ContentLength is long contentLength
                && contentLength != manifest.PackageSizeBytes - offset)
            {
                throw new InvalidDataException("下载文件大小与更新清单不符。");
            }

            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var output = new FileStream(
                partialPath,
                offset == 0 ? FileMode.Create : FileMode.Append,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            var total = offset;
            progress?.Report(total);
            try
            {
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                    if (total > manifest.PackageSizeBytes
                        || total > UpdateManifestVerifier.MaximumPackageBytes)
                    {
                        throw new InvalidDataException("下载文件超过清单声明大小。");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    progress?.Report(total);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            await output.FlushAsync(timeout.Token);
            if (total != manifest.PackageSizeBytes)
            {
                throw new EndOfStreamException("下载暂未完成；已保留已下载部分，下次检查时会继续下载。");
            }

            if (!MatchesExpectedPackage(partialPath, manifest))
            {
                throw new InvalidDataException("更新包 SHA-256 校验失败。");
            }

            File.Move(partialPath, completedPath);
            return completedPath;
        }
        catch (InvalidDataException)
        {
            AppStorage.DeleteIfExists(partialPath);
            throw;
        }
        catch
        {
            if (File.Exists(partialPath) && new FileInfo(partialPath).Length == 0)
            {
                AppStorage.DeleteIfExists(partialPath);
            }

            throw;
        }
    }

    private static void ValidateContentRange(HttpResponseMessage response, long expectedStart, long totalSize)
    {
        var contentRange = response.Content.Headers.ContentRange;
        if (contentRange is null
            || !string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
            || contentRange.From != expectedStart
            || contentRange.To != totalSize - 1
            || contentRange.Length != totalSize)
        {
            throw new InvalidDataException("更新服务器返回的分段范围与预期不符。");
        }
    }

    private static bool MatchesExpectedPackage(string path, UpdateManifest manifest)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length != manifest.PackageSizeBytes)
        {
            return false;
        }

        using var stream = file.OpenRead();
        var actualHash = SHA256.HashData(stream);
        var expectedHash = Convert.FromHexString(manifest.Sha256);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public static string ReadEmbeddedPublicKey()
    {
        var metadata = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(item => string.Equals(item.Key, "SteadyDesk.UpdatePublicKeyBase64", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(metadata?.Value))
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(metadata.Value));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
