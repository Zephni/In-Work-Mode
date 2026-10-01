# In Work Mode
For tracking time and notes for one or multiple workspaces.
<img width="600" height="ato" alt="image" src="https://github.com/user-attachments/assets/bd9dd3fa-94ec-4076-afe2-09673671f537" />

## Download
[https://raw.githubusercontent.com/Zephni/In-Work-Mode/main/In%20Work%20Mode.exe](https://raw.githubusercontent.com/Zephni/In-Work-Mode/main/In%20Work%20Mode.exe)

## Signing

`build.bat` always signs the executable. By default it uses the local development certificate installed by:

```powershell
.\sign.ps1 -InstallCertificate
```

That certificate validates locally but does not satisfy Windows Smart App Control. Distributable builds require a Public Trust profile from [Microsoft Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/quickstart) or another publicly trusted code-signing provider.

After creating an Artifact Signing account and Public Trust certificate profile, install the PowerShell integration and configure the build session:

```powershell
Install-PSResource -Name ArtifactSigning -Scope CurrentUser -TrustRepository

$env:ARTIFACT_SIGNING_ENDPOINT = 'https://<region>.codesigning.azure.net'
$env:ARTIFACT_SIGNING_ACCOUNT = '<account-name>'
$env:ARTIFACT_SIGNING_PROFILE = '<certificate-profile-name>'

.\build.bat
```

Authentication uses Azure `DefaultAzureCredential`. The signed executable is timestamped with Microsoft's Artifact Signing timestamp service.
