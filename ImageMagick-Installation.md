# ImageMagick Installation Guide for ICO Conversion

## Installing ImageMagick on Different Platforms

### Ubuntu/Debian (WSL)
```bash
sudo apt update
sudo apt install imagemagick
```

### macOS
```bash
brew install imagemagick
```

### Windows
1. Download from: https://imagemagick.org/script/download.php#windows
2. Run the installer and ensure "Install legacy utilities (convert)" is checked
3. Add to PATH during installation

### Verify Installation
```bash
convert --version
# or
magick --version
```

## Creating Multi-Resolution ICO Files

### Basic Command
```bash
convert input.png -resize 256x256 -define icon:auto-resize=256,128,64,48,32,16 output.ico
```

### Command Breakdown
- `-resize 256x256`: Ensures the source image is at least 256x256
- `-define icon:auto-resize=256,128,64,48,32,16`: Creates multiple resolutions
- Common sizes: 256x256, 128x128, 64x64, 48x48, 32x32, 16x16

### Examples
```bash
# Convert company logo
convert company-logo.png -resize 256x256 -define icon:auto-resize=256,128,64,48,32,16 app-icon.ico

# Convert with specific sizes only
convert logo.png -define icon:auto-resize=48,32,16 small-icon.ico

# Convert maintaining aspect ratio
convert logo.png -resize 256x256 -background transparent -gravity center -extent 256x256 -define icon:auto-resize=256,128,64,48,32,16 icon.ico
```

## Troubleshooting

### "convert: command not found"
- On newer versions, use `magick` instead of `convert`
- Example: `magick input.png -resize 256x256 -define icon:auto-resize=256,128,64,48,32,16 output.ico`

### Permission Issues on WSL
```bash
sudo apt install --reinstall imagemagick
```

### Quality Issues
- Start with a high-resolution source image (at least 256x256)
- Use PNG format for best quality
- Avoid JPG as source due to compression artifacts