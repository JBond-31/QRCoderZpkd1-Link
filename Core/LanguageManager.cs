using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{
  /// <summary>
  /// Модель для представления языка в меню
  /// </summary>
  public class LanguageItem
  {
    public string Code { get; set; }        // Системное имя файла (например, "German")
    public string DisplayName { get; set; } // Родное название из JSON (например, "Deutsch")
  }

  public static class LanguageManager
  {
    private static Dictionary<string, string> _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string> Translations => _translations;
    public static string CurrentLanguage { get; private set; } = "English";

    public static void Initialize()
    {
      string detectedLanguage = GetSystemLanguage();
      SwitchLanguage(detectedLanguage);
    }

    /// <summary>
    /// Определяет язык операционной системы и сопоставляет его с доступными файлами
    /// </summary>
    private static string GetSystemLanguage()
    {
      try
      {
        string langCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
        var available = GetAvailableLanguages();

        string targetLanguageName = langCode switch
        {
          "ru" => "Russian",
          "uk" => "Ukrainian",
          "de" => "German",
          _ => "English"
        };

        // Ищем точное совпадение по системному коду файла
        var matched = available.FirstOrDefault(l => string.Equals(l.Code, targetLanguageName, StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
          return matched.Code;
        }

        // Умный фолбек на случай, если файлы названы на родном языке (например, "Русский.json" или "Deutsch.json")
        if (langCode == "de")
        {
          var match = available.FirstOrDefault(l => l.Code.StartsWith("De", StringComparison.OrdinalIgnoreCase) || l.DisplayName.StartsWith("De", StringComparison.OrdinalIgnoreCase));
          if (match != null) return match.Code;
        }
        if (langCode == "ru")
        {
          var match = available.FirstOrDefault(l => l.Code.StartsWith("Ру", StringComparison.OrdinalIgnoreCase) || l.DisplayName.StartsWith("Ру", StringComparison.OrdinalIgnoreCase));
          if (match != null) return match.Code;
        }
        if (langCode == "uk")
        {
          var match = available.FirstOrDefault(l => l.Code.StartsWith("Ук", StringComparison.OrdinalIgnoreCase) || l.DisplayName.StartsWith("Ук", StringComparison.OrdinalIgnoreCase));
          if (match != null) return match.Code;
        }
      }
      catch
      {
        // Санитарный сейв
      }

      return "English";
    }

    private static string GetLanguagesDirectory()
    {
      string baseDir = AppContext.BaseDirectory;

      string path = Path.Combine(baseDir, "Language");
      if (Directory.Exists(path)) return path;

      path = Path.Combine(baseDir, "Assets", "Language");
      if (Directory.Exists(path)) return path;

      return Path.Combine(baseDir, "Language");
    }

    /// <summary>
    /// Сканирует папку и возвращает список доступных языков с их родными названиями
    /// Сортировка: English всегда первый, остальные — строго по алфавиту имен файлов (Code) одинаково на всех .NET платформах.
    /// </summary>
    public static List<LanguageItem> GetAvailableLanguages()
    {
      var languages = new List<LanguageItem>();
      try
      {
        string dir = GetLanguagesDirectory();
        if (Directory.Exists(dir))
        {
          var files = Directory.GetFiles(dir, "*.json");
          foreach (var file in files)
          {
            string code = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrEmpty(code))
            {
              // Считываем родное имя из файла. Если не вышло — используем имя файла
              string displayName = GetNativeNameFromFile(file) ?? code;
              languages.Add(new LanguageItem { Code = code, DisplayName = displayName });
            }
          }
        }
      }
      catch
      {
        // Игнорируем ошибки доступа к файловой системе
      }

      if (languages.Count == 0)
      {
        languages.Add(new LanguageItem { Code = "English", DisplayName = "English" });
      }

      // Группируем по коду с учетом регистронезависимости, ставим English первым, 
      // а остальные сортируем по имени файла (Code) с использованием OrdinalIgnoreCase для идентичной работы в .NET Framework и .NET 10.
      return languages.GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
                      .Select(g => g.First())
                      .OrderBy(l => string.Equals(l.Code, "English", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                      .ThenBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
                      .ToList();
    }

    /// <summary>
    /// Быстрое чтение только одного ключа "LanguageName" без полной десериализации словаря
    /// </summary>
    private static string GetNativeNameFromFile(string path)
    {
      try
      {
        if (File.Exists(path))
        {
          string json = File.ReadAllText(path);
          using (JsonDocument doc = JsonDocument.Parse(json))
          {
            if (doc.RootElement.TryGetProperty("LanguageName", out JsonElement val))
            {
              return val.GetString();
            }
          }
        }
      }
      catch
      {
        // Файл поврежден или не является корректным JSON
      }
      return null;
    }

    public static void SwitchLanguage(string languageName)
    {
      _translations.Clear();

      // 1. Сначала загружаем резервный английский
      LoadLanguageFile("English.json");

      // 2. Поверх накатываем выбранный язык
      if (!string.Equals(languageName, "English", StringComparison.OrdinalIgnoreCase))
      {
        LoadLanguageFile($"{languageName}.json");
      }

      CurrentLanguage = languageName;
    }

    private static void LoadLanguageFile(string fileName)
    {
      try
      {
        string dir = GetLanguagesDirectory();
        string path = Path.Combine(dir, fileName);

        if (File.Exists(path))
        {
          string json = File.ReadAllText(path);
          var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
          if (data != null)
          {
            foreach (var kvp in data)
            {
              _translations[kvp.Key] = kvp.Value;
            }
          }
        }
      }
      catch
      {
        // Игнорируем ошибки поврежденных файлов перевода
      }
    }

    public static string GetString(string key)
    {
      if (string.IsNullOrEmpty(key)) return string.Empty;

      if (_translations.TryGetValue(key, out string translation))
      {
        return translation;
      }

      return $"[{key}]";
    }
  }
}