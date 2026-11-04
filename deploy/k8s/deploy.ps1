param(
    [Parameter(Mandatory = $true)]
    [string]$DbPassword
)

$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $root

try {
    Write-Host "Construyendo imagen de la base de datos..."
    docker build -t primevideo-db:latest .\deploy\db 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "No se pudo construir la imagen primevideo-db." }

    Write-Host "Construyendo imagen de la aplicacion..."
    docker build -t primevideo-app:latest . 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "No se pudo construir la imagen primevideo-app." }

    Write-Host "Creando namespace y secreto..."
    kubectl create namespace primevideo --dry-run=client -o yaml | kubectl apply -f -
    if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el namespace." }

    kubectl -n primevideo create secret generic primevideo-db `
        --from-literal=POSTGRES_DB=PrimeVideoDB `
        --from-literal=POSTGRES_USER=postgres `
        --from-literal=POSTGRES_PASSWORD=$DbPassword `
        --dry-run=client -o yaml | kubectl apply -f -
    if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el secreto." }

    Write-Host "Aplicando manifiestos..."
    kubectl apply -f .\deploy\k8s\primevideo.yaml
    if ($LASTEXITCODE -ne 0) { throw "No se pudieron aplicar los manifiestos." }

    kubectl -n primevideo rollout status deployment/primevideo-db --timeout=240s
    if ($LASTEXITCODE -ne 0) { throw "La base de datos no inicio correctamente." }

    kubectl -n primevideo rollout status deployment/primevideo-app --timeout=240s
    if ($LASTEXITCODE -ne 0) { throw "La aplicacion no inicio correctamente." }

    Write-Host ""
    Write-Host "Aplicacion disponible en http://localhost:30080"
    kubectl -n primevideo get pods,svc
}
finally {
    Pop-Location
}
