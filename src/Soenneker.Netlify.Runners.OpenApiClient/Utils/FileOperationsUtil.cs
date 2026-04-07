using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Soenneker.Extensions.String;
using Soenneker.Git.Util.Abstract;
using Soenneker.Netlify.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.Process.Abstract;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Kiota.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.File.Download.Abstract;
using System.Collections.Generic;
using Soenneker.OpenApi.Converter.Abstract;
using Soenneker.Utils.Yaml.Abstract;

namespace Soenneker.Netlify.Runners.OpenApiClient.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IConfiguration _configuration;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IKiotaUtil _kiotaUtil;
    private readonly IYamlUtil _yamlUtil;
    private readonly IOpenApiConverter _converter;
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IConfiguration configuration, IGitUtil gitUtil, IDotnetUtil dotnetUtil,
        IFileDownloadUtil fileDownloadUtil, IFileUtil fileUtil, IDirectoryUtil directoryUtil, IKiotaUtil kiotaUtil, IYamlUtil yamlUtil,
        IOpenApiConverter converter)
    {
        _logger = logger;
        _configuration = configuration;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _kiotaUtil = kiotaUtil;
        _yamlUtil = yamlUtil;
        _converter = converter;
        _fileDownloadUtil = fileDownloadUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
    }

    public async ValueTask Process(CancellationToken cancellationToken = default)
    {
        string gitDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.Library.ToLowerInvariantFast()}",
            cancellationToken: cancellationToken);

        string yamlPath = Path.Combine(gitDirectory, "openapi.yml");

        await _fileUtil.DeleteIfExists(yamlPath, cancellationToken: cancellationToken);

        string openApiDocumentUrl = _configuration["Netlify:ClientGenerationUrl"] ??
                                    "https://raw.githubusercontent.com/netlify/open-api/refs/heads/master/swagger.yml";

        await _fileDownloadUtil.Download(openApiDocumentUrl, yamlPath, fileExtension: ".yml", cancellationToken: cancellationToken);

        string openApi2Path = Path.Combine(gitDirectory, "openapi2.json");

        await _yamlUtil.SaveAsJson(yamlPath, openApi2Path, true, cancellationToken);

        string openApi3Path = Path.Combine(gitDirectory, "openapi3.json");

        await _converter.ConvertFile(openApi2Path, openApi3Path, cancellationToken);

        await _kiotaUtil.EnsureInstalled(cancellationToken);

        string srcDirectory = Path.Combine(gitDirectory, "src", Constants.Library);

        await DeleteAllExceptCsproj(srcDirectory, cancellationToken);

        await _kiotaUtil.Generate(openApi3Path, "NetlifyOpenApiClient", Constants.Library, gitDirectory, cancellationToken)
                        .NoSync();

        await BuildAndPush(gitDirectory, cancellationToken)
            .NoSync();
    }

    public async ValueTask DeleteAllExceptCsproj(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!(await _directoryUtil.Exists(directoryPath, cancellationToken)))
        {
            _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return;
        }

        try
        {
            // Delete all files except .csproj
            List<string> files = await _directoryUtil.GetFilesByExtension(directoryPath, "", true, cancellationToken);
            foreach (string file in files)
            {
                if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _fileUtil.Delete(file, ignoreMissing: true, log: false, cancellationToken);
                        _logger.LogInformation("Deleted file: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete file: {FilePath}", file);
                    }
                }
            }

            // Delete all empty subdirectories
            List<string> dirs = await _directoryUtil.GetAllDirectoriesRecursively(directoryPath, cancellationToken);
            foreach (string dir in dirs.OrderByDescending(d => d.Length)) // Sort by depth to delete from deepest first
            {
                try
                {
                    List<string> dirFiles = await _directoryUtil.GetFilesByExtension(dir, "", false, cancellationToken);
                    List<string> subDirs = await _directoryUtil.GetAllDirectories(dir, cancellationToken);
                    if (dirFiles.Count == 0 && subDirs.Count == 0)
                    {
                        await _directoryUtil.Delete(dir, cancellationToken);
                        _logger.LogInformation("Deleted empty directory: {DirectoryPath}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete directory: {DirectoryPath}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning the directory: {DirectoryPath}", directoryPath);
        }
    }

    private async ValueTask BuildAndPush(string gitDirectory, CancellationToken cancellationToken)
    {
        string projFilePath = Path.Combine(gitDirectory, "src", Constants.Library, $"{Constants.Library}.csproj");

        await _dotnetUtil.Restore(projFilePath, cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projFilePath, true, "Release", false, cancellationToken: cancellationToken);

        if (!successful)
        {
            _logger.LogError("Build was not successful, exiting...");
            return;
        }

        string gitHubToken = EnvironmentUtil.GetVariableStrict("GH__TOKEN");
        string name = EnvironmentUtil.GetVariableStrict("GIT__NAME");
        string email = EnvironmentUtil.GetVariableStrict("GIT__EMAIL");

        await _gitUtil.CommitAndPush(gitDirectory, "Automated update", gitHubToken, name, email, cancellationToken);
    }
}