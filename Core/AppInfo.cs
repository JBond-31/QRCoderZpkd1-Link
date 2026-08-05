using System.Reflection;

namespace QRCoderZpkd1_Link.Core
{
  public static class AppInfo
  {
    public static string GetDisplayVersion()
    {
      // Берем текущую сборку (наш exe)
      var assembly = Assembly.GetEntryAssembly();

      // Получаем атрибут InformationalVersion, который мы задали в .csproj
      var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

      return infoVersion ?? "v.unknown";
    }
  }
}