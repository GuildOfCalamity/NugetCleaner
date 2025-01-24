using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;

namespace NugetCleaner.Support;

public static class EmbeddedHelper
{
    /// <summary>
    /// Loads an embedded PNG as a <see cref="BitmapImage"/>.
    /// </summary>
    /// <param name="resourceName">The resource name of the embedded PNG.</param>
    /// <returns><see cref="BitmapImage"/></returns>
    public static BitmapImage LoadEmbeddedImage(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Ensure the resource exists
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

            // Create a BitmapImage from the stream
            var bitmap = new BitmapImage();
            stream.Seek(0, SeekOrigin.Begin);
            bitmap.SetSourceAsync(stream.AsRandomAccessStream()).AsTask().Wait();
            return bitmap;
        }
    }

    /// <summary>
    /// You can load it as a WriteableBitmap instead, if you need more control over the image or want to modify it.
    /// </summary>
    /// <param name="resourceName"></param>
    /// <returns><see cref="WriteableBitmap"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static WriteableBitmap LoadEmbeddedImageAsWriteableBitmap(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

            var bitmap = new WriteableBitmap(1, 1);
            stream.Seek(0, SeekOrigin.Begin);
            bitmap.SetSourceAsync(stream.AsRandomAccessStream()).AsTask().Wait();
            return bitmap;
        }
    }

    /// <summary>
    /// Loads an embedded font into a FontFamily.
    /// </summary>
    /// <returns><see cref="FontFamily"/></returns>
    public static FontFamily LoadEmbeddedFontFamily(string ttfFontName, string name)
    {
        if (string.IsNullOrEmpty(ttfFontName))
            return new FontFamily("Segoe");

        try
        {
            //var _customFont = new FontFamily($"ms-appx:///Assets/Fonts/Hack.ttf#Hack");
            var _customFont = new FontFamily($"ms-appx:///Assets/Fonts/{ttfFontName}#{name}");
            return _customFont;
        }
        catch (Exception ex)
        {
            App.DebugLog($"LoadEmbeddedFontFamily: {ex.Message}");
        }
        return new FontFamily("Segoe");
    }

    /// <summary>
    /// Loads an embedded font into a FontFamily.
    /// </summary>
    /// <returns>The loaded font in bytes.</returns>
    public static byte[] LoadEmbeddedFontBytes(string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
            return new byte[0];

        try
        {
            // Get the assembly containing the font
            var assembly = Assembly.GetExecutingAssembly();

            // Find the embedded resource (replace the path with your actual namespace and folder)
            string resourceName = $"{App.GetCurrentNamespace()}.Assets.Fonts.{fontName}.ttf";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

                byte[] fontData = new byte[stream.Length];
                stream.Read(fontData, 0, fontData.Length);
                return fontData;
            }
        }
        catch (Exception ex)
        {
            App.DebugLog($"LoadEmbeddedFontBytes: {ex.Message}");
        }
        return new byte[0];
    }

    /// <summary>
    /// Dumps all embedded resource names to the debug console.
    /// </summary>
    public static void DumpManifestResourceNames()
    {
        var resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        foreach (var name in resourceNames)
        {
            System.Diagnostics.Debug.WriteLine($"[RESOURCE] '{name}'"); // NugetCleaner.Assets.Fonts.Hack.ttf
        }
    }
}
