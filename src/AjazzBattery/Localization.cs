using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AjazzBattery;

/// <summary>Код языка и подпись для меню выбора.</summary>
internal sealed record LanguageOption(string Code, string Label);

/// <summary>
/// Локализация интерфейса. Строки лежат в JSON-ресурсах Strings\strings.xx.json
/// и встраиваются в сборку, поэтому приложению не нужны внешние файлы.
/// Loc.Language = "auto" — брать язык системы.
/// </summary>
internal static class Loc
{
    public const string Auto = "auto";
    public const string Default = "en";

    private static readonly string[] Supported = { "ru", "en", "de", "es", "fr", "pt", "zh", "ja" };

    /// <summary>Языки, доступные в меню настроек: код + подпись.</summary>
    public static readonly IReadOnlyList<LanguageOption> Languages = new LanguageOption[]
    {
        new(Auto, "Авто (системный)"),
        new("ru", "Русский"),
        new("en", "English"),
        new("de", "Deutsch"),
        new("es", "Español"),
        new("fr", "Français"),
        new("pt", "Português"),
        new("zh", "中文"),
        new("ja", "日本語"),
    };

    private static readonly object Sync = new();
    private static readonly Dictionary<string, Dictionary<string, string>?> Bundles = new(StringComparer.OrdinalIgnoreCase);
    private static string _language = Auto;

    /// <summary>Выбранный язык: "auto" или код из Languages.</summary>
    public static string Language
    {
        get => _language;
        set => _language = Normalize(value);
    }

    /// <summary>Язык, который реально используется сейчас ("auto" уже развёрнут в код).</summary>
    public static string EffectiveLanguage => Resolve(_language);

    /// <summary>Приводит код языка к допустимому значению; всё неизвестное — "auto".</summary>
    public static string Normalize(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0)
        {
            return Auto;
        }

        if (value == Auto)
        {
            return Auto;
        }

        return Supported.Contains(value, StringComparer.Ordinal) ? value : Auto;
    }

    /// <summary>Строка по ключу.</summary>
    public static string T(string key) => T(key, Array.Empty<object?>());

    /// <summary>Строка по ключу с подстановкой {0}, {1}, …</summary>
    public static string T(string key, params object?[] args)
    {
        var text = Find(key);
        if (text is null)
        {
            return args.Length == 0 ? key : Format(key, args);
        }

        return args.Length == 0 ? text : Format(text, args);
    }

    private static string Format(string template, object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private static string Resolve(string code)
    {
        if (!string.Equals(code, Auto, StringComparison.OrdinalIgnoreCase))
        {
            return code;
        }

        var culture = CultureInfo.CurrentUICulture;
        var twoLetter = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        if (Supported.Contains(twoLetter, StringComparer.Ordinal))
        {
            return twoLetter;
        }

        // китайский и японский иногда приходят как zh-Hans / zh-CN / ja-JP — код уже двухбуквенный
        var name = culture.Name.ToLowerInvariant();
        foreach (var supported in Supported)
        {
            if (name.StartsWith(supported + "-", StringComparison.Ordinal))
            {
                return supported;
            }
        }

        return Default;
    }

    /// <summary>Ключ: выбранный язык → en → null (тогда вернём сам ключ).</summary>
    private static string? Find(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var language = Resolve(_language);
        var bundle = GetBundle(language);
        if (bundle is not null && bundle.TryGetValue(key, out var value))
        {
            return value;
        }

        if (!string.Equals(language, Default, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = GetBundle(Default);
            if (fallback is not null && fallback.TryGetValue(key, out var english))
            {
                return english;
            }
        }

        return null;
    }

    private static Dictionary<string, string>? GetBundle(string code)
    {
        lock (Sync)
        {
            if (Bundles.TryGetValue(code, out var cached))
            {
                return cached;
            }

            var loaded = Load(code);
            Bundles[code] = loaded;
            return loaded;
        }
    }

    private static Dictionary<string, string>? Load(string code)
    {
        try
        {
            using var stream = OpenResource(code);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
            return parsed is null ? null : new Dictionary<string, string>(parsed, StringComparer.Ordinal);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ресурс называется "AjazzBattery.Strings.strings.xx.json" (главный проект) или
    /// "&lt;RootNamespace&gt;.Strings.strings.xx.json" (установщик, линкует те же файлы),
    /// поэтому сначала ищем точное имя, потом — по суффиксу.
    /// </summary>
    private static Stream? OpenResource(string code)
    {
        var assembly = typeof(Loc).Assembly;
        var suffix = $".strings.{code}.json";

        var direct = assembly.GetManifestResourceStream($"AjazzBattery.Strings{suffix}");
        if (direct is not null)
        {
            return direct;
        }

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                name.Equals($"strings.{code}.json", StringComparison.OrdinalIgnoreCase))
            {
                return assembly.GetManifestResourceStream(name);
            }
        }

        return null;
    }
}
