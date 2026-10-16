using Spout2.NET;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace neo_bpsys_wpf.Services.DirectOutput;

/// <summary>隔离 Alpha Spout API；所有 GPU 操作串行在输出工作任务中执行。</summary>
internal sealed class SpoutFrameSender : IDisposable
{
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11Texture2D? _texture;
    private SpoutSender? _sender;
    private readonly int _width;
    private readonly int _height;

    /// <summary>创建一个在整个输出会话中复用的设备、BGRA 纹理和 Sender。</summary>
    /// <param name="name">稳定的 Sender 名称。</param>
    /// <param name="width">像素宽度。</param>
    /// <param name="height">像素高度。</param>
    /// <exception cref="PlatformNotSupportedException">当前进程不是 x64；固定 Alpha 包只携带 x64 shim。</exception>
    public SpoutFrameSender(string name, int width, int height)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Spout2.NET 0.1.0-alpha requires an x64 process. Run the win-x64 build.");
        _width = width;
        _height = height;
        try
        {
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
                [FeatureLevel.Level_11_0], out _device, out _context).CheckError();
            _texture = _device.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource
            });
            // Fail instead of letting the SDK silently rename a second application's sender.
            using var senders = new SpoutSenders();
            if (senders.Names().Contains(name, StringComparer.Ordinal))
                throw new InvalidOperationException($"Spout sender '{name}' already exists.");
            _sender = new SpoutSender(name, _device.NativePointer);
            _sender.SetFormat(DxgiFormat.B8G8R8A8UNorm);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>将 WPF 预乘 BGRA 原地转换为 straight BGRA，再上传并发布一帧。</summary>
    /// <param name="pixels">从上到下、Row Pitch 为 width × 4 的 Pbgra32 帧；调用后不再是预乘颜色。</param>
    /// <exception cref="ObjectDisposedException">Sender 已释放。</exception>
    /// <exception cref="ArgumentException">像素缓冲区长度不匹配。</exception>
    public unsafe void Send(byte[] pixels)
    {
        ObjectDisposedException.ThrowIf(_sender is null, this);
        if (pixels.Length != checked(_width * _height * 4)) throw new ArgumentException("Unexpected frame size.", nameof(pixels));
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha == 255) continue;
            if (alpha == 0)
            {
                pixels[i] = pixels[i + 1] = pixels[i + 2] = 0;
                continue;
            }
            pixels[i] = (byte)Math.Min(255, (pixels[i] * 255 + alpha / 2) / alpha);
            pixels[i + 1] = (byte)Math.Min(255, (pixels[i + 1] * 255 + alpha / 2) / alpha);
            pixels[i + 2] = (byte)Math.Min(255, (pixels[i + 2] * 255 + alpha / 2) / alpha);
        }
        fixed (byte* data = pixels)
            _context!.UpdateSubresource(_texture!, 0, null, (nint)data, (uint)(_width * 4), 0);
        // SDK SendTexture copies to its shared texture under the Spout access mutex and
        // flushes the D3D command queue before releasing it. This is the native Spout protocol,
        // not a promise that the CPU has waited for GPU completion. Keep the input texture alive.
        _sender.Send(_texture!.NativePointer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try { _sender?.Dispose(); }
        finally
        {
            _sender = null;
            _texture?.Dispose();
            _texture = null;
            _context?.Dispose();
            _context = null;
            _device?.Dispose();
            _device = null;
        }
    }
}
