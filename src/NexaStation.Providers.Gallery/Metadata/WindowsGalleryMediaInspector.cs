using NexaStation.Core;
using NexaStation.Domain;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace NexaStation.Providers.Gallery.Metadata;

public sealed class WindowsGalleryMediaInspector : IGalleryMediaInspector
{
    public async Task<GalleryMediaInspectionResult> InspectAsync(GalleryMediaInspectionRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();GalleryMediaCandidate candidate=request.Candidate;
        try
        {
            StorageFile file=await StorageFile.GetFileFromPathAsync(candidate.FilePath).AsTask(cancellationToken).ConfigureAwait(false);
            if(candidate.MediaType==GalleryMediaType.Video)
            {
                Windows.Storage.FileProperties.VideoProperties properties=await file.Properties.GetVideoPropertiesAsync().AsTask(cancellationToken).ConfigureAwait(false);
                return new(candidate,(int?)properties.Width,(int?)properties.Height,properties.Duration==TimeSpan.Zero?null:(long)properties.Duration.TotalMilliseconds,null,candidate.CreatedAtUtc);
            }
            await using Stream stream=await file.OpenStreamForReadAsync().ConfigureAwait(false);
            using Windows.Storage.Streams.IRandomAccessStream random=stream.AsRandomAccessStream();BitmapDecoder decoder=await BitmapDecoder.CreateAsync(random).AsTask(cancellationToken).ConfigureAwait(false);
            GalleryMediaCandidate normalized=decoder.FrameCount>1&&candidate.Extension is ".gif" or ".webp"?candidate with{MediaType=GalleryMediaType.AnimatedImage}:candidate;
            int? orientation=null;DateTime? captured=null;string? camera=null;
            if(request.ReadExtendedMetadata)
            {
                BitmapPropertySet values=await decoder.BitmapProperties.GetPropertiesAsync(["System.Photo.Orientation","System.Photo.DateTaken","System.Photo.CameraModel"]).AsTask(cancellationToken).ConfigureAwait(false);
                if(values.TryGetValue("System.Photo.Orientation",out BitmapTypedValue? o)&&o.Value is ushort number)orientation=number;
                if(values.TryGetValue("System.Photo.DateTaken",out BitmapTypedValue? d)&&d.Value is DateTimeOffset date)captured=date.UtcDateTime;
                if(values.TryGetValue("System.Photo.CameraModel",out BitmapTypedValue? c)&&c.Value is string model)camera=model;
            }
            return new(normalized,(int)decoder.PixelWidth,(int)decoder.PixelHeight,null,orientation,captured,camera);
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or ArgumentException or COMException)
        { return new(candidate,UnsupportedCodec:true,Warning:$"Metadata unavailable: {exception.GetType().Name}."); }
    }
}
