using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SymbolAutoUpload;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    Console.WriteLine();
    Console.WriteLine("취소 요청을 감지했습니다. 정리 후 종료합니다...");
    e.Cancel = true;
    cts.Cancel();
};

var exitCode = await RunAsync(args, cts.Token);
return exitCode;

static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
{
    try
    {
        var configuration = BuildConfiguration(args);
        var settings = configuration.GetSection(SymbolUploadSettings.SectionName).Get<SymbolUploadSettings>() ?? new();

        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        {
            Console.Error.WriteLine("SymbolUpload.ApiBaseUrl 설정이 필요합니다. appsettings.json을 확인하세요.");
            return 1;
        }

        await ExecuteAsync(settings, cancellationToken);
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("사용자 요청으로 작업을 중단했습니다.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"치명적인 오류가 발생했습니다: {ex.Message}");
#if DEBUG
        Console.Error.WriteLine(ex);
#endif
        return 1;
    }
}

static IConfigurationRoot BuildConfiguration(string[] args) =>
    new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables(prefix: "SYMBOLUPLOAD_")
        .AddCommandLine(args)
        .Build();

static async Task ExecuteAsync(SymbolUploadSettings settings, CancellationToken cancellationToken)
{
    var baseUri = BuildBaseUri(settings.ApiBaseUrl);
    var timeoutSeconds = settings.HttpTimeoutSeconds <= 0 ? 300 : settings.HttpTimeoutSeconds;
    using var http = new HttpClient
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 30, 3600))
    };
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    var username = !string.IsNullOrWhiteSpace(settings.Username)
        ? settings.Username.Trim()
        : PromptForInput("아이디: ");
    if (string.IsNullOrWhiteSpace(username))
    {
        throw new InvalidOperationException("아이디를 입력해야 합니다.");
    }

    var password = !string.IsNullOrEmpty(settings.Password)
        ? settings.Password
        : ReadPassword("비밀번호: ");
    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException("비밀번호를 입력해야 합니다.");
    }

    Console.WriteLine("자격 증명을 확인하는 중...");
    await EnsureCredentialsValidAsync(http, username, password, cancellationToken);
    Console.WriteLine("로그인 성공. Authorization 헤더를 구성합니다.");

    var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

    var searchRoot = ResolveSearchRoot(settings);
    if (!Directory.Exists(searchRoot))
    {
        throw new DirectoryNotFoundException($"PDB 검색 경로를 찾을 수 없습니다: {searchRoot}");
    }

    Console.WriteLine($"검색 경로: {searchRoot}");
    var searchOption = settings.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var pdbFiles = Directory.EnumerateFiles(searchRoot, "*.pdb", searchOption)
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .ToArray();

    if (pdbFiles.Length == 0)
    {
        Console.WriteLine("업로드할 PDB 파일이 없습니다.");
        return;
    }

    Console.WriteLine($"{pdbFiles.Length}개의 PDB 파일을 찾았습니다. 업로드를 시작합니다.");
    var uploadedBy = string.IsNullOrWhiteSpace(settings.UploadedByOverride) ? username : settings.UploadedByOverride!.Trim();
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    var successCount = 0;
    foreach (var pdb in pdbFiles)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetFileName(pdb);
        Console.Write($"- {fileName} 업로드 중... ");
        try
        {
            var response = await UploadPdbAsync(http, pdb, settings, uploadedBy, jsonOptions, cancellationToken);
            successCount++;
            Console.WriteLine("완료");
            Console.WriteLine($"  ↳ 서버 메시지: {response.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"실패 ({ex.Message})");
        }
    }

    Console.WriteLine($"작업 완료: 총 {pdbFiles.Length}개 중 {successCount}개 성공");
}

static async Task EnsureCredentialsValidAsync(HttpClient http, string username, string password, CancellationToken cancellationToken)
{
    var payload = new { Username = username, Password = password };
    using var response = await http.PostAsJsonAsync("api/auth/validate", payload, cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"자격 증명 확인 실패 (HTTP {(int)response.StatusCode}): {body}");
    }

    var validation = JsonSerializer.Deserialize<CredentialValidationResponse>(body, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (validation is null || !validation.Valid)
    {
        throw new InvalidOperationException("아이디 또는 비밀번호가 올바르지 않습니다.");
    }
}

static async Task<PdbUploadResponse> UploadPdbAsync(
    HttpClient http,
    string pdbPath,
    SymbolUploadSettings settings,
    string uploadedBy,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    using var form = new MultipartFormDataContent();
    await using var fileStream = File.OpenRead(pdbPath);
    var fileContent = new StreamContent(fileStream);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    form.Add(fileContent, "file", Path.GetFileName(pdbPath));

    AddFormValue(form, "ProductName", settings.ProductName);
    AddFormValue(form, "Version", settings.Version);
    AddFormValue(form, "Comment", settings.Comment);
    AddFormValue(form, "UploadedBy", uploadedBy);

    using var response = await http.PostAsync("api/pdb/upload", form, cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"업로드 실패 (HTTP {(int)response.StatusCode}): {body}");
    }

    var payload = JsonSerializer.Deserialize<PdbUploadResponse>(body, jsonOptions);
    if (payload == null)
    {
        throw new InvalidOperationException("서버 응답을 해석할 수 없습니다.");
    }

    return payload;
}

static void AddFormValue(MultipartFormDataContent form, string fieldName, string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return;
    form.Add(new StringContent(value), fieldName);
}

static string ResolveSearchRoot(SymbolUploadSettings settings)
{
    if (!string.IsNullOrWhiteSpace(settings.PdbDirectory))
    {
        var expanded = Environment.ExpandEnvironmentVariables(settings.PdbDirectory);
        return Path.GetFullPath(expanded);
    }

    return Path.GetFullPath(AppContext.BaseDirectory);
}

static Uri BuildBaseUri(string raw)
{
    var trimmed = raw.Trim();
    if (!trimmed.EndsWith("/"))
    {
        trimmed += "/";
    }

    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"유효하지 않은 API URL입니다: {raw}");
    }

    return uri;
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var sb = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0)
            {
                sb.Length--;
                Console.Write("\b \b");
            }
            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            sb.Append(key.KeyChar);
            Console.Write('*');
        }
    }

    return sb.ToString();
}

static string PromptForInput(string prompt)
{
    Console.Write(prompt);
    return (Console.ReadLine() ?? string.Empty).Trim();
}
