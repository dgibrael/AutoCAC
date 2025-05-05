using Renci.SshNet;
using Renci.SshNet.Async;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class FtpUploadService : IAsyncDisposable
{
    private SftpClient _sftpClient;

    private const string Host = "161.223.31.1";
    private const int Port = 22;

    public bool IsConnected => _sftpClient?.IsConnected == true;

    public async Task ConnectAsync(string username, char[] password)
    {
        string passwordString = new string(password);
        Array.Clear(password, 0, password.Length);

        _sftpClient = new SftpClient(Host, Port, username, passwordString);
        _sftpClient.Connect(); // still sync; no async Connect in SSH.NET yet
    }

    public async Task UploadFileAsync(Stream fileStream, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected.");

        await _sftpClient.UploadFileAsync(fileStream, remoteFilePath, FileMode.Create, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (_sftpClient != null)
        {
            if (_sftpClient.IsConnected)
                _sftpClient.Disconnect();

            _sftpClient.Dispose();
            _sftpClient = null;
        }

        return ValueTask.CompletedTask;
    }
}

// ✅ Add extension methods directly below
public static class SftpClientExtensions
{
    private const int BufferSize = 81920;

    public static async Task UploadFileAsync(this SftpClient sftpClient, Stream input, string path, FileMode createMode, CancellationToken cancellationToken = default)
    {
        await using Stream remoteStream = await sftpClient.OpenAsync(path, createMode, FileAccess.Write, cancellationToken).ConfigureAwait(false);
        await input.CopyToAsync(remoteStream, BufferSize, cancellationToken).ConfigureAwait(false);
    }
}

