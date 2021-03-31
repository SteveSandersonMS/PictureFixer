using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PictureFixer.Client
{
    // Simplified patch match algoritm derived from https://github.com/YuanTingHsieh/Image_Completion

    public class PatchMatch
    {
        private readonly int patch_w;
        private const int defaultCutoff = int.MaxValue;
        private const int rs_max = int.MaxValue; // random search
        private Image<Rgb24> sourceImage;
        private Image<Rgb24> maskImage;
        private readonly int width;
        private readonly int height;
        private readonly int accum_length;
        private readonly int mew, meh;
        private readonly Random rng;
        private readonly AnnEntry[] annArray;
        private readonly int[] anndArray;
        private readonly double[] accumArray;
        private readonly int expectedIterations;
        private int iter;
        private Rect box;
        private bool finishedEarly;

        public PatchMatch(Image<Rgb24> sourceImage, Image<Rgb24> maskImage, int patchSize)
        {
            this.sourceImage = sourceImage;
            this.maskImage = maskImage;
            patch_w = patchSize;
            width = sourceImage.Width;
            height = sourceImage.Height;
            accum_length = height * width * 4;
            mew = width - patch_w + 1;
            meh = height - patch_w + 1;
            rng = new Random(0);
            iter = 0;
            annArray = new AnnEntry[width * height];
            anndArray = new int[width * height];
            accumArray = new double[accum_length];
            Initialize();
            expectedIterations = 3 + Math.Max(box.XMax - box.XMin, box.YMax - box.YMin) / (3 * patchSize);
        }

        public Image<Rgb24> Image => sourceImage;

        public double PercentCompleted => (100.0 * iter) / expectedIterations;

        public void Iterate()
        {
            RunIteration();
            iter++;
        }

        private unsafe bool isHole(Rgb24* mask_pixels, int x, int y)
        {
            return mask_pixels[y * width + x] != default;
        }

        private bool inBox(int x, int y)
        {
            return x >= box.XMin && x <= box.XMax + patch_w && y >= box.YMin && y <= box.YMax + patch_w;
        }

        /* Measure distance between 2 patches with upper left corners (ax, ay) and (bx, by), terminating early if we exceed a cutoff distance.
           You could implement your own descriptor here. */
        unsafe int dist(Rgb24* a, Rgb24* b, Rgb24* mask, int ax, int ay, int bx, int by, int cutoff)
        {
            int ans = 0;
            int holeCount = 0;
            if (isHole(mask, ax, ay) && isHole(mask, ax + patch_w - 1, ay + patch_w - 1)) { return int.MaxValue; }
            for (int dy = 0; dy < patch_w; dy ++)
            {
                var maskrow_slice = &mask[(ay + dy) * width + ax];
                var arow = &a[(ay + dy) * width + ax];
                var brow = &b[(by + dy) * width + bx];
                for (int dx = 0; dx < patch_w; dx ++)
                {
                    if (maskrow_slice[dx] != default)
                    {
                        holeCount += 1;
                        continue;
                    }
                    //Debug.Assert(!isHole(bx + dx, by + dy));
                    var ac = arow[dx];
                    var bc = brow[dx];
                    var dr = ac.R - bc.R;
                    var dg = ac.G - bc.G;
                    var db = ac.B - bc.B;
                    ans += dr * dr + dg * dg + db * db;
                }
                if (ans >= cutoff) { return cutoff; }
            }
            double percent = 1 - (double)(holeCount) / (patch_w * patch_w);
            ans = (int)(ans / percent);
            if (ans < 0) return int.MaxValue;
            return ans;
        }

        private unsafe void improve_guess(Rgb24* a, Rgb24* b, Rgb24* mask, int ax, int ay, ref int xbest, ref int ybest, ref int dbest, int bx, int by)
        {
            int d = dist(a, b, mask, ax, ay, bx, by, dbest);
            if ((d < dbest) && (ax != bx || ay != by))
            {
                if (isHole(mask, ax, ay) && d == 0)
                {
                    //Console.WriteLine($"  try improve ({ax}, {ay}) old dist {dbest} new dist {d}");
                    //Console.WriteLine($"  try improve ({ax}, {ay}) old nn ({xbest}, {ybest}) new nn ({bx}, {by})");
                    return;
                }
                dbest = d;
                xbest = bx;
                ybest = by;
            }
        }

        /* Get the bounding box of hole */
        unsafe Rect getBox(Rgb24* maskPixels, Rect prev)
        {
            var xmin = int.MaxValue;
            var ymin = int.MaxValue;
            var xmax = 0;
            var ymax = 0;

            for (int h = prev.YMin; h < prev.YMax; h++)
            {
                var maskRow = &maskPixels[h * width];
                for (int w = prev.XMin; w < prev.XMax; w++)
                {
                    var c = maskRow[w];
                    // hole means non-black pixels in mask
                    if (c != default)
                    {
                        if (h < ymin)
                            ymin = h;
                        if (h > ymax)
                            ymax = h;
                        if (w < xmin)
                            xmin = w;
                        if (w > xmax)
                            xmax = w;
                    }
                }
            }
            xmin = xmin - patch_w + 1;
            ymin = ymin - patch_w + 1;
            xmin = (xmin < 0) ? 0 : xmin;
            ymin = (ymin < 0) ? 0 : ymin;

            xmax = (xmax > width - patch_w + 1) ? width - patch_w + 1 : xmax;
            ymax = (ymax > height - patch_w + 1) ? height - patch_w + 1 : ymax;
            Debug.Assert(xmin >= 0);
            Debug.Assert(xmax < width);
            Debug.Assert(ymin >= 0);
            Debug.Assert(ymax < width);

            return new Rect(xmin, xmax, ymin, ymax);
        }

        unsafe Image<Rgb24> norm_image(double* accum, Image<Rgb24> source)
        {
            var ans_img = source.Clone();
            var w = source.Width;
            if (!ans_img.TryGetSinglePixelSpan(out var ans_span))
            {
                throw new InvalidOperationException("ans_img is too big");
            }

            fixed (Rgb24* ans = ans_span)
            {
                for (int y = box.YMin; y < box.YMax; y++)
                {
                    var row = &ans[y * w];
                    var prow = &accum[4 * y * w];

                    for (int x = box.XMin; x < box.XMax; x++)
                    {
                        var p = &prow[4 * x];
                        double c = p[3] != 0 ? p[3] : 1;
                        //int c2 = c >> 1;
                        byte* row_x = (byte*)&row[x];
                        row_x[0] = (byte)((p[0]/c));
                        row_x[1] = (byte)((p[1]/c));
                        row_x[2] = (byte)((p[2]/c));
                    }
                }
            }

            return ans_img;
        }

        struct AnnEntry
        {
            public int X;
            public int Y;

            public AnnEntry(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        unsafe void Initialize()
        {
            if (!sourceImage.TryGetSinglePixelSpan(out var source_pixels_span))
            {
                throw new InvalidOperationException("source image too big");
            }

            if (!maskImage.TryGetSinglePixelSpan(out var mask_span))
            {
                throw new InvalidOperationException("mask image too big");
            }

            /* Initialize with random nearest neighbor field (NNF). */
            fixed (AnnEntry* ann = annArray)
            fixed (int* annd = anndArray)
            fixed (double* accum = accumArray)
            fixed (Rgb24* source_pixels = source_pixels_span)
            fixed (Rgb24* mask_pixels = mask_span)
            {
                box = getBox(mask_pixels, new Rect(0, width, 0, height));
                if (box.XMax <= box.XMin || box.YMax <= box.YMin)
                {
                    finishedEarly = true;
                    return;
                }

                // Initialization
                for (int ay = box.YMin; ay < box.YMax; ay++)
                {
                    var ann_row = &ann[ay * width];
                    var annd_row = &annd[ay * width];
                    for (int ax = box.XMin; ax < box.XMax; ax++)
                    {
                        bool valid = false;
                        int bx = 0, by = 0;
                        while (!valid)
                        {
                            bx = rng.Next(mew);
                            by = rng.Next(meh);
                            // should find patches outside bounding box
                            if (inBox(bx, by))
                            {
                                // or outside the hole
                                //if (isHole(mask, bx, by) && isHole(mask, bx+patch_w, by+patch_w)) {
                                valid = false;
                            }
                            else
                            {
                                valid = true;
                            }
                        }

                        var ann_pt = &ann_row[ax];
                        ann_pt->X = bx;
                        ann_pt->Y = by;
                        annd_row[ax] = dist(source_pixels, source_pixels, mask_pixels, ax, ay, bx, by, defaultCutoff);
                    }
                }
            }
        }

        private unsafe void RunIteration()
        {
            if (finishedEarly)
            {
                return;
            }

            if (!sourceImage.TryGetSinglePixelSpan(out var source_pixels_span))
            {
                throw new InvalidOperationException("sourceImage too big");
            }

            if (!maskImage.TryGetSinglePixelSpan(out var mask_pixels_span))
            {
                throw new InvalidOperationException("maskImage too big");
            }

            var new_mask = maskImage.Clone();
            if (!new_mask.TryGetSinglePixelSpan(out var new_mask_span))
            {
                throw new InvalidOperationException("new_mask too big");
            }

            fixed (Rgb24* sourcePixels = source_pixels_span)
            fixed (Rgb24* maskPixels = mask_pixels_span)
            fixed (Rgb24* new_mask_pixels = new_mask_span)
            fixed (AnnEntry* ann = annArray)
            fixed (int* annd = anndArray)
            fixed (double* accum = accumArray)
            {
                // to store pixels in the new patch
                Unsafe.InitBlock(accum, 0, (uint)accum_length * sizeof(double));

                /* In each iteration, improve the NNF, by looping in scanline or reverse-scanline order. */
                int ystart = box.YMin, yend = box.YMax, ychange = 1;
                int xstart = box.XMin, xend = box.XMax, xchange = 1;
                if (iter % 2 == 1)
                {
                    xstart = box.XMax - 1; xend = box.XMin - 1; xchange = -1;
                    ystart = box.YMax - 1; yend = box.YMin - 1; ychange = -1;
                }
                for (int ay = ystart; ay != yend; ay += ychange)
                {
                    var ann_row = &ann[ay * width];
                    var annd_row = &annd[ay * width];
                    for (int ax = xstart; ax != xend; ax += xchange)
                    {

                        if (isHole(maskPixels, ax, ay) && isHole(maskPixels, ax + patch_w - 1, ay + patch_w - 1))
                        {
                            continue;
                        }

                        /* Current (best) guess. */
                        ref var v = ref ann_row[ax];
                        int xbest = v.X, ybest = v.Y;
                        int dbest = annd_row[ax];

                        /* Propagation: Improve current guess by trying instead correspondences from left and above (below and right on odd iterations). */
                        if ((ax >= xchange) && (ax - xchange) < mew)
                        {
                            //if (inBox(ax - xchange, ay, box_xmin, box_xmax, box_ymin, box_ymax)) {
                            ref var vp = ref ann_row[ax - xchange];
                            int xp = vp.X + xchange, yp = vp.Y;
                            if (((uint)xp < mew) && !inBox(xp, yp))
                            {
                                //if (((unsigned) xp < (unsigned) mew)) {
                                //printf("Propagation x\n");
                                improve_guess(sourcePixels, sourcePixels, new_mask_pixels, ax, ay, ref xbest, ref ybest, ref dbest, xp, yp);
                            }
                        }

                        if ((ay >= ychange) && (ay - ychange) < meh)
                        {
                            //if (inBox(ax, ay - ychange, box_xmin, box_xmax, box_ymin, box_ymax)) {
                            ref var vp = ref ann[(ay - ychange) * width + ax];
                            int xp = vp.X, yp = vp.Y + ychange;
                            if (((uint)yp < meh) && !inBox(xp, yp))
                            {
                                //if (((unsigned) yp < (unsigned) meh)) {
                                //printf("Propagation y\n");
                                improve_guess(sourcePixels, sourcePixels, new_mask_pixels, ax, ay, ref xbest, ref ybest, ref dbest, xp, yp);
                            }
                        }

                        /* Random search: Improve current guess by searching in boxes of exponentially decreasing size around the current best guess. */
                        int rs_start = Math.Min(rs_max, Math.Max(width, height));

                        for (int mag = rs_start; mag >= 1; mag /= 2)
                        {
                            /* Sampling window */
                            int xmin = Math.Max(xbest - mag, 0), xmax = Math.Min(xbest + mag + 1, mew);
                            int ymin = Math.Max(ybest - mag, 0), ymax = Math.Min(ybest + mag + 1, meh);
                            int xp = xmin + rng.Next(xmax - xmin);
                            int yp = ymin + rng.Next(ymax - ymin);
                            if (!inBox(xp, yp))
                            {
                                //printf("Random\n");
                                improve_guess(sourcePixels, sourcePixels, new_mask_pixels, ax, ay, ref xbest, ref ybest, ref dbest, xp, yp);
                            }
                        }

                        ann_row[ax] = new AnnEntry(xbest, ybest);
                        annd_row[ax] = dbest;
                    }
                }


                // fill in missing pixels
                for (int ay = box.YMin; ay < box.YMax; ay++)
                {
                    var ann_row = &ann[ay * width];

                    for (int ax = box.XMin; ax < box.XMax; ax++)
                    {

                        if (isHole(maskPixels, ax, ay) && isHole(maskPixels, ax + patch_w - 1, ay + patch_w - 1))
                        {
                            continue;
                        }

                        ref var vp = ref ann_row[ax];
                        int xp = vp.X, yp = vp.Y;
                        int half_patch_w = patch_w / 2;
                        for (int dy = 0; dy < patch_w; dy++)
                        {
                            var arow_plus_xp = &sourcePixels[(yp + dy) * width + xp];
                            var prow_plus_4ax = &accum[4 * (width * (ay + dy) + ax)];
                            var new_maskrow_plus_ax = &new_mask_pixels[(ay + dy) * width + ax];
                            var annd_row_dy_plus_xp = &annd[(yp + dy) * width + xp];
                            int dist_from_middle_y_sqr = (half_patch_w - dy)*(half_patch_w - dy);
                            for (int dx = 0; dx < patch_w; dx++)
                            {
                                if (annd_row_dy_plus_xp[dx] == int.MaxValue) { continue; }
                                var c = arow_plus_xp[dx];
                                var p = &prow_plus_4ax[4 * dx];
                                int dist_from_middle_x_sqr = (half_patch_w - dx)* (half_patch_w - dx);
                                double weight = 1.0 / (1.0 + (dist_from_middle_x_sqr + dist_from_middle_y_sqr));
                                p[0] = (p[0] + c.R * weight);
                                p[1] = (p[1] + c.G * weight);
                                p[2] = (p[2] + c.B * weight);
                                p[3] = (p[3] + weight);
                                // change mask
                                new_maskrow_plus_ax[dx] = default;
                            }
                        }
                    }
                }


                //save_bitmap(accum, width, height, "accum.jpg");
                var ans_img = norm_image(&accum[0], sourceImage);
                if (!ans_img.TryGetSinglePixelSpan(out var ans_span))
                {
                    throw new InvalidOperationException("ans_img too big");
                }

                fixed (Rgb24* ans = ans_span)
                {
                    //save_bitmap(ans, "ans.jpg");

                    // join with original picture
                    for (int h = box.YMin; h < box.YMax; h++)
                    {
                        var source_row = &sourcePixels[h * width];
                        var ans_row = &ans[h * width];
                        for (int w = box.XMin; w < box.XMax; w++)
                        {
                            if (!isHole(maskPixels, w, h))
                            {
                                // if (!inBox(w, h, box_xmin, box_xmax, box_ymin, box_ymax)) {
                                ans_row[w] = source_row[w];
                            }
                        }
                    }

                    // update distance (annd)
                    for (int ay = box.YMin; ay < box.YMax; ay++)
                    {
                        var ann_row = &ann[ay * width];
                        var annd_row = &annd[ay * width];
                        for (int ax = box.XMin; ax < box.XMax; ax++)
                        {
                            var vp = &ann_row[ax];
                            var bx = vp->X;
                            var by = vp->Y;
                            annd_row[ax] = dist(ans, ans, new_mask_pixels, ax, ay, bx, by, defaultCutoff);
                        }
                    }
                }

                sourceImage = ans_img;
                maskImage = new_mask;

                box = getBox(maskPixels, box);
                if (box.XMax <= box.XMin || box.YMax <= box.YMin)
                {
                    finishedEarly = true;
                }
            }
        }

        private void save_bitmap(double[] pixels, int w, int h, string filename)
        {
            var img = new Image<Rgb24>(w, h);
            for (var y = 0; y < h; y++)
            {
                var row = img.GetPixelRowSpan(y);
                var prow = pixels.AsSpan(4 * w * y);
                for (var x = 0; x < w; x++)
                {
                    var p = prow.Slice(4 * x);
                    row[x] = new Rgba32((byte)p[0], (byte)p[1], (byte)p[2], (byte)p[3]).Rgb;
                }
            }
            save_bitmap(img, filename);
        }

        private void save_bitmap(int[,] pixels, string filename)
        {
            var w = pixels.GetUpperBound(0);
            var h = pixels.GetUpperBound(1);
            var img = new Image<Rgb24>(w, h);
            for (var y = 0; y < h; y++)
            {
                var row = img.GetPixelRowSpan(y);
                for (var x = 0; x < w; x++)
                {
                    row[x] = new Rgba32((uint)pixels[x, y]).Rgb;
                }
            }
            save_bitmap(img, filename);
        }

        private void save_bitmap<TPixel>(Image<TPixel> img, string filename) where TPixel : unmanaged, IPixel<TPixel>
        {
            if (filename.EndsWith(".jpg"))
            {
                img.SaveAsJpeg(filename);
            }
            else
            {
                throw new ArgumentException("Unknown extension on " + filename);
            }
        }

        private readonly struct Rect
        {
            public readonly int XMin, XMax, YMin, YMax;

            public Rect(int xmin, int xmax, int ymin, int ymax)
            {
                XMin = xmin;
                XMax = xmax;
                YMin = ymin;
                YMax = ymax;
            }
        }
    }

    public static class ImageExtensions
    {
        public static string ToDataUrl<TPixel>(this Image<TPixel> image) where TPixel: unmanaged, IPixel<TPixel>
        {
            // This is not an efficient way to get the image data into the browser. We could use
            // unmanaged interop instead. But it keeps things simple here.
            using var ms = new MemoryStream();
            image.SaveAsBmp(ms);
            var resultBase64 = Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);
            return $"data:image/bmp;base64,{resultBase64}";
        }
    }
}
