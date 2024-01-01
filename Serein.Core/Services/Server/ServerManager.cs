using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

using Microsoft.Extensions.Logging;

using Serein.Core.Models;
using Serein.Core.Models.Commands;
using Serein.Core.Models.Exceptions;
using Serein.Core.Models.Server;
using Serein.Core.Services.Data;
using Serein.Core.Utils;

namespace Serein.Core.Services.Server;

public class ServerManager
{
    public ServerStatus Status =>
        _serverProcess is null
            ? ServerStatus.Unknown
            : _serverProcess.HasExited
                ? ServerStatus.Stopped
                : ServerStatus.Running;
    public int? Pid => _serverProcess?.Id;
    public IServerInfo? ServerInfo => _serverInfo;

    public IReadOnlyList<string> CommandHistory => _commandHistory;
    public int CommandHistoryIndex { get; private set; }
    private readonly List<string> _commandHistory = new();

    private readonly Timer _updateTimer;
    private BinaryWriter? _inputWriter;
    private Process? _serverProcess;
    private RestartStatus _restartStatus;
    private ServerInfo? _serverInfo;
    private TimeSpan _prevProcessCpuTime = TimeSpan.Zero;
    private bool _isTerminated;
    private readonly IOutputHandler _logger;
    private readonly Matcher _matcher;
    private readonly SettingProvider _settingProvider;

    public ServerManager(IOutputHandler output, SettingProvider settingManager, Matcher matcher)
    {
        _settingProvider = settingManager;
        _logger = output;
        _matcher = matcher;
        _updateTimer = new(2000) { AutoReset = true };
        _updateTimer.Elapsed += (_, _) => UpdateInfo();
        _updateTimer.Start();
    }

    public void Start()
    {
        if (Status == ServerStatus.Running)
            throw new ServerException("服务器已在运行");

        if (string.IsNullOrEmpty(_settingProvider.Value.Server.FileName))
            throw new ServerException("启动文件为空");

        _serverProcess = Process.Start(
            new ProcessStartInfo
            {
                FileName = _settingProvider.Value.Server.FileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = EncodingMap.GetEncoding(
                    _settingProvider.Value.Server.OutputEncoding
                ),
                StandardErrorEncoding = EncodingMap.GetEncoding(
                    _settingProvider.Value.Server.OutputEncoding
                ),
                WorkingDirectory = Path.GetDirectoryName(_settingProvider.Value.Server.FileName),
                Arguments = _settingProvider.Value.Server.Argument ?? string.Empty
            }
        );
        _serverProcess!.EnableRaisingEvents = true;
        _isTerminated = true;
        _restartStatus = RestartStatus.None;
        _serverInfo = new() { StartTime = _serverProcess.StartTime };
        _commandHistory.Clear();
        _prevProcessCpuTime = TimeSpan.Zero;

        _inputWriter = new(_serverProcess.StandardInput.BaseStream);

        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();
        _serverProcess.OutputDataReceived += OnOutputDataReceived;
        _serverProcess.ErrorDataReceived += OnOutputDataReceived;
        _serverProcess.Exited += OnExit;

        _logger.LogServerNotice($"“{_settingProvider.Value.Server.FileName}”启动中");
    }

    public void Stop()
    {
        if (Status != ServerStatus.Running)
            throw new ServerException("服务器未运行");

        foreach (string command in _settingProvider.Value.Server.StopCommands)
        {
            if (!string.IsNullOrEmpty(command))
            {
                Input(command);
            }
        }
    }

    public void InputFromCommand(string command, EncodingMap.EncodingType? encodingType = null)
    {
        if (command == "start")
            Start();
        else if (command == "stop")
            _restartStatus = RestartStatus.None;
        else
            Input(command, encodingType);
    }

    public void Input(
        string command,
        EncodingMap.EncodingType? encodingType = null,
        bool fromUser = false
    )
    {
        if (_inputWriter is null || Status != ServerStatus.Running)
            return;

        _inputWriter.Write(
            EncodingMap
                .GetEncoding(encodingType ?? _settingProvider.Value.Server.InputEncoding)
                .GetBytes(
                    command
                        + _settingProvider.Value.Server.LineTerminator
                            .Replace("\\n", "\n")
                            .Replace("\\r", "\r")
                )
        );
        _inputWriter.Flush();

        _serverInfo ??= new();
        _serverInfo.InputLines++;

        if (
            (
                _commandHistory.Count > 0 && _commandHistory[^1] != command
                || _commandHistory.Count == 0
            )
            && fromUser
            && !string.IsNullOrEmpty(command)
        )
            _commandHistory.Add(command);

        CommandHistoryIndex = CommandHistory.Count;

        _matcher.MatchServerInputAsync(command);
    }

    public void Terminate()
    {
        if (Status != ServerStatus.Running)
            throw new ServerException("服务器未运行");

        _serverProcess?.Kill(true);
        _isTerminated = true;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        var exitCode = _serverProcess?.ExitCode;
        _serverProcess = null;
        _logger.LogServerNotice($"进程已退出，退出代码为 {exitCode} (0x{exitCode:x8})");

        if (_settingProvider.Value.Server.AutoRestart && !_isTerminated)
            if (
                _restartStatus == RestartStatus.Waiting
                || _restartStatus == RestartStatus.None && exitCode != 0
            )
                _ = WaitAndRestartAsync();
    }

    private void OnOutputDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
            return;

        _serverInfo ??= new();
        _serverInfo.OutputLines++;

        _logger.LogServerOriginalOutput(e.Data);
        _matcher.MatchServerOutputAsync(OutputFilter.Clear(e.Data));
    }

    private async Task WaitAndRestartAsync()
    {
        var i = 0;

        _restartStatus = RestartStatus.Preparing;
        _logger.LogServerNotice($"将在五秒后({DateTime.Now.AddSeconds(5):T})重启服务器");
        while (i < 50 && _restartStatus == RestartStatus.Preparing)
        {
            await Task.Delay(100);
            i++;
        }

        try
        {
            Start();
        }
        catch (Exception e)
        {
            _logger.LogWarning("重启失败：{}", e.Message);
        }
    }

    private void UpdateInfo()
    {
        _serverInfo ??= new();

        if (Status != ServerStatus.Running || _serverProcess is null)
        {
            _serverInfo.Argument = null;
            _serverInfo.FileName = null;
            _serverInfo.ExitTime = null;
            _serverInfo.StartTime = null;
            _serverInfo.Motd = null;
            _serverInfo.OutputLines = 0;
            _serverInfo.InputLines = 0;
            _serverInfo.CPUUsage = 0;
            return;
        }

        _serverInfo.CPUUsage =
            (_serverProcess.TotalProcessorTime - _prevProcessCpuTime).TotalMilliseconds
            / 2000
            / Environment.ProcessorCount
            * 100;
        _prevProcessCpuTime = _serverProcess.TotalProcessorTime;
    }
}