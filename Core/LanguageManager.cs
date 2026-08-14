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
        // Получаем 2-буквенный код (например, "ru", "de", "en")
        string langCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
        var available = GetAvailableLanguages();

        // Универсальный поиск:
        // Ищем файл, в названии которого (Code) или в "родном" имени (DisplayName) 
        // встречается код языка ОС (например, "ru" в "Russian" или "fr" в "French")
        var match = available.FirstOrDefault(l =>
            l.Code.Contains(langCode, StringComparison.OrdinalIgnoreCase) ||
            l.DisplayName.Contains(langCode, StringComparison.OrdinalIgnoreCase) ||
            langCode.Contains(l.Code, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match.Code;
      }
      catch
      {
        // Если что-то пошло не так, вернем дефолтный English
      }

      return "English";
    }

    /// <summary>
    /// Кроссплатформенный поиск папки с языками с защитой от регистра в Linux/macOS
    /// </summary>
    private static string GetLanguagesDirectory()
    {
      string baseDir = AppContext.BaseDirectory;

      // 1. Прямая проверка папки Language
      string path = Path.Combine(baseDir, "Language");
      if (Directory.Exists(path)) return path;

      // 2. Проверка Assets/Language
      path = Path.Combine(baseDir, "Assets", "Language");
      if (Directory.Exists(path)) return path;

      // 3. Регистронезависимый поиск папки на уровне AppContext.BaseDirectory
      try
      {
        if (Directory.Exists(baseDir))
        {
          var foundDir = Directory.GetDirectories(baseDir)
              .FirstOrDefault(d => Path.GetFileName(d).Equals("Language", StringComparison.OrdinalIgnoreCase) ||
                                   Path.GetFileName(d).Equals("Languages", StringComparison.OrdinalIgnoreCase));
          if (foundDir != null) return foundDir;

          // Проверяем внутри папки Assets, если она есть
          var assetsDir = Directory.GetDirectories(baseDir)
              .FirstOrDefault(d => Path.GetFileName(d).Equals("Assets", StringComparison.OrdinalIgnoreCase));
          if (assetsDir != null)
          {
            var foundInAssets = Directory.GetDirectories(assetsDir)
                .FirstOrDefault(d => Path.GetFileName(d).Equals("Language", StringComparison.OrdinalIgnoreCase) ||
                                     Path.GetFileName(d).Equals("Languages", StringComparison.OrdinalIgnoreCase));
            if (foundInAssets != null) return foundInAssets;
          }
        }
      }
      catch
      {
        // Игнорируем ошибки файловой системы при поиске
      }

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

    /// <summary>
    /// Кроссплатформенная загрузка файла перевода с защитой от регистра символов
    /// </summary>
    private static void LoadLanguageFile(string fileName)
    {
      try
      {
        string dir = GetLanguagesDirectory();
        string path = Path.Combine(dir, fileName);

        // Если файл не найден напрямую, ищем его с учетом регистра (для Linux/macOS)
        if (!File.Exists(path) && Directory.Exists(dir))
        {
          var matchedFile = Directory.GetFiles(dir, "*.json")
              .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

          if (matchedFile != null)
          {
            path = matchedFile;
          }
        }

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
