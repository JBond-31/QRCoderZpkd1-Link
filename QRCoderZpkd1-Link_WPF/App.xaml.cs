#nullable disable // Отключаем проверку на null, чтобы не было предупреждений
using System; //Для диагностики. почему не видно окно приложения
using System.Windows;
using QRCoderZpkd1_Link.Core; // Подключаем пространство имен нашего менеджера
using System.IO; // Обязательно для работы с файлами
using System.Reflection; // При получении релиза для передоса *.dll
using System.Linq;

namespace QRCoderZpkd1_Link
{
  //public partial class App : Application { } // По умолчанию
  //Для диагностики. почему не видно окно приложения
  /// <summary>
  /// Логика взаимодействия для App.xaml
  /// </summary>
  public partial class App : Application
  {
    protected override void OnStartup(StartupEventArgs e)
    {
      // 1. ЗАГРУЖАЕМ ПОЛЬЗОВАТЕЛЬСКИЕ НАСТРОЙКИ ПЕРЕД ОТРИСОВКОЙ ИНТЕРФЕЙСА
      SettingsManager.Load();

      // 2. Инициализируем локализацию
      if (!string.IsNullOrEmpty(SettingsManager.Current.Language))
      {
        // Если язык был ранее сохранен пользователем, применяем строго его
        LanguageManager.SwitchLanguage(SettingsManager.Current.Language);
      }
      else
      {
        // Если запуск первый (или язык не сохранен), подхватываем язык Windows
        LanguageManager.Initialize();
      }

      // Переносим все загруженные слова в глобальные ресурсы WPF
      UpdateLanguageResources();

      // 3. Применяем сохраненную тему (Светлую или Темную)
      ApplyGlobalTheme(SettingsManager.Current.Theme);

      try
      {
        base.OnStartup(e);
        // Если ты используешь StartupUri в XAML, WPF сам создаст MainWindow.
        // Если окно не появится, значит, падение происходит внутри конструктора MainWindow
        // или при загрузке стилей в App.xaml.
      }
      catch (Exception ex)
      {
        // Это заставит программу показать окно с ошибкой перед закрытием
        MessageBox.Show($"Критическая ошибка при запуске:\n{ex.Message}\n\n{ex.StackTrace}",
                        "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);

        // Также выведем в консоль для терминала
        Console.WriteLine(ex.ToString());

        Shutdown();
      }
    }
    public static void ApplyGlobalTheme(string themeName)
    {
      try
      {
        var mergedDictionaries = Current.Resources.MergedDictionaries;
        // Ищем словарь, который отвечает за темы (заканчивается на Theme.xaml)
        var themeDict = mergedDictionaries.FirstOrDefault(d =>
            d.Source != null && d.Source.ToString().EndsWith("Theme.xaml"));

        if (themeDict != null)
        {
          string uriPath = $"Styles/Themes/{themeName}.xaml";
          themeDict.Source = new Uri(uriPath, UriKind.Relative);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Не удалось сменить тему:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
    /// <summary>
    /// Публичный метод, который можно вызвать из любого окна для смены языка
    /// </summary>
    public static void ChangeLanguage(string languageName)
    {
      // Переключаем словарь в Core
      LanguageManager.SwitchLanguage(languageName);

      // Обновляем ресурсы в WPF
      UpdateLanguageResources();

      // Сохраняем выбранный язык в файле настроек
      SettingsManager.Current.Language = languageName;
      SettingsManager.Save();

      // Срочно обновляем выпадающий список в главном окне, чтобы плейсхолдер перевелся
      if (Current.MainWindow is MainWindow mainWin)
      {
        mainWin.LoadResolutions();
      }
    }

    /// <summary>
    /// Переносит все текущие переводы из Core в глобальные ресурсы WPF
    /// </summary>
    private static void UpdateLanguageResources()
    {
      foreach (var kvp in LanguageManager.Translations)
      {
        string resourceKey = $"Lang_{kvp.Key}";
        Current.Resources[resourceKey] = kvp.Value;
      }
    }

    /// <summary>
    /// Настройки для создания релизной сборки приложения
    /// <summary>
    public App()
    {
      // Подписываемся на событие поиска библиотек
      AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
      // Получаем имя сборки
      string assemblyName = new AssemblyName(args.Name).Name + ".dll";

      // Путь к папке Libs
      string libsFolder = Path.Combine(AppContext.BaseDirectory, "Libs");

      // Переменная называется assemblyPath
      string assemblyPath = Path.Combine(libsFolder, assemblyName);

      // Проверяем именно assemblyPath
      if (File.Exists(assemblyPath))
      {
        return Assembly.LoadFrom(assemblyPath);
      }
      return null;
    }
  }
}