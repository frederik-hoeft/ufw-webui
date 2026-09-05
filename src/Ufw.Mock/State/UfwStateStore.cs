using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Mock.Cli;

namespace Ufw.Mock.State;

internal sealed class UfwStateStore
{
    public const string STATE_PATH_ENVIRONMENT_VARIABLE = "UFW_MOCK_STATE_PATH";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public UfwStateStore(string? path = null)
    {
        try
        {
            _path = path ?? ResolveDefaultPath();
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            throw new UfwCliException($"Could not resolve mock state path: {exception.Message}");
        }
    }

    public T Read<T>(Func<UfwMockState, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using FileStream lockHandle = AcquireLock();
        UfwMockState state = Load();
        return operation(state);
    }

    public T Update<T>(bool dryRun, Func<UfwMockState, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using FileStream lockHandle = AcquireLock();
        UfwMockState state = Load();
        T result = operation(state);
        if (!dryRun)
        {
            Save(state);
        }
        return result;
    }

    private UfwMockState Load()
    {
        if (!File.Exists(_path))
        {
            return UfwMockState.CreateDefault();
        }

        try
        {
            string json = File.ReadAllText(_path);
            UfwMockState state = JsonSerializer.Deserialize<UfwMockState>(json, s_jsonOptions)
                ?? throw new JsonException("State document is empty.");
            UfwStateValidator.Validate(state);
            return state;
        }
        catch (UfwCliException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new UfwCliException($"Could not read mock state '{_path}': {exception.Message}");
        }
    }

    private void Save(UfwMockState state)
    {
        UfwStateValidator.Validate(state);

        string temporaryPath = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            EnsureDirectory(_path);
            string json = JsonSerializer.Serialize(state, s_jsonOptions);
            File.WriteAllText(temporaryPath, json + Environment.NewLine);
            File.Move(temporaryPath, _path, true);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            throw new UfwCliException($"Could not write mock state '{_path}': {exception.Message}");
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private FileStream AcquireLock()
    {
        string lockPath = _path + ".lock";
        try
        {
            EnsureDirectory(lockPath);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            throw new UfwCliException($"Could not lock mock state '{_path}': {exception.Message}");
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                throw new UfwCliException($"Could not lock mock state '{_path}': {exception.Message}");
            }
        }
    }

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static bool IsFileSystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string ResolveDefaultPath()
    {
        string? configured = Environment.GetEnvironmentVariable(STATE_PATH_ENVIRONMENT_VARIABLE);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = AppContext.BaseDirectory;
        }
        return Path.Combine(localApplicationData, "Ufw.Mock", "state.json");
    }
}
