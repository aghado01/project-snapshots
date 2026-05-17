using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Repo.TestHarness;

internal sealed class ArtifactRun
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public ArtifactRun(string runDirectory, string manifestPath)
    {
        RunDirectory = runDirectory;
        ManifestPath = manifestPath;
    }

    public string RunDirectory { get; }

    public string ManifestPath { get; }

    public string WriteJson<T>(string fileName, T value)
    {
        string path = Path.Combine(RunDirectory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        return path;
    }

    public string WriteText(string fileName, string content)
    {
        string path = Path.Combine(RunDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}

internal static class HarnessArtifacts
{
    public static ArtifactRun Create(
        string runKind,
        string suiteName,
        string runName,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        string repositoryRoot = FindRepositoryRoot();
        string suiteDirectory = Path.Combine(
            repositoryRoot,
            "artifacts",
            SanitizePathSegment(runKind),
            SanitizePathSegment(suiteName));
        Directory.CreateDirectory(suiteDirectory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string runDirectory = Path.Combine(suiteDirectory, timestamp);
        int suffix = 1;
        while (Directory.Exists(runDirectory))
        {
            runDirectory = Path.Combine(suiteDirectory, $"{timestamp}_{suffix:00}");
            suffix++;
        }

        return CreateCore(runKind, suiteName, runName, runDirectory, repositoryRoot, metadata);
    }

    public static ArtifactRun Attach(
        string runKind,
        string suiteName,
        string runName,
        string runDirectory,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        string repositoryRoot = FindRepositoryRoot();
        return CreateCore(runKind, suiteName, runName, Path.GetFullPath(runDirectory), repositoryRoot, metadata);
    }

    private static ArtifactRun CreateCore(
        string runKind,
        string suiteName,
        string runName,
        string runDirectory,
        string repositoryRoot,
        IReadOnlyDictionary<string, object?>? metadata)
    {
        Directory.CreateDirectory(runDirectory);

        string manifestPath = Path.Combine(runDirectory, "manifest.json");
        var manifest = new HarnessRunManifest(
            RunKind: runKind,
            SuiteName: suiteName,
            RunName: runName,
            CreatedAtLocal: DateTime.Now,
            MachineName: Environment.MachineName,
            ProcessId: Environment.ProcessId,
            FrameworkDescription: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            RepositoryRoot: repositoryRoot,
            RunDirectory: runDirectory,
            Metadata: metadata is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(metadata));

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

        return new ArtifactRun(runDirectory, manifestPath);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(current.FullName, "changelog.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the harness output directory.");
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '-';
        }

        return new string(chars);
    }

    private sealed record HarnessRunManifest(
        string RunKind,
        string SuiteName,
        string RunName,
        DateTime CreatedAtLocal,
        string MachineName,
        int ProcessId,
        string FrameworkDescription,
        string RepositoryRoot,
        string RunDirectory,
        Dictionary<string, object?> Metadata);
}
