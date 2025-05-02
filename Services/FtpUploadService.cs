using Renci.SshNet;
using System;
using System.IO;

public class FtpUploadService : IDisposable
{
    private SftpClient _sftpClient;

    private const string Host = "161.223.31.1";
    private const int Port = 22;

    public bool IsConnected => _sftpClient?.IsConnected == true;

    public void Connect(string username, string password)
    {
        _sftpClient = new SftpClient(Host, Port, username, password);
        _sftpClient.Connect();
    }

    public void UploadFile(Stream fileStream, string remoteFilePath)
    {
        if (!_sftpClient?.IsConnected ?? true)
            throw new InvalidOperationException("SFTP client is not connected.");

        _sftpClient.UploadFile(fileStream, remoteFilePath, true);
    }

    public void Dispose()
    {
        if (_sftpClient != null)
        {
            if (_sftpClient.IsConnected)
                _sftpClient.Disconnect();

            _sftpClient.Dispose();
            _sftpClient = null;
        }
    }
}

