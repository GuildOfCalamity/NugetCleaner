using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

using Windows.Storage.Streams;

namespace NugetCleaner.Support
{
    public class ScreenCapture
    {
        /*  --[ EXAMPLE USAGE ]--
        
            DispatcherTimer? tmr;

            // Create a timer for capturing animations during page load.
            tmr = new DispatcherTimer();
            tmr.Interval = TimeSpan.FromMilliseconds(100);
            tmr.Tick += tmrOnTick;
        
            async void tmrOnTick(object? sender, object e) => await UpdateScreenshot(rootGrid, null);
        */

        static int _counter = 0; // for file naming

        #region [Screen Capture Routines]
        /// <summary>
        /// I'm using this as a sort of "poor man's screen capture".
        /// Apply a page's visual state to an <see cref="Microsoft.UI.Xaml.Controls.Image"/> source. 
        /// If the target is null then the image is saved to disk.
        /// </summary>
        /// <param name="root">host <see cref="Microsoft.UI.Xaml.UIElement"/>. Can be a grid, a page, etc.</param>
        /// <param name="target">optional <see cref="Microsoft.UI.Xaml.Controls.Image"/> target</param>
        /// <remarks>
        /// Using a RenderTargetBitmap, you can accomplish scenarios such as applying image effects to a visual that 
        /// originally came from a XAML UI composition, generating thumbnail images of child pages for a navigation 
        /// system, or enabling the user to save parts of the UI as an image source and then share that image with 
        /// other applications. 
        /// Because RenderTargetBitmap is a subclass of <see cref="Microsoft.UI.Xaml.Media.ImageSource"/>, 
        /// it can be used as the image source for <see cref="Microsoft.UI.Xaml.Controls.Image"/> elements or an 
        /// <see cref="Microsoft.UI.Xaml.Media.ImageBrush"/> brush. 
        /// Calling RenderAsync() provides a useful image source but the full buffer representation of rendering 
        /// content is not copied out of video memory until the app calls GetPixelsAsync().
        /// It is faster to call RenderAsync() only, without calling GetPixelsAsync, and use the RenderTargetBitmap as an 
        /// <see cref="Microsoft.UI.Xaml.Controls.Image"/> or <see cref="Microsoft.UI.Xaml.Media.ImageBrush"/> 
        /// source if the app only intends to display the rendered content and does not need the pixel data. 
        /// [Stipulations]
        ///  - Content that's in the tree but with its Visibility set to Collapsed won't be captured.
        ///  - Content that's not directly connected to the XAML visual tree and the content of the main window won't be captured. This includes Popup content, which is considered to be like a sub-window.
        ///  - Content that can't be captured will appear as blank in the captured image, but other content in the same visual tree can still be captured and will render (the presence of content that can't be captured won't invalidate the entire capture of that XAML composition).
        ///  - Content that's in the XAML visual tree but offscreen can be captured, so long as it's not Visibility = Collapsed.
        /// https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.media.imaging.rendertargetbitmap?view=winrt-22621
        /// </remarks>
        public async Task UpdateScreenshot(Microsoft.UI.Xaml.UIElement root, Microsoft.UI.Xaml.Controls.Image? target)
        {
            Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap renderTargetBitmap = new();

            // Try and use the root's native size first, if that fails then use the app's static size.
            var ras = root.ActualSize.ToSize();
            if (ras.Width != double.NaN && ras.Height != double.NaN && ras.Width > 0 && ras.Height > 0)
                await renderTargetBitmap.RenderAsync(root, (int)(ras.Width + 1), (int)(ras.Height + 1));
            else
                await renderTargetBitmap.RenderAsync(root, App.m_width, App.m_height);

            // If you want the image to be applied to the UIElement, pass in a target control of type Image.
            if (target is not null)
            {
                // A render target bitmap is a viable ImageSource.
                target.Source = renderTargetBitmap;
            }
            else // save to disk
            {
                // Convert RenderTargetBitmap to SoftwareBitmap
                IBuffer pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
                byte[] pixels = pixelBuffer.ToArray();
                if (pixels.Length == 0 || renderTargetBitmap.PixelWidth == 0 || renderTargetBitmap.PixelHeight == 0)
                {
                    Debug.WriteLine($"[ERROR] The width and height are not valid, cannot save.");
                }
                else
                {
                    Windows.Graphics.Imaging.SoftwareBitmap softwareBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, renderTargetBitmap.PixelWidth, renderTargetBitmap.PixelHeight, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                    softwareBitmap.CopyFromBuffer(pixelBuffer);
                    _counter++;
                    Debug.WriteLine($"[INFO] Saving frame #{_counter} to disk.");
                    await SaveSoftwareBitmapToFileAsync(softwareBitmap, Path.Combine(AppContext.BaseDirectory, $"Frame_{_counter.ToString().PadLeft(3, '0')}.png"), Windows.Graphics.Imaging.BitmapInterpolationMode.NearestNeighbor);
                    softwareBitmap.Dispose();
                }
            }
        }

        /// <summary>
        /// Uses a <see cref="Windows.Graphics.Imaging.BitmapEncoder"/> to save the output.
        /// </summary>
        /// <param name="softwareBitmap"><see cref="Windows.Graphics.Imaging.SoftwareBitmap"/></param>
        /// <param name="filePath">output file path to save</param>
        /// <param name="interpolation">In general, moving from NearestNeighbor to Fant, interpolation quality increases while performance decreases.</param>
        /// <remarks>
        /// Assumes <see cref="Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId"/>.
        /// [Windows.Graphics.Imaging.BitmapInterpolationMode]
        /// 3 Fant...........: A Fant resampling algorithm. Destination pixel values are computed as a weighted average of the all the pixels that map to the new pixel in a box shaped kernel.
        /// 2 Cubic..........: A bicubic interpolation algorithm. Destination pixel values are computed as a weighted average of the nearest sixteen pixels in a 4x4 grid.
        /// 1 Linear.........: A bilinear interpolation algorithm. The output pixel values are computed as a weighted average of the nearest four pixels in a 2x2 grid.
        /// 0 NearestNeighbor: A nearest neighbor interpolation algorithm. Also known as nearest pixel or point interpolation. The output pixel is assigned the value of the pixel that the point falls within. No other pixels are considered.
        /// </remarks>
        public async Task SaveSoftwareBitmapToFileAsync(Windows.Graphics.Imaging.SoftwareBitmap softwareBitmap, string filePath, Windows.Graphics.Imaging.BitmapInterpolationMode interpolation = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant)
        {
            if (File.Exists(filePath))
            {
                Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                using (Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                {
                    Windows.Graphics.Imaging.BitmapEncoder encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    encoder.BitmapTransform.InterpolationMode = interpolation;
                    try
                    {
                        await encoder.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] SaveSoftwareBitmapToFileAsync({ex.HResult}): {ex.Message}");
                    }
                }
            }
            else
            {
                // Get the folder and file name from the file path.
                string? folderPath = System.IO.Path.GetDirectoryName(filePath);
                string? fileName = System.IO.Path.GetFileName(filePath);
                // Create the folder if it does not exist.
                Windows.Storage.StorageFolder storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
                Windows.Storage.StorageFile file = await storageFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                using (Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                {
                    Windows.Graphics.Imaging.BitmapEncoder encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    encoder.BitmapTransform.InterpolationMode = interpolation;
                    try
                    {
                        await encoder.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] SaveSoftwareBitmapToFileAsync({ex.HResult}): {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// This was not trivial and proved to be a challenge when UriSource is unavailable.
        /// Because we're extracting the asset from a DLL, the UriSource is null which immediately limits our options.
        /// I'm sure someone will correct my misadventure, but this works — and you can't argue with results.
        /// </summary>
        /// <param name="hostGrid"><see cref="Microsoft.UI.Xaml.Controls.Grid"/> to serve as the liaison.</param>
        /// <param name="imageSource"><see cref="Microsoft.UI.Xaml.Media.ImageSource"/> to save.</param>
        /// <param name="filePath">The full path to write the image.</param>
        /// <param name="width">16 to 256</param>
        /// <param name="height">16 to 256</param>
        /// <remarks>
        /// If the width or height is not correct the render target cannot be saved.
        /// The following types derive from <see cref="Microsoft.UI.Xaml.Media.ImageSource"/>:
        ///  - <see cref="Microsoft.UI.Xaml.Media.Imaging.BitmapSource"/>
        ///  - <see cref="Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap"/>
        ///  - <see cref="Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource"/>
        ///  - <see cref="Microsoft.UI.Xaml.Media.Imaging.SurfaceImageSource"/>
        ///  - <see cref="Microsoft.UI.Xaml.Media.Imaging.SvgImageSource"/>
        /// </remarks>
        public async Task SaveImageSourceToFileAsync(Microsoft.UI.Xaml.Controls.Grid hostGrid, Microsoft.UI.Xaml.Media.ImageSource imageSource, string filePath, int width = 32, int height = 32)
        {
            // Create an Image control to hold the ImageSource
            Microsoft.UI.Xaml.Controls.Image imageControl = new Microsoft.UI.Xaml.Controls.Image
            {
                Source = imageSource,
                Width = width,
                Height = height,
            };

            // This is clunky, but for some reason the Image resource is never
            // fully created if it's not appended to a rendered host control; as a
            // workaround we'll add the Image control to the host Grid and remove later.
            hostGrid.Children.Add(imageControl);

            // Wait for the image to be loaded and rendered (down to 20ms seems to work).
            await Task.Delay(50);

            // Render the Image control to a RenderTargetBitmap
            Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap renderTargetBitmap = new();
            await renderTargetBitmap.RenderAsync(imageControl);

            // Convert RenderTargetBitmap to SoftwareBitmap
            IBuffer pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            byte[] pixels = pixelBuffer.ToArray();

            // Remove the Image control from the host Grid
            hostGrid.Children.Remove(imageControl);

            try
            {
                if (pixels.Length == 0 || renderTargetBitmap.PixelWidth == 0 || renderTargetBitmap.PixelHeight == 0)
                {
                    Debug.WriteLine($"[ERROR] The width and height are not a match for this asset. Try a different value other than {width},{height}.");
                }
                else
                {
                    // NOTE: A SoftwareBitmap displayed in a XAML app must be in BGRA pixel format with pre-multiplied alpha values.
                    Windows.Graphics.Imaging.SoftwareBitmap softwareBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        renderTargetBitmap.PixelWidth,
                        renderTargetBitmap.PixelHeight,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                    softwareBitmap.CopyFromBuffer(pixelBuffer);
                    // Save SoftwareBitmap to file
                    await SaveSoftwareBitmapToFileAsync(softwareBitmap, filePath, Windows.Graphics.Imaging.BitmapInterpolationMode.NearestNeighbor);
                    softwareBitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] SaveImageSourceToFileAsync: {ex.Message}");
            }
        }
        #endregion
    }
}
