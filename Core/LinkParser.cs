using System;
using System.Text.RegularExpressions;
using System.Globalization;

namespace QRCoderZpkd1_Link.Core
{
  /// <summary>
  /// Логический класс-помощник для парсинга ссылки на файл .zpk.
  /// Обрабатывает извлечение имени, сложных версий (в т.ч. внутри имени перед суффиксами устройств) и очистку.
  /// </summary>
  public static class LinkParser
  {
    public class ParseResult
    {
      public string Name { get; set; } = string.Empty;
      public string Version { get; set; } = string.Empty;
    }

    public static ParseResult Parse(string urlOrPath)
    {
      var result = new ParseResult();

      if (string.IsNullOrWhiteSpace(urlOrPath))
        return result;

      try
      {
        // 1. Извлекаем имя файла из URI или пути
        Uri uri;
        string fileName = string.Empty;

        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out uri))
        {
          fileName = System.IO.Path.GetFileName(uri.LocalPath);
        }
        else
        {
          fileName = System.IO.Path.GetFileName(urlOrPath);
        }

        if (string.IsNullOrEmpty(fileName))
          return result;

        // 2. Декодируем символы (%20 -> пробелы)
        fileName = Uri.UnescapeDataString(fileName);

        // 3. Удаляем расширение .zpk
        if (fileName.EndsWith(".zpk", StringComparison.OrdinalIgnoreCase))
        {
          fileName = fileName.Substring(0, fileName.Length - 4);
        }

        // 4. Ищем версию, которая может быть как в конце, так и перед суффиксом устройства (например, _V2_02_Bip6)
        // Ищем шаблон: [vV] + цифры, разделенные подчеркиваниями или точками (например, V2_02 или v1.0)
        var complexVersionRegex = new Regex(@"[vV](\d+(?:[_\.]\d+)*)", RegexOptions.Compiled);
        var match = complexVersionRegex.Match(fileName);

        if (match.Success)
        {
          string rawVersionPart = match.Groups[1].Value;

          // Превращаем разделители вроде "2_02" в стандартный вид версии "2.0.2"
          string formattedVersion = rawVersionPart.Replace('_', '.');

          if (double.TryParse(formattedVersion.Replace(".", NumberFormatInfo.CurrentInfo.NumberDecimalSeparator),
              NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal) && parsedVal > 0)
          {
            result.Version = formattedVersion;

            // Отрезаем всё, начиная с найденной версии, чтобы имя файла стало чистым 
            // (например, из "AS023_WeatherAnimation_V2_02_Bip6" останется "AS023_WeatherAnimation")
            int versionIndex = match.Index;
            fileName = fileName.Substring(0, versionIndex);

            // Удаляем хвостовые тире, длинные тире и подчеркивания
            fileName = fileName.TrimEnd('-', '—', '_', ' ');
          }
        }

        result.Name = fileName.Trim();
      }
      catch (Exception)
      {
        // При сбое возвращаем пустые значения
      }

      return result;
    }
    /// <summary>
    /// Декодирует URL-адрес, превращая %20 в пробелы и т.д.
    /// </summary>
    public static string DecodeUrl(string input)
    {
      if (string.IsNullOrEmpty(input))
        return string.Empty;

      return System.Net.WebUtility.UrlDecode(input);
    }

    /// <summary>
    /// Преобразует ссылку на файл в репозитории GitHub в прямую ссылку GitHub Pages.
    /// </summary>
    public static string ConvertGitHubBlobToPagesUrl(string url)
    {
      if (string.IsNullOrEmpty(url))
        return url;

      try
      {
        Uri uri = new Uri(url);
        // Разбиваем путь на сегменты и удаляем пустые
        string[] segments = uri.AbsolutePath.Trim('/').Split('/');

        // Проверяем, что это структура вида: /UserName/RepoName/blob/main/...
        if (segments.Length >= 5 && segments[2] == "blob" && segments[3] == "main")
        {
          string user = segments[0];
          string repo = segments[1];
          // Собираем оставшийся путь к файлу
          string path = string.Join("/", segments.Skip(4));

          // Если репозиторий является корневым сайтом (UserName.github.io)
          if (repo.Equals($"{user}.github.io", StringComparison.OrdinalIgnoreCase))
          {
            return $"https://{user}.github.io/{path}";
          }
          else
          {
            // Если это обычный репозиторий
            return $"https://{user}.github.io/{repo}/{path}";
          }
        }
      }
      catch
      {
        // При любой ошибке парсинга просто возвращаем исходную ссылку
      }

      return url;
    }

    /// <summary>
    /// Корректирует ссылку, заменяя http/https на внутренний протокол приложения zpkd1://
    /// </summary>
    public static string CorrectUrl(string input)
    {
      if (string.IsNullOrEmpty(input))
        return input;

      // Если ссылка уже начинается с http или https — меняем протокол
      if (input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
          input.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
      {
        return input.Replace("https://", "zpkd1://").Replace("http://", "zpkd1://");
      }

      // Если протокол вообще не указан, подставляем его принудительно
      if (!input.StartsWith("zpkd1://", StringComparison.OrdinalIgnoreCase))
      {
        return "zpkd1://" + input;
      }

      return input;
    }

    /// <summary>
    /// Асинхронно проверяет, доступен ли файл по указанной ссылке (возвращает ли сервер код 200 OK).
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> UrlFileExistsAsync(string url)
    {
      // Для проверки по сети нам нужно использовать http/https, а не локальный протокол
      url = url.Replace("zpkd1://", "http://");

      try
      {
        using (var client = new System.Net.Http.HttpClient())
        {
          // Устанавливаем таймаут ожидания ответа в 5 секунд
          client.Timeout = TimeSpan.FromSeconds(5);

          // Отправляем HEAD запрос (скачиваем только заголовки, без самого файла) для экономии трафика
          using (var response = await client.SendAsync(
              new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url)))
          {
            return response.IsSuccessStatusCode;
          }
        }
      }
      catch
      {
        // Если произошла ошибка (нет интернета, таймаут, неверный домен) - считаем, что файл недоступен
        return false;
      }
    }
  }
}