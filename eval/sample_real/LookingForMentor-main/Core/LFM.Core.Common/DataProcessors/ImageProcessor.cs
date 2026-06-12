using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace LFM.Core.Common.DataProcessors
{
    public static class ImageProcessor
    {
        private static void DisposeObjects(params IDisposable[] iDisposables)
        {
            foreach (var d in iDisposables)
            {
                d.Dispose();
            }
        }
        
        public static byte[] ReduceImage(byte[] imageBytes, int toSize)
        {
            MemoryStream fromStream = new MemoryStream(imageBytes);
            var image = Image.FromStream(fromStream);
            MemoryStream toStream = new MemoryStream();
            try
            {
                int newWidth;
                int newHeight;

                if (image.Width < image.Height)
                {
                    if (image.Width < toSize)
                    {
                        DisposeObjects(fromStream, image);
                        return imageBytes;
                    }

                    newWidth = toSize;
                    newHeight = (int) (image.Height * (Convert.ToDouble(newWidth) / image.Width));
                }
                else
                {
                    if (image.Height < toSize)
                    {
                        DisposeObjects(fromStream, image);
                        return imageBytes;
                    }

                    newHeight = toSize;
                    newWidth = (int) (image.Width * (Convert.ToDouble(newHeight) / image.Height));
                }

                var thumbnailBitmap = new Bitmap(newWidth, newHeight);

                var thumbnailGraph = Graphics.FromImage(thumbnailBitmap);
                thumbnailGraph.CompositingQuality = CompositingQuality.HighQuality;
                thumbnailGraph.SmoothingMode = SmoothingMode.HighQuality;
                thumbnailGraph.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var imageRectangle = new Rectangle(0, 0, newWidth, newHeight);
                thumbnailGraph.DrawImage(image, imageRectangle);

                byte[] res;
                thumbnailBitmap.Save(toStream, image.RawFormat);
                res = toStream.ToArray();
                
                DisposeObjects(toStream, fromStream, thumbnailBitmap, thumbnailGraph, image);
                
                return res;
            }
            catch
            {
                DisposeObjects(fromStream, toStream, image);
                return null;
            }
        }


        public static byte[] MakeImageSquare(byte[] imageBytes)
        {
            MemoryStream fromStream = new MemoryStream(imageBytes);
            Image image = Image.FromStream(fromStream, true, true);
            MemoryStream toStream = new MemoryStream();
            try
            {
                byte[] res;
                int smallestDimension = Math.Min(image.Height, image.Width);

                Size squareSize = new Size(smallestDimension, smallestDimension);
                Bitmap bmp = new Bitmap(squareSize.Width, squareSize.Height);
                using (Graphics graphics = Graphics.FromImage(bmp))
                {
                    graphics.FillRectangle(Brushes.White, 0, 0, squareSize.Width, squareSize.Height);
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    graphics.DrawImage(image, (squareSize.Width / 2) - (image.Width / 2),
                        (squareSize.Height / 2) - (image.Height / 2), image.Width, image.Height);
                }
                
                bmp.Save(toStream, ImageFormat.Jpeg);

                res = toStream.ToArray();

                DisposeObjects(fromStream, toStream, image);

                return res;
            }
            catch
            {
                DisposeObjects(fromStream, toStream, image);
                return null;
            }
        }

        public static byte[] CompressImage(byte[] imageBytes, long qualityValue)
        {
            MemoryStream fromStream = new MemoryStream(imageBytes);
            Image image = Image.FromStream(fromStream);
            MemoryStream toStream = new MemoryStream();

            try
            {

                Bitmap bm = (Bitmap)image;
                ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
                ImageCodecInfo ici = null;

                foreach (ImageCodecInfo codec in codecs)
                {
                    if (codec.MimeType == "image/jpeg")
                    {
                        ici = codec;
                    }
                }

                EncoderParameters ep = new EncoderParameters();
                ep.Param[0] = new EncoderParameter(Encoder.Quality, qualityValue);

                bm.Save(toStream, ici, ep);

                byte[] result = toStream.ToArray();
                
                DisposeObjects(fromStream, toStream, image);
                return result;
            }
            catch
            {
                DisposeObjects(fromStream, toStream, image);
                return null;
            }
        }
    }
}



