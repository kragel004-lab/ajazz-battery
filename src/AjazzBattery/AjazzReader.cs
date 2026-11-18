using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace AjazzBattery;

public sealed class BatteryReading
{
    public bool Found { get; init; }
    public int Percent { get; init; }
    public string Product { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public byte[] Raw { get; init; } = Array.Empty<byte>();
    public string Error { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public static BatteryReading NotConnected(string error) => new() { Found = false, Error = error };
}

/// <summary>
/// Протокол AJAZZ AJ159 APEX (приёмник/док-станция 2.4G 8K, VID 0x3151 / PID 0x5007).
///
/// Транспорт — HID Feature Report, report id 0, пакет 64 байта (+ байт report id).
///   запрос статуса: [F7 00 ... 00]
///   ответ:          [00 00 <заряд %> <неизв.> <таблица кнопок/DPI/RGB ...>]
///   команда времени дока: [28 00 00 00 00 00 00 D7 <год hi> <год lo> <мес> <день> <час> <мин> <сек> ...]
///   очистка экрана дока:  [AC 00 00 00 00 00 00 53 ...]
/// </summary>
public static class AjazzReader
{
    public const ushort VendorId = 0x3151;
    public const ushort ProductId = 0x5007;
    public const ushort VendorUsagePage = 0xFFFF;
    public const int ReportSize = 65; // 1 байт report id + 64 байта данных

    private const byte CmdStatus = 0xF7;
    private const byte CmdSetTime = 0x28;
    private const byte CmdClearScreen = 0xAC;

    public static BatteryReading Read()
    {
        var candidates = FindCandidates();
        if (candidates.Count == 0)
        {
            return BatteryReading.NotConnected(Loc.T("error.receiverNotFound"));
        }

        string lastError = Loc.T("error.noAnswer");
        foreach (var candidate in candidates)
        {
            try
            {
                using var handle = HidInterop.Open(candidate.Path, writeAccess: true);
                if (handle.IsInvalid)
                {
                    lastError = Loc.T("error.deviceBusy");
                    continue;
                }

                if (TryQueryStatus(handle, out var raw))
                {
                    return new BatteryReading
                    {
                        Found = true,
                        Percent = raw[3],
                        Product = candidate.Product,
                        Path = candidate.Path,
                        Raw = raw,
                    };
                }

                lastError = Loc.T("error.noBatteryReply");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return BatteryReading.NotConnected(lastError);
    }

    /// <summary>Синхронизация часов на экране док-станции.</summary>
    public static bool SyncClock(DateTime? moment = null)
    {
        var now = moment ?? DateTime.Now;
        var packet = new byte[ReportSize];
        packet[0] = 0x00;       // report id
        packet[1] = CmdSetTime;
        packet[8] = (byte)(0xFF - CmdSetTime);
        packet[9] = (byte)(now.Year >> 8);
        packet[10] = (byte)(now.Year & 0xFF);
        packet[11] = (byte)now.Month;
        packet[12] = (byte)now.Day;
        packet[13] = (byte)now.Hour;
        packet[14] = (byte)now.Minute;
        packet[15] = (byte)now.Second;
        return SendToDevice(packet);
    }

    /// <summary>Убрать картинку с экрана док-станции (вернётся стандартный вид с часами).</summary>
    public static bool ClearDockScreen()
    {
        var packet = new byte[ReportSize];
        packet[0] = 0x00;
        packet[1] = CmdClearScreen;
        packet[8] = 0x53;
        return SendToDevice(packet);
    }

    private static bool SendToDevice(byte[] packet)
    {
        foreach (var candidate in FindCandidates())
        {
            try
            {
                using var handle = HidInterop.Open(candidate.Path, writeAccess: true);
                if (handle.IsInvalid)
                {
                    continue;
                }

                return HidInterop.SetFeature(handle, packet);
            }
            catch
            {
                // пробуем следующий интерфейс
            }
        }

        return false;
    }

    private static bool TryQueryStatus(SafeFileHandle handle, out byte[] raw)
    {
        raw = Array.Empty<byte>();
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var request = new byte[ReportSize];
            request[0] = 0x00;
            request[1] = CmdStatus;

            if (!HidInterop.SetFeature(handle, request))
            {
                Thread.Sleep(20);
                continue;
            }

            var response = new byte[ReportSize];
            response[0] = 0x00;
            if (!HidInterop.GetFeature(handle, response))
            {
                Thread.Sleep(20);
                continue;
            }

            if (response[1] == 0x00 && response[2] == 0x00 && response[3] <= 100)
            {
                raw = response;
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }

    private sealed record Candidate(string Path, string Product);

    private static List<Candidate> FindCandidates()
    {
        var result = new List<Candidate>();
        foreach (var path in HidInterop.EnumerateDevicePaths())
        {
            SafeFileHandle? handle = null;
            try
            {
                handle = HidInterop.Open(path, writeAccess: false);
                if (handle.IsInvalid)
                {
                    continue;
                }

                if (!HidInterop.TryGetAttributes(handle, out var vid, out _))
                {
                    continue;
                }

                var product = HidInterop.GetProductString(handle);
                if (vid != VendorId && !product.Contains("AJAZZ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!HidInterop.TryGetCaps(handle, out var usagePage, out _, out var featureLength))
                {
                    continue;
                }

                if (usagePage == VendorUsagePage && featureLength >= ReportSize)
                {
                    result.Add(new Candidate(path, product));
                }
            }
            catch
            {
                // игнорируем интерфейсы, которые не удалось открыть
            }
            finally
            {
                handle?.Dispose();
            }
        }

        return result;
    }

    /// <summary>Диагностика: все найденные вендорские интерфейсы и сырые ответы.</summary>
    public static string Diagnose()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"AJAZZ Battery {typeof(AjazzReader).Assembly.GetName().Version} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine(Loc.T("diag.os", Environment.OSVersion, Environment.Version));
        report.AppendLine();

        var candidates = FindCandidates();
        report.AppendLine(Loc.T("diag.candidates", candidates.Count));
        foreach (var candidate in candidates)
        {
            report.AppendLine(Loc.T("diag.candidate", candidate.Product, candidate.Path));
        }

        report.AppendLine();
        foreach (var candidate in candidates)
        {
            try
            {
                using var handle = HidInterop.Open(candidate.Path, writeAccess: true);
                if (handle.IsInvalid)
                {
                    report.AppendLine(Loc.T("diag.openFailed", candidate.Path));
                    continue;
                }

                var sw = Stopwatch.StartNew();
                bool ok = TryQueryStatus(handle, out var raw);
                sw.Stop();
                report.AppendLine(Loc.T("diag.reply", candidate.Product, ok, sw.ElapsedMilliseconds));
                if (ok)
                {
                    report.AppendLine("  " + string.Join(' ', raw.Select(b => b.ToString("x2"))));
                    report.AppendLine(Loc.T("diag.charge", raw[3]));
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"{candidate.Path}: {ex.Message}");
            }
        }

        return report.ToString();
    }
}
