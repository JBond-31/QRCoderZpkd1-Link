using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{
  public class WatchModelInfo
  {
    public string Key { get; set; }
    public string Name { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string ScreenType { get; set; }
    // Чистое имя без слова "Amazfit " для вывода на превью
    public string CleanName => Name.StartsWith("Amazfit ", StringComparison.OrdinalIgnoreCase)
        ? Name.Substring(8).Trim()
        : Name;
  }

  public static class ConfigManager
  {
    /// <summary>
    /// Возвращает путь к файлу configurations.json рядом с .exe файлом (с поддержкой кроссплатформенного фолбека и учета регистра)
    /// </summary>
    public static string GetConfigFilePath()
    {
      string baseDir = AppContext.BaseDirectory;

      // Основной путь в папке Data
      string dataPath = Path.Combine(baseDir, "Data", "configurations.json");
      if (File.Exists(dataPath)) return dataPath;

      // Кроссплатформенный поиск с учетом возможного различия регистра в Linux/macOS
      string dataDir = Path.Combine(baseDir, "Data");
      if (Directory.Exists(dataDir))
      {
        var foundFile = Directory.GetFiles(dataDir, "*.json")
            .FirstOrDefault(f => Path.GetFileName(f).Equals("configurations.json", StringComparison.OrdinalIgnoreCase));
        if (foundFile != null) return foundFile;
      }
      else
      {
        // Поиск папки с учетом регистра (например, если папка названа в нижнем регистре)
        var foundDir = Directory.GetDirectories(baseDir)
            .FirstOrDefault(d => Path.GetFileName(d).Equals("Data", StringComparison.OrdinalIgnoreCase));
        if (foundDir != null)
        {
          dataPath = Path.Combine(foundDir, "configurations.json");
          if (File.Exists(dataPath)) return dataPath;

          var foundFile = Directory.GetFiles(foundDir, "*.json")
              .FirstOrDefault(f => Path.GetFileName(f).Equals("configurations.json", StringComparison.OrdinalIgnoreCase));
          if (foundFile != null) return foundFile;
        }
      }

      // Защитный фолбек на случай старого кэша сборки в папке Language
      string langPath = Path.Combine(baseDir, "Language", "configurations.json");
      if (File.Exists(langPath)) return langPath;

      return dataPath;
    }

    /// <summary>
    /// Загружает все модели из конфигурационного файла
    /// </summary>
    public static Dictionary<string, WatchModelInfo> LoadModels()
    {
      var dict = new Dictionary<string, WatchModelInfo>(StringComparer.OrdinalIgnoreCase);
      try
      {
        string path = GetConfigFilePath();
        if (!File.Exists(path))
        {
          return dict;
        }

        string json = File.ReadAllText(path);
        using (JsonDocument doc = JsonDocument.Parse(json))
        {
          foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
          {
            var val = prop.Value;
            string name = val.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : prop.Name;
            string screenType = val.TryGetProperty("screenType", out var stProp) ? stProp.GetString() : "round";

            int w = 0, h = 0;
            if (val.TryGetProperty("background", out var bgProp))
            {
              if (bgProp.TryGetProperty("w", out var wP)) w = wP.GetInt32();
              if (bgProp.TryGetProperty("h", out var hP)) h = hP.GetInt32();
            }
            // Если background пустой, берем designWidth
            if (w == 0 && val.TryGetProperty("designWidth", out var dwP))
            {
              w = dwP.GetInt32();
              h = w; // для квадратов/кругов часто совпадает или берем дефолт
            }

            dict[prop.Name] = new WatchModelInfo
            {
              Key = prop.Name,
              Name = name ?? prop.Name,
              Width = w,
              Height = h,
              ScreenType = screenType.ToLower()
            };
          }
        }
      }
      catch
      {
        // Игнорируем ошибки чтения/парсинга в фоновой библиотеке
      }
      return dict;
    }

    /// <summary>
    /// Формирует список разрешений в строгом соответствии с правилами:
    /// Сначала Round (от большего к меньшему по h), затем Square (от большего к меньшему по h), затем Bar (от большего к меньшему по h).
    /// Формат: {h}x{w} {ScreenType} (с заглавной буквы)
    /// </summary>
    public static List<string> GetResolutions()
    {
      var models = LoadModels().Values;

      var uniqueItems = models
          .Select(m => new
          {
            H = m.Height,
            W = m.Width,
            Type = m.ScreenType.ToLower(),
            FormattedType = char.ToUpper(m.ScreenType[0]) + m.ScreenType.Substring(1).ToLower()
          })
          .Distinct()
          .ToList();

      // Приоритеты сортировки типов экранов: Round = 0, Square = 1, Bar = 2
      int GetTypePriority(string type)
      {
        if (type == "round") return 0;
        if (type == "square") return 1;
        return 2; // bar и прочие
      }

      var sorted = uniqueItems
          .OrderBy(x => GetTypePriority(x.Type))
          .ThenByDescending(x => x.H)
          .ThenByDescending(x => x.W)
          .Select(x => $"{x.H}x{x.W} {x.FormattedType}")
          .ToList();

      return sorted;
    }

    /// <summary>
    /// Возвращает список моделей для конкретного разрешения (например, "480x480 Round")
    /// </summary>
    public static List<WatchModelInfo> GetModelsForResolution(string resolutionString)
    {
      if (string.IsNullOrWhiteSpace(resolutionString))
        return new List<WatchModelInfo>();

      // Парсим строку вида "480x480 Round"
      var parts = resolutionString.Split(' ');
      if (parts.Length < 2) return new List<WatchModelInfo>();

      var dimParts = parts[0].Split('x');
      if (dimParts.Length != 2) return new List<WatchModelInfo>();

      if (!int.TryParse(dimParts[0], out int targetH) || !int.TryParse(dimParts[1], out int targetW))
        return new List<WatchModelInfo>();

      string targetType = parts[1].ToLower();

      var allModels = LoadModels().Values;

      return allModels
          .Where(m => m.Height == targetH && m.Width == targetW && m.ScreenType.Equals(targetType, StringComparison.OrdinalIgnoreCase))
          .OrderBy(m => m.Name)
          .ToList();
    }
  }
}
