param(
    [string]$Env = "dev"
)

# Seleccionar el certificado en función del entorno
switch ($Env.ToLower()) {
    "dev" { $certPath = Join-Path $PSScriptRoot "dev.cer"; break }
    "pre" { $certPath = Join-Path $PSScriptRoot "pre.cer"; break }
	"pro" { $certPath = Join-Path $PSScriptRoot "pro.cer"; break }
    default {
         Write-Host "Entorno desconocido: $Env"
         exit 1
    }
}

# Cargar el certificado para obtener su Thumbprint
try {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath)
} catch {
    Write-Host "No se pudo cargar el certificado desde $certPath"
    exit 1
}
$thumbprint = $cert.Thumbprint

# Función para comprobar y, si es necesario, instalar el certificado en un almacén
function Install-CertificateIfNotExists {
    param(
        [string]$StoreName,
        [string]$StoreLocation,
        [string]$CertPath,
        [string]$Thumbprint
    )
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($StoreName, $StoreLocation)
    try {
        $store.Open("ReadOnly")
    } catch {
        Write-Host "No se pudo abrir el almacén $StoreName en $StoreLocation"
        return
    }
    $found = $store.Certificates | Where-Object { $_.Thumbprint -eq $Thumbprint }
    $store.Close()
    if ($found) {
         Write-Host "El certificado ya está instalado en el almacén $StoreName."
    } else {
         Write-Host "Instalando certificado en el almacén $StoreName..."
         certutil.exe -addstore $StoreName "$CertPath"
    }
}

# Comprobar e instalar en Root y My
Install-CertificateIfNotExists -StoreName "Root" -StoreLocation "LocalMachine" -CertPath $certPath -Thumbprint $thumbprint
Install-CertificateIfNotExists -StoreName "My" -StoreLocation "LocalMachine" -CertPath $certPath -Thumbprint $thumbprint

Write-Host "Proceso de instalación de certificados completado."
