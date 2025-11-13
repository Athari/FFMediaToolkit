namespace FFMediaToolkit.Common.Internal
{
    using System;
    using System.Drawing;
    using FFMediaToolkit.Graphics;
    using FFMediaToolkit.Helpers;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represent a video frame.
    /// </summary>
    internal unsafe class VideoFrame : MediaFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoFrame"/> class with empty frame data.
        /// </summary>
        public VideoFrame()
            : base(ffmpeg.av_frame_alloc())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoFrame"/> class using existing <see cref="AVFrame"/>.
        /// </summary>
        /// <param name="frame">The video <see cref="AVFrame"/>.</param>
        public VideoFrame(AVFrame* frame)
            : base(frame)
        {
            if (frame->GetMediaType() == MediaType.Audio)
                throw new ArgumentException("Cannot create a VideoFrame instance from the AVFrame containing audio.");
        }

        /// <summary>
        /// Gets the frame dimensions.
        /// </summary>
        public Size Layout => Pointer != null ? new Size(Pointer->width, Pointer->height) : default;

        /// <summary>
        /// Gets the frame pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat => Pointer != null ? (AVPixelFormat)Pointer->format : default;

        /// <summary>
        /// Creates a video frame with given dimensions and allocates a buffer for it.
        /// </summary>
        /// <param name="dimensions">The dimensions of the video frame.</param>
        /// <param name="pixelFormat">The video pixel format.</param>
        /// <returns>The new video frame.</returns>
        public static VideoFrame Create(Size dimensions, AVPixelFormat pixelFormat)
        {
            var frame = ffmpeg.av_frame_alloc();

            frame->width = dimensions.Width;
            frame->height = dimensions.Height;
            frame->format = (int)pixelFormat;

            ffmpeg.av_frame_get_buffer(frame, 32);

            return new VideoFrame(frame);
        }

        /// <summary>
        /// Creates an empty frame for decoding.
        /// </summary>
        /// <returns>The empty <see cref="VideoFrame"/>.</returns>
        public static VideoFrame CreateEmpty() => new VideoFrame();

        /// <summary>
        /// Overrides this video frame data with the converted <paramref name="bitmap"/> using specified <see cref="ImageConverter"/> object.
        /// </summary>
        /// <param name="bitmap">The bitmap to convert.</param>
        /// <param name="converter">A <see cref="ImageConverter"/> object, used for caching the FFMpeg <see cref="SwsContext"/> when converting many frames of the same video.</param>
        public void UpdateFromBitmap(ImageData bitmap, ImageConverter converter) => converter.FillAVFrame(bitmap, this);

        /// <summary>
        /// Converts this video frame to the <see cref="ImageData"/> with the specified pixel format.
        /// </summary>
        /// <param name="converter">A <see cref="ImageConverter"/> object, used for caching the FFMpeg <see cref="SwsContext"/> when converting many frames of the same video.</param>
        /// <param name="targetFormat">The output bitmap pixel format.</param>
        /// /// <param name="targetSize">The output bitmap size.</param>
        /// <returns>A <see cref="ImageData"/> instance containing converted bitmap data.</returns>
        public ImageData ToBitmap(ImageConverter converter, ImagePixelFormat targetFormat, Size targetSize)
        {
            var bitmap = ImageData.CreatePooled(targetSize, targetFormat); // Rents memory for the output bitmap.
            fixed (byte* ptr = bitmap.Data)
            {
                // Converts the raw video frame using the given size and pixel format and writes it to the ImageData bitmap.
                converter.AVFrameToBitmap(this, ptr, bitmap.Stride);
            }

            return bitmap;
        }

        /// <inheritdoc/>
        internal override unsafe void Update(AVFrame* newFrame)
        {
            if (newFrame->GetMediaType() != MediaType.Video)
            {
                throw new ArgumentException("The new frame doesn't contain video data.");
            }

            base.Update(newFrame);
        }

        /// <summary>
        /// Set up sws context color space based on frame metadata.
        /// </summary>
        /// <param name="swsContext">Context to set up.</param>
        internal void SetSwsContextColorSpace(SwsContext* swsContext)
        {
            const int FixedPointZero = 0;
            const int FixedPointOne = 1 << 16;
            const int ColorRangeJpegPC = 1;
            const int ColorRangeMpegTV = 0;

            // var trc = Pointer->color_trc;
            // var primaries = Pointer->color_primaries;
            var cs = Pointer->colorspace;
            var range = Pointer->color_range;

            int swsCs = ffmpeg.SWS_CS_DEFAULT;
            if (cs != AVColorSpace.AVCOL_SPC_UNSPECIFIED)
            {
                swsCs = AVColorSpaceToSwsColorSpace(cs) ?? swsCs;
            }

            var srcCoeffs = GetSwsCoefficients(swsCs);
            var dstCoeffs = GetSwsCoefficients(ffmpeg.SWS_CS_DEFAULT);
            var srcRange = range == AVColorRange.AVCOL_RANGE_JPEG ? ColorRangeJpegPC : ColorRangeMpegTV;
            var dstRange = ColorRangeJpegPC;

            ffmpeg.sws_setColorspaceDetails(swsContext, srcCoeffs, srcRange, dstCoeffs, dstRange, brightness: FixedPointZero, contrast: FixedPointOne, saturation: FixedPointOne);
        }

        // https://github.com/FFmpeg/FFmpeg/blob/6cdd2cbe323e04cb4bf88bea50c32aad60cba26e/libavfilter/vf_colormatrix.c#L424-L431
        private static int? AVColorSpaceToSwsColorSpace(AVColorSpace cs)
        {
            switch (cs)
            {
                case AVColorSpace.AVCOL_SPC_BT709:
                    return ffmpeg.SWS_CS_ITU709;
                case AVColorSpace.AVCOL_SPC_FCC:
                    return ffmpeg.SWS_CS_FCC;
                case AVColorSpace.AVCOL_SPC_SMPTE240M:
                    return ffmpeg.SWS_CS_SMPTE240M;
                case AVColorSpace.AVCOL_SPC_BT470BG:
                case AVColorSpace.AVCOL_SPC_SMPTE170M:
                    return ffmpeg.SWS_CS_ITU601;
                case AVColorSpace.AVCOL_SPC_BT2020_NCL:
                case AVColorSpace.AVCOL_SPC_BT2020_CL:
                    return ffmpeg.SWS_CS_BT2020;
                default:
                    return ffmpeg.SWS_CS_DEFAULT;
            }
        }

        private static int_array4 GetSwsCoefficients(int colorspace)
        {
            var coeffs = ffmpeg.sws_getCoefficients(colorspace);
            var vector = default(int_array4);
            vector[0] = coeffs[0];
            vector[1] = coeffs[1];
            vector[2] = coeffs[2];
            vector[3] = coeffs[3];
            return vector;
        }
    }
}
