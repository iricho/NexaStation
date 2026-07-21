using NexaStation.Domain;

namespace NexaStation.Gallery.Viewing;

public static class GalleryViewportController
{
    public static GalleryViewport Create(double viewportWidth,double viewportHeight,double imageWidth,double imageHeight,GalleryZoomMode mode=GalleryZoomMode.Fit)
    {
        Validate(viewportWidth,viewportHeight,imageWidth,imageHeight);double fit=Math.Min(viewportWidth/imageWidth,viewportHeight/imageHeight);double fill=Math.Max(viewportWidth/imageWidth,viewportHeight/imageHeight);double scale=mode switch{GalleryZoomMode.Fill=>fill,GalleryZoomMode.ActualSize=>1,_=>fit};return Clamp(new(viewportWidth,viewportHeight,imageWidth,imageHeight,scale,0,0,mode));
    }
    public static GalleryViewport Zoom(GalleryViewport value,double factor,double anchorX=0,double anchorY=0)
    {
        if(factor<=0||double.IsNaN(factor))return value;double fit=Math.Min(value.ViewportWidth/value.ImageWidth,value.ViewportHeight/value.ImageHeight);double maximum=Math.Max(fit,fit*8);double scale=Math.Clamp(value.Scale*factor,fit,maximum);double ratio=scale/value.Scale;return Clamp(value with{Scale=scale,PanX=(value.PanX-anchorX)*ratio+anchorX,PanY=(value.PanY-anchorY)*ratio+anchorY,Mode=GalleryZoomMode.Custom});
    }
    public static GalleryViewport Pan(GalleryViewport value,double deltaX,double deltaY)=>Clamp(value with{PanX=value.PanX+deltaX,PanY=value.PanY+deltaY,Mode=GalleryZoomMode.Custom});
    public static GalleryViewport Reset(GalleryViewport value,GalleryZoomMode mode=GalleryZoomMode.Fit)=>Create(value.ViewportWidth,value.ViewportHeight,value.ImageWidth,value.ImageHeight,mode);
    public static GalleryViewport Resize(GalleryViewport value,double width,double height)=>Create(width,height,value.ImageWidth,value.ImageHeight,value.Mode==GalleryZoomMode.Custom?GalleryZoomMode.Fit:value.Mode);
    private static GalleryViewport Clamp(GalleryViewport value){double displayedWidth=value.ImageWidth*value.Scale,displayedHeight=value.ImageHeight*value.Scale;double maxX=Math.Max(0,(displayedWidth-value.ViewportWidth)/2),maxY=Math.Max(0,(displayedHeight-value.ViewportHeight)/2);return value with{PanX=Math.Clamp(value.PanX,-maxX,maxX),PanY=Math.Clamp(value.PanY,-maxY,maxY)};}
    private static void Validate(params double[] values){if(values.Any(value=>value<=0||double.IsNaN(value)||double.IsInfinity(value)))throw new ArgumentOutOfRangeException(nameof(values),"Viewport and image dimensions must be finite and positive.");}
}
