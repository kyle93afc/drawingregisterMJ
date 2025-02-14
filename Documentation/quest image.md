Here's how to embed an image in a PDF using QuestPDF with solutions for common IDE/cursor issues:

Basic Image Embedding
Use QuestPDF's .Image() method with either a file path or byte array:

csharp
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(20);
        
        page.Content()
            .Image("path/to/your-image.png") // File path
            .FitWidth(); // Scaling option
    });
})
.GeneratePdf("output.pdf");
Common Solutions for IDE/Cursor Issues
Missing IntelliSense
Ensure you have:

bash
Install-Package QuestPDF
Install-Package SkiaSharp.NativeAssets.Linux # For Linux/macOS
Restart your IDE after installation.

Image Scaling Options
Choose appropriate scaling:

csharp
.Image("image.jpg")
  .FitArea()    // Maintain aspect ratio within available space
  .FitWidth()   // Match container width
  .FitHeight()  // Match container height
  .Stretch()    // Ignore aspect ratio
Byte Array Approach
For dynamic images:

csharp
byte[] imageData = File.ReadAllBytes("image.png");
page.Content().Image(imageData);
Advanced Configuration
Issue	Solution
Blurry images	Use .WithRasterDpi(300) for higher resolution
Compression artifacts	Add .WithCompressionQuality(ImageCompressionQuality.Best)
Missing transparency	Ensure PNG format with alpha channel
Linux Docker issues	Include SkiaSharp.NativeAssets.Linux package8
Troubleshooting Checklist
Verify file path exists and is accessible

Check image format support (PNG/JPEG recommended)

Test with absolute paths first:
@"C:\Projects\image.jpg" (Windows)
@"/home/user/image.png" (Linux/macOS)

Use try-catch blocks for error handling:

csharp
try
{
    document.GeneratePdf("output.pdf");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
For complex layouts, combine with QuestPDF's Layers API to position images behind/above text content15. If issues persist, convert images to PDF-compatible formats first using SkiaSharp411.