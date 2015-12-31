using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WpfApplication1
{
    class ImageFilter
    {
        private static int GetPixelSize(Bitmap image)
        {
            int pixelSize;
            switch (image.PixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    pixelSize = 1;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format16bppArgb1555:
                case System.Drawing.Imaging.PixelFormat.Format16bppGrayScale:
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb555:
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb565:
                    pixelSize = 2;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    pixelSize = 3;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    pixelSize = 4;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format48bppRgb:
                    pixelSize = 6;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format64bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format64bppPArgb:
                    pixelSize = 8;
                    break;
                default:
                    pixelSize = -1;
                    break;

            }
            return pixelSize;
        }

        private static bool HasAlpha(Bitmap image)
        {
            System.Drawing.Imaging.PixelFormat pf = image.PixelFormat;
            if (pf == System.Drawing.Imaging.PixelFormat.Format16bppArgb1555 || pf == System.Drawing.Imaging.PixelFormat.Format32bppArgb || pf == System.Drawing.Imaging.PixelFormat.Format32bppPArgb ||
                pf == System.Drawing.Imaging.PixelFormat.Format64bppArgb || pf == System.Drawing.Imaging.PixelFormat.Format64bppPArgb)
                return true;
            else
                return false;

        }
        public Bitmap ImageConvolution(Bitmap image, ConvolutionMatrix fmat)
        {
            if (fmat.Factor == 0)
                return null;

            Bitmap srcImage = (Bitmap)image.Clone();

            int x, y, filterx, filtery, tempx, tempy;
            int s = fmat.Size / 2;
            int r, g, b, tr, tg, tb;
            int pixelSize = GetPixelSize(image);

            BitmapData imageData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, image.PixelFormat);
            BitmapData srcImageData = srcImage.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, image.PixelFormat);

            int stride = srcImageData.Stride;
            IntPtr scan0 = srcImageData.Scan0;

            unsafe
            {
                byte* tempPixel;

                for (y = s; y < srcImageData.Height - s; y++)
                {
                    for (x = s; x < srcImageData.Width - s; x++)
                    {
                        r = g = b = 0;

                        for (filtery = 0; filtery < fmat.Size; filtery++)
                        {
                            for (filterx = 0; filterx < fmat.Size; filterx++)
                            {
                                tempx = x + filterx - s;
                                tempy = y + filtery - s;

                                tempPixel = (byte*)scan0 + (tempy * stride) + (tempx * pixelSize);
                                tb = (int)*tempPixel;
                                tg = (int)*(tempPixel + 1);
                                tr = (int)*(tempPixel + 2);

                                r += fmat.Matrix[filtery, filterx] * tr;
                                g += fmat.Matrix[filtery, filterx] * tg;
                                b += fmat.Matrix[filtery, filterx] * tb;
                            }
                        }

                        r = Math.Min(Math.Max((r / fmat.Factor) + fmat.Offset, 0), 255);
                        g = Math.Min(Math.Max((g / fmat.Factor) + fmat.Offset, 0), 255);
                        b = Math.Min(Math.Max((b / fmat.Factor) + fmat.Offset, 0), 255);

                        byte* newpixel = (byte*)imageData.Scan0 + (y * imageData.Stride) + (x * pixelSize);
                        *newpixel = (byte)b;
                        *(newpixel + 1) = (byte)g;
                        *(newpixel + 2) = (byte)r;

                        if (HasAlpha(image))
                            *(newpixel + 3) = 255;
                    }

                }
            }

            image.UnlockBits(imageData);
            srcImage.UnlockBits(srcImageData);
            return image;
        }
        public Bitmap SafeImageConvolution(Bitmap image, ConvolutionMatrix fmat)
        {
            //Avoid division by 0 
            if (fmat.Factor == 0)
                return null;

            Bitmap srcImage = (Bitmap)image.Clone();
            Bitmap retImage = new Bitmap(image.Width, image.Height);

            int x, y, filterx, filtery;
            int s = fmat.Size / 2;
            int r, g, b;
            System.Drawing.Color tempPix;

            for (y = s; y < srcImage.Height - s; y++)
            {
                for (x = s; x < srcImage.Width - s; x++)
                {
                    r = g = b = 0;

                    // Convolution 
                    for (filtery = 0; filtery < fmat.Size; filtery++)
                    {
                        for (filterx = 0; filterx < fmat.Size; filterx++)
                        {

                            tempPix = srcImage.GetPixel(x + filterx - s, y + filtery - s);

                            r += fmat.Matrix[filtery, filterx] * tempPix.R;
                            g += fmat.Matrix[filtery, filterx] * tempPix.G;
                            b += fmat.Matrix[filtery, filterx] * tempPix.B;
                        }
                    }

                    r = Math.Min(Math.Max((r / fmat.Factor) + fmat.Offset, 0), 255);
                    g = Math.Min(Math.Max((g / fmat.Factor) + fmat.Offset, 0), 255);
                    b = Math.Min(Math.Max((b / fmat.Factor) + fmat.Offset, 0), 255);
                    try
                    {
                        retImage.SetPixel(x, y, System.Drawing.Color.FromArgb(r, g, b));


                    }
                    catch (Exception e)
                    {

                        string msg = e.Message;
                    }
                }

            }
            return retImage;
        }
    }
}
