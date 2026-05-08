using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TikTokUploadMethod;

public sealed class EncoderProgress
{
    public string Stage { get; init; } = "";
    public double Percent { get; init; }
}

public sealed class VideoEncoder
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly string _baseDir;

    public string FfmpegPath => _ffmpegPath;
    public string FfprobePath => _ffprobePath;

    public VideoEncoder(string baseDir)
    {
        _baseDir = baseDir;
        _ffmpegPath = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");
        _ffprobePath = Path.Combine(baseDir, "ffmpeg", "ffprobe.exe");
    }

    public bool ToolsExist()
    {
        return File.Exists(_ffmpegPath) && File.Exists(_ffprobePath);
    }

    public async Task<double> ProbeDurationSecondsAsync(string inputPath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{inputPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _baseDir,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        return 0;
    }

    public async Task<string> RunPipelineAsync(
        string inputPath,
        IProgress<EncoderProgress> progress,
        CancellationToken ct = default)
    {
        if (!ToolsExist())
        {
            throw new FileNotFoundException(
                $"ffmpeg.exe or ffprobe.exe not found.\nffmpeg: {_ffmpegPath}\nffprobe: {_ffprobePath}");
        }

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameNoExt = Path.GetFileNameWithoutExtension(inputPath);
        var finalOutput = Path.Combine(dir, $"{nameNoExt} Upload Method - Oncxzy.mp4");

        var totalSec = await ProbeDurationSecondsAsync(inputPath, ct);

        
        await RunFfmpegStage1Async(inputPath, finalOutput, totalSec, progress, ct);

        
        await RunElstPatchAsync(finalOutput, progress, ct);

        return finalOutput;
    }

    private async Task RunFfmpegStage1Async(
        string input, string output, double totalSec,
        IProgress<EncoderProgress> progress, CancellationToken ct)
    {
        
        
        
        var args = string.Join(" ", new[]
        {
            "-y -hide_banner -loglevel error -progress pipe:1 -nostats",
            $"-i \"{input}\"",
            "-map 0:v -map 0:a?",
            "-c:v libx264 -profile:v high -level 4.2 -pix_fmt yuv420p",
            "-crf 11 -preset slow",
            "-maxrate 50M -bufsize 100M",
            "-x264-params \"force-cfr=1\"",
            "-c:a aac -b:a 320k",
            "-movflags +faststart",
            "-metadata comment=\"Upload Method By Oncxzy On Tiktok\"",
            "-metadata performer=\"Oncxzy\"",
            "-metadata artist=\"Oncxzy\"",
            $"\"{output}\""
        });

        await RunFfmpegWithProgressAsync(args, totalSec, "Encoding Video", progress, ct);
    }

    private Task RunElstPatchAsync(
        string finalPath,
        IProgress<EncoderProgress> progress,
        CancellationToken ct)
    {
        progress.Report(new EncoderProgress { Stage = "Finalizing", Percent = 50 });

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var result = Mp4ElstPatcher.Patch(finalPath);

            try
            {
                var logPath = Path.Combine(_baseDir, "crash.log");
                var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.AppendAllText(logPath,
                    $"[{stamp}] PATCH: {(result.Success ? "OK" : "SKIP")} — {result.Message}\n");
            }
            catch { }

            progress.Report(new EncoderProgress { Stage = "Finalizing", Percent = 100 });
        }, ct);
    }

    private async Task RunFfmpegWithProgressAsync(
        string args, double totalSec, string stageLabel,
        IProgress<EncoderProgress> progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _baseDir,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var errBuf = new System.Text.StringBuilder();
        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) errBuf.AppendLine(e.Data);
        };

        try
        {
            if (!proc.Start())
                throw new InvalidOperationException("Could not start the encoder process.");
        }
        catch (System.ComponentModel.Win32Exception wex)
        {
            throw new InvalidOperationException(
                $"Couldn't launch ffmpeg.exe.\nPath: {_ffmpegPath}\nReason: {wex.Message}\n" +
                "Possible causes: antivirus blocked the file, or the ffmpeg.exe in your folder is corrupted.",
                wex);
        }

        proc.BeginErrorReadLine();
        progress.Report(new EncoderProgress { Stage = stageLabel, Percent = 0 });

        var outTimeRegex = new Regex(@"out_time_us=(\d+)", RegexOptions.Compiled);

        try
        {
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync(ct)) != null)
            {
                ct.ThrowIfCancellationRequested();

                var m = outTimeRegex.Match(line);
                if (m.Success && totalSec > 0)
                {
                    if (long.TryParse(m.Groups[1].Value, out var us))
                    {
                        var sec = us / 1_000_000.0;
                        var pct = Math.Min(100.0, (sec / totalSec) * 100.0);
                        progress.Report(new EncoderProgress { Stage = stageLabel, Percent = pct });
                    }
                }

                if (line.StartsWith("progress=end", StringComparison.OrdinalIgnoreCase))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch { }
            throw;
        }

        await proc.WaitForExitAsync(ct);
        progress.Report(new EncoderProgress { Stage = stageLabel, Percent = 100 });

        if (proc.ExitCode != 0)
        {
            var errMsg = errBuf.ToString().Trim();
            if (string.IsNullOrEmpty(errMsg)) errMsg = $"Encoder exited with code {proc.ExitCode}.";
            throw new InvalidOperationException(errMsg);
        }
    }
}
