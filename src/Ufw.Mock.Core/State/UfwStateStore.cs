using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Mock.Cli;
using Ufw.Mock.Filtering;
using Ufw.Mock.Formatting;
using Ufw.Shared.Firewall;

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

    public const string USER_RULES_PATH_ENVIRONMENT_VARIABLE = "UFW_MOCK_USER_RULES_PATH";
    public const string USER6_RULES_PATH_ENVIRONMENT_VARIABLE = "UFW_MOCK_USER6_RULES_PATH";

    private readonly string _path;
    private readonly string _userRulesPath;
    private readonly string _user6RulesPath;

    public UfwStateStore(string? path = null)
    {
        try
        {
            _path = path ?? ResolveDefaultPath();
            string directory = Path.GetDirectoryName(Path.GetFullPath(_path))!;
            _userRulesPath = ResolveUserRulesPath(USER_RULES_PATH_ENVIRONMENT_VARIABLE, Path.Combine(directory, "user.rules"));
            _user6RulesPath = ResolveUserRulesPath(USER6_RULES_PATH_ENVIRONMENT_VARIABLE, Path.Combine(directory, "user6.rules"));
            if (Path.GetFullPath(_path) == _userRulesPath || Path.GetFullPath(_path) == _user6RulesPath || _userRulesPath == _user6RulesPath)
            {
                throw new ArgumentException("Mock state and user-rule files must have distinct paths.");
            }
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
        EnsureUserRules(state);
        return operation(state);
    }

    /// <summary>Observe the installed table without generating UFW configuration files.</summary>
    public T ReadInstalled<T>(Func<UfwMockState, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using FileStream lockHandle = AcquireLock();
        return operation(Load());
    }

    public T Update<T>(bool dryRun, Func<UfwMockState, T> operation, bool reloadFixtures = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using FileStream lockHandle = AcquireLock();
        UfwMockState state = Load();
        bool wasEnabled = state.Enabled;
        T result = operation(state);
        if (!dryRun)
        {
            if (state.Enabled)
            {
                try
                {
                    state.Installed = MockInstalledFilterState.Install(state, state.Installed, reloadFixtures);
                }
                catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
                {
                    throw new UfwCliException($"Could not install mock firewall: {exception.Message}");
                }
            }
            else if (wasEnabled || state.Installed is null)
            {
                state.Installed = MockInstalledFilterState.Stop(state.Installed);
            }
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
            // Older versions stored only UFW configuration and generated physical
            // output on demand. Upgrade on the next persisted mutation.
            if (state.SchemaVersion == 1)
            {
                state.SchemaVersion = UfwMockState.CURRENT_SCHEMA_VERSION;
            }
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
            WriteUserRules(state);
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

    private void EnsureUserRules(UfwMockState state)
    {
        if (!File.Exists(_userRulesPath) || !File.Exists(_user6RulesPath))
        {
            WriteUserRules(state);
        }
    }

    private void WriteUserRules(UfwMockState state)
    {
        WriteTextAtomically(_userRulesPath, MockUserRulesFormatter.Format(state, FirewallAddressFamily.IPv4));
        WriteTextAtomically(_user6RulesPath, MockUserRulesFormatter.Format(state, FirewallAddressFamily.IPv6));
    }

    private static void WriteTextAtomically(string path, string contents)
    {
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            EnsureDirectory(path);
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, path, true);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            throw new UfwCliException($"Could not write mock user rules '{path}': {exception.Message}");
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

    private static string ResolveUserRulesPath(string variable, string fallback)
    {
        string? configured = Environment.GetEnvironmentVariable(variable);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? fallback : configured);
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
