# Deploy.ps1

# Remote SSH info
$remoteUser = "zinmin"
$remoteHost = "ubuntu.local"
$remoteDir = "/home/zinmin/DockerUaServer_Deploy"
$dockerImageName = "docker-ua-server"
$dockerContainerName = "docker-ua-server"

# Required project folders relative to this script
$projectFolders = @(
    "DockerUaServer"
)

# Compose the remote temporary target directory
$remoteScpTarget = "${remoteUser}@${remoteHost}:${remoteDir}-temp"

Write-Host "==> Creating temporary directory on remote..."
ssh "$remoteUser@$remoteHost" "mkdir -p ${remoteDir}-temp"

Write-Host "==> Uploading required project folders..."
foreach ($folder in $projectFolders) {
    $fullLocalPath = Join-Path $PSScriptRoot $folder
    if (Test-Path $fullLocalPath) {
        Write-Host "Uploading $folder..."
        scp -r "$fullLocalPath" "$remoteScpTarget"
    } else {
        Write-Warning "Local folder not found: $fullLocalPath"
    }
}

Write-Host "==> Connecting to remote host to deploy..."

$remoteCommands = @"
set -e

echo '==> Fixing permissions on old deployment folder...'
chmod -R u+w $remoteDir || true

echo '==> Cleaning up old deployment folder...'
rm -rf $remoteDir
mv ${remoteDir}-temp $remoteDir

cd $remoteDir

echo '==> Stopping and removing old container if exists...'
docker stop $dockerContainerName || true
docker rm $dockerContainerName || true

echo '==> Building Docker image...'
docker build -f DockerUaServer/Dockerfile -t $dockerImageName .

# Run the container
docker run -d --name $dockerContainerName -p 7273:7273  -p 7274:7274 \
-v ~/opc-ua-pki:/app/pki \
-v $remoteDir/DockerUaServer/DockerUaServer.prod.config.xml:/app/DockerUaServer.config.xml $dockerImageName

echo '==> Deployment complete!'
"@

# Replace all CRLF (`\r\n`) with LF (`\n`)
$remoteCommands = $remoteCommands -replace "`r`n", "`n"

# If any isolated CR left, replace them as well
$remoteCommands = $remoteCommands -replace "`r", ""

ssh "$remoteUser@$remoteHost" $remoteCommands

Write-Host "Deployment to $remoteHost complete."
