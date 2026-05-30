
#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../../Frontend/PlantProcess.Web"

npm run build:storybook

sudo mkdir -p /opt/PlantProcess-IQ/Website/storybook
sudo rsync -a --delete storybook-static/ /opt/PlantProcess-IQ/Website/storybook/

echo "Storybook copied to /opt/PlantProcess-IQ/Website/storybook"
