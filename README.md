## Setup

1. To view current secrets on server: 
	Production: C:\Windows\System32\inetsrv\appcmd.exe list apppool "AutoCAC" /config
	Dev: dotnet user-secrets list --project "C:\Users\dgibrael\source\repos\AutoCAC\AutoCAC.csproj"
2. The RPMS verify code is a combination of the "RMPS" : "Verify" value from the secrets file + the "RPMSVerifySeed" from the "AppSettings" database table
