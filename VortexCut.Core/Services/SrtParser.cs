using System.Text;
using System.Text.RegularExpressions;

namespace VortexCut.Core.Services;

/// <summary>
/// SRT 자막 항목
/// </summary>
public record SubtitleEntry(int Index, long StartMs, long EndMs, string Text);

/// <summary>
/// SRT 자막 파일 파서/내보내기
/// </summary>
public static class SrtParser
{
    // SRT 시간 형식: HH:MM:SS,mmm
    private static readonly Regex TimePattern = new(
        @"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})",
        RegexOptions.Compiled);

    /// <summary>
    /// SRT 파일 파싱 → SubtitleEntry 리스트
    /// </summary>
    public static List<SubtitleEntry> Parse(string filePath)
    {
        var entries = new List<SubtitleEntry>();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        int i = 0;
        while (i < lines.Length)
        {
            // 빈 줄 스킵
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                i++;
                continue;
            }

            // 인덱스 번호 파싱
            if (!int.TryParse(lines[i].Trim(), out int index))
            {
                i++;
                continue;
            }
            i++;

            // 타임코드 라인
            if (i >= lines.Length) break;
            var timeMatch = TimePattern.Match(lines[i]);
            if (!timeMatch.Success)
            {
                i++;
                continue;
            }

            long startMs = ParseTimeMs(timeMatch, 1);
            long endMs = ParseTimeMs(timeMatch, 5);
            i++;

            // 텍스트 라인 (빈 줄까지)
            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(lines[i]);
                i++;
            }

            string text = string.Join("\n", textLines);
            entries.Add(new SubtitleEntry(index, startMs, endMs, text));
        }

        return entries;
    }

    /// <summary>
    /// SubtitleEntry 리스트 → SRT 파일 내보내기
    /// </summary>
    public static void Export(string filePath, List<SubtitleEntry> entries)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatTime(entry.StartMs)} --> {FormatTime(entry.EndMs)}");
            sb.AppendLine(entry.Text);
            sb.AppendLine(); // 빈 줄 구분
        }
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// SRT 타임코드 → ms 변환
    /// </summary>
    private static long ParseTimeMs(Match match, int groupOffset)
    {
        int hours = int.Parse(match.Groups[groupOffset].Value);
        int minutes = int.Parse(match.Groups[groupOffset + 1].Value);
        int seconds = int.Parse(match.Groups[groupOffset + 2].Value);
        int millis = int.Parse(match.Groups[groupOffset + 3].Value);
        return hours * 3600000L + minutes * 60000L + seconds * 1000L + millis;
    }

    /// <summary>
    /// ms → SRT 타임코드 형식 (HH:MM:SS,mmm)
    /// </summary>
    private static string FormatTime(long ms)
    {
        long totalSeconds = ms / 1000;
        int millis = (int)(ms % 1000);
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2},{millis:D3}";
    }
}
