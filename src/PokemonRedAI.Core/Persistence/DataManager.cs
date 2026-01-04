using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokemonRedAI.Core.Persistence;

public class DataManager : IDisposable
{
    private readonly string _saveFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Timer _autoSaveTimer;
    private SaveData _currentData;
    private bool _isDirty;
    private bool _disposed;

    public event EventHandler<SaveData>? DataLoaded;
    public event EventHandler<SaveData>? DataSaved;
    public event EventHandler<Exception>? SaveError;

    public SaveData CurrentData => _currentData;
    public bool HasUnsavedChanges => _isDirty;

    public DataManager(string? saveFilePath = null, int autoSaveIntervalSeconds = 60)
    {
        _saveFilePath = saveFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PokemonRedAI",
            "learned_data.json"
        );

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        _currentData = new SaveData();
        _isDirty = false;

        _autoSaveTimer = new Timer(
            AutoSaveCallback,
            null,
            TimeSpan.FromSeconds(autoSaveIntervalSeconds),
            TimeSpan.FromSeconds(autoSaveIntervalSeconds)
        );
    }

    public async Task<bool> LoadAsync()
    {
        try
        {
            if (!File.Exists(_saveFilePath))
            {
                _currentData = new SaveData();
                _isDirty = false;
                return false;
            }

            var json = await File.ReadAllTextAsync(_saveFilePath);
            var loadedData = JsonSerializer.Deserialize<SaveData>(json, _jsonOptions);

            if (loadedData != null)
            {
                _currentData = loadedData;
                _isDirty = false;
                DataLoaded?.Invoke(this, _currentData);
                return true;
            }

            _currentData = new SaveData();
            return false;
        }
        catch (JsonException)
        {
            _currentData = new SaveData();
            return false;
        }
        catch (Exception ex)
        {
            SaveError?.Invoke(this, ex);
            _currentData = new SaveData();
            return false;
        }
    }

    public async Task<bool> SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_saveFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _currentData.LastSaved = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(_currentData, _jsonOptions);
            await File.WriteAllTextAsync(_saveFilePath, json);

            _isDirty = false;
            DataSaved?.Invoke(this, _currentData);
            return true;
        }
        catch (Exception ex)
        {
            SaveError?.Invoke(this, ex);
            return false;
        }
    }

    public void MarkDirty()
    {
        _isDirty = true;
    }

    public TileState GetTileState(string mapId, int x, int y)
    {
        var key = $"{x},{y}";
        if (_currentData.WalkabilityMaps.TryGetValue(mapId, out var mapData))
        {
            if (mapData.Tiles.TryGetValue(key, out var state))
            {
                return state;
            }
        }
        return TileState.Unknown;
    }

    public void SetTileState(string mapId, int x, int y, TileState state)
    {
        var key = $"{x},{y}";

        if (!_currentData.WalkabilityMaps.TryGetValue(mapId, out var mapData))
        {
            mapData = new MapWalkabilityData { MapId = mapId };
            _currentData.WalkabilityMaps[mapId] = mapData;
        }

        var previousState = mapData.Tiles.TryGetValue(key, out var existing) ? existing : TileState.Unknown;
        mapData.Tiles[key] = state;

        if (previousState == TileState.Unknown && state != TileState.Unknown)
        {
            _currentData.GameProgress.TilesDiscovered++;
        }

        _isDirty = true;
    }

    public void IncrementSteps()
    {
        _currentData.GameProgress.TotalSteps++;
        _isDirty = true;
    }

    public void UpdatePosition(string mapId, int x, int y)
    {
        _currentData.GameProgress.LastMapId = mapId;
        _currentData.GameProgress.LastPositionX = x;
        _currentData.GameProgress.LastPositionY = y;
        _isDirty = true;
    }

    public void AddPlayTime(TimeSpan elapsed)
    {
        _currentData.GameProgress.TotalPlayTime += elapsed;
        _isDirty = true;
    }

    private async void AutoSaveCallback(object? state)
    {
        if (_isDirty && !_disposed)
        {
            await SaveAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autoSaveTimer.Dispose();

        if (_isDirty)
        {
            SaveAsync().GetAwaiter().GetResult();
        }
    }
}
