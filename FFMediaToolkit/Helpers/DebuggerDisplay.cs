using System.Diagnostics;
using FFmpeg.AutoGen;

[assembly: DebuggerDisplay("AVCodec id={id}", Target = typeof(AVCodec))]
[assembly: DebuggerDisplay("AVDeviceRect {x} {y} {width} {height}", Target = typeof(AVDeviceRect))]
[assembly: DebuggerDisplay("AVRational {num}/{den}", Target = typeof(AVRational))]
