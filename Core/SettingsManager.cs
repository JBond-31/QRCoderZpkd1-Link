using System;
using System.IO;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{
  /// <summary>
  /// Модель данных для хранения пользовательских настроек
  /// </summary>
  public class UserSettings
  {
    // Делаем координаты nullable (int?), чтобы при первом запуске (когда их еще нет) 
    // окно могло отцентрироваться по умолчанию
    public int? WindowLeft { get; set; }
    public int? WindowTop { get; set; }

    // По умолчанию приложение запускается с темной темой
    public string Theme { get; set; } = "DarkTheme";

    // Если язык пустой, LanguageManager сам подхватит язык системы
    public string Language { get; set; } = string.Empty;
  }

  /// <summary>
  /// Логический класс для управления сохранением и загрузкой настроек пользователя
  /// </summary>
  public static class SettingsManager
  {
    // Путь к файлу конфигурации строго рядом с исполняемым файлом .exe
    private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "UserSetting.json");

    // Текущие активные настройки
    public static UserSettings Current { get; private set; } = new UserSettings();

    /// <summary>
    /// Загрузка настроек из файла UserSetting.json
    /// </summary>
    public static void Load()
    {
      try
      {
        if (File.Exists(SettingsFilePath))
        {
          string json = File.ReadAllText(SettingsFilePath);
          Current = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
      }
      catch
      {
        // В случае повреждения файла или ошибки прав доступа создаем чистый дефолтный конфиг
        Current = new UserSettings();
      }
    }

    /// <summary>
    /// Сохранение текущих настроек в файл UserSetting.json
    /// </summary>
    public static void Save()
    {
      try
      {
        // Форматируем JSON с отступами, чтобы пользователю было удобно его читать при необходимости
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(Current, options);
        File.WriteAllText(SettingsFilePath, json);
      }
      catch
      {
        // Игнорируем возможные ошибки записи (например, если программа запущена из защищенной директории без прав)
      }
    }
  }
}